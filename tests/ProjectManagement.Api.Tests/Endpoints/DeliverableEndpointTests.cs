using System.Net;
using System.Net.Http.Json;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Tests.Endpoints;

public class DeliverableEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DeliverableEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDeliverables_ReturnsOkWithData()
    {
        var response = await _client.GetAsync("/api/deliverables");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var deliverables = await response.Content.ReadFromJsonAsync<List<DeliverableResponse>>();
        Assert.NotNull(deliverables);
        Assert.True(deliverables.Count >= 4);
    }

    [Fact]
    public async Task GetDeliverables_FilterByProject_ReturnsFiltered()
    {
        var response = await _client.GetAsync("/api/deliverables?projectId=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var deliverables = await response.Content.ReadFromJsonAsync<List<DeliverableResponse>>();
        Assert.NotNull(deliverables);
        Assert.All(deliverables, d => Assert.Equal(1, d.ProjectId));
    }

    [Fact]
    public async Task GetDeliverableById_ExistingId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/deliverables/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var deliverable = await response.Content.ReadFromJsonAsync<DeliverableResponse>();
        Assert.NotNull(deliverable);
        Assert.Equal(1, deliverable.Id);
    }

    [Fact]
    public async Task CreateDeliverable_ValidData_ReturnsCreated()
    {
        var request = new CreateDeliverableRequest(
            ProjectId: 1,
            CommitmentId: 1,
            Title: "Nuevo Entregable Test",
            Description: "Desc",
            Status: null,
            QADate: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            PREDate: null,
            PROTDate: null
        );

        var response = await _client.PostAsJsonAsync("/api/deliverables", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var deliverable = await response.Content.ReadFromJsonAsync<DeliverableResponse>();
        Assert.NotNull(deliverable);
        Assert.Equal("Nuevo Entregable Test", deliverable.Title);
    }

    [Fact]
    public async Task CreateDeliverable_InvalidProject_ReturnsBadRequest()
    {
        var request = new CreateDeliverableRequest(
            ProjectId: 999,
            CommitmentId: null,
            Title: "Invalid",
            Description: null,
            Status: null,
            QADate: null, PREDate: null, PROTDate: null
        );

        var response = await _client.PostAsJsonAsync("/api/deliverables", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
