using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Models;

namespace ProjectManagement.Api.Endpoints;

public static class ProjectEndpoints
{
    private static readonly string[] ValidStatuses = ["Planning", "Active", "OnHold", "Completed", "Cancelled"];

    public static void MapProjectEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/projects").WithTags("Projects");

        group.MapGet("/", GetAll)
            .WithName("GetProjects")
            .WithSummary("Obtiene proyectos con conteos de compromisos y Product Owners")
            .Produces<List<ProjectResponse>>(StatusCodes.Status200OK);
        group.MapGet("/{id:int}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:int}", Update);
        group.MapDelete("/{id:int}", Delete);
        group.MapGet("/{projectId:int}/members", GetMembers);
    }

    private static async Task<IResult> GetAll(AppDbContext db, CancellationToken cancellationToken)
    {
        var projects = await ProjectQuery(db)
            .ToListAsync(cancellationToken);

        return Results.Ok(await AddProductOwnerNamesAsync(db, projects, cancellationToken));
    }

    private static async Task<IResult> GetById(int id, AppDbContext db, CancellationToken cancellationToken)
    {
        var project = await ProjectQuery(db)
            .Where(p => p.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null)
            return Results.NotFound(ApiErrorResponse.Error($"Proyecto con ID {id} no encontrado."));

        var projectsWithOwners = await AddProductOwnerNamesAsync(db, [project], cancellationToken);
        return Results.Ok(projectsWithOwners[0]);
    }

    private static async Task<IResult> Create(
        CreateProjectRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(ApiErrorResponse.Error("El nombre del proyecto es obligatorio."));

        if (request.EndDate.HasValue && request.EndDate < request.StartDate)
            return Results.BadRequest(ApiErrorResponse.Error("La fecha final no puede ser anterior a la fecha de inicio."));

        var status = request.Status ?? "Planning";
        if (!ValidStatuses.Contains(status))
            return Results.BadRequest(ApiErrorResponse.Error($"Estado inválido. Valores permitidos: {string.Join(", ", ValidStatuses)}"));

        var duplicate = await db.Projects.AnyAsync(p => p.Name == request.Name, cancellationToken);
        if (duplicate)
            return Results.Conflict(ApiErrorResponse.Error($"Ya existe un proyecto con el nombre '{request.Name}'."));

        var project = new Project
        {
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Product = request.Product ?? string.Empty,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        var response = new ProjectResponse(
            project.Id, project.Name, project.Description, project.Product,
            project.StartDate, project.EndDate, project.Status,
            project.CreatedAt, project.UpdatedAt, 0, 0, 0, 0, 0
        );

        return Results.Created($"/api/projects/{project.Id}", response);
    }

    private static async Task<IResult> Update(
        int id,
        UpdateProjectRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var project = await db.Projects.FindAsync([id], cancellationToken);
        if (project is null)
            return Results.NotFound(ApiErrorResponse.Error($"Proyecto con ID {id} no encontrado."));

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(ApiErrorResponse.Error("El nombre del proyecto es obligatorio."));

        if (request.EndDate.HasValue && request.EndDate < request.StartDate)
            return Results.BadRequest(ApiErrorResponse.Error("La fecha final no puede ser anterior a la fecha de inicio."));

        if (!ValidStatuses.Contains(request.Status))
            return Results.BadRequest(ApiErrorResponse.Error($"Estado inválido. Valores permitidos: {string.Join(", ", ValidStatuses)}"));

        var duplicate = await db.Projects.AnyAsync(p => p.Name == request.Name && p.Id != id, cancellationToken);
        if (duplicate)
            return Results.Conflict(ApiErrorResponse.Error($"Ya existe otro proyecto con el nombre '{request.Name}'."));

        project.Name = request.Name;
        project.Description = request.Description ?? string.Empty;
        project.Product = request.Product ?? string.Empty;
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        project.Status = request.Status;
        project.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var updatedProject = await ProjectQuery(db)
            .FirstAsync(p => p.Id == id, cancellationToken);
        var projectsWithOwners = await AddProductOwnerNamesAsync(db, [updatedProject], cancellationToken);
        return Results.Ok(projectsWithOwners[0]);
    }

    private static async Task<IResult> Delete(int id, AppDbContext db, CancellationToken cancellationToken)
    {
        var project = await db.Projects.FindAsync([id], cancellationToken);
        if (project is null)
            return Results.NotFound(ApiErrorResponse.Error($"Proyecto con ID {id} no encontrado."));

        db.Projects.Remove(project);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetMembers(
        int projectId,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, cancellationToken);
        if (!projectExists)
            return Results.NotFound(ApiErrorResponse.Error($"Proyecto con ID {projectId} no encontrado."));

        var members = await db.ProjectMembers
            .Where(m => m.ProjectId == projectId)
            .Include(m => m.User)
            .Select(m => new ProjectMemberResponse(
                m.Id, m.UserId, m.User.DisplayName, m.User.Email,
                m.Role, m.JoinedAt, m.IsActive
            ))
            .ToListAsync(cancellationToken);

        return Results.Ok(members);
    }

    private static IQueryable<ProjectResponse> ProjectQuery(AppDbContext db) =>
        db.Projects
            .AsNoTracking()
            .Select(project => new ProjectResponse(
                project.Id,
                project.Name,
                project.Description,
                project.Product,
                project.StartDate,
                project.EndDate,
                project.Status,
                project.CreatedAt,
                project.UpdatedAt,
                project.Members.Count,
                project.Commitments.Count,
                project.Commitments.Count(commitment => commitment.Status == "Pending"),
                project.Commitments.Count(commitment => commitment.Status == "InProgress"),
                project.Commitments.Count(commitment => commitment.Status == "Completed")));

    private static async Task<List<ProjectResponse>> AddProductOwnerNamesAsync(
        AppDbContext db,
        List<ProjectResponse> projects,
        CancellationToken cancellationToken)
    {
        if (projects.Count == 0)
            return projects;

        var projectIds = projects.Select(project => project.Id).ToArray();
        var memberships = await db.ProjectMembers
            .AsNoTracking()
            .Where(member => projectIds.Contains(member.ProjectId)
                && member.IsActive
                && member.Role == "ProductOwner"
                && member.User.IsActive)
            .Select(member => new { member.ProjectId, member.User.DisplayName })
            .ToListAsync(cancellationToken);
        var namesByProject = memberships
            .GroupBy(member => member.ProjectId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(member => member.DisplayName)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToArray());

        return projects
            .Select(project => project with
            {
                ProductOwnerNames = namesByProject.GetValueOrDefault(project.Id) ?? Array.Empty<string>()
            })
            .ToList();
    }
}
