using System.Net;
using System.Net.Http.Json;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Tests.Endpoints;

public class DashboardEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DashboardEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetSummary_ReturnsIndicatorsMatchingExistingApiResources()
    {
        var response = await _client.GetAsync("/api/dashboard/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryResponse>();
        Assert.NotNull(summary);

        var projects = await _client.GetFromJsonAsync<List<ProjectResponse>>("/api/projects");
        var commitments = await _client.GetFromJsonAsync<List<CommitmentResponse>>("/api/commitments");
        var developers = await _client.GetFromJsonAsync<List<DeveloperResponse>>("/api/developers?includeInactive=true");
        var productOwners = await _client.GetFromJsonAsync<List<ProductOwnerResponse>>("/api/product-owners");
        var alerts = await _client.GetFromJsonAsync<AlertSummaryResponse>("/api/alerts/summary");

        Assert.NotNull(projects);
        Assert.NotNull(commitments);
        Assert.NotNull(developers);
        Assert.NotNull(productOwners);
        Assert.NotNull(alerts);

        Assert.Equal(projects.Count, summary.TotalProjects);
        Assert.Equal(projects.Count(project => project.Status == "Active"), summary.ActiveProjects);
        Assert.Equal(commitments.Count, summary.TotalCommitments);
        Assert.Equal(commitments.Count(commitment => commitment.Status == "Pending"), summary.PendingCommitments);
        Assert.Equal(commitments.Count(commitment => commitment.Status == "InProgress"), summary.InProgressCommitments);
        Assert.Equal(commitments.Count(commitment => commitment.Status == "Completed"), summary.CompletedCommitments);
        Assert.Equal(alerts.Critical, summary.CriticalAlerts);
        Assert.Equal(alerts.Warning, summary.WarningAlerts);
        Assert.Equal(developers.Count, summary.RegisteredDevelopers);
        Assert.Equal(productOwners.Count, summary.RegisteredProductOwners);

        var openCommitments = commitments
            .Where(commitment => IsOpen(commitment.Status))
            .ToList();
        Assert.Equal(openCommitments.Count, summary.OpenCommitments);

        var today = DateTime.UtcNow.Date;
        Assert.Equal(
            openCommitments.Count(commitment => commitment.CommitmentDate.Date < today),
            summary.OverdueCommitments);
        Assert.Equal(
            openCommitments.Count(commitment =>
            {
                var dueDate = commitment.CommitmentDate.Date;
                return dueDate >= today && dueDate < today.AddDays(31);
            }),
            summary.UpcomingCommitments);

        var projectWorkload = openCommitments
            .GroupBy(commitment => new { commitment.ProjectId, commitment.ProjectName })
            .Select(group => new DashboardWorkloadResponse(group.Key.ProjectId, group.Key.ProjectName, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name);
        Assert.Equal(projectWorkload, summary.PendingByProject);

        var developerWorkload = openCommitments
            .GroupBy(commitment => new { commitment.DeveloperId, commitment.DeveloperName })
            .Select(group => new DashboardWorkloadResponse(group.Key.DeveloperId, group.Key.DeveloperName, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name);
        Assert.Equal(developerWorkload, summary.PendingByDeveloper);

        Assert.Equal(
            new[] { "Pending", "InProgress", "Completed", "Delayed", "Cancelled" },
            summary.StatusDistribution.Select(item => item.Status));
        Assert.Equal(summary.TotalCommitments, summary.StatusDistribution.Sum(item => item.Count));
    }

    private static bool IsOpen(string status) =>
        status is "Pending" or "InProgress" or "Delayed";
}
