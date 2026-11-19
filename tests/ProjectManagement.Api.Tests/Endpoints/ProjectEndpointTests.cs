using System.Net;
using System.Net.Http.Json;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Tests.Endpoints;

public class ProjectEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProjectEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProjects_ReturnsOkWithSeededData()
    {
        var response = await _client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var projects = await response.Content.ReadFromJsonAsync<List<ProjectResponse>>();
        Assert.NotNull(projects);
        Assert.True(projects.Count >= 3);
    }

    [Fact]
    public async Task GetProjects_IncludesCommitmentStatusCountsAndActiveProductOwners()
    {
        var response = await _client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var projects = await response.Content.ReadFromJsonAsync<List<ProjectResponse>>();
        Assert.NotNull(projects);

        foreach (var project in projects)
        {
            var commitments = await _client.GetFromJsonAsync<List<CommitmentResponse>>(
                $"/api/commitments?projectId={project.Id}");
            var members = await _client.GetFromJsonAsync<List<ProjectMemberResponse>>(
                $"/api/projects/{project.Id}/members");

            Assert.NotNull(commitments);
            Assert.NotNull(members);
            Assert.Equal(commitments.Count, project.CommitmentCount);
            Assert.Equal(commitments.Count(item => item.Status == "Pending"), project.PendingCommitmentCount);
            Assert.Equal(commitments.Count(item => item.Status == "InProgress"), project.InProgressCommitmentCount);
            Assert.Equal(commitments.Count(item => item.Status == "Completed"), project.CompletedCommitmentCount);
            Assert.Equal(
                members.Where(member => member.IsActive && member.Role == "ProductOwner")
                    .Select(member => member.DisplayName)
                    .Distinct()
                    .OrderBy(name => name),
                project.ProductOwnerNames);
        }
    }

    [Fact]
    public async Task GetProjectById_ExistingId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/projects/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(project);
        Assert.Equal(1, project.Id);
        Assert.Equal("Portal Banca Digital v2", project.Name);
    }

    [Fact]
    public async Task GetProjectById_NonExistingId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/projects/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.False(error.Success);
    }

    [Fact]
    public async Task CreateProject_ValidData_ReturnsCreated()
    {
        var request = new CreateProjectRequest(
            "Proyecto Nuevo Test",
            "Descripción del proyecto de prueba",
            "Producto Test",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            "Planning"
        );

        var response = await _client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(project);
        Assert.Equal("Proyecto Nuevo Test", project.Name);
        Assert.Equal("Planning", project.Status);
    }

    [Fact]
    public async Task CreateProject_EmptyName_ReturnsBadRequest()
    {
        var request = new CreateProjectRequest(
            "", "Desc", "Product",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), null, null
        );

        var response = await _client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_EndDateBeforeStartDate_ReturnsBadRequest()
    {
        var request = new CreateProjectRequest(
            "Bad Dates Project", "Desc", "Product",
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            null
        );

        var response = await _client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProject_ValidData_ReturnsOk()
    {
        var request = new UpdateProjectRequest(
            "Portal Banca Digital v2 Updated",
            "Updated description",
            "Banca Digital",
            new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            "Active"
        );

        var response = await _client.PutAsJsonAsync("/api/projects/1", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(project);
        Assert.Contains("Updated", project.Name);
    }

    [Fact]
    public async Task UpdateProject_NonExistingId_ReturnsNotFound()
    {
        var request = new UpdateProjectRequest(
            "Name", "Desc", "Product",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), null, "Active"
        );

        var response = await _client.PutAsJsonAsync("/api/projects/999", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectMembers_ExistingProject_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/projects/1/members");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var members = await response.Content.ReadFromJsonAsync<List<ProjectMemberResponse>>();
        Assert.NotNull(members);
        Assert.True(members.Count > 0);
    }

    [Fact]
    public async Task GetProjectMembers_NonExistingProject_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/projects/999/members");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
