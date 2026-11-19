using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.Models;
using ProjectManagement.Api.Services;

namespace ProjectManagement.Api.Tests.Services;

public class ConflictDetectionServiceTests
{
    private static AppDbContext CreateContext() =>
        TestDbHelper.CreateContext(Guid.NewGuid().ToString());

    private static async Task<AppDbContext> SeedBaseData(AppDbContext ctx)
    {
        ctx.Users.AddRange(
            new User { Id = 1, DisplayName = "Dev A", Email = "a@test.com", Role = "Developer" },
            new User { Id = 2, DisplayName = "Dev B", Email = "b@test.com", Role = "Developer" },
            new User { Id = 3, DisplayName = "PO A", Email = "po@test.com", Role = "ProductOwner" }
        );
        await ctx.SaveChangesAsync();

        ctx.Developers.AddRange(
            new Developer { Id = 1, UserId = 1, Specialty = "Backend", Seniority = "Senior" },
            new Developer { Id = 2, UserId = 2, Specialty = "Frontend", Seniority = "Mid" }
        );
        await ctx.SaveChangesAsync();

        ctx.ProductOwners.Add(new ProductOwner { Id = 1, UserId = 3, Department = "Ops" });
        await ctx.SaveChangesAsync();

        ctx.Projects.Add(new Project
        {
            Id = 1, Name = "Test Project", Description = "Desc", Product = "P",
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), Status = "Active"
        });
        await ctx.SaveChangesAsync();

