using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Models;

namespace ProjectManagement.Api.Endpoints;

public static class CommitmentEndpoints
{
    private static readonly string[] ValidStatuses = ["Pending", "InProgress", "Completed", "Delayed", "Cancelled"];
    private static readonly string[] ValidPriorities = ["Low", "Medium", "High", "Critical"];

    public static void MapCommitmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/commitments").WithTags("Commitments");

        group.MapGet("/", GetAll);
        group.MapGet("/{id:int}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:int}", Update);
        group.MapDelete("/{id:int}", Delete);
    }

    private static async Task<IResult> GetAll(
        AppDbContext db,
        int? developerId,
        int? projectId,
        int? productOwnerId,
        DateTime? startDate,
        DateTime? endDate,
        string? status)
    {
        var query = db.Commitments
            .Include(c => c.Project)
            .Include(c => c.Developer).ThenInclude(d => d.User)
            .Include(c => c.ProductOwner).ThenInclude(po => po.User)
            .AsQueryable();

        if (developerId.HasValue)
            query = query.Where(c => c.DeveloperId == developerId.Value);

        if (projectId.HasValue)
            query = query.Where(c => c.ProjectId == projectId.Value);

        if (productOwnerId.HasValue)
            query = query.Where(c => c.ProductOwnerId == productOwnerId.Value);

        if (startDate.HasValue)
            query = query.Where(c => c.StartDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(c => c.CommitmentDate <= endDate.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);

        var commitments = await query
            .Select(c => new CommitmentResponse(
                c.Id, c.ProjectId, c.Project.Name,
                c.DeveloperId, c.Developer.User.DisplayName,
                c.ProductOwnerId, c.ProductOwner.User.DisplayName,
                c.Title, c.Description,
                c.StartDate, c.CommitmentDate, c.CompletedDate,
                c.Status, c.Priority,
                c.QADate, c.PREDate, c.PROTDate,
                c.Notes, c.CreatedAt, c.UpdatedAt
            ))
            .ToListAsync();

        return Results.Ok(commitments);
    }

    private static async Task<IResult> GetById(int id, AppDbContext db)
    {
        var commitment = await db.Commitments
            .Include(c => c.Project)
            .Include(c => c.Developer).ThenInclude(d => d.User)
            .Include(c => c.ProductOwner).ThenInclude(po => po.User)
            .Where(c => c.Id == id)
            .Select(c => new CommitmentResponse(
                c.Id, c.ProjectId, c.Project.Name,
                c.DeveloperId, c.Developer.User.DisplayName,
                c.ProductOwnerId, c.ProductOwner.User.DisplayName,
                c.Title, c.Description,
                c.StartDate, c.CommitmentDate, c.CompletedDate,
                c.Status, c.Priority,
                c.QADate, c.PREDate, c.PROTDate,
                c.Notes, c.CreatedAt, c.UpdatedAt
            ))
            .FirstOrDefaultAsync();

        return commitment is null
            ? Results.NotFound(ApiErrorResponse.Error($"Compromiso con ID {id} no encontrado."))
            : Results.Ok(commitment);
    }

    private static async Task<IResult> Create(CreateCommitmentRequest request, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest(ApiErrorResponse.Error("El título del compromiso es obligatorio."));

        if (request.CommitmentDate < request.StartDate)
            return Results.BadRequest(ApiErrorResponse.Error("La fecha de compromiso no puede ser anterior a la fecha de inicio."));

        var status = request.Status ?? "Pending";
        if (!ValidStatuses.Contains(status))
            return Results.BadRequest(ApiErrorResponse.Error($"Estado inválido. Valores permitidos: {string.Join(", ", ValidStatuses)}"));

        var priority = request.Priority ?? "Medium";
        if (!ValidPriorities.Contains(priority))
            return Results.BadRequest(ApiErrorResponse.Error($"Prioridad inválida. Valores permitidos: {string.Join(", ", ValidPriorities)}"));

        if (!await db.Projects.AnyAsync(p => p.Id == request.ProjectId))
            return Results.BadRequest(ApiErrorResponse.Error($"Proyecto con ID {request.ProjectId} no encontrado."));

        if (!await db.Developers.AnyAsync(d => d.Id == request.DeveloperId && d.User.IsActive))
            return Results.BadRequest(ApiErrorResponse.Error($"Desarrollador activo con ID {request.DeveloperId} no encontrado."));

        if (!await db.ProductOwners.AnyAsync(po => po.Id == request.ProductOwnerId))
            return Results.BadRequest(ApiErrorResponse.Error($"Product Owner con ID {request.ProductOwnerId} no encontrado."));

        var commitment = new Commitment
        {
            ProjectId = request.ProjectId,
            DeveloperId = request.DeveloperId,
            ProductOwnerId = request.ProductOwnerId,
            Title = request.Title,
            Description = request.Description ?? string.Empty,
            StartDate = request.StartDate,
            CommitmentDate = request.CommitmentDate,
            Status = status,
            Priority = priority,
            QADate = request.QADate,
            PREDate = request.PREDate,
            PROTDate = request.PROTDate,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        db.Commitments.Add(commitment);
        await db.SaveChangesAsync();

        // Reload with navigation properties
        await db.Entry(commitment).Reference(c => c.Project).LoadAsync();
        await db.Entry(commitment).Reference(c => c.Developer).LoadAsync();
        await db.Entry(commitment.Developer).Reference(d => d.User).LoadAsync();
        await db.Entry(commitment).Reference(c => c.ProductOwner).LoadAsync();
        await db.Entry(commitment.ProductOwner).Reference(po => po.User).LoadAsync();

        var response = new CommitmentResponse(
            commitment.Id, commitment.ProjectId, commitment.Project.Name,
            commitment.DeveloperId, commitment.Developer.User.DisplayName,
            commitment.ProductOwnerId, commitment.ProductOwner.User.DisplayName,
            commitment.Title, commitment.Description,
            commitment.StartDate, commitment.CommitmentDate, commitment.CompletedDate,
            commitment.Status, commitment.Priority,
            commitment.QADate, commitment.PREDate, commitment.PROTDate,
            commitment.Notes, commitment.CreatedAt, commitment.UpdatedAt
        );

        return Results.Created($"/api/commitments/{commitment.Id}", response);
    }

    private static async Task<IResult> Update(int id, UpdateCommitmentRequest request, AppDbContext db)
    {
        var commitment = await db.Commitments.FindAsync(id);
        if (commitment is null)
            return Results.NotFound(ApiErrorResponse.Error($"Compromiso con ID {id} no encontrado."));

        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest(ApiErrorResponse.Error("El título del compromiso es obligatorio."));

        if (request.CommitmentDate < request.StartDate)
            return Results.BadRequest(ApiErrorResponse.Error("La fecha de compromiso no puede ser anterior a la fecha de inicio."));

        if (!ValidStatuses.Contains(request.Status))
            return Results.BadRequest(ApiErrorResponse.Error($"Estado inválido. Valores permitidos: {string.Join(", ", ValidStatuses)}"));

        if (!ValidPriorities.Contains(request.Priority))
            return Results.BadRequest(ApiErrorResponse.Error($"Prioridad inválida. Valores permitidos: {string.Join(", ", ValidPriorities)}"));

        if (!await db.Projects.AnyAsync(p => p.Id == request.ProjectId))
            return Results.BadRequest(ApiErrorResponse.Error($"Proyecto con ID {request.ProjectId} no encontrado."));

        if (!await db.Developers.AnyAsync(d => d.Id == request.DeveloperId && d.User.IsActive))
            return Results.BadRequest(ApiErrorResponse.Error($"Desarrollador activo con ID {request.DeveloperId} no encontrado."));

        if (!await db.ProductOwners.AnyAsync(po => po.Id == request.ProductOwnerId))
            return Results.BadRequest(ApiErrorResponse.Error($"Product Owner con ID {request.ProductOwnerId} no encontrado."));

        commitment.ProjectId = request.ProjectId;
        commitment.DeveloperId = request.DeveloperId;
        commitment.ProductOwnerId = request.ProductOwnerId;
        commitment.Title = request.Title;
        commitment.Description = request.Description ?? string.Empty;
        commitment.StartDate = request.StartDate;
        commitment.CommitmentDate = request.CommitmentDate;
        commitment.CompletedDate = request.CompletedDate;
        commitment.Status = request.Status;
        commitment.Priority = request.Priority;
        commitment.QADate = request.QADate;
        commitment.PREDate = request.PREDate;
        commitment.PROTDate = request.PROTDate;
        commitment.Notes = request.Notes;
        commitment.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        await db.Entry(commitment).Reference(c => c.Project).LoadAsync();
        await db.Entry(commitment).Reference(c => c.Developer).LoadAsync();
        await db.Entry(commitment.Developer).Reference(d => d.User).LoadAsync();
        await db.Entry(commitment).Reference(c => c.ProductOwner).LoadAsync();
        await db.Entry(commitment.ProductOwner).Reference(po => po.User).LoadAsync();

        return Results.Ok(new CommitmentResponse(
            commitment.Id, commitment.ProjectId, commitment.Project.Name,
            commitment.DeveloperId, commitment.Developer.User.DisplayName,
            commitment.ProductOwnerId, commitment.ProductOwner.User.DisplayName,
            commitment.Title, commitment.Description,
            commitment.StartDate, commitment.CommitmentDate, commitment.CompletedDate,
            commitment.Status, commitment.Priority,
            commitment.QADate, commitment.PREDate, commitment.PROTDate,
            commitment.Notes, commitment.CreatedAt, commitment.UpdatedAt
        ));
    }

    private static async Task<IResult> Delete(int id, AppDbContext db)
    {
        var commitment = await db.Commitments.FindAsync(id);
        if (commitment is null)
            return Results.NotFound(ApiErrorResponse.Error($"Compromiso con ID {id} no encontrado."));

        db.Commitments.Remove(commitment);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }
}
