using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Models;
using ProjectManagement.Api.Services.AI;
using NSubstitute;

namespace ProjectManagement.Api.Tests.Services;

public class NoteAssistantServiceTests
{
    [Fact]
    public async Task AskAsync_UsesDraftAndOnlySavedNotesFromSelectedProject()
    {
        await using var db = CreateDbContext();
        db.Projects.AddRange(new Project { Id = 41, Name = "Portal" }, new Project { Id = 42, Name = "Otro" });
        db.ProjectDocuments.AddRange(
            Note(41, "Acuerdos", "La entrega es el 15 de noviembre."),
            Note(42, "Privada de otro proyecto", "No incluir este texto."));
        var oldestNote = Note(41, "Nota antigua", "También se consulta esta nota, aunque sea antigua.");
        oldestNote.CreatedAt = DateTime.UtcNow.AddYears(-1);
        db.ProjectDocuments.Add(oldestNote);
        for (var index = 0; index < 27; index++)
        {
            var recentNote = Note(41, $"Reciente {index}", $"Texto reciente {index}.");
            recentNote.CreatedAt = DateTime.UtcNow.AddMinutes(index);
            db.ProjectDocuments.Add(recentNote);
        }
        await db.SaveChangesAsync();

        var provider = Substitute.For<IAiAnalysisProvider>();
        provider.IsConfigured.Returns(true);
        provider.AskAboutNotesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("La entrega será el 15 de noviembre."));
        var service = new NoteAssistantService(db, provider, new AiAnalysisValidator());

        var response = await service.AskAsync(
            new NoteAssistantQuestionRequest(41, null, "Borrador", "Compromiso de entrega acordado.", "¿Cuándo es la entrega?"),
            CancellationToken.None);

