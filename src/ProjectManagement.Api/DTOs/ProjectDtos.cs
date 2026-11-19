namespace ProjectManagement.Api.DTOs;

/// <summary>Project details and current commitment totals returned by the API.</summary>
public record ProjectResponse(
    int Id,
    string Name,
    string Description,
    string Product,
    DateTime StartDate,
    DateTime? EndDate,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int MemberCount,
    int CommitmentCount,
    int PendingCommitmentCount,
    int InProgressCommitmentCount,
    int CompletedCommitmentCount)
{
    public IReadOnlyList<string> ProductOwnerNames { get; init; } = Array.Empty<string>();
}

// ===== Request =====
public record CreateProjectRequest(
    string Name,
    string Description,
    string Product,
    DateTime StartDate,
    DateTime? EndDate,
    string? Status
);

public record UpdateProjectRequest(
    string Name,
    string Description,
    string Product,
    DateTime StartDate,
    DateTime? EndDate,
    string Status
);
