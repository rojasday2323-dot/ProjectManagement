namespace ProjectManagement.Api.Models;

public class EnvironmentDelivery
{
    public int Id { get; set; }
    public int? CommitmentId { get; set; }
    public int? DeliverableId { get; set; }
    public string Environment { get; set; } = string.Empty; // QA, PRE, PROT
    public DateTime? PlannedDate { get; set; }
    public DateTime? ActualDate { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Deployed, Failed, Rollback
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Commitment? Commitment { get; set; }
    public Deliverable? Deliverable { get; set; }
}
