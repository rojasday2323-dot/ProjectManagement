namespace ProjectManagement.Api.DTOs;

// ===== Response =====
public record MilestoneResponse(
    int Id,
    int ProjectId,
    string ProjectName,
    string Title,
    string? Description,
    DateTime DueDate,
    DateTime? CompletedDate,
    string Status,
    DateTime CreatedAt
);

// ===== Request =====
public record CreateMilestoneRequest(
    int ProjectId,
    string Title,
    string? Description,
    DateTime DueDate,
    string? Status
);

public record UpdateMilestoneRequest(
    int ProjectId,
    string Title,
    string? Description,
    DateTime DueDate,
    DateTime? CompletedDate,
    string Status
);
