namespace ProjectManagement.Api.Models;

public class AzureDevOpsWorkItem
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? CommitmentId { get; set; }
    public int DevOpsWorkItemId { get; set; }
    public string DevOpsOrganization { get; set; } = string.Empty;
    public string DevOpsProject { get; set; } = string.Empty;
    public string WorkItemType { get; set; } = string.Empty; // UserStory, Task, Bug, Feature, Epic
    public string Title { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? AssignedTo { get; set; }
    public string? Url { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Project Project { get; set; } = null!;
    public Commitment? Commitment { get; set; }
}
