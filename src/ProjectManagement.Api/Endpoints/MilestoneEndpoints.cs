using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Models;

namespace ProjectManagement.Api.Endpoints;

public static class MilestoneEndpoints
{
    private static readonly string[] ValidStatuses = ["Pending", "InProgress", "Completed", "Missed"];

    public static void MapMilestoneEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/milestones").WithTags("Milestones");

        group.MapGet("/", GetAll);
        group.MapGet("/{id:int}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:int}", Update);
        group.MapDelete("/{id:int}", Delete);
    }

    private static async Task<IResult> GetAll(AppDbContext db, int? projectId)
    {
        var query = db.Milestones
            .Include(m => m.Project)
            .AsQueryable();

        if (projectId.HasValue)
            query = query.Where(m => m.ProjectId == projectId.Value);

        var milestones = await query
            .Select(m => new MilestoneResponse(
                m.Id, m.ProjectId, m.Project.Name,
                m.Title, m.Description,
                m.DueDate, m.CompletedDate, m.Status, m.CreatedAt
            ))
            .ToListAsync();

        return Results.Ok(milestones);
    }

    private static async Task<IResult> GetById(int id, AppDbContext db)
    {
        var milestone = await db.Milestones
            .Include(m => m.Project)
            .Where(m => m.Id == id)
            .Select(m => new MilestoneResponse(
                m.Id, m.ProjectId, m.Project.Name,
                m.Title, m.Description,
                m.DueDate, m.CompletedDate, m.Status, m.CreatedAt
            ))
            .FirstOrDefaultAsync();

        return milestone is null
            ? Results.NotFound(ApiErrorResponse.Error($"Milestone con ID {id} no encontrado."))
            : Results.Ok(milestone);
    }

    private static async Task<IResult> Create(CreateMilestoneRequest request, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest(ApiErrorResponse.Error("El título del milestone es obligatorio."));

        var status = request.Status ?? "Pending";
        if (!ValidStatuses.Contains(status))
            return Results.BadRequest(ApiErrorResponse.Error($"Estado inválido. Valores permitidos: {string.Join(", ", ValidStatuses)}"));

        if (!await db.Projects.AnyAsync(p => p.Id == request.ProjectId))
            return Results.BadRequest(ApiErrorResponse.Error($"Proyecto con ID {request.ProjectId} no encontrado."));

        var milestone = new Milestone
        {
            ProjectId = request.ProjectId,
            Title = request.Title,
            Description = request.Description,
            DueDate = request.DueDate,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        db.Milestones.Add(milestone);
        await db.SaveChangesAsync();

        var projectName = await db.Projects.Where(p => p.Id == milestone.ProjectId).Select(p => p.Name).FirstAsync();

        return Results.Created($"/api/milestones/{milestone.Id}", new MilestoneResponse(
            milestone.Id, milestone.ProjectId, projectName,
            milestone.Title, milestone.Description,
            milestone.DueDate, milestone.CompletedDate, milestone.Status, milestone.CreatedAt
        ));
    }

    private static async Task<IResult> Update(int id, UpdateMilestoneRequest request, AppDbContext db)
    {
        var milestone = await db.Milestones.FindAsync(id);
        if (milestone is null)
            return Results.NotFound(ApiErrorResponse.Error($"Milestone con ID {id} no encontrado."));

        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest(ApiErrorResponse.Error("El título del milestone es obligatorio."));

        if (!ValidStatuses.Contains(request.Status))
            return Results.BadRequest(ApiErrorResponse.Error($"Estado inválido. Valores permitidos: {string.Join(", ", ValidStatuses)}"));

        if (!await db.Projects.AnyAsync(p => p.Id == request.ProjectId))
            return Results.BadRequest(ApiErrorResponse.Error($"Proyecto con ID {request.ProjectId} no encontrado."));

        milestone.ProjectId = request.ProjectId;
        milestone.Title = request.Title;
        milestone.Description = request.Description;
        milestone.DueDate = request.DueDate;
        milestone.CompletedDate = request.CompletedDate;
        milestone.Status = request.Status;

        await db.SaveChangesAsync();

        var projectName = await db.Projects.Where(p => p.Id == milestone.ProjectId).Select(p => p.Name).FirstAsync();

        return Results.Ok(new MilestoneResponse(
            milestone.Id, milestone.ProjectId, projectName,
            milestone.Title, milestone.Description,
            milestone.DueDate, milestone.CompletedDate, milestone.Status, milestone.CreatedAt
        ));
    }

    private static async Task<IResult> Delete(int id, AppDbContext db)
    {
        var milestone = await db.Milestones.FindAsync(id);
        if (milestone is null)
            return Results.NotFound(ApiErrorResponse.Error($"Milestone con ID {id} no encontrado."));

        db.Milestones.Remove(milestone);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }
}
