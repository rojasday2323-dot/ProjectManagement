using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Endpoints;

public static class DeveloperEndpoints
{
    public static void MapDeveloperEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/developers").WithTags("Developers");

        group.MapGet("/", GetAll)
            .WithSummary("List developers")
            .WithDescription("Returns active developers for selectors by default; includeInactive=true returns the full catalog.")
            .Produces<IReadOnlyList<DeveloperResponse>>(StatusCodes.Status200OK);
        group.MapGet("/{id:int}", GetById)
            .WithSummary("Get a developer")
            .Produces<DeveloperResponse>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
        group.MapPost("/", Create)
            .WithSummary("Create a developer")
            .Produces<DeveloperResponse>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);
        group.MapPut("/{id:int}", Update)
            .WithSummary("Update a developer")
            .Produces<DeveloperResponse>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);
        group.MapPatch("/{id:int}/status", UpdateStatus)
            .WithSummary("Activate or deactivate a developer without deleting history")
            .Produces<DeveloperResponse>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetAll(
        AppDbContext db,
        bool? includeInactive,
        CancellationToken cancellationToken)
    {
        var query = db.Developers.AsQueryable();
        if (includeInactive != true)
            query = query.Where(developer => developer.User.IsActive);

        var developers = await query
            .Include(d => d.User)
            .Select(d => new DeveloperResponse(
                d.Id, d.UserId, d.User.DisplayName, d.User.Email,
                d.Specialty, d.Seniority, d.CreatedAt, d.User.IsActive
            ))
            .ToListAsync(cancellationToken);

        return Results.Ok(developers);
    }

    private static async Task<IResult> GetById(int id, AppDbContext db, CancellationToken cancellationToken)
    {
        var developer = await db.Developers
            .Include(d => d.User)
            .Where(d => d.Id == id)
            .Select(d => new DeveloperResponse(
                d.Id, d.UserId, d.User.DisplayName, d.User.Email,
                d.Specialty, d.Seniority, d.CreatedAt, d.User.IsActive
            ))
            .FirstOrDefaultAsync(cancellationToken);

        return developer is null
            ? Results.NotFound(ApiErrorResponse.Error($"Desarrollador con ID {id} no encontrado."))
            : Results.Ok(developer);
    }

    private static async Task<IResult> Create(
        CreateDeveloperRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var displayName = request.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            return Results.BadRequest(ApiErrorResponse.Error("El nombre del desarrollador es obligatorio."));
        if (displayName.Length > 200)
            return Results.BadRequest(ApiErrorResponse.Error("El nombre no puede superar los 200 caracteres."));
        if (HasProfileFieldTooLong(request.Specialty, request.Seniority))
            return Results.BadRequest(ApiErrorResponse.Error("La especialidad y el seniority no pueden superar los 50 caracteres."));

        if (await HasDeveloperNameAsync(db, displayName, null, cancellationToken))
            return Results.Conflict(ApiErrorResponse.Error("Ya existe un desarrollador con ese nombre."));

        var user = new Models.User
        {
            DisplayName = displayName,
            Email = null,
            Role = "Developer",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var developer = new Models.Developer
        {
            User = user,
            Specialty = request.Specialty?.Trim() ?? string.Empty,
            Seniority = request.Seniority?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };
        db.Developers.Add(developer);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/developers/{developer.Id}", ToResponse(developer));
    }

    private static async Task<IResult> Update(
        int id,
        UpdateDeveloperRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var developer = await db.Developers
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (developer is null)
            return Results.NotFound(ApiErrorResponse.Error($"Desarrollador con ID {id} no encontrado."));

        var displayName = request.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            return Results.BadRequest(ApiErrorResponse.Error("El nombre del desarrollador es obligatorio."));
        if (displayName.Length > 200)
            return Results.BadRequest(ApiErrorResponse.Error("El nombre no puede superar los 200 caracteres."));
        if (HasProfileFieldTooLong(request.Specialty, request.Seniority))
            return Results.BadRequest(ApiErrorResponse.Error("La especialidad y el seniority no pueden superar los 50 caracteres."));

        if (await HasDeveloperNameAsync(db, displayName, id, cancellationToken))
            return Results.Conflict(ApiErrorResponse.Error("Ya existe otro desarrollador con ese nombre."));

        developer.User.DisplayName = displayName;
        developer.User.UpdatedAt = DateTime.UtcNow;
        developer.Specialty = request.Specialty?.Trim() ?? string.Empty;
        developer.Seniority = request.Seniority?.Trim() ?? string.Empty;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(developer));
    }

    private static async Task<IResult> UpdateStatus(
        int id,
        UpdateDeveloperStatusRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var developer = await db.Developers
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (developer is null)
            return Results.NotFound(ApiErrorResponse.Error($"Desarrollador con ID {id} no encontrado."));

        developer.User.IsActive = request.IsActive;
        developer.User.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(developer));
    }

    private static DeveloperResponse ToResponse(Models.Developer developer) =>
        new(
            developer.Id,
            developer.UserId,
            developer.User.DisplayName,
            developer.User.Email,
            developer.Specialty,
            developer.Seniority,
            developer.CreatedAt,
            developer.User.IsActive);

    private static async Task<bool> HasDeveloperNameAsync(
        AppDbContext db,
        string displayName,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var existingNames = await db.Developers
            .Where(item => excludedId == null || item.Id != excludedId.Value)
            .Select(item => item.User.DisplayName)
            .ToListAsync(cancellationToken);

        return existingNames.Any(name =>
            string.Equals(name.Trim(), displayName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasProfileFieldTooLong(string? specialty, string? seniority) =>
        specialty is { Length: > 50 } || seniority is { Length: > 50 };
}
