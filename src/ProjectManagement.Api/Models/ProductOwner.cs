namespace ProjectManagement.Api.Models;

public class ProductOwner
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Department { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<Commitment> Commitments { get; set; } = [];
}
