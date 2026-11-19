namespace ProjectManagement.Api.Models;

public class AIExtraction
{
    public int Id { get; set; }
    public int? ProjectId { get; set; }
    public int? CommitmentId { get; set; }
    public string SourceType { get; set; } = string.Empty; // Email, Document, Meeting, Manual
    public string? SourceReference { get; set; }
    public string ExtractedData { get; set; } = string.Empty; // JSON with extracted information
    public string? Summary { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Processed, Reviewed, Rejected
    public string? ProcessingError { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Project? Project { get; set; }
    public Commitment? Commitment { get; set; }
    public User? ReviewedBy { get; set; }
    public ICollection<AIProposalRegistration> ProposalRegistrations { get; set; } = [];
}
