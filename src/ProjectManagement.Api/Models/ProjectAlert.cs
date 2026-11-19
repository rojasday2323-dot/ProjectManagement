namespace ProjectManagement.Api.Models;

public class ProjectAlert
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? DeveloperId { get; set; }
    public int? CommitmentId { get; set; }
    public int? RelatedCommitmentId { get; set; }
    public string AlertType { get; set; } = string.Empty; // Overlap, DeliveryConflict, Overload, DeadlineApproaching, Overdue
    public string Severity { get; set; } = "Info"; // Info, Warning, Critical
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = "Open"; // Open, Acknowledged, Resolved
    public DateTime? ConflictStartDate { get; set; }
    public DateTime? ConflictEndDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Project Project { get; set; } = null!;
    public Developer? Developer { get; set; }
    public Commitment? Commitment { get; set; }
    public Commitment? RelatedCommitment { get; set; }
}
