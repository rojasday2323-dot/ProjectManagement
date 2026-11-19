using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Models;

namespace ProjectManagement.Api.Endpoints;

public static class DeliverableEndpoints
{
    private static readonly string[] ValidStatuses = ["Pending", "InProgress", "Delivered", "Rejected"];

    public static void MapDeliverableEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/deliverables").WithTags("Deliverables");

        group.MapGet("/", GetAll);
        group.MapGet("/{id:int}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:int}", Update);
        group.MapDelete("/{id:int}", Delete);
    }

    private static async Task<IResult> GetAll(AppDbContext db, int? projectId, int? commitmentId)
    {
        var query = db.Deliverables
            .Include(d => d.Project)
            .Include(d => d.Commitment)
            .AsQueryable();

        if (projectId.HasValue)
            query = query.Where(d => d.ProjectId == projectId.Value);

        if (commitmentId.HasValue)
            query = query.Where(d => d.CommitmentId == commitmentId.Value);

        var deliverables = await query
            .Select(d => new DeliverableResponse(
                d.Id, d.ProjectId, d.Project.Name,
                d.CommitmentId, d.Commitment != null ? d.Commitment.Title : null,
                d.Title, d.Description, d.Status,
                d.QADate, d.PREDate, d.PROTDate,
                d.CreatedAt, d.UpdatedAt
            ))
            .ToListAsync();

        return Results.Ok(deliverables);
    }

    private static async Task<IResult> GetById(int id, AppDbContext db)
    {
        var deliverable = await db.Deliverables
            .Include(d => d.Project)
            .Include(d => d.Commitment)
            .Where(d => d.Id == id)
            .Select(d => new DeliverableResponse(
                d.Id, d.ProjectId, d.Project.Name,
                d.CommitmentId, d.Commitment != null ? d.Commitment.Title : null,
                d.Title, d.Description, d.Status,
                d.QADate, d.PREDate, d.PROTDate,
                d.CreatedAt, d.UpdatedAt
            ))
            .FirstOrDefaultAsync();

        return deliverable is null
            ? Results.NotFound(ApiErrorResponse.Error($"Entregable con ID {id} no encontrado."))
            : Results.Ok(deliverable);
    }

    private static async Task<IResult> Create(CreateDeliverableRequest request, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest(ApiErrorResponse.Error("El título del entregable es obligatorio."));

        var status = request.Status ?? "Pending";
        if (!ValidStatuses.Contains(status))
            return Results.BadRequest(ApiErrorResponse.Error($"Estado inválido. Valores permitidos: {string.Join(", ", ValidStatuses)}"));

        if (!await db.Projects.AnyAsync(p => p.Id == request.ProjectId))
            return Results.BadRequest(ApiErrorResponse.Error($"Proyecto con ID {request.ProjectId} no encontrado."));

        if (request.CommitmentId.HasValue && !await db.Commitments.AnyAsync(c => c.Id == request.CommitmentId.Value))
            return Results.BadRequest(ApiErrorResponse.Error($"Compromiso con ID {request.CommitmentId} no encontrado."));

        var deliverable = new Deliverable
        {
            ProjectId = request.ProjectId,
            CommitmentId = request.CommitmentId,
            Title = request.Title,
            Description = request.Description,
            Status = status,
            QADate = request.QADate,
            PREDate = request.PREDate,
            PROTDate = request.PROTDate,
            CreatedAt = DateTime.UtcNow
        };

        db.Deliverables.Add(deliverable);
        await db.SaveChangesAsync();

        var projectName = await db.Projects.Where(p => p.Id == deliverable.ProjectId).Select(p => p.Name).FirstAsync();
        string? commitmentTitle = null;
        if (deliverable.CommitmentId.HasValue)
            commitmentTitle = await db.Commitments.Where(c => c.Id == deliverable.CommitmentId).Select(c => c.Title).FirstOrDefaultAsync();

        return Results.Created($"/api/deliverables/{deliverable.Id}", new DeliverableResponse(
            deliverable.Id, deliverable.ProjectId, projectName,
            deliverable.CommitmentId, commitmentTitle,
            deliverable.Title, deliverable.Description, deliverable.Status,
            deliverable.QADate, deliverable.PREDate, deliverable.PROTDate,
            deliverable.CreatedAt, deliverable.UpdatedAt
        ));
    }

    private static async Task<IResult> Update(int id, UpdateDeliverableRequest request, AppDbContext db)
    {
        var deliverable = await db.Deliverables.FindAsync(id);
        if (deliverable is null)
            return Results.NotFound(ApiErrorResponse.Error($"Entregable con ID {id} no encontrado."));

        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest(ApiErrorResponse.Error("El título del entregable es obligatorio."));

        if (!ValidStatuses.Contains(request.Status))
            return Results.BadRequest(ApiErrorResponse.Error($"Estado inválido. Valores permitidos: {string.Join(", ", ValidStatuses)}"));

        if (!await db.Projects.AnyAsync(p => p.Id == request.ProjectId))
            return Results.BadRequest(ApiErrorResponse.Error($"Proyecto con ID {request.ProjectId} no encontrado."));

        if (request.CommitmentId.HasValue && !await db.Commitments.AnyAsync(c => c.Id == request.CommitmentId.Value))
            return Results.BadRequest(ApiErrorResponse.Error($"Compromiso con ID {request.CommitmentId} no encontrado."));

        deliverable.ProjectId = request.ProjectId;
        deliverable.CommitmentId = request.CommitmentId;
        deliverable.Title = request.Title;
        deliverable.Description = request.Description;
        deliverable.Status = request.Status;
        deliverable.QADate = request.QADate;
        deliverable.PREDate = request.PREDate;
        deliverable.PROTDate = request.PROTDate;
        deliverable.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var projectName = await db.Projects.Where(p => p.Id == deliverable.ProjectId).Select(p => p.Name).FirstAsync();
        string? commitmentTitle = null;
        if (deliverable.CommitmentId.HasValue)
            commitmentTitle = await db.Commitments.Where(c => c.Id == deliverable.CommitmentId).Select(c => c.Title).FirstOrDefaultAsync();

        return Results.Ok(new DeliverableResponse(
            deliverable.Id, deliverable.ProjectId, projectName,
            deliverable.CommitmentId, commitmentTitle,
            deliverable.Title, deliverable.Description, deliverable.Status,
            deliverable.QADate, deliverable.PREDate, deliverable.PROTDate,
            deliverable.CreatedAt, deliverable.UpdatedAt
        ));
    }

    private static async Task<IResult> Delete(int id, AppDbContext db)
    {
        var deliverable = await db.Deliverables.FindAsync(id);
        if (deliverable is null)
            return Results.NotFound(ApiErrorResponse.Error($"Entregable con ID {id} no encontrado."));

        db.Deliverables.Remove(deliverable);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }
}
