namespace ProjectManagement.Api.Models;

public class Deliverable
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? CommitmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Delivered, Rejected

    // Environment delivery dates
    public DateTime? QADate { get; set; }
    public DateTime? PREDate { get; set; }
    public DateTime? PROTDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Project Project { get; set; } = null!;
    public Commitment? Commitment { get; set; }
    public ICollection<EnvironmentDelivery> EnvironmentDeliveries { get; set; } = [];
}
