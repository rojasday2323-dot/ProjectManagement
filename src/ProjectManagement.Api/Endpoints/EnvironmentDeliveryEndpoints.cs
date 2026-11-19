using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Endpoints;

public static class EnvironmentDeliveryEndpoints
{
    public static void MapEnvironmentDeliveryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/environment-deliveries").WithTags("Environment Deliveries");

        group.MapGet("/", GetAll);
    }

    private static async Task<IResult> GetAll(
        AppDbContext db,
        int? commitmentId,
        int? projectId,
        string? environment)
    {
        var query = db.EnvironmentDeliveries
            .Include(ed => ed.Commitment)
            .Include(ed => ed.Deliverable)
            .AsQueryable();

        if (commitmentId.HasValue)
            query = query.Where(ed => ed.CommitmentId == commitmentId.Value);

        if (projectId.HasValue)
            query = query.Where(ed => ed.Commitment != null && ed.Commitment.ProjectId == projectId.Value);

        if (!string.IsNullOrWhiteSpace(environment))
            query = query.Where(ed => ed.Environment == environment);

        var deliveries = await query
            .Select(ed => new EnvironmentDeliveryResponse(
                ed.Id,
                ed.CommitmentId,
                ed.Commitment != null ? ed.Commitment.Title : null,
                ed.DeliverableId,
                ed.Deliverable != null ? ed.Deliverable.Title : null,
                ed.Environment,
                ed.PlannedDate, ed.ActualDate,
                ed.Status, ed.Notes, ed.CreatedAt
            ))
            .ToListAsync();

        return Results.Ok(deliveries);
    }
}