        return ctx;
    }

    [Fact]
    public async Task DetectConflicts_NoActiveCommitments_ReturnsEmpty()
    {
        var ctx = await SeedBaseData(CreateContext());
        ctx.Commitments.Add(new Commitment
        {
            ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1,
            Title = "Done", Description = "",
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CommitmentDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = "Completed", Priority = "Medium"
        });
        await ctx.SaveChangesAsync();

        var service = new ConflictDetectionService(ctx);
        var alerts = await service.DetectConflictsAsync();

        Assert.Empty(alerts);
    }

    [Fact]
    public async Task DetectConflicts_SingleCommitment_ReturnsEmpty()
    {
        var ctx = await SeedBaseData(CreateContext());
        ctx.Commitments.Add(new Commitment
        {
            ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1,
            Title = "Solo", Description = "",
            StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            CommitmentDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = "InProgress", Priority = "High"
        });
        await ctx.SaveChangesAsync();

        var service = new ConflictDetectionService(ctx);
        var alerts = await service.DetectConflictsAsync();

        Assert.Empty(alerts);
    }

    [Fact]
    public async Task DetectConflicts_TwoOverlapping_DetectsOverlap()
    {
        var ctx = await SeedBaseData(CreateContext());
        ctx.Commitments.AddRange(
            new Commitment
            {
                Id = 1, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1,
                Title = "Task A", Description = "",
                StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
                Status = "InProgress", Priority = "High"
            },
            new Commitment
            {
                Id = 2, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1,
                Title = "Task B", Description = "",
                StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                Status = "Pending", Priority = "Medium"
            }
        );
        await ctx.SaveChangesAsync();

        var service = new ConflictDetectionService(ctx);
        var alerts = await service.DetectConflictsAsync();

        Assert.Contains(alerts, a => a.AlertType == "Overlap");
        var overlap = alerts.First(a => a.AlertType == "Overlap");
        Assert.Equal(1, overlap.DeveloperId);
        Assert.NotNull(overlap.ConflictStartDate);
    }

    [Fact]
    public async Task DetectConflicts_NonOverlapping_NoAlerts()
    {
        var ctx = await SeedBaseData(CreateContext());
        ctx.Commitments.AddRange(
            new Commitment
            {
                Id = 1, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1,
                Title = "Jan Task", Description = "",
                StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc),
                Status = "InProgress", Priority = "High"
            },
            new Commitment
            {
                Id = 2, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1,
                Title = "May Task", Description = "",
                StartDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                Status = "Pending", Priority = "Medium"
            }
        );
        await ctx.SaveChangesAsync();

        var service = new ConflictDetectionService(ctx);
        var alerts = await service.DetectConflictsAsync();

        Assert.DoesNotContain(alerts, a => a.AlertType == "Overlap");
    }

    [Fact]
    public async Task DetectConflicts_MultipleOverlapping_DetectsAll()
    {
        var ctx = await SeedBaseData(CreateContext());
        ctx.Commitments.AddRange(
            new Commitment { Id = 1, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1, Title = "A", Description = "", StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), Status = "InProgress", Priority = "High" },
            new Commitment { Id = 2, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1, Title = "B", Description = "", StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), Status = "InProgress", Priority = "High" },
            new Commitment { Id = 3, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1, Title = "C", Description = "", StartDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), Status = "Pending", Priority = "Medium" }
        );
        await ctx.SaveChangesAsync();

        var service = new ConflictDetectionService(ctx);
        var alerts = await service.DetectConflictsAsync();

        var overlaps = alerts.Where(a => a.AlertType == "Overlap").ToList();
        Assert.True(overlaps.Count >= 3); // A-B, A-C, B-C
    }

    [Fact]
    public async Task DetectConflicts_DifferentDevelopers_NoOverlapAlerts()
    {
        var ctx = await SeedBaseData(CreateContext());
        ctx.Commitments.AddRange(
            new Commitment { Id = 1, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1, Title = "Dev1 Task", Description = "", StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc), Status = "InProgress", Priority = "High" },
            new Commitment { Id = 2, ProjectId = 1, DeveloperId = 2, ProductOwnerId = 1, Title = "Dev2 Task", Description = "", StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc), Status = "InProgress", Priority = "High" }
        );
        await ctx.SaveChangesAsync();

        var service = new ConflictDetectionService(ctx);
        var alerts = await service.DetectConflictsAsync();

        Assert.DoesNotContain(alerts, a => a.AlertType == "Overlap");
    }

    [Fact]
    public async Task DetectConflicts_DeliveryDateConflict_Detected()
    {
        var ctx = await SeedBaseData(CreateContext());
        ctx.Commitments.AddRange(
            new Commitment
            {
                Id = 1, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1,
                Title = "Task X", Description = "",
                StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
                Status = "InProgress", Priority = "High",
                QADate = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc),
                PROTDate = new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc),
            },
            new Commitment
            {
                Id = 2, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1,
                Title = "Task Y", Description = "",
                StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                Status = "InProgress", Priority = "High",
                QADate = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc), // Same QA date!
                PROTDate = new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc), // Same PROT date!
            }
        );
        await ctx.SaveChangesAsync();

        var service = new ConflictDetectionService(ctx);
        var alerts = await service.DetectConflictsAsync();

        Assert.Contains(alerts, a => a.AlertType == "DeliveryConflict");
        var dc = alerts.First(a => a.AlertType == "DeliveryConflict");
        Assert.Equal("Critical", dc.Severity); // PROT conflict = Critical
    }

    [Fact]
    public async Task DetectConflicts_Overload_ThreeOrMore()
    {
        var ctx = await SeedBaseData(CreateContext());
        ctx.Commitments.AddRange(
            new Commitment { Id = 1, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1, Title = "A", Description = "", StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), Status = "InProgress", Priority = "High" },
            new Commitment { Id = 2, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1, Title = "B", Description = "", StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), Status = "InProgress", Priority = "High" },
            new Commitment { Id = 3, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1, Title = "C", Description = "", StartDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), Status = "Pending", Priority = "Medium" }
        );
        await ctx.SaveChangesAsync();

        var service = new ConflictDetectionService(ctx);
        var alerts = await service.DetectConflictsAsync();

        Assert.Contains(alerts, a => a.AlertType == "Overload");
    }

    [Fact]
    public async Task DetectConflicts_TwoCommitments_NoOverload()
    {
        var ctx = await SeedBaseData(CreateContext());
        ctx.Commitments.AddRange(
            new Commitment { Id = 1, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1, Title = "A", Description = "", StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), Status = "InProgress", Priority = "High" },
            new Commitment { Id = 2, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1, Title = "B", Description = "", StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), Status = "Pending", Priority = "Medium" }
        );
        await ctx.SaveChangesAsync();

        var service = new ConflictDetectionService(ctx);
        var alerts = await service.DetectConflictsAsync();

        Assert.DoesNotContain(alerts, a => a.AlertType == "Overload");
    }

    [Fact]
    public async Task DetectConflicts_SeverityScaling_LongOverlapIsCritical()
    {
        var ctx = await SeedBaseData(CreateContext());
        ctx.Commitments.AddRange(
            new Commitment { Id = 1, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1, Title = "Long A", Description = "", StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), Status = "InProgress", Priority = "High" },
            new Commitment { Id = 2, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1, Title = "Long B", Description = "", StartDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc), Status = "InProgress", Priority = "High" }
        );
        await ctx.SaveChangesAsync();

        var service = new ConflictDetectionService(ctx);
        var alerts = await service.DetectConflictsAsync();

        var overlap = alerts.First(a => a.AlertType == "Overlap");
        Assert.Equal("Critical", overlap.Severity);
    }

    [Fact]
    public async Task DetectConflicts_NoDuplicateAlerts_OnSecondRun()
    {
        var ctx = await SeedBaseData(CreateContext());
        ctx.Commitments.AddRange(
            new Commitment { Id = 1, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1, Title = "A", Description = "", StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc), Status = "InProgress", Priority = "High" },
            new Commitment { Id = 2, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1, Title = "B", Description = "", StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), Status = "Pending", Priority = "Medium" }
        );
        await ctx.SaveChangesAsync();

        var service = new ConflictDetectionService(ctx);
        var first = await service.DetectConflictsAsync();
        var second = await service.DetectConflictsAsync();

        Assert.NotEmpty(first);
        Assert.Empty(second);
    }

    [Fact]
    public static void CommitmentsOverlap_BoundaryDates_NoOverlap()
    {
        var a = new Commitment { StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc) };
        var b = new Commitment { StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc) };

        Assert.False(ConflictDetectionService.CommitmentsOverlap(a, b));
    }

    [Fact]
    public static void CommitmentsOverlap_OneDayOverlap_True()
    {
        var a = new Commitment { StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc) };
        var b = new Commitment { StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CommitmentDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc) };

        Assert.True(ConflictDetectionService.CommitmentsOverlap(a, b));
    }
}
