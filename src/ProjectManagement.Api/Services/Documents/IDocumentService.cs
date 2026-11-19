using Microsoft.AspNetCore.Http;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Services.Documents;

public interface IDocumentService
{
    Task<DocumentResponse> UploadFileAsync(
        int? projectId,
        IFormFile file,
        CancellationToken cancellationToken);

    Task<DocumentResponse> CreateNotesAsync(
        CreateMeetingNotesRequest request,
        CancellationToken cancellationToken);

    Task<DocumentResponse?> UpdateNotesAsync(
        int id,
        UpdateMeetingNotesRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentSummaryResponse>> GetAllAsync(
        string? search,
        int? projectId,
        string? type,
        bool unassignedOnly,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentSummaryResponse>?> GetProjectDocumentsAsync(
        int projectId,
        CancellationToken cancellationToken);

    Task<DocumentResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<DocumentFile?> DownloadAsync(int id, CancellationToken cancellationToken);

    Task<DeleteDocumentResponse?> DeleteAsync(int id, CancellationToken cancellationToken);
}
