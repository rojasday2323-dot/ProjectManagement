using Microsoft.AspNetCore.Http.HttpResults;
using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Services.Dashboard;

namespace ProjectManagement.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/dashboard/summary", GetSummary)
            .WithTags("Dashboard")
            .WithName("GetDashboardSummary")
            .WithSummary("Obtiene los indicadores y la distribución de carga del dashboard")
            .WithDescription("Calcula los indicadores desde los proyectos, compromisos, desarrolladores y alertas almacenados.")
            .Produces<DashboardSummaryResponse>(StatusCodes.Status200OK);
    }

    private static async Task<Ok<DashboardSummaryResponse>> GetSummary(
        IProjectDashboardService dashboardService,
        CancellationToken cancellationToken)
    {
        var summary = await dashboardService.GetSummaryAsync(cancellationToken);
        return TypedResults.Ok(summary);
    }
}
