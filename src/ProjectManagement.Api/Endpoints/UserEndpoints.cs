using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users");

        group.MapGet("/", GetAll);
        group.MapGet("/{id:int}", GetById);
    }

    private static async Task<IResult> GetAll(AppDbContext db)
    {
        var users = await db.Users
            .Select(u => new UserResponse(
                u.Id, u.DisplayName, u.Email, u.Role, u.IsActive, u.CreatedAt
            ))
            .ToListAsync();

        return Results.Ok(users);
    }

    private static async Task<IResult> GetById(int id, AppDbContext db)
    {
        var user = await db.Users
            .Where(u => u.Id == id)
            .Select(u => new UserResponse(
                u.Id, u.DisplayName, u.Email, u.Role, u.IsActive, u.CreatedAt
            ))
            .FirstOrDefaultAsync();

        return user is null
            ? Results.NotFound(ApiErrorResponse.Error($"Usuario con ID {id} no encontrado."))
            : Results.Ok(user);
    }
}
