using System.Globalization;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Services.AzureDevOps;

public sealed class AzureDevOpsService(
    ITokenAcquisition tokenAcquisition,
    IHttpContextAccessor httpContextAccessor,
    IAzureDevOpsClientFactory clientFactory,
    IOptions<AzureDevOpsOptions> options,
    IConfiguration configuration,
    AppDbContext db) : IAzureDevOpsService
{
    internal static readonly string[] WorkItemFields =
    [
        "System.Id",
        "System.Title",
        "System.WorkItemType",
        "System.State",
        "System.AssignedTo",
        "System.TeamProject",
        "System.CreatedDate",
        "System.ChangedDate",
        "Microsoft.VSTS.Scheduling.StartDate",
        "Microsoft.VSTS.Scheduling.TargetDate"
    ];

    public async Task<AzureDevOpsStatusResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        using var client = await CreateClientAsync(cancellationToken);
        var projects = await GetProjectsAsync(client, cancellationToken);
        return new AzureDevOpsStatusResponse(
            options.Value.OrganizationUrl.TrimEnd('/'),
            true,
            projects.Count);
    }

    public async Task<IReadOnlyList<AzureDevOpsProjectResponse>> GetProjectsAsync(
        CancellationToken cancellationToken)
    {
        using var client = await CreateClientAsync(cancellationToken);
        var projects = await GetProjectsAsync(client, cancellationToken);
        return projects.Select(MapProject).ToArray();
    }

    public async Task<IReadOnlyList<AzureDevOpsWorkItemResponse>> GetProjectWorkItemsAsync(
        Guid projectId,
        int top,
        CancellationToken cancellationToken)
    {
        using var client = await CreateClientAsync(cancellationToken);
        var project = await GetProjectAsync(client, projectId, cancellationToken);
        var workItems = await GetWorkItemsAsync(client, projectId, top, cancellationToken);
        var links = await GetLocalLinksAsync(project.Name, workItems, cancellationToken);
        return workItems.Select(item => MapWorkItem(item, project.Name, links)).ToArray();
    }

    public async Task<AzureDevOpsWorkItemDetailResponse> GetWorkItemAsync(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            throw new AzureDevOpsIntegrationException(400, "invalid_work_item_id", "El ID del work item debe ser positivo.");

        using var client = await CreateClientAsync(cancellationToken);
        var item = await GetWorkItemAsync(client, id, cancellationToken);
        var projectName = GetString(item.Fields, "System.TeamProject") ?? string.Empty;
        var links = await GetLocalLinksAsync(projectName, [item], cancellationToken);
        var mapped = MapWorkItem(item, projectName, links);
        var safeFields = item.Fields
            .Where(pair => WorkItemFields.Contains(pair.Key, StringComparer.Ordinal))
            .ToDictionary(pair => pair.Key, pair => SanitizeField(pair.Value), StringComparer.Ordinal);
        return new AzureDevOpsWorkItemDetailResponse(mapped, safeFields);
    }

    private async Task<IAzureDevOpsClient> CreateClientAsync(CancellationToken cancellationToken)
    {
        if (!AzureDevOpsOptions.IsValidOrganizationUrl(options.Value.OrganizationUrl))
            throw new AzureDevOpsIntegrationException(
                503,
                "azure_devops_not_configured",
                "El administrador debe configurar una URL válida de organización en Azure DevOps.");

        var hasSecret = !string.IsNullOrWhiteSpace(configuration["AzureAd:ClientSecret"]);
        var hasCertificate = configuration.GetSection("AzureAd:ClientCertificates").GetChildren().Any();
        if (!hasSecret && !hasCertificate)
            throw new AzureDevOpsIntegrationException(
                503,
                "backend_credential_not_configured",
                "El administrador debe configurar la credencial confidencial del backend mediante User Secrets o un almacén seguro.");

        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            throw new AzureDevOpsIntegrationException(401, "authentication_required", "Inicia sesión con Microsoft Entra ID para continuar.");

        string accessToken;
        try
        {
            accessToken = await tokenAcquisition
                .GetAccessTokenForUserAsync(options.Value.Scopes)
                .WaitAsync(cancellationToken);
        }
        catch (MsalUiRequiredException)
        {
            throw new AzureDevOpsIntegrationException(
                403,
                "consent_required",
                "Se requiere consentimiento para el permiso delegado de Azure DevOps. Solicita al administrador que conceda el consentimiento y vuelve a iniciar sesión.");
        }
        catch (MicrosoftIdentityWebChallengeUserException)
        {
            throw new AzureDevOpsIntegrationException(
                403,
                "consent_required",
                "Se requiere consentimiento o autenticación adicional para acceder a Azure DevOps.");
        }
        catch (MsalServiceException)
        {
            throw new AzureDevOpsIntegrationException(
                503,
                "token_acquisition_failed",
                "Microsoft Entra ID no pudo emitir el token delegado. Verifica la credencial del backend y el consentimiento configurado.");
        }
        catch (MsalClientException)
        {
            throw new AzureDevOpsIntegrationException(
                503,
                "token_acquisition_failed",
                "No fue posible adquirir el token delegado para Azure DevOps.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return clientFactory.Create(new Uri(options.Value.OrganizationUrl.TrimEnd('/')), accessToken);
    }

    private static async Task<TeamProjectReference> GetProjectAsync(
        IAzureDevOpsClient client,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetProjectAsync(projectId, cancellationToken);
        }
        catch (VssUnauthorizedException)
        {
            throw new AzureDevOpsIntegrationException(403, "permission_denied", "Azure DevOps denegó el acceso a este proyecto.");
        }
        catch (VssAuthenticationException)
        {
            throw new AzureDevOpsIntegrationException(403, "permission_denied", "Azure DevOps rechazó la autenticación delegada.");
        }
        catch (VssServiceResponseException exception)
        {
            throw MapSdkError(exception.HttpStatusCode, "project_not_found", "El proyecto no existe o no es visible para este usuario.");
        }
        catch (VssServiceException)
        {
            throw new AzureDevOpsIntegrationException(502, "azure_devops_request_failed", "Azure DevOps no pudo consultar el proyecto.");
        }
    }

    private static async Task<IReadOnlyList<TeamProjectReference>> GetProjectsAsync(
        IAzureDevOpsClient client,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetProjectsAsync(cancellationToken);
        }
        catch (VssUnauthorizedException)
        {
            throw new AzureDevOpsIntegrationException(403, "permission_denied", "Azure DevOps denegó el acceso a la organización.");
        }
        catch (VssAuthenticationException)
        {
            throw new AzureDevOpsIntegrationException(403, "permission_denied", "Azure DevOps rechazó la autenticación delegada.");
        }
        catch (VssServiceResponseException exception)
        {
            throw MapSdkError(exception.HttpStatusCode, "organization_not_found", "La organización no existe o no está disponible para este usuario.");
        }
        catch (VssServiceException)
        {
            throw new AzureDevOpsIntegrationException(502, "azure_devops_request_failed", "Azure DevOps no pudo consultar la organización.");
        }
    }

    private static async Task<IReadOnlyList<WorkItem>> GetWorkItemsAsync(
        IAzureDevOpsClient client,
        Guid projectId,
        int top,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetProjectWorkItemsAsync(projectId, top, cancellationToken);
        }
        catch (VssUnauthorizedException)
        {
            throw new AzureDevOpsIntegrationException(403, "permission_denied", "Azure DevOps denegó el acceso a los work items de este proyecto.");
        }
        catch (VssAuthenticationException)
        {
            throw new AzureDevOpsIntegrationException(403, "permission_denied", "Azure DevOps rechazó la autenticación delegada.");
        }
        catch (VssServiceResponseException exception)
        {
            throw MapSdkError(exception.HttpStatusCode, "project_not_found", "El proyecto no existe o no es visible para este usuario.");
        }
        catch (VssServiceException)
        {
            throw new AzureDevOpsIntegrationException(502, "azure_devops_request_failed", "Azure DevOps no pudo completar la consulta de work items.");
        }
    }

    private static async Task<WorkItem> GetWorkItemAsync(
        IAzureDevOpsClient client,
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetWorkItemAsync(id, cancellationToken);
        }
        catch (VssUnauthorizedException)
        {
            throw new AzureDevOpsIntegrationException(403, "permission_denied", "Azure DevOps denegó el acceso a este work item.");
        }
        catch (VssAuthenticationException)
        {
            throw new AzureDevOpsIntegrationException(403, "permission_denied", "Azure DevOps rechazó la autenticación delegada.");
        }
        catch (VssServiceResponseException exception)
        {
            throw MapSdkError(exception.HttpStatusCode, "work_item_not_found", "El work item no existe o no es visible para este usuario.");
        }
        catch (VssServiceException)
        {
            throw new AzureDevOpsIntegrationException(404, "work_item_not_found", "El work item no existe o no es visible para este usuario.");
        }
    }

    private async Task<IReadOnlyDictionary<int, LocalWorkItemLink>> GetLocalLinksAsync(
        string projectName,
        IReadOnlyList<WorkItem> workItems,
        CancellationToken cancellationToken)
    {
        if (workItems.Count == 0)
            return new Dictionary<int, LocalWorkItemLink>();

        var ids = workItems
            .Where(item => item.Id.HasValue)
            .Select(item => item.Id!.Value)
            .ToArray();
        if (ids.Length == 0)
            return new Dictionary<int, LocalWorkItemLink>();

        var organization = new Uri(options.Value.OrganizationUrl).AbsolutePath.Trim('/');
        var links = await db.AzureDevOpsWorkItems
            .AsNoTracking()
            .Where(link => ids.Contains(link.DevOpsWorkItemId)
                && link.DevOpsProject == projectName
                && (link.DevOpsOrganization == organization
                    || link.DevOpsOrganization == options.Value.OrganizationUrl.TrimEnd('/')))
            .Select(link => new LocalWorkItemLink(
                link.DevOpsWorkItemId,
                link.ProjectId,
                link.Project.Name,
                link.CommitmentId,
                link.Commitment == null ? null : link.Commitment.Title))
            .ToListAsync(cancellationToken);

        return links.ToDictionary(link => link.WorkItemId);
    }

    private static AzureDevOpsProjectResponse MapProject(TeamProjectReference project) =>
        new(
            project.Id,
            project.Name,
            project.State.ToString(),
            project.Visibility.ToString());

    private static AzureDevOpsWorkItemResponse MapWorkItem(
        WorkItem item,
        string projectName,
        IReadOnlyDictionary<int, LocalWorkItemLink> links)
    {
        var id = item.Id ?? 0;
        links.TryGetValue(id, out var link);
        return new AzureDevOpsWorkItemResponse(
            id,
            GetString(item.Fields, "System.Title") ?? string.Empty,
            GetString(item.Fields, "System.WorkItemType") ?? string.Empty,
            GetString(item.Fields, "System.State"),
            GetAssignedTo(item.Fields),
            GetDate(item.Fields, "Microsoft.VSTS.Scheduling.StartDate"),
            GetDate(item.Fields, "Microsoft.VSTS.Scheduling.TargetDate"),
            GetDate(item.Fields, "System.CreatedDate"),
            GetDate(item.Fields, "System.ChangedDate"),
            GetAzureDevOpsUrl(item.Url),
            projectName,
            link?.ProjectId,
            link?.ProjectName,
            link?.CommitmentId,
            link?.CommitmentTitle);
    }

    private static string? GetString(IDictionary<string, object> fields, string name) =>
        fields.TryGetValue(name, out var value) ? value?.ToString() : null;

    private static string? GetAssignedTo(IDictionary<string, object> fields)
    {
        if (!fields.TryGetValue("System.AssignedTo", out var value) || value is null)
            return null;

        var displayName = value is IdentityRef identity ? identity.DisplayName : value.ToString();
        var emailSeparator = displayName?.IndexOf('<', StringComparison.Ordinal) ?? -1;
        return emailSeparator > 0 ? displayName![..emailSeparator].Trim() : displayName;
    }

    private static DateTimeOffset? GetDate(IDictionary<string, object> fields, string name)
    {
        if (!fields.TryGetValue(name, out var value) || value is null)
            return null;

        if (value is DateTimeOffset offset)
            return offset;
        if (value is DateTime dateTime)
            return new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));

        return DateTimeOffset.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static string? GetAzureDevOpsUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(uri.UserInfo)
            ? uri.AbsoluteUri
            : null;
    }

    private static object? SanitizeField(object? value) =>
        value is IdentityRef identity ? identity.DisplayName : value;

    private static AzureDevOpsIntegrationException MapSdkError(
        System.Net.HttpStatusCode statusCode,
        string notFoundCode,
        string notFoundMessage)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                new AzureDevOpsIntegrationException(403, "permission_denied", "Azure DevOps denegó el acceso solicitado."),
            System.Net.HttpStatusCode.NotFound =>
                new AzureDevOpsIntegrationException(404, notFoundCode, notFoundMessage),
            _ => new AzureDevOpsIntegrationException(
                502,
                "azure_devops_request_failed",
                "Azure DevOps devolvió un error al procesar la solicitud.")
        };
    }

    private sealed record LocalWorkItemLink(
        int WorkItemId,
        int ProjectId,
        string ProjectName,
        int? CommitmentId,
        string? CommitmentTitle);
}
