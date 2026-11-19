using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Services.Dashboard;

public sealed class ProjectDashboardService(AppDbContext db) : IProjectDashboardService
{
    private static readonly string[] OpenStatuses = ["Pending", "InProgress", "Delayed"];
    private static readonly string[] CommitmentStatuses = ["Pending", "InProgress", "Completed", "Delayed", "Cancelled"];

    public async Task<DashboardSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var endOfUpcomingWindow = today.AddDays(31);

        var totalProjects = await db.Projects.CountAsync(cancellationToken);
        var activeProjects = await db.Projects.CountAsync(
            project => project.Status == "Active",
            cancellationToken);

        var statusCounts = await db.Commitments
            .AsNoTracking()
            .GroupBy(commitment => commitment.Status)
            .Select(group => new DashboardStatusCountResponse(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        var statusCountByName = statusCounts.ToDictionary(item => item.Status, item => item.Count);
        var totalCommitments = statusCounts.Sum(item => item.Count);
        var openCommitments = statusCounts
            .Where(item => OpenStatuses.Contains(item.Status))
            .Sum(item => item.Count);

        var alertCounts = await db.ProjectAlerts
            .AsNoTracking()
            .Where(alert => alert.Status != "Resolved")
            .GroupBy(alert => alert.Severity)
            .Select(group => new { Severity = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var alertCountBySeverity = alertCounts.ToDictionary(item => item.Severity, item => item.Count);

        var overdueCommitments = await db.Commitments.CountAsync(
            commitment => OpenStatuses.Contains(commitment.Status)
                && commitment.CommitmentDate < today,
            cancellationToken);
        var upcomingCommitments = await db.Commitments.CountAsync(
            commitment => OpenStatuses.Contains(commitment.Status)
                && commitment.CommitmentDate >= today
                && commitment.CommitmentDate < endOfUpcomingWindow,
            cancellationToken);

        var projectWorkloadCounts = await db.Commitments
            .AsNoTracking()
            .Where(commitment => OpenStatuses.Contains(commitment.Status))
            .GroupBy(commitment => commitment.ProjectId)
            .Select(group => new { ProjectId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var projectIds = projectWorkloadCounts.Select(item => item.ProjectId).ToArray();
        var projectNames = await db.Projects
            .AsNoTracking()
            .Where(project => projectIds.Contains(project.Id))
            .Select(project => new DashboardWorkloadResponse(project.Id, project.Name, 0))
            .ToListAsync(cancellationToken);
        var projectCountsById = projectWorkloadCounts.ToDictionary(item => item.ProjectId, item => item.Count);
        var pendingByProject = projectNames
            .Select(project => project with { Count = projectCountsById[project.Id] })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name)
            .ToList();

        var developerWorkloadCounts = await db.Commitments
            .AsNoTracking()
            .Where(commitment => OpenStatuses.Contains(commitment.Status))
            .GroupBy(commitment => commitment.DeveloperId)
            .Select(group => new { DeveloperId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var developerIds = developerWorkloadCounts.Select(item => item.DeveloperId).ToArray();
        var developerNames = await db.Developers
            .AsNoTracking()
            .Where(developer => developerIds.Contains(developer.Id))
            .Select(developer => new DashboardWorkloadResponse(
                developer.Id,
                developer.User.DisplayName,
                0))
            .ToListAsync(cancellationToken);
        var developerCountsById = developerWorkloadCounts.ToDictionary(item => item.DeveloperId, item => item.Count);
        var pendingByDeveloper = developerNames
            .Select(developer => developer with { Count = developerCountsById[developer.Id] })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name)
            .ToList();

        var registeredDevelopers = await db.Developers.CountAsync(cancellationToken);
        var registeredProductOwners = await db.ProductOwners.CountAsync(cancellationToken);

        var statusDistribution = CommitmentStatuses
            .Select(status => new DashboardStatusCountResponse(
                status,
                statusCountByName.GetValueOrDefault(status)))
            .ToList();

        return new DashboardSummaryResponse(
            totalProjects,
            activeProjects,
            totalCommitments,
            statusCountByName.GetValueOrDefault("Pending"),
            statusCountByName.GetValueOrDefault("InProgress"),
            statusCountByName.GetValueOrDefault("Completed"),
            alertCountBySeverity.GetValueOrDefault("Critical"),
            alertCountBySeverity.GetValueOrDefault("Warning"),
            registeredDevelopers,
            registeredProductOwners,
            openCommitments,
            overdueCommitments,
            upcomingCommitments,
            pendingByProject,
            pendingByDeveloper,
            statusDistribution);
    }
}
