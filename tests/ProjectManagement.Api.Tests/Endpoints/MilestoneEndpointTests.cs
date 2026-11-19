using System.Net;
using System.Net.Http.Json;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Tests.Endpoints;

public class MilestoneEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MilestoneEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMilestones_ReturnsOkWithData()
    {
        var response = await _client.GetAsync("/api/milestones");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var milestones = await response.Content.ReadFromJsonAsync<List<MilestoneResponse>>();
        Assert.NotNull(milestones);
        Assert.True(milestones.Count >= 6);
    }

    [Fact]
    public async Task GetMilestones_FilterByProject_ReturnsFiltered()
    {
        var response = await _client.GetAsync("/api/milestones?projectId=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var milestones = await response.Content.ReadFromJsonAsync<List<MilestoneResponse>>();
        Assert.NotNull(milestones);
        Assert.All(milestones, m => Assert.Equal(1, m.ProjectId));
    }

    [Fact]
    public async Task GetMilestoneById_ExistingId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/milestones/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var milestone = await response.Content.ReadFromJsonAsync<MilestoneResponse>();
        Assert.NotNull(milestone);
        Assert.Equal(1, milestone.Id);
    }

    [Fact]
    public async Task CreateMilestone_ValidData_ReturnsCreated()
    {
        var request = new CreateMilestoneRequest(
            ProjectId: 1,
            Title: "Nuevo Milestone Test",
            Description: "Desc",
            DueDate: new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc),
            Status: null
        );

        var response = await _client.PostAsJsonAsync("/api/milestones", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var milestone = await response.Content.ReadFromJsonAsync<MilestoneResponse>();
        Assert.NotNull(milestone);
        Assert.Equal("Nuevo Milestone Test", milestone.Title);
    }

    [Fact]
    public async Task CreateMilestone_InvalidProject_ReturnsBadRequest()
    {
        var request = new CreateMilestoneRequest(
            ProjectId: 999,
            Title: "Invalid",
            Description: null,
            DueDate: new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc),
            Status: null
        );

        var response = await _client.PostAsJsonAsync("/api/milestones", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
