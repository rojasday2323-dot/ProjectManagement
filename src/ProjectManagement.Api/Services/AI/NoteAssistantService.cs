using System.Text;
using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Services.AI;

public interface INoteAssistantService
{
    Task<string?> AskAsync(NoteAssistantQuestionRequest request, CancellationToken cancellationToken);
    Task<AiAnalysisContent> AnalyzeDraftAsync(NoteDraftAnalysisRequest request, CancellationToken cancellationToken);
}

public sealed class NoteAssistantService(
    AppDbContext db,
    IAiAnalysisProvider provider,
    AiAnalysisValidator validator) : INoteAssistantService
{
    private const int MaxQuestionLength = 2000;
    private const int MaxDraftLength = 100_000;
    private const int MaxContextLength = 60_000;

    public async Task<AiAnalysisContent> AnalyzeDraftAsync(
        NoteDraftAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        var text = request.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new NoteAssistantValidationException("Escribe texto en la nota antes de identificar sus campos.");
        if (text.Length > MaxDraftLength)
            throw new NoteAssistantValidationException("La nota supera el límite de 100,000 caracteres para análisis.");
        if (request.ProjectId is <= 0)
            throw new NoteAssistantValidationException("El identificador del proyecto debe ser mayor que cero.");
        if (!provider.IsConfigured)
            throw new AiAnalysisProviderException(
                $"El análisis de notas no está configurado. Faltan estas opciones: {string.Join(", ", provider.MissingConfiguration)}.",
                StatusCodes.Status503ServiceUnavailable);

        string? projectName = null;
        if (request.ProjectId.HasValue)
        {
            projectName = await db.Projects
                .Where(project => project.Id == request.ProjectId.Value)
                .Select(project => project.Name)
                .FirstOrDefaultAsync(cancellationToken);
            if (projectName is null)
                throw new NoteAssistantValidationException("El proyecto seleccionado no existe.");
        }

        var knownDeveloperNames = await db.Developers
            .AsNoTracking()
            .Where(developer => developer.User.IsActive)
            .Select(developer => developer.User.DisplayName)
            .ToListAsync(cancellationToken);
        var knownProductOwnerNames = await db.ProductOwners
            .AsNoTracking()
            .Select(owner => owner.User.DisplayName)
            .ToListAsync(cancellationToken);

        try
        {
            var result = await provider.AnalyzeDraftAsync(text, cancellationToken);
            return validator.Validate(
                result,
                projectName,
                knownDeveloperNames,
                knownProductOwnerNames,
                text);
        }
        catch (AiAnalysisValidationException exception)
        {
            throw new AiAnalysisProviderException(
                $"No se pudo validar la identificación de campos: {exception.Message}",
                StatusCodes.Status502BadGateway);
        }
    }

    public async Task<string?> AskAsync(
        NoteAssistantQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var question = request.Question?.Trim();
        if (string.IsNullOrWhiteSpace(question) || question.Length > MaxQuestionLength)
            throw new NoteAssistantValidationException("La pregunta es obligatoria y no puede superar 2,000 caracteres.");
        if ((request.CurrentText?.Length ?? 0) > MaxDraftLength)
            throw new NoteAssistantValidationException("El borrador supera el límite permitido para consultas.");
        if (request.ProjectId is <= 0)
            throw new NoteAssistantValidationException("El identificador del proyecto debe ser mayor que cero.");
        if (!provider.IsConfigured)
            throw new AiAnalysisProviderException(
                $"El chat de notas no está configurado. Faltan estas opciones: {string.Join(", ", provider.MissingConfiguration)}.",
                StatusCodes.Status503ServiceUnavailable);

        if (request.ProjectId.HasValue
            && !await db.Projects.AnyAsync(project => project.Id == request.ProjectId.Value, cancellationToken))
            return null;

        var savedNotesQuery = request.ProjectId.HasValue
            ? db.ProjectDocuments
                .AsNoTracking()
                .Where(note => note.ProjectId == request.ProjectId.Value
                    && note.DocumentType == "MeetingNote"
                    && !note.IsDeleted
                    && note.Id != request.CurrentNoteId)
            : db.ProjectDocuments
                .AsNoTracking()
                .Where(_ => false);

        var savedTextLength = await savedNotesQuery
            .SumAsync(note => note.ExtractedText == null ? 0 : note.ExtractedText.Length, cancellationToken);
        if (savedTextLength + (request.CurrentText?.Length ?? 0) > MaxContextLength)
            throw new NoteAssistantValidationException(
                "El borrador y las notas del proyecto superan el límite de contexto de 60,000 caracteres. Reduce el contenido antes de consultar al asistente.");

        var notes = await savedNotesQuery
            .OrderByDescending(note => note.UpdatedAt ?? note.CreatedAt)
            .Select(note => new { note.Id, note.Title, note.ExtractedText })
            .ToListAsync(cancellationToken);

        if (request.CurrentNoteId.HasValue)
        {
            var currentNoteMatchesScope = await db.ProjectDocuments
                .AsNoTracking()
                .AnyAsync(note => note.Id == request.CurrentNoteId.Value
                    && note.DocumentType == "MeetingNote"
                    && !note.IsDeleted
                    && note.ProjectId == request.ProjectId, cancellationToken);
            if (!currentNoteMatchesScope)
                throw new NoteAssistantValidationException("La nota actual no pertenece al proyecto seleccionado.");
        }

        var context = new StringBuilder();
        AppendContext(context, "Borrador actual", request.CurrentTitle, request.CurrentText);
        foreach (var note in notes)
            AppendContext(context, $"Nota guardada #{note.Id}", note.Title, note.ExtractedText);

        if (context.Length == 0)
            throw new NoteAssistantValidationException("Escribe contenido en la nota o selecciona un proyecto con notas guardadas.");

        var answer = await provider.AskAboutNotesAsync(context.ToString(), question, cancellationToken);
        if (string.IsNullOrWhiteSpace(answer))
            throw new AiAnalysisProviderException(
                "Azure AI Foundry devolvió una respuesta vacía. Inténtalo nuevamente.",
                StatusCodes.Status502BadGateway);
        return answer.Trim();
    }

    private static void AppendContext(StringBuilder context, string label, string? title, string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || context.Length >= MaxContextLength)
            return;

        var entry = $"{label}: {title?.Trim()}\n{text.Trim()}\n\n";
        if (entry.Length > MaxContextLength - context.Length)
            throw new NoteAssistantValidationException(
                "Las notas del proyecto superan el límite de contexto de 60,000 caracteres.");
        context.Append(entry);
    }
}

public sealed class NoteAssistantValidationException(string message) : Exception(message);
