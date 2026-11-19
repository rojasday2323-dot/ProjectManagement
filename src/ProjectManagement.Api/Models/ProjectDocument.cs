namespace ProjectManagement.Api.Models;

public class ProjectDocument
{
    public int Id { get; set; }
    public int? ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DocumentType { get; set; } = string.Empty; // Requirements, Design, Minutes, Report
    public string? OriginalFileName { get; set; }
    public long? FileSizeBytes { get; set; }
    public string ProcessingStatus { get; set; } = "Prepared";
    public string? ExtractedText { get; set; }
    public string? FilePath { get; set; }
    public string? Url { get; set; }
    public int? UploadedByUserId { get; set; }
    public int? SourceDocumentId { get; set; }
    public int? SourceAnalysisId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Project? Project { get; set; }
    public User? UploadedBy { get; set; }
}
