namespace ProjectManagement.Api.DTOs;

public record EnvironmentDeliveryResponse(
    int Id,
    int? CommitmentId,
    string? CommitmentTitle,
    int? DeliverableId,
    string? DeliverableTitle,
    string Environment,
    DateTime? PlannedDate,
    DateTime? ActualDate,
    string Status,
    string? Notes,
    DateTime CreatedAt
);
