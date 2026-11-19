using System.Net;
using System.Net.Http.Json;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Tests.Endpoints;

public class TimelineEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TimelineEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetTimeline_ExistingProject_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/projects/1/timeline");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var timeline = await response.Content.ReadFromJsonAsync<TimelineResponse>();
        Assert.NotNull(timeline);
        Assert.Equal(1, timeline.Project.Id);
        Assert.NotEmpty(timeline.Commitments);
    }

    [Fact]
    public async Task GetTimeline_HasMilestones()
    {
        var response = await _client.GetAsync("/api/projects/1/timeline");
        var timeline = await response.Content.ReadFromJsonAsync<TimelineResponse>();

        Assert.NotNull(timeline);
        Assert.NotEmpty(timeline.Milestones);
    }

    [Fact]
    public async Task GetTimeline_HasDeliverables()
    {
        var response = await _client.GetAsync("/api/projects/1/timeline");
        var timeline = await response.Content.ReadFromJsonAsync<TimelineResponse>();

        Assert.NotNull(timeline);
        Assert.NotEmpty(timeline.Deliverables);
    }

    [Fact]
    public async Task GetTimeline_HasAlerts()
    {
        var response = await _client.GetAsync("/api/projects/1/timeline");
        var timeline = await response.Content.ReadFromJsonAsync<TimelineResponse>();

        Assert.NotNull(timeline);
        Assert.NotEmpty(timeline.Alerts);
    }

    [Fact]
    public async Task GetTimeline_CommitmentsHaveConflictFlag()
    {
        var response = await _client.GetAsync("/api/projects/1/timeline");
        var timeline = await response.Content.ReadFromJsonAsync<TimelineResponse>();

        Assert.NotNull(timeline);
        Assert.Contains(timeline.Commitments, c => c.HasConflict);
    }

    [Fact]
    public async Task GetTimeline_ProjectWithNoCommitments_ReturnsEmpty()
    {
        var response = await _client.GetAsync("/api/projects/3/timeline");
        var timeline = await response.Content.ReadFromJsonAsync<TimelineResponse>();

        Assert.NotNull(timeline);
        Assert.Equal(3, timeline.Project.Id);
        // Project 3 has commitments from seed, but no deliverables
        Assert.Empty(timeline.Deliverables);
    }

    [Fact]
    public async Task GetTimeline_NonExistingProject_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/projects/999/timeline");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTimeline_ProjectDatesAreValid()
    {
        var response = await _client.GetAsync("/api/projects/1/timeline");
        var timeline = await response.Content.ReadFromJsonAsync<TimelineResponse>();

        Assert.NotNull(timeline);
        Assert.True(timeline.Project.StartDate < (timeline.Project.EndDate ?? DateTime.MaxValue));
    }
}
