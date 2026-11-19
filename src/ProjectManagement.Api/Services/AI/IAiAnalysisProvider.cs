using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Services.AI;

public interface IAiAnalysisProvider
{
    bool IsConfigured { get; }
    IReadOnlyList<string> MissingConfiguration { get; }

    Task<AiAnalysisContent> AnalyzeAsync(
        string documentText,
        CancellationToken cancellationToken);

    Task<AiAnalysisContent> AnalyzeDraftAsync(
        string documentText,
        CancellationToken cancellationToken);

    Task<string> AskAboutNotesAsync(
        string notesContext,
        string question,
        CancellationToken cancellationToken);

    Task<string> ExtractImageTextAsync(
        string mediaType,
        string base64Image,
        CancellationToken cancellationToken);
}

public sealed class AiAnalysisProviderException(string message, int statusCode)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
