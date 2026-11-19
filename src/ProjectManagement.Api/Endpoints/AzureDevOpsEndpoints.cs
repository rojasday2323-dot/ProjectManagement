using Microsoft.AspNetCore.Mvc;
using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Services.AzureDevOps;

namespace ProjectManagement.Api.Endpoints;

public static class AzureDevOpsEndpoints
{
    public static void MapAzureDevOpsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/azure-devops")
            .WithTags("Azure DevOps")
            .RequireAuthorization();

        group.MapGet("/status", GetStatus)
            .WithName("GetAzureDevOpsStatus")
            .WithSummary("Comprueba la conexión delegada con Azure DevOps")
            .Produces<AzureDevOpsStatusResponse>()
            .Produces<ProblemDetails>(401)
            .Produces<ProblemDetails>(403)
            .Produces<ProblemDetails>(503);

        group.MapGet("/projects", GetProjects)
            .WithName("GetAzureDevOpsProjects")
            .WithSummary("Lista los proyectos visibles para el usuario autenticado")
            .Produces<IReadOnlyList<AzureDevOpsProjectResponse>>()
            .Produces<ProblemDetails>(401)
            .Produces<ProblemDetails>(403)
            .Produces<ProblemDetails>(503);

        group.MapGet("/projects/{projectId:guid}/work-items", GetProjectWorkItems)
            .WithName("GetAzureDevOpsProjectWorkItems")
            .WithSummary("Lista work items de un proyecto visible en Azure DevOps")
            .Produces<IReadOnlyList<AzureDevOpsWorkItemResponse>>()
            .Produces<ProblemDetails>(401)
            .Produces<ProblemDetails>(403)
            .Produces<ProblemDetails>(404);

        group.MapGet("/work-items/{id:int}", GetWorkItem)
            .WithName("GetAzureDevOpsWorkItem")
            .WithSummary("Obtiene el detalle de un work item y sus vínculos locales existentes")
            .Produces<AzureDevOpsWorkItemDetailResponse>()
            .Produces<ProblemDetails>(401)
            .Produces<ProblemDetails>(403)
            .Produces<ProblemDetails>(404);
    }

    private static async Task<IResult> GetStatus(
        IAzureDevOpsService service,
        CancellationToken cancellationToken) =>
        await Execute(() => service.GetStatusAsync(cancellationToken));

    private static async Task<IResult> GetProjects(
        IAzureDevOpsService service,
        CancellationToken cancellationToken) =>
        await Execute(() => service.GetProjectsAsync(cancellationToken));

    private static async Task<IResult> GetProjectWorkItems(
        Guid projectId,
        int? top,
        IAzureDevOpsService service,
        CancellationToken cancellationToken)
    {
        var requestedTop = top ?? 100;
        if (requestedTop is < 1 or > 200)
            return Results.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Parámetro inválido",
                Detail = "El parámetro top debe estar entre 1 y 200."
            });

        return await Execute(() => service.GetProjectWorkItemsAsync(projectId, requestedTop, cancellationToken));
    }

    private static async Task<IResult> GetWorkItem(
        int id,
        IAzureDevOpsService service,
        CancellationToken cancellationToken) =>
        await Execute(() => service.GetWorkItemAsync(id, cancellationToken));

    private static async Task<IResult> Execute<T>(Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (AzureDevOpsIntegrationException exception)
        {
            return Results.Problem(
                statusCode: exception.StatusCode,
                title: exception.Code,
                detail: exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = exception.Code });
        }
        catch (HttpRequestException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "azure_devops_network_error",
                detail: "No fue posible comunicarse con Azure DevOps. Comprueba la red e inténtalo de nuevo.",
                extensions: new Dictionary<string, object?> { ["code"] = "azure_devops_network_error" });
        }
        catch (OperationCanceledException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status504GatewayTimeout,
                title: "azure_devops_timeout",
                detail: "La operación con Azure DevOps excedió el tiempo permitido.",
                extensions: new Dictionary<string, object?> { ["code"] = "azure_devops_timeout" });
        }
    }
}
