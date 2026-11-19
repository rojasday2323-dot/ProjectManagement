namespace ProjectManagement.Api.Models;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "Planning"; // Planning, Active, OnHold, Completed, Cancelled
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<ProjectMember> Members { get; set; } = [];
    public ICollection<Commitment> Commitments { get; set; } = [];
    public ICollection<Milestone> Milestones { get; set; } = [];
    public ICollection<Deliverable> Deliverables { get; set; } = [];
    public ICollection<ProjectDocument> Documents { get; set; } = [];
    public ICollection<ProjectAlert> Alerts { get; set; } = [];
    public ICollection<AzureDevOpsWorkItem> WorkItems { get; set; } = [];
}
