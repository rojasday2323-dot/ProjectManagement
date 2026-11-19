using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.Models;
using ProjectManagement.Api.Services.AzureDevOps;
using NSubstitute;

namespace ProjectManagement.Api.Tests.Services;

public sealed class AzureDevOpsServiceTests
{
    private const string OrganizationUrl = "https://dev.azure.com/test-organization";

    [Fact]
    public async Task GetProjectsAsync_UsesDelegatedTokenAndConfiguredOrganization()
    {
        var (service, tokenAcquisition, clientFactory, client) = CreateService();
        var projectId = Guid.NewGuid();
        client.GetProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TeamProjectReference>>(
                [new TeamProjectReference { Id = projectId, Name = "Visible project" }]));

        var projects = await service.GetProjectsAsync(CancellationToken.None);

        Assert.Single(projects);
        Assert.Equal("Visible project", projects[0].Name);
        await tokenAcquisition.Received(1).GetAccessTokenForUserAsync(
            Arg.Is<IEnumerable<string>>(scopes => scopes.SequenceEqual(
                new[] { AzureDevOpsOptions.UserImpersonationScope })));
        clientFactory.Received(1).Create(new Uri(OrganizationUrl), "delegated-test-token");
    }

    [Fact]
    public async Task GetStatusAsync_ReportsConnectedOnlyAfterReadingOrganization()
    {
        var (service, _, _, client) = CreateService();
        client.GetProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TeamProjectReference>>(
                [new TeamProjectReference { Id = Guid.NewGuid(), Name = "Visible project" }]));

        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.True(status.Connected);
        Assert.Equal(1, status.VisibleProjectCount);
        Assert.Equal(OrganizationUrl, status.OrganizationUrl);
    }

    [Fact]
    public async Task GetProjectWorkItemsAsync_MapsFieldsAndRetainsNoTokenInResponse()
    {
        var (service, _, _, client) = CreateService();
        var projectId = Guid.NewGuid();
        client.GetProjectAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TeamProjectReference { Id = projectId, Name = "Project Alpha" }));
        client.GetProjectWorkItemsAsync(projectId, 50, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorkItem>>(
            [
                new WorkItem
                {
                    Id = 412,
                    Url = "https://dev.azure.com/test-organization/project/_apis/wit/workItems/412",
                    Fields = new Dictionary<string, object>
                    {
                        ["System.Title"] = "Review release plan",
                        ["System.WorkItemType"] = "Task",
                        ["System.State"] = "Active",
                        ["System.AssignedTo"] = "Developer Example",
                        ["System.TeamProject"] = "Project Alpha"
                    }
                }
            ]));

        var items = await service.GetProjectWorkItemsAsync(projectId, 50, CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal(412, item.Id);
        Assert.Equal("Review release plan", item.Title);
        Assert.Equal("Task", item.WorkItemType);
        Assert.Equal("Developer Example", item.AssignedTo);
        Assert.Null(item.CommitmentId);
        Assert.DoesNotContain("delegated-test-token", System.Text.Json.JsonSerializer.Serialize(item));
    }

    [Fact]
    public async Task GetWorkItemAsync_IncludesExistingLocalCommitmentRelationship()
    {
        var db = await TestDbHelper.CreateSeededContext();
        db.AzureDevOpsWorkItems.Add(new AzureDevOpsWorkItem
        {
            ProjectId = 1,
            CommitmentId = 1,
            DevOpsWorkItemId = 412,
            DevOpsOrganization = "test-organization",
            DevOpsProject = "Project Alpha",
            WorkItemType = "Task",
            Title = "Remote title"
        });
        await db.SaveChangesAsync();

        var (service, _, _, client) = CreateService(db);
        client.GetWorkItemAsync(412, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new WorkItem
            {
                Id = 412,
                Fields = new Dictionary<string, object>
                {
                    ["System.Title"] = "Remote title",
                    ["System.WorkItemType"] = "Task",
                    ["System.TeamProject"] = "Project Alpha"
                }
            }));

        var detail = await service.GetWorkItemAsync(412, CancellationToken.None);

        Assert.Equal(1, detail.WorkItem.LocalProjectId);
        Assert.Equal(1, detail.WorkItem.CommitmentId);
        Assert.Equal("Compromiso Test 1", detail.WorkItem.CommitmentTitle);
        await db.DisposeAsync();
    }

    [Fact]
    public async Task GetProjectsAsync_RejectsUnauthenticatedUserBeforeTokenAcquisition()
    {
        var (service, tokenAcquisition, _, _) = CreateService(authenticated: false);

        var exception = await Assert.ThrowsAsync<AzureDevOpsIntegrationException>(
            () => service.GetProjectsAsync(CancellationToken.None));

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal("authentication_required", exception.Code);
        await tokenAcquisition.DidNotReceive().GetAccessTokenForUserAsync(Arg.Any<IEnumerable<string>>());
    }

    [Fact]
    public async Task GetProjectsAsync_ReportsMissingConsentWithoutTokenContents()
    {
        var tokenAcquisition = Substitute.For<ITokenAcquisition>();
        tokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<IEnumerable<string>>())
            .Returns<Task<string>>(_ => throw new MsalUiRequiredException("consent_required", "Consent is missing."));
        var clientFactory = Substitute.For<IAzureDevOpsClientFactory>();
        var service = CreateService(tokenAcquisition, clientFactory);

        var exception = await Assert.ThrowsAsync<AzureDevOpsIntegrationException>(
            () => service.GetProjectsAsync(CancellationToken.None));

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal("consent_required", exception.Code);
        Assert.DoesNotContain("token", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetProjectsAsync_MapsAzureDevOpsAccessDenialToForbidden()
    {
        var (service, _, _, client) = CreateService();
        client.GetProjectsAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<TeamProjectReference>>>(_ =>
                throw new VssUnauthorizedException("access denied"));

        var exception = await Assert.ThrowsAsync<AzureDevOpsIntegrationException>(
            () => service.GetProjectsAsync(CancellationToken.None));

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal("permission_denied", exception.Code);
        Assert.DoesNotContain("access denied", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetProjectsAsync_MapsTokenProviderFailureToServiceUnavailable()
    {
        var tokenAcquisition = Substitute.For<ITokenAcquisition>();
        tokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<IEnumerable<string>>())
            .Returns<Task<string>>(_ => throw new MsalServiceException("temporarily_unavailable", "Provider response omitted."));
        var service = CreateService(tokenAcquisition, Substitute.For<IAzureDevOpsClientFactory>());

        var exception = await Assert.ThrowsAsync<AzureDevOpsIntegrationException>(
            () => service.GetProjectsAsync(CancellationToken.None));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("token_acquisition_failed", exception.Code);
        Assert.DoesNotContain("Provider response omitted", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetProjectsAsync_RequiresBackendCredentialWithoutAttemptingTokenAcquisition()
    {
        var (service, tokenAcquisition, _, _) = CreateService(credentialConfigured: false);

        var exception = await Assert.ThrowsAsync<AzureDevOpsIntegrationException>(
            () => service.GetProjectsAsync(CancellationToken.None));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("backend_credential_not_configured", exception.Code);
        await tokenAcquisition.DidNotReceive().GetAccessTokenForUserAsync(Arg.Any<IEnumerable<string>>());
    }

    [Theory]
    [InlineData("http://dev.azure.com/org", false)]
    [InlineData("https://example.com/org", false)]
    [InlineData("https://dev.azure.com/org/path", false)]
    [InlineData("https://dev.azure.com/org?redirect=example.com", false)]
    [InlineData("https://user@dev.azure.com/org", false)]
    [InlineData("https://dev.azure.com:8443/org", false)]
    [InlineData("https://dev.azure.com/org", true)]
    public void OrganizationUrl_IsAcceptedOnlyForConfiguredDevAzureOrganization(string url, bool expected)
    {
        Assert.Equal(expected, AzureDevOpsOptions.IsValidOrganizationUrl(url));
    }

    [Fact]
    public void OrganizationOptions_RejectInvalidUrlAtStartupValidation()
    {
        var result = new AzureDevOpsOptionsValidator().Validate(
            Options.DefaultName,
            new AzureDevOpsOptions { OrganizationUrl = "https://example.com/org" });

        Assert.False(result.Succeeded);
    }

    private static (
        IAzureDevOpsService Service,
        ITokenAcquisition TokenAcquisition,
        IAzureDevOpsClientFactory ClientFactory,
        IAzureDevOpsClient Client) CreateService(
        AppDbContext? db = null,
        bool authenticated = true,
        bool credentialConfigured = true)
    {
        var tokenAcquisition = Substitute.For<ITokenAcquisition>();
        tokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<IEnumerable<string>>())
            .Returns("delegated-test-token");
        var clientFactory = Substitute.For<IAzureDevOpsClientFactory>();
        var client = Substitute.For<IAzureDevOpsClient>();
        clientFactory.Create(Arg.Any<Uri>(), Arg.Any<string>()).Returns(client);
        var httpContext = new DefaultHttpContext();
        if (authenticated)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-user")], "unit-test"));
        }

        var settings = credentialConfigured
            ? new Dictionary<string, string?> { ["AzureAd:ClientSecret"] = "unit-test-credential" }
            : new Dictionary<string, string?>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var context = db ?? TestDbHelper.CreateContext();
        var service = new AzureDevOpsService(
            tokenAcquisition,
            new HttpContextAccessor { HttpContext = httpContext },
            clientFactory,
            Options.Create(new AzureDevOpsOptions { OrganizationUrl = OrganizationUrl }),
            configuration,
            context);
        return (service, tokenAcquisition, clientFactory, client);
    }

    private static IAzureDevOpsService CreateService(
        ITokenAcquisition tokenAcquisition,
        IAzureDevOpsClientFactory clientFactory)
    {
        return new AzureDevOpsService(
            tokenAcquisition,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-user")], "unit-test"))
                }
            },
            clientFactory,
            Options.Create(new AzureDevOpsOptions { OrganizationUrl = OrganizationUrl }),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AzureAd:ClientSecret"] = "unit-test-credential"
                })
                .Build(),
            TestDbHelper.CreateContext());
    }
}
