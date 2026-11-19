using System.Net;

namespace ProjectManagement.Api.Tests.Endpoints;

public sealed class DevelopmentAccessTests
{
    [Fact]
    public async Task Dashboard_AllowsAnonymousRequestsWhenDevelopmentOptInIsEnabled()
    {
        using var factory = new CustomWebApplicationFactory { AllowAnonymousDevelopment = true };
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/summary");
        request.Headers.Add("X-Test-Anonymous", "true");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentBypass_DoesNotRemoveExplicitAzureDevOpsAuthorization()
    {
        using var factory = new CustomWebApplicationFactory { AllowAnonymousDevelopment = true };
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/azure-devops/status");
        request.Headers.Add("X-Test-Anonymous", "true");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Production_IgnoresDevelopmentBypassSetting()
    {
        using var factory = new CustomWebApplicationFactory
        {
            AllowAnonymousDevelopment = true,
            TestEnvironment = "Production"
        };
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/summary");
        request.Headers.Add("X-Test-Anonymous", "true");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
