using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Models;

namespace ProjectManagement.Api.Tests.Endpoints;

public class AiAnalysisEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AiAnalysisEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AiStatus_ReportsMissingSettingsWithoutValues()
    {
        var response = await _client.GetAsync("/api/ai/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<AiAnalysisConfigurationResponse>();
        Assert.NotNull(status);
        Assert.False(status.Configured);
        Assert.Contains("AzureAI:ApiKey", status.MissingSettings);
        Assert.DoesNotContain("unit-test-key", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NotesChat_WithoutAiConfigurationReturnsServiceUnavailable()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/notes/chat",
            new NoteAssistantQuestionRequest(null, null, "Reunión", "Se acordó revisar el despliegue.", "¿Qué se acordó?"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("no está configurado", body);
        Assert.DoesNotContain("unit-test-key", body);
    }

    [Fact]
    public async Task AnalyzeNoteDraft_WithoutAiConfigurationReturnsServiceUnavailableWithoutSaving()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/notes/analyze",
            new NoteDraftAnalysisRequest(null, "La reunión acordó revisar el despliegue."));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("no está configurado", body);
        Assert.DoesNotContain("unit-test-key", body);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.ProjectDocuments.AnyAsync(note => note.ExtractedText == "La reunión acordó revisar el despliegue."));
    }

    [Fact]
    public async Task ExtractTextAttachment_ReturnsTextWithoutPersistingFile()
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("La entrega será el 15 de noviembre."));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(file, "file", "meeting.txt");

        var response = await _client.PostAsync("/api/notes/attachments/extract", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var extracted = await response.Content.ReadFromJsonAsync<NoteAttachmentTextResponse>();
        Assert.NotNull(extracted);
        Assert.Equal("meeting.txt", extracted.FileName);
        Assert.Contains("15 de noviembre", extracted.ExtractedText);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.ProjectDocuments.AnyAsync(note => note.Title == "meeting"));
    }

    [Fact]
    public async Task AnalyzeDocument_WithoutProviderConfiguration_PersistsFailure()
    {
        int documentId;
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var document = new ProjectDocument
            {
                ProjectId = 1,
                Title = "AI test note",
                DocumentType = "MeetingNote",
                ExtractedText = "Review the milestone next week.",
                ProcessingStatus = "Prepared"
            };
            setupDb.ProjectDocuments.Add(document);
            await setupDb.SaveChangesAsync();
            documentId = document.Id;
        }

        var response = await _client.PostAsync($"/api/documents/{documentId}/analyze", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Contains("no está configurado", error.Message);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var analysis = await db.AIExtractions.SingleAsync(item => item.SourceReference == documentId.ToString());
        Assert.Equal("Failed", analysis.Status);
        Assert.NotNull(analysis.ProcessedAt);
        Assert.Equal("AnalysisFailed", (await db.ProjectDocuments.SingleAsync(item => item.Id == documentId)).ProcessingStatus);

        var history = await _client.GetFromJsonAsync<List<AiAnalysisResponse>>(
            $"/api/documents/{documentId}/analyses");
        Assert.NotNull(history);
        Assert.Contains(history, item => item.Id == analysis.Id);
    }

    [Fact]
    public async Task ReviewProposal_InvalidDecision_ReturnsBadRequest()
    {
        var response = await _client.PatchAsJsonAsync(
            "/api/analyses/1/proposals/nonexistent",
            new ReviewAiProposalRequest("CreateCommitment", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AcceptingCommitmentProposal_RegistersItForCommitmentsAndTimeline()
    {
        int analysisId;
        int documentId;
        string proposalId;
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var document = new ProjectDocument
            {
                ProjectId = 1,
                Title = "Proposal conversion test",
                DocumentType = "MeetingNote",
                ExtractedText = "Carlos hará la integración en octubre.",
                ProcessingStatus = "AnalysisReady"
            };
            db.ProjectDocuments.Add(document);
            await db.SaveChangesAsync();
            documentId = document.Id;

            proposalId = Guid.NewGuid().ToString("N");
            var proposal = new AiAnalysisProposal(
                proposalId,
                "Commitment",
                "Integración documentada",
                "Implementar la integración confirmada.",
                "Carlos Mendoza",
                "Laura Ramírez",
                new DateOnly(2027, 10, 1),
                new DateOnly(2027, 10, 15),
                null,
                null,
                null,
                null,
                [],
                [],
                "Carlos hará la integración en octubre.",
                0.98,
                false,
                "Pending");
            var content = new AiAnalysisContent("Portal Banca Digital v2", null, [proposal], []);
            var extraction = new AIExtraction
            {
                ProjectId = document.ProjectId,
                SourceType = "Meeting",
                SourceReference = document.Id.ToString(),
                ExtractedData = JsonSerializer.Serialize(content, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                Status = "PendingReview",
                CreatedAt = DateTime.UtcNow
            };
            db.AIExtractions.Add(extraction);
            await db.SaveChangesAsync();
            analysisId = extraction.Id;
        }

        var response = await _client.PatchAsJsonAsync(
            $"/api/analyses/{analysisId}/proposals/{proposalId}",
            new ReviewAiProposalRequest("Accepted", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var analysis = await response.Content.ReadFromJsonAsync<AiAnalysisResponse>();
        var registered = Assert.Single(analysis!.Result!.Proposals);
        Assert.Equal("Commitment", registered.RegisteredEntityType);
        Assert.NotNull(registered.RegisteredEntityId);

        var commitments = await _client.GetFromJsonAsync<List<CommitmentResponse>>(
            "/api/commitments?projectId=1");
        Assert.Contains(commitments!, item => item.Id == registered.RegisteredEntityId);

        var timeline = await _client.GetFromJsonAsync<TimelineResponse>("/api/projects/1/timeline");
        Assert.Contains(timeline!.Commitments, item => item.Id == registered.RegisteredEntityId);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var registration = await verifyDb.AIProposalRegistrations.SingleAsync(
            item => item.AIExtractionId == analysisId && item.ProposalId == proposalId);
        Assert.Equal(1, registration.ProjectId);
        Assert.Equal(documentId.ToString(), (await verifyDb.AIExtractions.SingleAsync(item => item.Id == analysisId)).SourceReference);
    }

    [Fact]
    public async Task SavingReviewedSummary_CreatesTraceableNoteAndRepeatedRequestDoesNotDuplicate()
    {
        int analysisId;
        int documentId;
        int existingCommitmentCount;
        int existingDeliverableCount;
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            existingCommitmentCount = await db.Commitments.CountAsync();
            existingDeliverableCount = await db.Deliverables.CountAsync();
            var document = new ProjectDocument
            {
                ProjectId = 1,
                Title = "Transcripción de prueba",
                DocumentType = "MeetingNote",
                ExtractedText = "El equipo revisará el despliegue el viernes.",
                ProcessingStatus = "AnalysisReady"
            };
            db.ProjectDocuments.Add(document);
            await db.SaveChangesAsync();
            documentId = document.Id;

            var extraction = new AIExtraction
            {
                ProjectId = document.ProjectId,
                SourceType = "Meeting",
                SourceReference = document.Id.ToString(),
                ExtractedData = JsonSerializer.Serialize(
                    new AiAnalysisContent(
                        null,
                        "Revisión de despliegue.",
                        [],
                        [],
                        new AiMeetingSummary(
                            "Seguimiento de despliegue",
                            "El equipo revisará el despliegue el viernes.",
                            [], [], [], [], [], [], [], [], [], [])),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                Status = "PendingReview",
                CreatedAt = DateTime.UtcNow
            };
            db.AIExtractions.Add(extraction);
            await db.SaveChangesAsync();
            analysisId = extraction.Id;
        }

        var request = new SaveAiSummaryNoteRequest("Resumen revisado", "Pendiente: revisar el despliegue.");
        var response = await _client.PostAsJsonAsync($"/api/analyses/{analysisId}/summary-note", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<AiAnalysisResponse>();
        Assert.NotNull(saved?.GeneratedNoteId);

        var retry = await _client.PostAsJsonAsync(
            $"/api/analyses/{analysisId}/summary-note",
            new SaveAiSummaryNoteRequest("Ignorado por idempotencia", "Otro contenido"));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retried = await retry.Content.ReadFromJsonAsync<AiAnalysisResponse>();
        Assert.Equal(saved.GeneratedNoteId, retried?.GeneratedNoteId);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var note = await verifyDb.ProjectDocuments.SingleAsync(item => item.SourceAnalysisId == analysisId);
        Assert.Equal(documentId, note.SourceDocumentId);
        Assert.Equal(1, note.ProjectId);
        Assert.Equal("Resumen revisado", note.Title);
        Assert.Equal("Pendiente: revisar el despliegue.", note.ExtractedText);
        Assert.Equal(existingCommitmentCount, await verifyDb.Commitments.CountAsync());
        Assert.Equal(existingDeliverableCount, await verifyDb.Deliverables.CountAsync());
    }
}