        Assert.Equal("La entrega será el 15 de noviembre.", response);
        await provider.Received(1).AskAboutNotesAsync(
            Arg.Is<string>(context =>
                context.Contains("Compromiso de entrega acordado.")
                && context.Contains("La entrega es el 15 de noviembre.")
                && context.Contains("También se consulta esta nota, aunque sea antigua.")
                && !context.Contains("No incluir este texto.")),
            "¿Cuándo es la entrega?",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AskAsync_ExcludesCurrentSavedNoteFromSavedContextButIncludesItsDraft()
    {
        await using var db = CreateDbContext();
        db.Projects.Add(new Project { Id = 9, Name = "Proyecto" });
        var note = Note(9, "Actual", "Versión guardada");
        db.ProjectDocuments.Add(note);
        await db.SaveChangesAsync();

        var provider = Substitute.For<IAiAnalysisProvider>();
        provider.IsConfigured.Returns(true);
        provider.AskAboutNotesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("Respuesta"));
        var service = new NoteAssistantService(db, provider, new AiAnalysisValidator());

        await service.AskAsync(
            new NoteAssistantQuestionRequest(9, note.Id, note.Title, "Borrador actualizado", "¿Qué cambió?"),
            CancellationToken.None);

        await provider.Received(1).AskAboutNotesAsync(
            Arg.Is<string>(context =>
                context.Contains("Borrador actualizado")
                && !context.Contains("Versión guardada")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AskAsync_UnconfiguredProviderFailsExplicitly()
    {
        await using var db = CreateDbContext();
        var provider = Substitute.For<IAiAnalysisProvider>();
        provider.IsConfigured.Returns(false);
        provider.MissingConfiguration.Returns(["AzureAI:ApiKey"]);
        var service = new NoteAssistantService(db, provider, new AiAnalysisValidator());

        var exception = await Assert.ThrowsAsync<AiAnalysisProviderException>(() =>
            service.AskAsync(
                new NoteAssistantQuestionRequest(null, null, "Borrador", "Contenido", "¿De qué se habló?"),
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, exception.StatusCode);
        Assert.Contains("AzureAI:ApiKey", exception.Message);
        await provider.DidNotReceive().AskAboutNotesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AskAsync_RejectsNoteFromDifferentProject()
    {
        await using var db = CreateDbContext();
        db.Projects.AddRange(new Project { Id = 4, Name = "Proyecto A" }, new Project { Id = 5, Name = "Proyecto B" });
        var note = Note(4, "Nota", "Texto");
        db.ProjectDocuments.Add(note);
        await db.SaveChangesAsync();

        var provider = Substitute.For<IAiAnalysisProvider>();
        provider.IsConfigured.Returns(true);
        var service = new NoteAssistantService(db, provider, new AiAnalysisValidator());

        var exception = await Assert.ThrowsAsync<NoteAssistantValidationException>(() =>
            service.AskAsync(
                new NoteAssistantQuestionRequest(5, note.Id, "Nota", "Texto", "Pregunta"),
                CancellationToken.None));

        Assert.Contains("no pertenece", exception.Message);
        await provider.DidNotReceive().AskAboutNotesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AskAsync_RejectsContextOverLimitInsteadOfSendingPartialNotes()
    {
        await using var db = CreateDbContext();
        db.Projects.Add(new Project { Id = 7, Name = "Proyecto" });
        db.ProjectDocuments.Add(Note(7, "Nota grande", new string('x', 60_001)));
        await db.SaveChangesAsync();

        var provider = Substitute.For<IAiAnalysisProvider>();
        provider.IsConfigured.Returns(true);
        var service = new NoteAssistantService(db, provider, new AiAnalysisValidator());

        var exception = await Assert.ThrowsAsync<NoteAssistantValidationException>(() =>
            service.AskAsync(
                new NoteAssistantQuestionRequest(7, null, null, null, "Pregunta"),
                CancellationToken.None));

        Assert.Contains("60,000", exception.Message);
        await provider.DidNotReceive().AskAboutNotesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnalyzeDraftAsync_ReturnsValidatedSummaryWithoutPersistingIt()
    {
        const string noteText = "El equipo acordó entregar el módulo el 15 de noviembre.";
        await using var db = CreateDbContext();
        var provider = Substitute.For<IAiAnalysisProvider>();
        provider.IsConfigured.Returns(true);
        provider.AnalyzeDraftAsync(noteText, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AiAnalysisContent(
                null,
                "Entrega del módulo acordada.",
                [],
                [],
                new AiMeetingSummary(
                    "Seguimiento del módulo",
                    "El equipo revisó la entrega del módulo.",
                    [],
                    [new AiSummaryFact("Entregar el módulo el 15 de noviembre.", noteText)],
                    [],
                    [],
                    [new AiSummaryFact("Entregar el módulo.", noteText)],
                    [],
                    [],
                    [new AiSummaryDate("Entrega del módulo", new DateOnly(2026, 11, 15), null, false, noteText)],
                    [],
                    []))));
        var service = new NoteAssistantService(db, provider, new AiAnalysisValidator());

        var response = await service.AnalyzeDraftAsync(new NoteDraftAnalysisRequest(null, noteText), CancellationToken.None);

        Assert.Equal("Seguimiento del módulo", response.MeetingSummary?.Title);
        Assert.Single(response.MeetingSummary!.Agreements);
        Assert.Equal(noteText, response.MeetingSummary.Agreements[0].Evidence);
        Assert.Empty(db.ProjectDocuments);
        await provider.Received(1).AnalyzeDraftAsync(noteText, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnalyzeDraftAsync_RejectsEmptyDraftWithoutCallingProvider()
    {
        await using var db = CreateDbContext();
        var provider = Substitute.For<IAiAnalysisProvider>();
        provider.IsConfigured.Returns(true);
        var service = new NoteAssistantService(db, provider, new AiAnalysisValidator());

        var exception = await Assert.ThrowsAsync<NoteAssistantValidationException>(() =>
            service.AnalyzeDraftAsync(new NoteDraftAnalysisRequest(null, " "), CancellationToken.None));

        Assert.Contains("Escribe texto", exception.Message);
        await provider.DidNotReceive().AnalyzeDraftAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnalyzeDraftAsync_UnconfiguredProviderFailsExplicitly()
    {
        await using var db = CreateDbContext();
        var provider = Substitute.For<IAiAnalysisProvider>();
        provider.IsConfigured.Returns(false);
        provider.MissingConfiguration.Returns(["AzureAI:ApiKey"]);
        var service = new NoteAssistantService(db, provider, new AiAnalysisValidator());

        var exception = await Assert.ThrowsAsync<AiAnalysisProviderException>(() =>
            service.AnalyzeDraftAsync(new NoteDraftAnalysisRequest(null, "Texto de la reunión."), CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, exception.StatusCode);
        Assert.Contains("AzureAI:ApiKey", exception.Message);
        await provider.DidNotReceive().AnalyzeDraftAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ProjectDocument Note(int projectId, string title, string text) => new()
    {
        ProjectId = projectId,
        Title = title,
        DocumentType = "MeetingNote",
        ExtractedText = text,
        ProcessingStatus = "Prepared"
    };
}
