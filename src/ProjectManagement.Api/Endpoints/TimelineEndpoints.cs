using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Endpoints;

public static class TimelineEndpoints
{
    public static void MapTimelineEndpoints(this WebApplication app)
    {
        app.MapGet("/api/projects/{projectId:int}/timeline", GetProjectTimeline)
            .WithTags("Timeline");
    }

    private static async Task<IResult> GetProjectTimeline(int projectId, AppDbContext db)
    {
        var project = await db.Projects.FindAsync(projectId);
        if (project is null)
            return Results.NotFound(ApiErrorResponse.Error($"Proyecto con ID {projectId} no encontrado."));

        // Load conflict commitment IDs
        var alertCommitIds = await db.ProjectAlerts
            .Where(a => a.ProjectId == projectId && a.Status != "Resolved" && a.CommitmentId != null)
            .Select(a => a.CommitmentId!.Value)
            .ToListAsync();
        var alertRelatedIds = await db.ProjectAlerts
            .Where(a => a.ProjectId == projectId && a.Status != "Resolved" && a.RelatedCommitmentId != null)
            .Select(a => a.RelatedCommitmentId!.Value)
            .ToListAsync();
        var conflictIds = new HashSet<int>(alertCommitIds.Concat(alertRelatedIds));

        // Load entities, project to DTOs in memory
        var commitmentEntities = await db.Commitments
            .Include(c => c.Developer).ThenInclude(d => d.User)
            .Include(c => c.ProductOwner).ThenInclude(po => po.User)
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.StartDate)
            .ToListAsync();
        var commitments = commitmentEntities.Select(c => new TimelineCommitment(
            c.Id, c.Title, c.DeveloperId, c.Developer.User.DisplayName,
            c.ProductOwnerId, c.ProductOwner.User.DisplayName,
            c.StartDate, c.CommitmentDate, c.CompletedDate,
            c.Status, c.Priority, c.Description,
            c.QADate, c.PREDate, c.PROTDate, conflictIds.Contains(c.Id)
        )).ToList();

        var milestoneEntities = await db.Milestones
            .Where(m => m.ProjectId == projectId).OrderBy(m => m.DueDate).ToListAsync();
        var milestones = milestoneEntities.Select(m => new TimelineMilestone(
            m.Id, m.Title, m.Description, m.DueDate, m.CompletedDate, m.Status
        )).ToList();

        var deliverableEntities = await db.Deliverables
            .Include(d => d.Commitment)
            .Where(d => d.ProjectId == projectId).ToListAsync();
        var deliverables = deliverableEntities.Select(d => new TimelineDeliverable(
            d.Id, d.Title, d.CommitmentId, d.Commitment?.Title,
            d.Status, d.QADate, d.PREDate, d.PROTDate
        )).ToList();

        var envEntities = await db.EnvironmentDeliveries
            .Include(ed => ed.Commitment)
            .Include(ed => ed.Deliverable)
            .Where(ed =>
                (ed.Commitment != null && ed.Commitment.ProjectId == projectId) ||
                (ed.Deliverable != null && ed.Deliverable.ProjectId == projectId))
            .ToListAsync();
        var envDeliveries = envEntities.Select(ed => new TimelineEnvironmentDelivery(
            ed.Id, ed.CommitmentId, ed.Commitment?.Title,
            ed.DeliverableId, ed.Deliverable?.Title,
            ed.Environment, ed.PlannedDate, ed.ActualDate, ed.Status
        )).ToList();

        var alertEntities = await db.ProjectAlerts
            .Include(a => a.Developer).ThenInclude(d => d!.User)
            .Include(a => a.Commitment).Include(a => a.RelatedCommitment)
            .Where(a => a.ProjectId == projectId && a.Status != "Resolved").ToListAsync();
        var alerts = alertEntities.Select(a => new TimelineAlert(
            a.Id, a.AlertType, a.Severity, a.Message, a.Status,
            a.DeveloperId, a.Developer?.User.DisplayName,
            a.CommitmentId, a.Commitment?.Title,
            a.RelatedCommitmentId, a.RelatedCommitment?.Title,
            a.ConflictStartDate, a.ConflictEndDate
        )).ToList();

        return Results.Ok(new TimelineResponse(
            new ProjectTimelineInfo(project.Id, project.Name, project.Product,
                project.StartDate, project.EndDate, project.Status),
            commitments, milestones, deliverables, envDeliveries, alerts
        ));
    }
}
