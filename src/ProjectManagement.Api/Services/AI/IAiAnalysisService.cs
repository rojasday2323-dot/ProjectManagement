using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Services.AI;

public interface IAiAnalysisService
{
    AiAnalysisConfigurationResponse GetConfigurationStatus();

    Task<AiAnalysisResponse> AnalyzeDocumentAsync(int documentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AiAnalysisResponse>?> GetDocumentAnalysesAsync(
        int documentId,
        CancellationToken cancellationToken);

    Task<AiAnalysisResponse?> ReviewProposalAsync(
        int analysisId,
        string proposalId,
        ReviewAiProposalRequest request,
        CancellationToken cancellationToken);

    Task<AiAnalysisResponse?> SaveSummaryAsNoteAsync(
        int analysisId,
        SaveAiSummaryNoteRequest request,
        CancellationToken cancellationToken);
}
