namespace ProjectManagement.Api.DTOs;

/// <summary>Structured facts and proposals extracted from an untrusted document.</summary>
public sealed record AiAnalysisContent(
    string? ProjectName,
    string? Summary,
    IReadOnlyList<AiAnalysisProposal> Proposals,
    IReadOnlyList<string> UncertainInformation,
    AiMeetingSummary? MeetingSummary = null);

/// <summary>Reviewable meeting summary; facts include source evidence and are never registered automatically.</summary>
public sealed record AiMeetingSummary(
    string Title,
    string GeneralSummary,
    IReadOnlyList<AiSummaryFact> Topics,
    IReadOnlyList<AiSummaryFact> Agreements,
    IReadOnlyList<AiSummaryFact> Decisions,
    IReadOnlyList<AiSummaryFact> PendingItems,
    IReadOnlyList<AiSummaryFact> Commitments,
    IReadOnlyList<AiSummaryFact> Deliverables,
    IReadOnlyList<AiSummaryResponsiblePerson> ResponsiblePeople,
    IReadOnlyList<AiSummaryDate> ImportantDates,
    IReadOnlyList<AiSummaryFact> RisksAndObservations,
    IReadOnlyList<AiSummaryFact> NextSteps);

public sealed record AiSummaryFact(string Text, string Evidence);

public sealed record AiSummaryResponsiblePerson(
    string? Name,
    string Responsibility,
    bool NeedsConfirmation,
    string Evidence);

public sealed record AiSummaryDate(
    string Description,
    DateOnly? Date,
    string? ResponsiblePerson,
    bool NeedsConfirmation,
    string Evidence);

/// <summary>A reviewable proposal that is not automatically written to project records.</summary>
public sealed record AiAnalysisProposal(
    string Id,
    string Category,
    string Title,
    string? Description,
    string? DeveloperName,
    string? ProductOwnerName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateOnly? QADate,
    DateOnly? PREDate,
    DateOnly? PROTDate,
    DateOnly? PRODDate,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> Risks,
    string Evidence,
    double Confidence,
    bool ConfirmationRequired,
    string Decision,
    string? RegisteredEntityType = null,
    int? RegisteredEntityId = null);

/// <summary>Persisted analysis result and its processing status.</summary>
public sealed record AiAnalysisResponse(
    int Id,
    int DocumentId,
    int? ProjectId,
    string Status,
    string? ProcessingError,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    AiAnalysisContent? Result,
    int? GeneratedNoteId = null);

public sealed record AiAnalysisConfigurationResponse(
    bool Configured,
    IReadOnlyList<string> MissingSettings);

public sealed record SaveAiSummaryNoteRequest(string Title, string Text);

/// <summary>Question about a draft and the saved meeting notes of its selected project.</summary>
public sealed record NoteAssistantQuestionRequest(
    int? ProjectId,
    int? CurrentNoteId,
    string? CurrentTitle,
    string? CurrentText,
    string Question);

/// <summary>Unpersisted note text to identify evidence-backed project fields.</summary>
public sealed record NoteDraftAnalysisRequest(int? ProjectId, string Text);

/// <summary>Answer grounded in the current draft and saved project meeting notes.</summary>
public sealed record NoteAssistantAnswerResponse(string Answer);

/// <summary>Text extracted from a transient note attachment; the original file is not persisted.</summary>
public sealed record NoteAttachmentTextResponse(string FileName, string ExtractedText);

/// <summary>Review action and optional corrected proposal values.</summary>
public sealed record ReviewAiProposalRequest(
    string Decision,
    AiAnalysisProposal? CorrectedProposal);
