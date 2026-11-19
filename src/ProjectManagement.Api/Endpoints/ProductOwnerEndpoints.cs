using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Endpoints;

public static class ProductOwnerEndpoints
{
    public static void MapProductOwnerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/product-owners").WithTags("Product Owners");

        group.MapGet("/", GetAll);
        group.MapGet("/{id:int}", GetById);
    }

    private static async Task<IResult> GetAll(AppDbContext db)
    {
        var owners = await db.ProductOwners
            .Include(po => po.User)
            .Select(po => new ProductOwnerResponse(
                po.Id, po.UserId, po.User.DisplayName, po.User.Email,
                po.Department, po.CreatedAt
            ))
            .ToListAsync();

        return Results.Ok(owners);
    }

    private static async Task<IResult> GetById(int id, AppDbContext db)
    {
        var owner = await db.ProductOwners
            .Include(po => po.User)
            .Where(po => po.Id == id)
            .Select(po => new ProductOwnerResponse(
                po.Id, po.UserId, po.User.DisplayName, po.User.Email,
                po.Department, po.CreatedAt
            ))
            .FirstOrDefaultAsync();

        return owner is null
            ? Results.NotFound(ApiErrorResponse.Error($"Product Owner con ID {id} no encontrado."))
            : Results.Ok(owner);
    }
}
