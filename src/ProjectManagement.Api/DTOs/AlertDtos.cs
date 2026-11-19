namespace ProjectManagement.Api.DTOs;

public record AlertResponse(
    int Id,
    int ProjectId,
    string ProjectName,
    int? DeveloperId,
    string? DeveloperName,
    int? CommitmentId,
    string? CommitmentTitle,
    int? RelatedCommitmentId,
    string? RelatedCommitmentTitle,
    string AlertType,
    string Severity,
    string Message,
    string Status,
    DateTime? ConflictStartDate,
    DateTime? ConflictEndDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record UpdateAlertStatusRequest(
    string Status
);

public record AlertSummaryResponse(
    int TotalOpen,
    int Critical,
    int Warning,
    int Info,
    List<AlertResponse> RecentAlerts
);
