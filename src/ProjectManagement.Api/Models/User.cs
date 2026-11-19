namespace ProjectManagement.Api.Models;

public class User
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty; // Developer, ProductOwner, ScrumMaster, Admin
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Developer? Developer { get; set; }
    public ProductOwner? ProductOwner { get; set; }
    public ICollection<ProjectMember> ProjectMemberships { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
