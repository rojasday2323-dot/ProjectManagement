namespace ProjectManagement.Api.Models;

public class Commitment
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int DeveloperId { get; set; }
    public int ProductOwnerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime CommitmentDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Delayed, Cancelled
    public string Priority { get; set; } = "Medium"; // Low, Medium, High, Critical

    // Environment delivery dates
    public DateTime? QADate { get; set; }
    public DateTime? PREDate { get; set; }
    public DateTime? PROTDate { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Project Project { get; set; } = null!;
    public Developer Developer { get; set; } = null!;
    public ProductOwner ProductOwner { get; set; } = null!;
    public ICollection<EnvironmentDelivery> EnvironmentDeliveries { get; set; } = [];
}
