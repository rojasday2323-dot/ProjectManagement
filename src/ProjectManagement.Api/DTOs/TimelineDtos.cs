namespace ProjectManagement.Api.DTOs;

public record TimelineResponse(
    ProjectTimelineInfo Project,
    List<TimelineCommitment> Commitments,
    List<TimelineMilestone> Milestones,
    List<TimelineDeliverable> Deliverables,
    List<TimelineEnvironmentDelivery> EnvironmentDeliveries,
    List<TimelineAlert> Alerts
);

public record ProjectTimelineInfo(
    int Id,
    string Name,
    string Product,
    DateTime StartDate,
    DateTime? EndDate,
    string Status
);

public record TimelineCommitment(
    int Id,
    string Title,
    int DeveloperId,
    string DeveloperName,
    int ProductOwnerId,
    string ProductOwnerName,
    DateTime StartDate,
    DateTime CommitmentDate,
    DateTime? CompletedDate,
    string Status,
    string Priority,
    string Description,
    DateTime? QADate,
    DateTime? PREDate,
    DateTime? PROTDate,
    bool HasConflict
);

public record TimelineMilestone(
    int Id,
    string Title,
    string? Description,
    DateTime DueDate,
    DateTime? CompletedDate,
    string Status
);

public record TimelineDeliverable(
    int Id,
    string Title,
    int? CommitmentId,
    string? CommitmentTitle,
    string Status,
    DateTime? QADate,
    DateTime? PREDate,
    DateTime? PROTDate
);

public record TimelineEnvironmentDelivery(
    int Id,
    int? CommitmentId,
    string? CommitmentTitle,
    int? DeliverableId,
    string? DeliverableTitle,
    string Environment,
    DateTime? PlannedDate,
    DateTime? ActualDate,
    string Status
);

public record TimelineAlert(
    int Id,
    string AlertType,
    string Severity,
    string Message,
    string Status,
    int? DeveloperId,
    string? DeveloperName,
    int? CommitmentId,
    string? CommitmentTitle,
    int? RelatedCommitmentId,
    string? RelatedCommitmentTitle,
    DateTime? ConflictStartDate,
    DateTime? ConflictEndDate
);
