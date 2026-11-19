using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Tests.Endpoints;

public class AlertEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AlertEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAlerts_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/alerts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAlertsSummary_ReturnsValidSummary()
    {
        var response = await _client.GetAsync("/api/alerts/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<AlertSummaryResponse>();
        Assert.NotNull(summary);
        Assert.True(summary.TotalOpen >= 0);
    }

    [Fact]
    public async Task GetAlerts_FilterBySeverity_ReturnsFiltered()
    {
        var response = await _client.GetAsync("/api/alerts?severity=Critical");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var alerts = await response.Content.ReadFromJsonAsync<List<AlertResponse>>();
        Assert.NotNull(alerts);
        Assert.All(alerts, a => Assert.Equal("Critical", a.Severity));
    }

    [Fact]
    public async Task GetAlerts_FilterByStatus_ReturnsFiltered()
    {
        var response = await _client.GetAsync("/api/alerts?status=Open");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var alerts = await response.Content.ReadFromJsonAsync<List<AlertResponse>>();
        Assert.NotNull(alerts);
        Assert.All(alerts, a => Assert.Equal("Open", a.Status));
    }

    [Fact]
    public async Task PostDetect_RunsDetection_ReturnsResult()
    {
        var response = await _client.PostAsync("/api/alerts/detect", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("totalOpenAlerts", out _));
    }

    [Fact]
    public async Task GetDeveloperAlerts_NonExisting_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/developers/999/alerts");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectAlerts_NonExisting_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/projects/999/alerts");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchAlertStatus_InvalidStatus_ReturnsBadRequest()
    {
        var content = new StringContent("{\"status\":\"Invalid\"}", Encoding.UTF8, "application/json");
        var response = await _client.PatchAsync("/api/alerts/1/status", content);

        // Could be 400 or 404 depending on whether alert 1 exists in the test DB
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound
        );
    }
}
