using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.Services.AzureDevOps;
using ProjectManagement.Api.Services.AI;
using NSubstitute;

namespace ProjectManagement.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly string _documentStorage = Path.Combine(Path.GetTempPath(), $"ProjectManagementTests-{Guid.NewGuid():N}");
    public IAzureDevOpsService? AzureDevOpsService { get; set; }
    public bool AllowAnonymousDevelopment { get; set; }
    public string TestEnvironment { get; set; } = "Development";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentStorage:Directory"] = _documentStorage,
                ["AzureAd:TenantId"] = "11111111-1111-1111-1111-111111111111",
                ["AzureAd:ClientId"] = "22222222-2222-2222-2222-222222222222",
                ["AzureDevOps:OrganizationUrl"] = "https://dev.azure.com/test-organization",
                ["Authentication:AllowAnonymousInDevelopment"] = AllowAnonymousDevelopment.ToString()
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Add InMemory database
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            var provider = Substitute.For<IAiAnalysisProvider>();
            provider.IsConfigured.Returns(false);
            provider.MissingConfiguration.Returns(["AzureAI:Endpoint", "AzureAI:ApiKey", "AzureAI:DeploymentName"]);
            services.AddScoped(_ => provider);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                _ => { });

            if (AzureDevOpsService is not null)
            {
                services.RemoveAll<IAzureDevOpsService>();
                services.AddSingleton(AzureDevOpsService);
            }
        });

        builder.UseEnvironment(TestEnvironment);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_documentStorage))
            Directory.Delete(_documentStorage, recursive: true);
    }
}
