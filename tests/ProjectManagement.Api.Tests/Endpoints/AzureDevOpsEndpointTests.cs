using System.Net;
using System.Text.Json;
using NSubstitute;
using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Services.AzureDevOps;

namespace ProjectManagement.Api.Tests.Endpoints;

public sealed class AzureDevOpsEndpointTests
{
    [Fact]
    public async Task StatusEndpoint_RequiresAnAuthenticatedUser()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/azure-devops/status");
        request.Headers.Add("X-Test-Anonymous", "true");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StatusEndpoint_ReturnsSafePermissionErrorWithoutToken()
    {
        var service = Substitute.For<IAzureDevOpsService>();
        service.GetStatusAsync(Arg.Any<CancellationToken>())
            .Returns<Task<AzureDevOpsStatusResponse>>(_ => throw new AzureDevOpsIntegrationException(
                403,
                "permission_denied",
                "Azure DevOps denegó el acceso a la organización configurada."));
        using var factory = new CustomWebApplicationFactory { AzureDevOpsService = service };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/azure-devops/status");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("permission_denied", body);
        Assert.DoesNotContain("access_token", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("delegated-test-token", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProjectsEndpoint_ReturnsNetworkFailureInsteadOfAnEmptyList()
    {
        var service = Substitute.For<IAzureDevOpsService>();
        service.GetProjectsAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<AzureDevOpsProjectResponse>>>(_ => throw new HttpRequestException());
        using var factory = new CustomWebApplicationFactory { AzureDevOpsService = service };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/azure-devops/projects");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal("azure_devops_network_error", problem.RootElement.GetProperty("code").GetString());
    }
}
