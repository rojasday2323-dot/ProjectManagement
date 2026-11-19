using System.Text.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Models;
using ProjectManagement.Api.Services;

namespace ProjectManagement.Api.Services.AI;

public sealed class AiAnalysisService(
    AppDbContext db,
    IAiAnalysisProvider provider,
    AiAnalysisValidator validator,
    ConflictDetectionService conflictDetection,
    ILogger<AiAnalysisService> logger) : IAiAnalysisService
{
    private const int MaxAnalysisCharacters = 100_000;
    private const int MaxNoteBytes = 10 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AiAnalysisConfigurationResponse GetConfigurationStatus() =>
        new(provider.IsConfigured, provider.MissingConfiguration);

    public async Task<AiAnalysisResponse> AnalyzeDocumentAsync(int documentId, CancellationToken cancellationToken)
    {
        var document = await db.ProjectDocuments
            .Include(item => item.Project)
            .FirstOrDefaultAsync(item => item.Id == documentId && !item.IsDeleted, cancellationToken);
        if (document is null)
            throw new AiAnalysisProviderException($"Documento con ID {documentId} no encontrado.", StatusCodes.Status404NotFound);
        if (string.IsNullOrWhiteSpace(document.ExtractedText))
            throw new AiAnalysisProviderException("El documento no contiene texto preparado para analizar.", StatusCodes.Status422UnprocessableEntity);

        var extraction = new AIExtraction
        {
            ProjectId = document.ProjectId,
            SourceType = document.DocumentType == "MeetingNote" ? "Meeting" : "Document",
            SourceReference = document.Id.ToString(),
            ExtractedData = "{}",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.AIExtractions.Add(extraction);
        document.ProcessingStatus = "Analyzing";
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            if (!provider.IsConfigured)
                throw new AiAnalysisProviderException(
                    BuildConfigurationMessage(provider.MissingConfiguration),
                    StatusCodes.Status503ServiceUnavailable);
            if (document.ExtractedText.Length > MaxAnalysisCharacters)
                throw new AiAnalysisProviderException(
                    "El documento supera el límite de 100 000 caracteres para análisis.",
                    StatusCodes.Status413PayloadTooLarge);

            var knownDeveloperNames = await db.Developers
                .AsNoTracking()
                .Where(developer => developer.User.IsActive)
                .Select(developer => developer.User.DisplayName)
                .ToListAsync(cancellationToken);
            var knownProductOwnerNames = await db.ProductOwners
                .AsNoTracking()
                .Select(owner => owner.User.DisplayName)
                .ToListAsync(cancellationToken);
            var modelResult = await provider.AnalyzeAsync(
                document.ExtractedText,
                cancellationToken);
            var validatedResult = validator.Validate(
                modelResult,
                document.Project?.Name,
                knownDeveloperNames,
                knownProductOwnerNames,
                document.ExtractedText);

            extraction.ExtractedData = JsonSerializer.Serialize(validatedResult, JsonOptions);
            extraction.Summary = validatedResult.Summary;
            extraction.Status = "PendingReview";
            extraction.ProcessedAt = DateTime.UtcNow;
            extraction.ProcessingError = null;
            document.ProcessingStatus = "AnalysisReady";
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (AiAnalysisProviderException exception)
        {
            MarkFailed(document, extraction, exception.Message);
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
        catch (AiAnalysisValidationException exception)
        {
            MarkFailed(document, extraction, exception.Message);
            await db.SaveChangesAsync(cancellationToken);
            throw new AiAnalysisProviderException(exception.Message, StatusCodes.Status502BadGateway);
        }

        logger.LogInformation("Completed document analysis {AnalysisId} for document {DocumentId}", extraction.Id, documentId);
        return ToResponse(extraction);
    }

    public async Task<IReadOnlyList<AiAnalysisResponse>?> GetDocumentAnalysesAsync(
        int documentId,
        CancellationToken cancellationToken)
    {
        if (!await db.ProjectDocuments.AnyAsync(
                document => document.Id == documentId && !document.IsDeleted,
                cancellationToken))
            return null;

        var analyses = await db.AIExtractions
            .Where(item => item.SourceType == "Document" || item.SourceType == "Meeting")
            .Where(item => item.SourceReference == documentId.ToString())
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var analysisIds = analyses.Select(item => item.Id).ToArray();
        var generatedNoteIds = await db.ProjectDocuments
            .AsNoTracking()
            .Where(document => !document.IsDeleted
                && document.SourceAnalysisId.HasValue
                && analysisIds.Contains(document.SourceAnalysisId.Value))
            .ToDictionaryAsync(document => document.SourceAnalysisId!.Value, document => document.Id, cancellationToken);
        return analyses.Select(item => ToResponse(
            item,
            generatedNoteIds.GetValueOrDefault(item.Id))).ToList();
    }

    public async Task<AiAnalysisResponse?> SaveSummaryAsNoteAsync(
        int analysisId,
        SaveAiSummaryNoteRequest request,
        CancellationToken cancellationToken)
    {
        var extraction = await db.AIExtractions.FirstOrDefaultAsync(
            item => item.Id == analysisId
                && (item.SourceType == "Document" || item.SourceType == "Meeting"),
            cancellationToken);
        if (extraction is null)
            return null;
        if (extraction.Status == "Failed")
            throw new AiAnalysisProviderException("No se puede guardar el resumen de un análisis fallido.", StatusCodes.Status409Conflict);

        var content = JsonSerializer.Deserialize<AiAnalysisContent>(extraction.ExtractedData, JsonOptions);
        if (content?.MeetingSummary is null)
            throw new AiAnalysisProviderException("Este análisis no contiene un resumen inteligente guardable.", StatusCodes.Status409Conflict);
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 300)
            throw new AiAnalysisProviderException("El título de la nota es obligatorio y no puede superar 300 caracteres.", StatusCodes.Status400BadRequest);
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new AiAnalysisProviderException("El contenido de la nota no puede estar vacío.", StatusCodes.Status400BadRequest);
        if (Encoding.UTF8.GetByteCount(request.Text) > MaxNoteBytes)
            throw new AiAnalysisProviderException("El contenido de la nota supera el límite de 10 MB.", StatusCodes.Status413PayloadTooLarge);
        if (!int.TryParse(extraction.SourceReference, out var sourceDocumentId))
            throw new AiAnalysisProviderException("No se pudo verificar el documento de origen del análisis.", StatusCodes.Status409Conflict);

        var sourceDocument = await db.ProjectDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(document => document.Id == sourceDocumentId && !document.IsDeleted, cancellationToken);
        if (sourceDocument is null || sourceDocument.ProjectId != extraction.ProjectId)
            throw new AiAnalysisProviderException("El documento de origen ya no está disponible o no coincide con el proyecto.", StatusCodes.Status409Conflict);

        var existingNoteId = await FindGeneratedNoteIdAsync(analysisId, cancellationToken);
        if (existingNoteId.HasValue)
            return ToResponse(extraction, existingNoteId);

        var note = new ProjectDocument
        {
            ProjectId = sourceDocument.ProjectId,
            Title = request.Title.Trim(),
            DocumentType = "MeetingNote",
            ProcessingStatus = "SummarySaved",
            ExtractedText = request.Text,
            SourceDocumentId = sourceDocument.Id,
            SourceAnalysisId = extraction.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.ProjectDocuments.Add(note);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Entry(note).State = EntityState.Detached;
            existingNoteId = await FindGeneratedNoteIdAsync(analysisId, cancellationToken);
            if (!existingNoteId.HasValue)
                throw;
            return ToResponse(extraction, existingNoteId);
        }

        logger.LogInformation("Saved AI summary note {NoteId} for analysis {AnalysisId}", note.Id, analysisId);
        return ToResponse(extraction, note.Id);
    }

    public async Task<AiAnalysisResponse?> ReviewProposalAsync(
        int analysisId,
        string proposalId,
        ReviewAiProposalRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Decision is not ("Accepted" or "Rejected"))
            throw new AiAnalysisProviderException("La decisión debe ser Accepted o Rejected.", StatusCodes.Status400BadRequest);

        var extraction = await db.AIExtractions.FirstOrDefaultAsync(
            item => item.Id == analysisId
                && (item.SourceType == "Document" || item.SourceType == "Meeting"),
            cancellationToken);
        if (extraction is null)
            return null;
        if (extraction.Status == "Failed")
            throw new AiAnalysisProviderException("No se pueden revisar propuestas de un análisis fallido.", StatusCodes.Status409Conflict);

        var content = JsonSerializer.Deserialize<AiAnalysisContent>(extraction.ExtractedData, JsonOptions);
        if (content is null)
            throw new AiAnalysisProviderException("No se pudo leer el resultado guardado.", StatusCodes.Status409Conflict);

        var proposal = content.Proposals.FirstOrDefault(item => item.Id == proposalId);
        if (proposal is null)
            return null;

        var existingRegistration = await db.AIProposalRegistrations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                registration => registration.AIExtractionId == analysisId
                    && registration.ProposalId == proposalId,
                cancellationToken);
        if (existingRegistration is not null)
        {
            if (request.Decision != "Accepted")
                throw new AiAnalysisProviderException(
                    "Una propuesta registrada no puede cambiarse a rechazada.",
                    StatusCodes.Status409Conflict);
            if (existingRegistration.EntityType == "Commitment")
                await conflictDetection.DetectConflictsAsync(cancellationToken);
            var generatedNoteId = await FindGeneratedNoteIdAsync(analysisId, cancellationToken);
            return ToResponse(extraction, generatedNoteId);
        }

        if (proposal.Decision != "Pending")
        {
            if (proposal.Decision != request.Decision)
                throw new AiAnalysisProviderException(
                    "La decisión de esta propuesta ya fue registrada.",
                    StatusCodes.Status409Conflict);
            return ToResponse(extraction);
        }

        var corrected = request.CorrectedProposal;
        if (corrected is not null)
        {
            AiAnalysisValidator.ValidateCorrectedProposal(corrected);
            var knownDeveloperNames = await db.Developers
                .AsNoTracking()
                .Where(developer => developer.User.IsActive)
                .Select(developer => developer.User.DisplayName)
                .ToListAsync(cancellationToken);
            var knownProductOwnerNames = await db.ProductOwners
                .AsNoTracking()
                .Select(owner => owner.User.DisplayName)
                .ToListAsync(cancellationToken);
            if ((!string.IsNullOrWhiteSpace(corrected.DeveloperName)
                    && !knownDeveloperNames.Contains(corrected.DeveloperName, StringComparer.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(corrected.ProductOwnerName)
                    && !knownProductOwnerNames.Contains(corrected.ProductOwnerName, StringComparer.OrdinalIgnoreCase)))
                throw new AiAnalysisProviderException("El responsable indicado no coincide con una persona registrada.", StatusCodes.Status400BadRequest);
        }

        var reviewedProposal = (corrected ?? proposal) with
        {
            Id = proposal.Id,
            Category = proposal.Category,
            Decision = proposal.Decision,
            RegisteredEntityType = null,
            RegisteredEntityId = null
        };
        AIProposalRegistration? registration = null;
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        if (request.Decision == "Accepted")
            registration = await RegisterAcceptedProposalAsync(extraction, reviewedProposal, cancellationToken);

        var updatedProposal = reviewedProposal with
        {
            Decision = request.Decision,
            ConfirmationRequired = false,
            RegisteredEntityType = registration?.EntityType,
            RegisteredEntityId = registration?.EntityId
        };
        var updatedContent = content with
        {
            Proposals = content.Proposals
                .Select(item => item.Id == proposalId ? updatedProposal : item)
                .ToArray()
        };
        extraction.ExtractedData = JsonSerializer.Serialize(updatedContent, JsonOptions);
        extraction.Status = updatedContent.Proposals.All(item => item.Decision != "Pending")
            ? "Reviewed"
            : "PendingReview";
        var documentId = int.TryParse(extraction.SourceReference, out var parsedDocumentId)
            ? parsedDocumentId
            : (int?)null;
        if (extraction.Status == "Reviewed" && documentId.HasValue)
        {
            var document = await db.ProjectDocuments.FindAsync(
                new object?[] { documentId.Value }, cancellationToken);
            if (document is not null)
                document.ProcessingStatus = "AnalysisReviewed";
        }
        await db.SaveChangesAsync(cancellationToken);
        if (registration?.EntityType == "Commitment")
            await conflictDetection.DetectConflictsAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Reviewed proposal {ProposalId} for analysis {AnalysisId} with decision {Decision} and entity type {EntityType}",
            proposalId,
            analysisId,
            request.Decision,
            registration?.EntityType ?? "None");
        return ToResponse(extraction);
    }

    private async Task<AIProposalRegistration?> RegisterAcceptedProposalAsync(
        AIExtraction extraction,
        AiAnalysisProposal proposal,
        CancellationToken cancellationToken)
    {
        var projectId = extraction.ProjectId;
        if (!projectId.HasValue
            || !await db.Projects.AnyAsync(project => project.Id == projectId.Value, cancellationToken))
            throw new AiAnalysisProviderException(
                "El documento no está asociado a un proyecto válido.",
                StatusCodes.Status400BadRequest);

        if (!int.TryParse(extraction.SourceReference, out var sourceDocumentId))
            throw new AiAnalysisProviderException(
                "No se pudo verificar el documento de origen de esta propuesta.",
                StatusCodes.Status400BadRequest);
        var sourceProjectId = await db.ProjectDocuments
            .Where(document => document.Id == sourceDocumentId && !document.IsDeleted)
            .Select(document => document.ProjectId)
            .SingleOrDefaultAsync(cancellationToken);
        if (sourceProjectId != projectId)
            throw new AiAnalysisProviderException(
                "El proyecto asociado a la propuesta no coincide con el documento de origen.",
                StatusCodes.Status400BadRequest);

        var project = await db.Projects
            .FirstAsync(item => item.Id == projectId.Value, cancellationToken);
        object? createdEntity = null;
        string? entityType = null;
        int? developerId = null;
        int? productOwnerId = null;

        switch (proposal.Category)
        {
            case "Commitment":
            case "Activity":
                if (string.IsNullOrWhiteSpace(proposal.DeveloperName)
                    || string.IsNullOrWhiteSpace(proposal.ProductOwnerName)
                    || proposal.StartDate is null
                    || proposal.EndDate is null)
                    throw new AiAnalysisProviderException(
                        "Para registrar un compromiso completa desarrollador, Product Owner, fecha de inicio y fecha compromiso.",
                        StatusCodes.Status400BadRequest);

                if (proposal.EndDate < proposal.StartDate)
                    throw new AiAnalysisProviderException(
                        "La fecha compromiso no puede ser anterior a la fecha de inicio.",
                        StatusCodes.Status400BadRequest);

                developerId = await FindResponsibleIdAsync(
                    db.Developers
                        .Where(developer => developer.User.IsActive)
                        .Select(developer => new Responsible(developer.Id, developer.User.DisplayName)),
                    proposal.DeveloperName,
                    "desarrollador",
                    cancellationToken);
                productOwnerId = await FindResponsibleIdAsync(
                    db.ProductOwners.Select(owner => new Responsible(owner.Id, owner.User.DisplayName)),
                    proposal.ProductOwnerName,
                    "Product Owner",
                    cancellationToken);

                var commitment = new Commitment
                {
                    ProjectId = project.Id,
                    DeveloperId = developerId.Value,
                    ProductOwnerId = productOwnerId.Value,
                    Title = proposal.Title,
                    Description = proposal.Description ?? string.Empty,
                    StartDate = proposal.StartDate.Value.ToDateTime(TimeOnly.MinValue),
                    CommitmentDate = proposal.EndDate.Value.ToDateTime(TimeOnly.MinValue),
                    Status = "Pending",
                    Priority = "Medium",
                    QADate = ToDateTime(proposal.QADate),
                    PREDate = ToDateTime(proposal.PREDate),
                    PROTDate = ToDateTime(proposal.PROTDate)
                };
                db.Commitments.Add(commitment);
                await db.SaveChangesAsync(cancellationToken);
                if (proposal.PRODDate.HasValue)
                    db.EnvironmentDeliveries.Add(new EnvironmentDelivery
                    {
                        CommitmentId = commitment.Id,
                        Environment = "PROD",
                        PlannedDate = ToDateTime(proposal.PRODDate),
                        Status = "Pending"
                    });
                createdEntity = commitment;
                entityType = "Commitment";
                break;

            case "Milestone":
                if (proposal.EndDate is null)
                    throw new AiAnalysisProviderException(
                        "Para registrar un milestone completa su fecha límite.",
                        StatusCodes.Status400BadRequest);

                var milestone = new Milestone
                {
                    ProjectId = project.Id,
                    Title = proposal.Title,
                    Description = proposal.Description,
                    DueDate = proposal.EndDate.Value.ToDateTime(TimeOnly.MinValue),
                    Status = "Pending"
                };
                db.Milestones.Add(milestone);
                await db.SaveChangesAsync(cancellationToken);
                createdEntity = milestone;
                entityType = "Milestone";
                break;

            case "Deliverable":
                var deliverable = new Deliverable
                {
                    ProjectId = project.Id,
                    Title = proposal.Title,
                    Description = proposal.Description,
                    Status = "Pending",
                    QADate = ToDateTime(proposal.QADate),
                    PREDate = ToDateTime(proposal.PREDate),
                    PROTDate = ToDateTime(proposal.PROTDate)
                };
                db.Deliverables.Add(deliverable);
                await db.SaveChangesAsync(cancellationToken);
                if (proposal.PRODDate.HasValue)
                    db.EnvironmentDeliveries.Add(new EnvironmentDelivery
                    {
                        DeliverableId = deliverable.Id,
                        Environment = "PROD",
                        PlannedDate = ToDateTime(proposal.PRODDate),
                        Status = "Pending"
                    });
                createdEntity = deliverable;
                entityType = "Deliverable";
                break;

            default:
                return null;
        }

        if (entityType is null || createdEntity is null)
            return null;

        var entityId = createdEntity switch
        {
            Commitment commitment => commitment.Id,
            Milestone milestone => milestone.Id,
            Deliverable deliverable => deliverable.Id,
            _ => throw new InvalidOperationException("Tipo de entidad de propuesta no compatible.")
        };
        var registration = new AIProposalRegistration
        {
            AIExtractionId = extraction.Id,
            ProposalId = proposal.Id,
            ProjectId = project.Id,
            EntityType = entityType,
            EntityId = entityId
        };
        db.AIProposalRegistrations.Add(registration);
        return registration;
    }

    private async Task<int> FindResponsibleIdAsync(
        IQueryable<Responsible> query,
        string displayName,
        string role,
        CancellationToken cancellationToken)
    {
        var people = await query
            .ToListAsync(cancellationToken);
        var matchingIds = people
            .Where(person => string.Equals(
                person.DisplayName.Trim(),
                displayName.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .Select(person => person.Id)
            .Take(2)
            .ToList();
        return matchingIds.Count switch
        {
            1 => matchingIds[0],
            0 => throw new AiAnalysisProviderException(
                $"No existe un {role} con el nombre indicado.",
                StatusCodes.Status400BadRequest),
            _ => throw new AiAnalysisProviderException(
                $"El nombre del {role} corresponde a más de una persona; selecciona uno inequívoco.",
                StatusCodes.Status400BadRequest)
        };
    }

    private static DateTime? ToDateTime(DateOnly? date) =>
        date?.ToDateTime(TimeOnly.MinValue);

    private sealed record Responsible(int Id, string DisplayName);

    private static void MarkFailed(ProjectDocument document, AIExtraction extraction, string message)
    {
        extraction.Status = "Failed";
        extraction.ProcessingError = message.Length <= 1000 ? message : message[..1000];
        extraction.ProcessedAt = DateTime.UtcNow;
        document.ProcessingStatus = "AnalysisFailed";
    }

    private async Task<int?> FindGeneratedNoteIdAsync(int analysisId, CancellationToken cancellationToken) =>
        await db.ProjectDocuments
            .AsNoTracking()
            .Where(document => document.SourceAnalysisId == analysisId && !document.IsDeleted)
            .Select(document => (int?)document.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static string BuildConfigurationMessage(IReadOnlyList<string> missingSettings) =>
        $"Azure AI Foundry no está configurado. Faltan estas opciones: {string.Join(", ", missingSettings)}. " +
        "Configúralas en User Secrets (desarrollo) o mediante variables de entorno (producción) y reinicia la API.";

    private static AiAnalysisResponse ToResponse(AIExtraction extraction, int? generatedNoteId = null)
    {
        var result = extraction.Status == "Failed"
            ? null
            : JsonSerializer.Deserialize<AiAnalysisContent>(extraction.ExtractedData, JsonOptions);
        return new AiAnalysisResponse(
            extraction.Id,
            int.TryParse(extraction.SourceReference, out var documentId) ? documentId : 0,
            extraction.ProjectId,
            extraction.Status,
            extraction.ProcessingError,
            extraction.CreatedAt,
            extraction.ProcessedAt,
            result,
            generatedNoteId);
    }
}
