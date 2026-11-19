namespace ProjectManagement.Api.Models;

public class Developer
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Specialty { get; set; } = string.Empty; // Backend, Frontend, FullStack, QA, DevOps
    public string Seniority { get; set; } = string.Empty; // Junior, Mid, Senior, Lead
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<Commitment> Commitments { get; set; } = [];
}
