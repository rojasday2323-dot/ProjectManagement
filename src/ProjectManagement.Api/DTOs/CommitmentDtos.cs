namespace ProjectManagement.Api.DTOs;

// ===== Response =====
public record CommitmentResponse(
    int Id,
    int ProjectId,
    string ProjectName,
    int DeveloperId,
    string DeveloperName,
    int ProductOwnerId,
    string ProductOwnerName,
    string Title,
    string Description,
    DateTime StartDate,
    DateTime CommitmentDate,
    DateTime? CompletedDate,
    string Status,
    string Priority,
    DateTime? QADate,
    DateTime? PREDate,
    DateTime? PROTDate,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

// ===== Request =====
public record CreateCommitmentRequest(
    int ProjectId,
    int DeveloperId,
    int ProductOwnerId,
    string Title,
    string Description,
    DateTime StartDate,
    DateTime CommitmentDate,
    string? Status,
    string? Priority,
    DateTime? QADate,
    DateTime? PREDate,
    DateTime? PROTDate,
    string? Notes
);

public record UpdateCommitmentRequest(
    int ProjectId,
    int DeveloperId,
    int ProductOwnerId,
    string Title,
    string Description,
    DateTime StartDate,
    DateTime CommitmentDate,
    DateTime? CompletedDate,
    string Status,
    string Priority,
    DateTime? QADate,
    DateTime? PREDate,
    DateTime? PROTDate,
    string? Notes
);
