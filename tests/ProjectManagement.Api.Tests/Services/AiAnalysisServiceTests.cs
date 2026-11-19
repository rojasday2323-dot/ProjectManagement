using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Models;
using ProjectManagement.Api.Services.AI;

namespace ProjectManagement.Api.Tests.Services;

public class AiAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeDocument_ValidTextPersistsReviewableProposals()
    {
        await using var db = await CreateDocumentContextAsync();
        var service = CreateService(db, new FakeProvider(AiAnalysisValidatorTests.CreateContent(
            AiAnalysisValidatorTests.CreateProposal(
                developerName: "Developer Known",
                startDate: new DateOnly(2026, 10, 21),
                endDate: new DateOnly(2026, 10, 30)))));

        var result = await service.AnalyzeDocumentAsync(1, CancellationToken.None);

        Assert.Equal("PendingReview", result.Status);
        Assert.NotNull(result.Result);
        var proposal = Assert.Single(result.Result.Proposals);
        Assert.Equal("Pending", proposal.Decision);
        Assert.Equal("Developer Known", proposal.DeveloperName);
        Assert.Equal("AnalysisReady", db.ProjectDocuments.Single().ProcessingStatus);
        Assert.Equal("1", db.AIExtractions.Single().SourceReference);
    }

    [Fact]
    public async Task SaveSummaryAsNote_PersistsEditedNoteWithSourceAndIsIdempotent()
    {
        await using var db = await CreateDocumentContextAsync();
        var service = CreateService(db, new FakeProvider(AiAnalysisValidatorTests.CreateContent()));
        var analysis = await service.AnalyzeDocumentAsync(1, CancellationToken.None);
        const string editedText = "Resumen corregido por la persona responsable.";

        var saved = await service.SaveSummaryAsNoteAsync(
            analysis.Id,
            new SaveAiSummaryNoteRequest("Resumen revisado", editedText),
            CancellationToken.None);
        var retried = await service.SaveSummaryAsNoteAsync(
            analysis.Id,
            new SaveAiSummaryNoteRequest("Otro título de reintento", "Otro texto"),
            CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal(saved.GeneratedNoteId, retried?.GeneratedNoteId);
        var note = Assert.Single(db.ProjectDocuments.Where(document => document.SourceAnalysisId == analysis.Id));
        Assert.Equal(1, note.SourceDocumentId);
        Assert.Equal(1, note.ProjectId);
        Assert.Equal("Resumen revisado", note.Title);
        Assert.Equal(editedText, note.ExtractedText);
        Assert.Equal("MeetingNote", note.DocumentType);
        Assert.Empty(db.Commitments);
        Assert.Empty(db.Deliverables);
    }

    [Fact]
    public async Task SaveSummaryAsNote_RejectsEmptyEditedText()
    {
        await using var db = await CreateDocumentContextAsync();
        var service = CreateService(db, new FakeProvider(AiAnalysisValidatorTests.CreateContent()));
        var analysis = await service.AnalyzeDocumentAsync(1, CancellationToken.None);

        var error = await Assert.ThrowsAsync<AiAnalysisProviderException>(() =>
            service.SaveSummaryAsNoteAsync(
                analysis.Id,
                new SaveAiSummaryNoteRequest("Resumen", "  "),
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Empty(db.ProjectDocuments.Where(document => document.SourceAnalysisId == analysis.Id));
    }

    [Fact]
    public async Task SaveSummaryAsNote_PersistsAndCanBeReadFromSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        int noteId;
        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.ProjectDocuments.Add(new ProjectDocument
            {
                Title = "Transcripción",
                DocumentType = "MeetingNote",
                ExtractedText = "Entregar módulo el 30 de octubre."
            });
            await db.SaveChangesAsync();

            var service = CreateService(db, new FakeProvider(AiAnalysisValidatorTests.CreateContent()));
            var analysis = await service.AnalyzeDocumentAsync(1, CancellationToken.None);
            var saved = await service.SaveSummaryAsNoteAsync(
                analysis.Id,
                new SaveAiSummaryNoteRequest("Resumen revisado", "Pendiente de confirmación por el equipo."),
                CancellationToken.None);
            noteId = Assert.IsType<int>(saved?.GeneratedNoteId);
        }

        await using var reopenedDb = new AppDbContext(options);
        var persisted = await reopenedDb.ProjectDocuments.AsNoTracking().SingleAsync(item => item.Id == noteId);
        Assert.Equal("Resumen revisado", persisted.Title);
        Assert.Equal("Pendiente de confirmación por el equipo.", persisted.ExtractedText);
        Assert.Equal(1, persisted.SourceDocumentId);
        Assert.NotNull(persisted.SourceAnalysisId);
    }

    [Fact]
    public async Task AnalyzeDocument_AmbiguousNamesAreWithheldForConfirmation()
    {
        await using var db = await CreateDocumentContextAsync();
        var source = AiAnalysisValidatorTests.CreateContent(
            AiAnalysisValidatorTests.CreateProposal(developerName: "Unregistered Person", confidence: 0.45));
        var service = CreateService(db, new FakeProvider(source));

        var result = await service.AnalyzeDocumentAsync(1, CancellationToken.None);

        var proposal = Assert.Single(Assert.IsType<AiAnalysisContent>(result.Result).Proposals);
        Assert.Equal("Unregistered Person", proposal.DeveloperName);
        Assert.True(proposal.ConfirmationRequired);
    }

    [Fact]
    public async Task AnalyzeDocument_InvalidModelResultIsPersistedAsFailed()
    {
        await using var db = await CreateDocumentContextAsync();
        var source = AiAnalysisValidatorTests.CreateContent(
            AiAnalysisValidatorTests.CreateProposal(category: "Unknown"));
        var service = CreateService(db, new FakeProvider(source));

        var error = await Assert.ThrowsAsync<AiAnalysisProviderException>(
            () => service.AnalyzeDocumentAsync(1, CancellationToken.None));

        Assert.Equal(StatusCodes.Status502BadGateway, error.StatusCode);
        Assert.Equal("Failed", db.AIExtractions.Single().Status);
        Assert.Equal("AnalysisFailed", db.ProjectDocuments.Single().ProcessingStatus);
        Assert.NotNull(db.AIExtractions.Single().ProcessedAt);
    }

    [Theory]
    [InlineData(StatusCodes.Status502BadGateway, "El proveedor no respondió.")]
    [InlineData(StatusCodes.Status504GatewayTimeout, "Tiempo de espera agotado.")]
    public async Task AnalyzeDocument_ProviderErrorIsPersisted(
        int statusCode,
        string message)
    {
        await using var db = await CreateDocumentContextAsync();
        var service = CreateService(db, new FakeProvider(
            new AiAnalysisProviderException(message, statusCode)));

        var error = await Assert.ThrowsAsync<AiAnalysisProviderException>(
            () => service.AnalyzeDocumentAsync(1, CancellationToken.None));

        Assert.Equal(statusCode, error.StatusCode);
        Assert.Equal(message, db.AIExtractions.Single().ProcessingError);
        Assert.NotNull(db.AIExtractions.Single().ProcessedAt);
    }

    [Fact]
    public async Task AnalyzeDocument_MissingConfigurationDoesNotCallProviderAndIsPersisted()
    {
        await using var db = await CreateDocumentContextAsync();
        var provider = new FakeProvider(
            AiAnalysisValidatorTests.CreateContent(),
            isConfigured: false);
        var service = CreateService(db, provider);

        var error = await Assert.ThrowsAsync<AiAnalysisProviderException>(
            () => service.AnalyzeDocumentAsync(1, CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, error.StatusCode);
        Assert.False(provider.WasCalled);
        Assert.Equal("Failed", db.AIExtractions.Single().Status);
        Assert.Contains("no está configurado", db.AIExtractions.Single().ProcessingError);
    }

    [Fact]
    public async Task ReviewProposal_RejectsProposalWithoutCreatingRecords()
    {
        await using var db = await CreateDocumentContextAsync();
        var service = CreateService(db, new FakeProvider(AiAnalysisValidatorTests.CreateContent()));
        var analysis = await service.AnalyzeDocumentAsync(1, CancellationToken.None);
        var proposal = Assert.Single(analysis.Result!.Proposals);

        var rejected = await service.ReviewProposalAsync(
            analysis.Id,
            proposal.Id,
            new ReviewAiProposalRequest("Rejected", null),
            CancellationToken.None);

        Assert.Equal("Reviewed", rejected?.Status);
        Assert.Equal("Rejected", Assert.Single(rejected!.Result!.Proposals).Decision);
        Assert.Empty(db.Commitments);

        var invalidDecision = await Assert.ThrowsAsync<AiAnalysisProviderException>(
            () => service.ReviewProposalAsync(
                analysis.Id,
                proposal.Id,
                new ReviewAiProposalRequest("CreateCommitment", null),
                CancellationToken.None));
        Assert.Equal(StatusCodes.Status400BadRequest, invalidDecision.StatusCode);
    }

    [Fact]
    public async Task ReviewProposal_CorrectedValuesAreRegisteredAndTraceable()
    {
        await using var db = await CreateDocumentContextAsync();
        var service = CreateService(db, new FakeProvider(AiAnalysisValidatorTests.CreateContent()));
        var analysis = await service.AnalyzeDocumentAsync(1, CancellationToken.None);
        var proposal = Assert.Single(analysis.Result!.Proposals);
        var corrected = proposal with
        {
            Title = "Corregido por usuario",
            DeveloperName = "Developer Known",
            ProductOwnerName = "Owner Known",
            StartDate = new DateOnly(2026, 10, 20),
            EndDate = new DateOnly(2026, 10, 25)
        };

        var result = await service.ReviewProposalAsync(
            analysis.Id,
            proposal.Id,
            new ReviewAiProposalRequest("Accepted", corrected),
            CancellationToken.None);

        var reviewed = Assert.Single(result!.Result!.Proposals);
        Assert.Equal("Corregido por usuario", reviewed.Title);
        Assert.Equal("Accepted", reviewed.Decision);
        Assert.Equal("Commitment", reviewed.RegisteredEntityType);
        Assert.Equal(1, reviewed.RegisteredEntityId);
        Assert.Equal("Developer Known", db.Commitments.Single().Developer.User.DisplayName);
        Assert.Equal(analysis.Id, db.AIProposalRegistrations.Single().AIExtractionId);
        Assert.Equal(proposal.Id, db.AIProposalRegistrations.Single().ProposalId);
        Assert.Equal("1", db.AIExtractions.Single().SourceReference);
    }

    [Fact]
    public async Task ReviewProposal_MissingRequiredFieldsLeavesProposalPending()
    {
        await using var db = await CreateDocumentContextAsync();
        var service = CreateService(db, new FakeProvider(AiAnalysisValidatorTests.CreateContent()));
        var analysis = await service.AnalyzeDocumentAsync(1, CancellationToken.None);
        var proposal = Assert.Single(analysis.Result!.Proposals);

        var error = await Assert.ThrowsAsync<AiAnalysisProviderException>(() =>
            service.ReviewProposalAsync(
                analysis.Id,
                proposal.Id,
                new ReviewAiProposalRequest("Accepted", null),
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        var persisted = JsonSerializer.Deserialize<AiAnalysisContent>(
            db.AIExtractions.Single().ExtractedData,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("Pending", Assert.Single(persisted!.Proposals).Decision);
        Assert.Empty(db.Commitments);
    }

    [Fact]
    public async Task ReviewProposal_RepeatedAcceptanceDoesNotDuplicateCommitment()
    {
        await using var db = await CreateDocumentContextAsync();
        var service = CreateService(db, new FakeProvider(AiAnalysisValidatorTests.CreateContent()));
        var analysis = await service.AnalyzeDocumentAsync(1, CancellationToken.None);
        var proposal = Assert.Single(analysis.Result!.Proposals);
        var request = new ReviewAiProposalRequest("Accepted", ValidCommitment(proposal));

        var first = await service.ReviewProposalAsync(analysis.Id, proposal.Id, request, CancellationToken.None);
        var second = await service.ReviewProposalAsync(analysis.Id, proposal.Id, request, CancellationToken.None);

        Assert.Equal(first!.Result!.Proposals.Single().RegisteredEntityId, second!.Result!.Proposals.Single().RegisteredEntityId);
        Assert.Single(db.Commitments);
        Assert.Single(db.AIProposalRegistrations);
    }

    [Theory]
    [InlineData("Milestone")]
    [InlineData("Deliverable")]
    public async Task ReviewProposal_AcceptsMilestoneAndDeliverableIntoProject(string category)
    {
        await using var db = await CreateDocumentContextAsync();
        var provider = new FakeProvider(AiAnalysisValidatorTests.CreateContent(
            AiAnalysisValidatorTests.CreateProposal(
                category: category,
                endDate: new DateOnly(2026, 10, 30))));
        var service = CreateService(db, provider);
        var analysis = await service.AnalyzeDocumentAsync(1, CancellationToken.None);
        var proposal = Assert.Single(analysis.Result!.Proposals);

        var result = await service.ReviewProposalAsync(
            analysis.Id,
            proposal.Id,
            new ReviewAiProposalRequest("Accepted", proposal),
            CancellationToken.None);

        var accepted = Assert.Single(result!.Result!.Proposals);
        Assert.Equal(category, accepted.RegisteredEntityType);
        Assert.Equal(1, accepted.RegisteredEntityId);
        Assert.Equal(1, db.AIProposalRegistrations.Single().ProjectId);
        Assert.Equal(1, db.Milestones.Count() + db.Deliverables.Count());
    }

    [Fact]
    public async Task ReviewProposal_InvalidResponsibleIsRejectedWithoutCreatingRecord()
    {
        await using var db = await CreateDocumentContextAsync();
        var service = CreateService(db, new FakeProvider(AiAnalysisValidatorTests.CreateContent()));
        var analysis = await service.AnalyzeDocumentAsync(1, CancellationToken.None);
        var proposal = Assert.Single(analysis.Result!.Proposals);
        var corrected = ValidCommitment(proposal) with { DeveloperName = "Unknown Developer" };

        var error = await Assert.ThrowsAsync<AiAnalysisProviderException>(() =>
            service.ReviewProposalAsync(
                analysis.Id,
                proposal.Id,
                new ReviewAiProposalRequest("Accepted", corrected),
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Empty(db.Commitments);
    }

    [Fact]
    public async Task ReviewProposal_InvalidProjectIsRejectedWithoutCreatingRecord()
    {
        await using var db = await CreateDocumentContextAsync();
        var service = CreateService(db, new FakeProvider(AiAnalysisValidatorTests.CreateContent()));
        var analysis = await service.AnalyzeDocumentAsync(1, CancellationToken.None);
        var proposal = Assert.Single(analysis.Result!.Proposals);
        db.AIExtractions.Single().ProjectId = 999;

        var error = await Assert.ThrowsAsync<AiAnalysisProviderException>(() =>
            service.ReviewProposalAsync(
                analysis.Id,
                proposal.Id,
                new ReviewAiProposalRequest("Accepted", ValidCommitment(proposal)),
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Empty(db.Commitments);
    }

    [Fact]
    public async Task ReviewProposal_InvalidDatesAreRejectedWithoutCreatingRecord()
    {
        await using var db = await CreateDocumentContextAsync();
        var service = CreateService(db, new FakeProvider(AiAnalysisValidatorTests.CreateContent()));
        var analysis = await service.AnalyzeDocumentAsync(1, CancellationToken.None);
        var proposal = Assert.Single(analysis.Result!.Proposals);
        var corrected = ValidCommitment(proposal) with
        {
            StartDate = new DateOnly(2026, 10, 30),
            EndDate = new DateOnly(2026, 10, 20)
        };

        var error = await Assert.ThrowsAsync<AiAnalysisValidationException>(() =>
            service.ReviewProposalAsync(
                analysis.Id,
                proposal.Id,
                new ReviewAiProposalRequest("Accepted", corrected),
                CancellationToken.None));

        Assert.Empty(db.Commitments);
    }

    [Fact]
    public async Task ReviewProposal_CommitmentTriggersConflictDetection()
    {
        await using var db = await CreateDocumentContextAsync();
        db.ProductOwners.Add(new ProductOwner { Id = 2, UserId = 3, Department = "Digital" });
        db.Users.Add(new User { Id = 3, DisplayName = "Owner Two", Email = "owner2@test.com", Role = "ProductOwner" });
        db.Commitments.Add(new Commitment
        {
            Id = 8, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1,
            Title = "Existente", Description = "Conflicto",
            StartDate = new DateTime(2026, 10, 20), CommitmentDate = new DateTime(2026, 11, 1),
            Status = "InProgress", Priority = "Medium"
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeProvider(AiAnalysisValidatorTests.CreateContent()));
        var analysis = await service.AnalyzeDocumentAsync(1, CancellationToken.None);
        var proposal = Assert.Single(analysis.Result!.Proposals);

        await service.ReviewProposalAsync(
            analysis.Id,
            proposal.Id,
            new ReviewAiProposalRequest("Accepted", ValidCommitment(proposal)),
            CancellationToken.None);

        Assert.Contains(db.ProjectAlerts, alert => alert.AlertType == "Overlap");
    }

    private static AiAnalysisProposal ValidCommitment(AiAnalysisProposal proposal) => proposal with
    {
        DeveloperName = "Developer Known",
        ProductOwnerName = "Owner Known",
        StartDate = new DateOnly(2026, 10, 20),
        EndDate = new DateOnly(2026, 10, 25)
    };

    private static IAiAnalysisService CreateService(AppDbContext db, FakeProvider provider) =>
        new AiAnalysisService(
            db,
            provider,
            new AiAnalysisValidator(),
            new ProjectManagement.Api.Services.ConflictDetectionService(db),
            NullLogger<AiAnalysisService>.Instance);

    private static async Task<AppDbContext> CreateDocumentContextAsync()
    {
        var db = TestDbHelper.CreateContext();
        db.Users.Add(new User
        {
            Id = 1,
            DisplayName = "Developer Known",
            Email = "known@test.com",
            Role = "Developer"
        });
        db.Users.Add(new User
        {
            Id = 2,
            DisplayName = "Owner Known",
            Email = "owner@test.com",
            Role = "ProductOwner"
        });
        db.Developers.Add(new Developer
        {
            Id = 1,
            UserId = 1,
            Specialty = "Backend",
            Seniority = "Senior"
        });
        db.ProductOwners.Add(new ProductOwner
        {
            Id = 1,
            UserId = 2,
            Department = "Digital"
        });
        db.Projects.Add(new Project
        {
            Id = 1,
            Name = "Portal",
            Description = "Test",
            Product = "Portal",
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = "Active"
        });
        db.ProjectDocuments.Add(new ProjectDocument
        {
            Id = 1,
            ProjectId = 1,
            Title = "Minuta",
            DocumentType = "MeetingNote",
            ExtractedText = "Entregar módulo el 30 de octubre.",
            ProcessingStatus = "Prepared"
        });
        await db.SaveChangesAsync();
        return db;
    }

    private sealed class FakeProvider : IAiAnalysisProvider
    {
        private readonly AiAnalysisContent? _result;
        private readonly Exception? _exception;

        public bool IsConfigured { get; }
        public IReadOnlyList<string> MissingConfiguration => IsConfigured ? [] : ["AzureAI:ApiKey"];
        public bool WasCalled { get; private set; }

        public FakeProvider(AiAnalysisContent? result, bool isConfigured = true)
        {
            _result = result;
            IsConfigured = isConfigured;
        }

        public FakeProvider(Exception exception)
        {
            _exception = exception;
            IsConfigured = true;
        }

        public Task<AiAnalysisContent> AnalyzeAsync(
            string documentText,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            if (_exception is not null)
                throw _exception;
            return Task.FromResult(_result!);
        }

        public Task<AiAnalysisContent> AnalyzeDraftAsync(
            string documentText,
            CancellationToken cancellationToken) =>
            AnalyzeAsync(documentText, cancellationToken);

        public Task<string> AskAboutNotesAsync(
            string notesContext,
            string question,
            CancellationToken cancellationToken) =>
            Task.FromResult("Fake note answer");

        public Task<string> ExtractImageTextAsync(
            string mediaType,
            string base64Image,
            CancellationToken cancellationToken) =>
            Task.FromResult("Fake image text");
    }
}
