namespace ProjectManagement.Api.DTOs;

/// <summary>Aggregated project, commitment, alert, and workload indicators for the dashboard.</summary>
public sealed record DashboardSummaryResponse(
    int TotalProjects,
    int ActiveProjects,
    int TotalCommitments,
    int PendingCommitments,
    int InProgressCommitments,
    int CompletedCommitments,
    int CriticalAlerts,
    int WarningAlerts,
    int RegisteredDevelopers,
    int RegisteredProductOwners,
    int OpenCommitments,
    int OverdueCommitments,
    int UpcomingCommitments,
    List<DashboardWorkloadResponse> PendingByProject,
    List<DashboardWorkloadResponse> PendingByDeveloper,
    List<DashboardStatusCountResponse> StatusDistribution
);

/// <summary>Open commitment count for a project or developer.</summary>
public sealed record DashboardWorkloadResponse(
    int Id,
    string Name,
    int Count
);

/// <summary>Commitment count for one workflow status.</summary>
public sealed record DashboardStatusCountResponse(
    string Status,
    int Count
);
