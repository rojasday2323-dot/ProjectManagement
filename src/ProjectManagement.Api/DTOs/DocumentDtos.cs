namespace ProjectManagement.Api.DTOs;

/// <summary>Document or note metadata shown in document lists.</summary>
public sealed record DocumentSummaryResponse(
    int Id,
    int? ProjectId,
    string? ProjectName,
    string Title,
    string DocumentType,
    string? OriginalFileName,
    long? FileSizeBytes,
    bool HasOriginalFile,
    string ProcessingStatus,
    DateTime UploadedAt,
    DateTime? UpdatedAt,
    int? SourceDocumentId,
    int? SourceAnalysisId);

/// <summary>Document metadata, extracted text, and original file information.</summary>
public sealed record DocumentResponse(
    int Id,
    int? ProjectId,
    string? ProjectName,
    string Title,
    string DocumentType,
    string? OriginalFileName,
    long? FileSizeBytes,
    bool HasOriginalFile,
    string ProcessingStatus,
    string ExtractedText,
    DateTime UploadedAt,
    DateTime? UpdatedAt,
    int? SourceDocumentId,
    int? SourceAnalysisId);

/// <summary>Meeting notes submitted as plain text.</summary>
public sealed record CreateMeetingNotesRequest(
    int? ProjectId,
    string? Title,
    string Text);

/// <summary>Changes the title, project association, or content of saved notes.</summary>
public sealed record UpdateMeetingNotesRequest(
    int? ProjectId,
    string Title,
    string Text);

/// <summary>Indicates whether a document was removed and its file storage cleaned.</summary>
public sealed record DeleteDocumentResponse(bool Deleted, bool FileRemoved, string? Message);

public sealed record DocumentFile(Stream Content, string ContentType, string DownloadName);
