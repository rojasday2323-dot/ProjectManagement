using System.Net;
using System.Net.Http.Json;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Tests.Endpoints;

public class CommitmentEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CommitmentEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCommitments_ReturnsOkWithData()
    {
        var response = await _client.GetAsync("/api/commitments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var commitments = await response.Content.ReadFromJsonAsync<List<CommitmentResponse>>();
        Assert.NotNull(commitments);
        Assert.True(commitments.Count >= 10);
    }

    [Fact]
    public async Task GetCommitments_FilterByDeveloper_ReturnsFiltered()
    {
        var response = await _client.GetAsync("/api/commitments?developerId=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var commitments = await response.Content.ReadFromJsonAsync<List<CommitmentResponse>>();
        Assert.NotNull(commitments);
        Assert.All(commitments, c => Assert.Equal(1, c.DeveloperId));
    }

    [Fact]
    public async Task GetCommitments_FilterByProject_ReturnsFiltered()
    {
        var response = await _client.GetAsync("/api/commitments?projectId=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var commitments = await response.Content.ReadFromJsonAsync<List<CommitmentResponse>>();
        Assert.NotNull(commitments);
        Assert.All(commitments, c => Assert.Equal(2, c.ProjectId));
    }

    [Fact]
    public async Task GetCommitments_FilterByStatus_ReturnsFiltered()
    {
        var response = await _client.GetAsync("/api/commitments?status=Completed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var commitments = await response.Content.ReadFromJsonAsync<List<CommitmentResponse>>();
        Assert.NotNull(commitments);
        Assert.All(commitments, c => Assert.Equal("Completed", c.Status));
    }

    [Fact]
    public async Task GetCommitments_CombinedFilters_ReturnsFiltered()
    {
        var response = await _client.GetAsync("/api/commitments?projectId=1&status=InProgress");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var commitments = await response.Content.ReadFromJsonAsync<List<CommitmentResponse>>();
        Assert.NotNull(commitments);
        Assert.All(commitments, c =>
        {
            Assert.Equal(1, c.ProjectId);
            Assert.Equal("InProgress", c.Status);
        });
    }

    [Fact]
    public async Task GetCommitmentById_ExistingId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/commitments/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var commitment = await response.Content.ReadFromJsonAsync<CommitmentResponse>();
        Assert.NotNull(commitment);
        Assert.Equal(1, commitment.Id);
        Assert.NotEmpty(commitment.DeveloperName);
        Assert.NotEmpty(commitment.ProjectName);
    }

    [Fact]
    public async Task GetCommitmentById_NonExistingId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/commitments/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateCommitment_ValidData_ReturnsCreated()
    {
        var request = new CreateCommitmentRequest(
            ProjectId: 1,
            DeveloperId: 1,
            ProductOwnerId: 1,
            Title: "Compromiso Nuevo Test",
            Description: "Descripción de prueba",
            StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            CommitmentDate: new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
            Status: null,
            Priority: "High",
            QADate: new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            PREDate: new DateTime(2026, 8, 22, 0, 0, 0, DateTimeKind.Utc),
            PROTDate: new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
            Notes: null
        );

        var response = await _client.PostAsJsonAsync("/api/commitments", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var commitment = await response.Content.ReadFromJsonAsync<CommitmentResponse>();
        Assert.NotNull(commitment);
        Assert.Equal("Compromiso Nuevo Test", commitment.Title);
        Assert.Equal("Pending", commitment.Status);
    }

    [Fact]
    public async Task CreateCommitment_InvalidProjectId_ReturnsBadRequest()
    {
        var request = new CreateCommitmentRequest(
            ProjectId: 999,
            DeveloperId: 1,
            ProductOwnerId: 1,
            Title: "Invalid",
            Description: "Desc",
            StartDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CommitmentDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Status: null, Priority: null,
            QADate: null, PREDate: null, PROTDate: null, Notes: null
        );

        var response = await _client.PostAsJsonAsync("/api/commitments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Contains("Proyecto", error.Message);
    }

    [Fact]
    public async Task CreateCommitment_InvalidDeveloperId_ReturnsBadRequest()
    {
        var request = new CreateCommitmentRequest(
            ProjectId: 1,
            DeveloperId: 999,
            ProductOwnerId: 1,
            Title: "Invalid Dev",
            Description: "Desc",
            StartDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CommitmentDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Status: null, Priority: null,
            QADate: null, PREDate: null, PROTDate: null, Notes: null
        );

        var response = await _client.PostAsJsonAsync("/api/commitments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Contains("Desarrollador", error.Message);
    }

    [Fact]
    public async Task CreateCommitment_CommitmentDateBeforeStartDate_ReturnsBadRequest()
    {
        var request = new CreateCommitmentRequest(
            ProjectId: 1,
            DeveloperId: 1,
            ProductOwnerId: 1,
            Title: "Bad dates",
            Description: "Desc",
            StartDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            CommitmentDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Status: null, Priority: null,
            QADate: null, PREDate: null, PROTDate: null, Notes: null
        );

        var response = await _client.PostAsJsonAsync("/api/commitments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
