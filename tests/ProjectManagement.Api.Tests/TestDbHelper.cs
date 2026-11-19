using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.Models;

namespace ProjectManagement.Api.Tests;

public static class TestDbHelper
{
    public static AppDbContext CreateContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    public static async Task<AppDbContext> CreateSeededContext(string? dbName = null)
    {
        var context = CreateContext(dbName);
        await SeedTestData(context);
        return context;
    }

    private static async Task SeedTestData(AppDbContext context)
    {
        var users = new List<User>
        {
            new() { Id = 1, DisplayName = "Dev Uno", Email = "dev1@test.com", Role = "Developer" },
            new() { Id = 2, DisplayName = "Dev Dos", Email = "dev2@test.com", Role = "Developer" },
            new() { Id = 3, DisplayName = "PO Uno", Email = "po1@test.com", Role = "ProductOwner" },
            new() { Id = 4, DisplayName = "PO Dos", Email = "po2@test.com", Role = "ProductOwner" },
        };
        context.Users.AddRange(users);
        await context.SaveChangesAsync();

        context.Developers.AddRange(
            new Developer { Id = 1, UserId = 1, Specialty = "Backend", Seniority = "Senior" },
            new Developer { Id = 2, UserId = 2, Specialty = "Frontend", Seniority = "Mid" }
        );
        await context.SaveChangesAsync();

        context.ProductOwners.AddRange(
            new ProductOwner { Id = 1, UserId = 3, Department = "Digital" },
            new ProductOwner { Id = 2, UserId = 4, Department = "Ops" }
        );
        await context.SaveChangesAsync();

        context.Projects.AddRange(
            new Project
            {
                Id = 1, Name = "Proyecto Alpha", Description = "Test project",
                Product = "Product A", StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), Status = "Active"
            },
            new Project
            {
                Id = 2, Name = "Proyecto Beta", Description = "Second project",
                Product = "Product B", StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                Status = "Planning"
            }
        );
        await context.SaveChangesAsync();

        context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = 1, UserId = 1, Role = "Developer" },
            new ProjectMember { ProjectId = 1, UserId = 3, Role = "ProductOwner" }
        );
        await context.SaveChangesAsync();

        context.Commitments.AddRange(
            new Commitment
            {
                Id = 1, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1,
                Title = "Compromiso Test 1", Description = "Desc 1",
                StartDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                Status = "InProgress", Priority = "High"
            },
            new Commitment
            {
                Id = 2, ProjectId = 1, DeveloperId = 2, ProductOwnerId = 1,
                Title = "Compromiso Test 2", Description = "Desc 2",
                StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                Status = "Pending", Priority = "Medium"
            },
            new Commitment
            {
                Id = 3, ProjectId = 2, DeveloperId = 1, ProductOwnerId = 2,
                Title = "Compromiso Test 3", Description = "Desc 3",
                StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                Status = "Completed", Priority = "Critical",
                CompletedDate = new DateTime(2026, 5, 28, 0, 0, 0, DateTimeKind.Utc)
            }
        );
        await context.SaveChangesAsync();

        context.Milestones.AddRange(
            new Milestone
            {
                Id = 1, ProjectId = 1, Title = "Milestone 1",
                DueDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), Status = "Pending"
            }
        );
        await context.SaveChangesAsync();

        context.Deliverables.AddRange(
            new Deliverable
            {
                Id = 1, ProjectId = 1, CommitmentId = 1,
                Title = "Entregable Test 1", Status = "InProgress",
                QADate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc)
            }
        );
        await context.SaveChangesAsync();
    }
}
