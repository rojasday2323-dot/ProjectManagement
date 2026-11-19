namespace ProjectManagement.Api.DTOs;

// ===== Response =====
public record DeliverableResponse(
    int Id,
    int ProjectId,
    string ProjectName,
    int? CommitmentId,
    string? CommitmentTitle,
    string Title,
    string? Description,
    string Status,
    DateTime? QADate,
    DateTime? PREDate,
    DateTime? PROTDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

// ===== Request =====
public record CreateDeliverableRequest(
    int ProjectId,
    int? CommitmentId,
    string Title,
    string? Description,
    string? Status,
    DateTime? QADate,
    DateTime? PREDate,
    DateTime? PROTDate
);

public record UpdateDeliverableRequest(
    int ProjectId,
    int? CommitmentId,
    string Title,
    string? Description,
    string Status,
    DateTime? QADate,
    DateTime? PREDate,
    DateTime? PROTDate
);
