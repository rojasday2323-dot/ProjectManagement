namespace ProjectManagement.Api.Models;

public class AIProposalRegistration
{
    public int Id { get; set; }
    public int AIExtractionId { get; set; }
    public string ProposalId { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AIExtraction AIExtraction { get; set; } = null!;
    public Project Project { get; set; } = null!;
}
