using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Services.AI;

namespace ProjectManagement.Api.Tests.Services;

public class AiAnalysisValidatorTests
{
    private readonly AiAnalysisValidator _validator = new();

    [Fact]
    public void Validate_CommitmentWithDatesAndKnownPeople_ReturnsPendingProposal()
    {
        var source = CreateContent(
            proposal: CreateProposal(
                developerName: "Ana García",
                startDate: new DateOnly(2026, 10, 21),
                endDate: new DateOnly(2026, 10, 30)));

        var validated = _validator.Validate(
            source, "Portal", ["Ana García"], ["Laura Ramírez"], "Entregar módulo el 30 de octubre.");

        var proposal = Assert.Single(validated.Proposals);
        Assert.Equal("Pending", proposal.Decision);
        Assert.Equal("Ana García", proposal.DeveloperName);
        Assert.False(proposal.ConfirmationRequired);
        Assert.NotEqual("model-supplied-id", proposal.Id);
    }

    [Fact]
    public void Validate_UnknownPersonAndLowConfidence_RequiresConfirmation()
    {
        var source = CreateContent(proposal: CreateProposal(
            developerName: "Someone Unknown",
            confidence: 0.4));

        var proposal = Assert.Single(
            _validator.Validate(source, "Portal", [], [], "Entregar módulo el 30 de octubre.").Proposals);

        Assert.Equal("Someone Unknown", proposal.DeveloperName);
        Assert.True(proposal.ConfirmationRequired);
    }

    [Fact]
    public void Validate_PersonInWrongRole_RequiresConfirmation()
    {
        var source = CreateContent(
            CreateProposal(developerName: "Laura Ramírez"));

        var proposal = Assert.Single(
            _validator.Validate(
                source, "Portal", [], ["Laura Ramírez"], "Entregar módulo el 30 de octubre.").Proposals);

        Assert.Equal("Laura Ramírez", proposal.DeveloperName);
        Assert.True(proposal.ConfirmationRequired);
    }

    [Fact]
    public void Validate_ProjectNameMismatch_RequiresConfirmation()
    {
        var content = CreateContent(projectName: "Otro proyecto");

        var result = _validator.Validate(content, "Portal", [], [], "Entregar módulo el 30 de octubre.");

        Assert.All(result.Proposals, proposal => Assert.True(proposal.ConfirmationRequired));
        Assert.Contains("Otro proyecto", result.ProjectName);
        Assert.Contains(result.UncertainInformation, item => item.Contains("no coincide", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_EndDateBeforeStartDate_RejectsModelResult()
    {
        var source = CreateContent(proposal: CreateProposal(
            startDate: new DateOnly(2026, 10, 30),
            endDate: new DateOnly(2026, 10, 21)));

        Assert.Throws<AiAnalysisValidationException>(
            () => _validator.Validate(source, "Portal", [], [], "Entregar módulo el 30 de octubre."));
    }

    [Fact]
    public void Validate_MissingEvidence_RejectsModelResult()
    {
        var source = CreateContent(proposal: CreateProposal(evidence: " "));

        Assert.Throws<AiAnalysisValidationException>(
            () => _validator.Validate(source, "Portal", [], [], "Entregar módulo el 30 de octubre."));
    }

    [Fact]
    public void Validate_SummaryFactWithoutSourceEvidenceIsRejected()
    {
        var content = CreateContent() with
        {
            MeetingSummary = CreateMeetingSummary() with
            {
                Agreements = [new AiSummaryFact("Inventado", "El documento acordó algo inexistente.")]
            }
        };

        Assert.Throws<AiAnalysisValidationException>(() =>
            _validator.Validate(content, "Portal", [], [], "Entregar módulo el 30 de octubre."));
    }

    [Fact]
    public void Validate_UnknownCategory_RejectsModelResult()
    {
        var source = CreateContent(proposal: CreateProposal(category: "Task"));

        Assert.Throws<AiAnalysisValidationException>(
            () => _validator.Validate(source, "Portal", [], [], "Entregar módulo el 30 de octubre."));
    }

    [Fact]
    public void Validate_EvidenceNotFoundInSource_RequiresConfirmation()
    {
        var proposal = AiAnalysisValidatorTests.CreateProposal(evidence: "El servidor ya se desplegó en producción.");
        var result = _validator.Validate(
            AiAnalysisValidatorTests.CreateContent(proposal),
            "Portal",
            [],
            [],
            "Entregar módulo el 30 de octubre.");

        Assert.True(Assert.Single(result.Proposals).ConfirmationRequired);
    }

    [Fact]
    public void ValidateCorrectedProposal_InvalidDateRange_RejectsCorrection()
    {
        var proposal = CreateProposal(
            startDate: new DateOnly(2026, 10, 30),
            endDate: new DateOnly(2026, 10, 21));

        Assert.Throws<AiAnalysisValidationException>(
            () => AiAnalysisValidator.ValidateCorrectedProposal(proposal));
    }

    internal static AiAnalysisContent CreateContent(
        AiAnalysisProposal? proposal = null,
        string? projectName = "Portal") =>
        new(
            projectName,
            "Revisión del proyecto",
            [proposal ?? CreateProposal()],
            ["La fecha de PROD no se determinó."],
            CreateMeetingSummary());

    private static AiMeetingSummary CreateMeetingSummary() =>
        new(
            "Seguimiento de reunión",
            "Se revisó la entrega del módulo.",
            [new AiSummaryFact("Se revisó la entrega del módulo.", "Entregar módulo el 30 de octubre.")],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            []);

    internal static AiAnalysisProposal CreateProposal(
        string category = "Commitment",
        string? developerName = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        double confidence = 0.95,
        string evidence = "Entregar módulo el 30 de octubre.") =>
        new(
            "model-supplied-id",
            category,
            "Entregar módulo",
            "Preparar la entrega del módulo.",
            developerName,
            null,
            startDate,
            endDate,
            null,
            null,
            null,
            null,
            [],
            [],
            evidence,
            confidence,
            false,
            "Accepted");
}
