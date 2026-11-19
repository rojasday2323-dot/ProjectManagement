using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.Endpoints;
using ProjectManagement.Api.Auth;
using ProjectManagement.Api.Middleware;
using ProjectManagement.Api.Services;
using ProjectManagement.Api.Services.AI;
using ProjectManagement.Api.Services.AzureDevOps;
using ProjectManagement.Api.Services.Documents;
using ProjectManagement.Api.Services.Dashboard;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ConflictDetectionService>();
builder.Services.AddScoped<IProjectDashboardService, ProjectDashboardService>();
builder.Services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.Configure<AzureAiFoundryOptions>(
    builder.Configuration.GetSection(AzureAiFoundryOptions.SectionName));
builder.Services.Configure<AzureAiFoundryOptions>(
    builder.Configuration.GetSection("AzureAI"));
builder.Services.AddHttpClient<AzureAiFoundryClaudeProvider>(client =>
    client.Timeout = TimeSpan.FromSeconds(180))
    .RedactLoggedHeaders(["x-api-key"]);
builder.Services.AddScoped<IAiAnalysisProvider>(services =>
    services.GetRequiredService<AzureAiFoundryClaudeProvider>());
builder.Services.AddSingleton<AiAnalysisValidator>();
builder.Services.AddScoped<IAiAnalysisService, AiAnalysisService>();
builder.Services.AddScoped<INoteAssistantService, NoteAssistantService>();
builder.Services
    .AddOptions<AzureDevOpsOptions>()
    .Bind(builder.Configuration.GetSection(AzureDevOpsOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AzureDevOpsOptions>, AzureDevOpsOptionsValidator>();
builder.Services.AddScoped<IAzureDevOpsClientFactory, AzureDevOpsClientFactory>();
builder.Services.AddScoped<IAzureDevOpsService, AzureDevOpsService>();
builder.Services.AddHttpContextAccessor();

var entraTenantId = builder.Configuration["AzureAd:TenantId"];
var entraClientId = builder.Configuration["AzureAd:ClientId"];
var entraConfigured = Guid.TryParse(entraTenantId, out _) && Guid.TryParse(entraClientId, out _);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

if (!entraConfigured)
{
    builder.Services
        .AddAuthentication("EntraNotConfigured")
        .AddScheme<AuthenticationSchemeOptions, AuthenticationNotConfiguredHandler>(
            "EntraNotConfigured",
            _ => { });
}

var allowAnonymousDevelopment = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("Authentication:AllowAnonymousInDevelopment");

builder.Services.AddAuthorization(options =>
{
    if (!allowAnonymousDevelopment)
    {
        options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    }
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = DocumentService.MaxFileSizeBytes + 1024 * 1024;
    options.ValueCountLimit = 10;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "ProjectManagement API",
        Version = "v1",
        Description = "API para seguimiento y administración de proyectos"
    });
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandler>();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ProjectManagement API v1");
        options.RoutePrefix = "swagger";
    });
}

// Apply migrations and seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsSqlite())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }
    await DbSeeder.SeedAsync(db);
}

app.MapGet("/api/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow
})).WithTags("Health").AllowAnonymous();

// Map all endpoints
app.MapProjectEndpoints();
app.MapDashboardEndpoints();
app.MapUserEndpoints();
app.MapDeveloperEndpoints();
app.MapProductOwnerEndpoints();
app.MapCommitmentEndpoints();
app.MapMilestoneEndpoints();
app.MapDeliverableEndpoints();
app.MapEnvironmentDeliveryEndpoints();
app.MapAlertEndpoints();
app.MapAlertSubEndpoints();
app.MapTimelineEndpoints();
app.MapDocumentEndpoints();
app.MapAiAnalysisEndpoints();
app.MapNoteAssistantEndpoints();
app.MapAzureDevOpsEndpoints();

app.Run();

// Marker class for WebApplicationFactory<Program> in integration tests
public partial class Program { }
