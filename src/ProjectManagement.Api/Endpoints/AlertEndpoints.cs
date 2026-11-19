using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Models;
using ProjectManagement.Api.Services;

namespace ProjectManagement.Api.Endpoints;

public static class AlertEndpoints
{
    private static readonly string[] ValidStatuses = ["Open", "Acknowledged", "Resolved"];

    public static void MapAlertEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/alerts").WithTags("Alerts");

        group.MapGet("/", GetAll);
        group.MapGet("/summary", GetSummary);
        group.MapGet("/{id:int}", GetById);
        group.MapPatch("/{id:int}/status", UpdateStatus);
        group.MapPost("/detect", RunDetection);
    }

    public static void MapAlertSubEndpoints(this WebApplication app)
    {
        app.MapGet("/api/developers/{id:int}/alerts", GetByDeveloper).WithTags("Developers");
        app.MapGet("/api/projects/{projectId:int}/alerts", GetByProject).WithTags("Projects");
    }

    private static async Task<IResult> GetAll(
        AppDbContext db,
        int? developerId,
        int? projectId,
        DateTime? startDate,
        DateTime? endDate,
        string? severity,
        string? status)
    {
        var query = db.ProjectAlerts
            .Include(a => a.Project)
            .Include(a => a.Developer).ThenInclude(d => d!.User)
            .Include(a => a.Commitment)
            .Include(a => a.RelatedCommitment)
            .AsQueryable();

        if (developerId.HasValue)
            query = query.Where(a => a.DeveloperId == developerId.Value);

        if (projectId.HasValue)
            query = query.Where(a => a.ProjectId == projectId.Value);

        if (startDate.HasValue)
            query = query.Where(a => a.ConflictEndDate == null || a.ConflictEndDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.ConflictStartDate == null || a.ConflictStartDate <= endDate.Value);

        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(a => a.Severity == severity);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status);
        else
            query = query.Where(a => a.Status != "Resolved");

        var alerts = await query
            .OrderByDescending(a => a.Severity == "Critical" ? 3 : a.Severity == "Warning" ? 2 : 1)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => MapToResponse(a))
            .ToListAsync();

        return Results.Ok(alerts);
    }

    private static async Task<IResult> GetSummary(AppDbContext db)
    {
        var openAlerts = await db.ProjectAlerts
            .Include(a => a.Project)
            .Include(a => a.Developer).ThenInclude(d => d!.User)
            .Include(a => a.Commitment)
            .Include(a => a.RelatedCommitment)
            .Where(a => a.Status != "Resolved")
            .OrderByDescending(a => a.Severity == "Critical" ? 3 : a.Severity == "Warning" ? 2 : 1)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();

        var summary = new AlertSummaryResponse(
            TotalOpen: openAlerts.Count,
            Critical: openAlerts.Count(a => a.Severity == "Critical"),
            Warning: openAlerts.Count(a => a.Severity == "Warning"),
            Info: openAlerts.Count(a => a.Severity == "Info"),
            RecentAlerts: openAlerts.Take(10).Select(a => MapToResponse(a)).ToList()
        );

        return Results.Ok(summary);
    }

    private static async Task<IResult> GetById(int id, AppDbContext db)
    {
        var alert = await db.ProjectAlerts
            .Include(a => a.Project)
            .Include(a => a.Developer).ThenInclude(d => d!.User)
            .Include(a => a.Commitment)
            .Include(a => a.RelatedCommitment)
            .FirstOrDefaultAsync(a => a.Id == id);

        return alert is null
            ? Results.NotFound(ApiErrorResponse.Error($"Alerta con ID {id} no encontrada."))
            : Results.Ok(MapToResponse(alert));
    }

    private static async Task<IResult> UpdateStatus(int id, UpdateAlertStatusRequest request, AppDbContext db)
    {
        if (!ValidStatuses.Contains(request.Status))
            return Results.BadRequest(ApiErrorResponse.Error($"Estado inválido. Valores permitidos: {string.Join(", ", ValidStatuses)}"));

        var alert = await db.ProjectAlerts.FindAsync(id);
        if (alert is null)
            return Results.NotFound(ApiErrorResponse.Error($"Alerta con ID {id} no encontrada."));

        alert.Status = request.Status;
        alert.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new { id = alert.Id, status = alert.Status });
    }

    private static async Task<IResult> RunDetection(ConflictDetectionService conflictService, AppDbContext db)
    {
        var newAlerts = await conflictService.DetectConflictsAsync();

        var totalOpen = await db.ProjectAlerts.CountAsync(a => a.Status != "Resolved");

        return Results.Ok(new
        {
            newAlertsDetected = newAlerts.Count,
            totalOpenAlerts = totalOpen,
            message = newAlerts.Count > 0
                ? $"Se detectaron {newAlerts.Count} nuevos conflictos."
                : "No se detectaron conflictos nuevos."
        });
    }

    private static async Task<IResult> GetByDeveloper(int id, AppDbContext db)
    {
        if (!await db.Developers.AnyAsync(d => d.Id == id))
            return Results.NotFound(ApiErrorResponse.Error($"Desarrollador con ID {id} no encontrado."));

        var alerts = await db.ProjectAlerts
            .Include(a => a.Project)
            .Include(a => a.Developer).ThenInclude(d => d!.User)
            .Include(a => a.Commitment)
            .Include(a => a.RelatedCommitment)
            .Where(a => a.DeveloperId == id && a.Status != "Resolved")
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => MapToResponse(a))
            .ToListAsync();

        return Results.Ok(alerts);
    }

    private static async Task<IResult> GetByProject(int projectId, AppDbContext db)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId))
            return Results.NotFound(ApiErrorResponse.Error($"Proyecto con ID {projectId} no encontrado."));

        var alerts = await db.ProjectAlerts
            .Include(a => a.Project)
            .Include(a => a.Developer).ThenInclude(d => d!.User)
            .Include(a => a.Commitment)
            .Include(a => a.RelatedCommitment)
            .Where(a => a.ProjectId == projectId && a.Status != "Resolved")
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => MapToResponse(a))
            .ToListAsync();

        return Results.Ok(alerts);
    }

    private static AlertResponse MapToResponse(ProjectAlert a) => new(
        a.Id,
        a.ProjectId,
        a.Project.Name,
        a.DeveloperId,
        a.Developer?.User.DisplayName,
        a.CommitmentId,
        a.Commitment?.Title,
        a.RelatedCommitmentId,
        a.RelatedCommitment?.Title,
        a.AlertType,
        a.Severity,
        a.Message,
        a.Status,
        a.ConflictStartDate,
        a.ConflictEndDate,
        a.CreatedAt,
        a.UpdatedAt
    );
}
