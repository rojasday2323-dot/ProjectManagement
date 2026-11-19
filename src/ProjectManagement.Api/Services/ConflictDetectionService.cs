using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.Models;

namespace ProjectManagement.Api.Services;

public class ConflictDetectionService
{
    private readonly AppDbContext _db;
    private const int OverloadThreshold = 3;

    public ConflictDetectionService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Scans all active commitments and generates alerts for detected conflicts.
    /// Only creates alerts that don't already exist (avoids duplicates).
    /// </summary>
    public async Task<List<ProjectAlert>> DetectConflictsAsync(CancellationToken cancellationToken = default)
    {
        var newAlerts = new List<ProjectAlert>();

        var activeCommitments = await _db.Commitments
            .Include(c => c.Project)
            .Include(c => c.Developer).ThenInclude(d => d.User)
            .Where(c => c.Status == "Pending" || c.Status == "InProgress")
            .OrderBy(c => c.DeveloperId)
            .ThenBy(c => c.StartDate)
            .ToListAsync(cancellationToken);

        var existingAlertKeys = await _db.ProjectAlerts
            .Where(a => a.Status != "Resolved")
            .Select(a => new ExistingAlertKey(a.AlertType, a.CommitmentId, a.RelatedCommitmentId, a.DeveloperId))
            .ToListAsync(cancellationToken);

        var byDeveloper = activeCommitments.GroupBy(c => c.DeveloperId);

        foreach (var group in byDeveloper)
        {
            var devCommitments = group.ToList();
            var developer = devCommitments.First().Developer;

            // --- Rule 1: Overlapping commitments ---
            for (int i = 0; i < devCommitments.Count; i++)
            {
                for (int j = i + 1; j < devCommitments.Count; j++)
                {
                    var a = devCommitments[i];
                    var b = devCommitments[j];

                    if (CommitmentsOverlap(a, b))
                    {
                        if (AlertAlreadyExists(existingAlertKeys, "Overlap", a.Id, b.Id, developer.Id))
                            continue;

                        var overlapStart = a.StartDate > b.StartDate ? a.StartDate : b.StartDate;
                        var overlapEnd = (a.CommitmentDate < b.CommitmentDate ? a.CommitmentDate : b.CommitmentDate);
                        var overlapDays = (overlapEnd - overlapStart).Days;

                        var severity = overlapDays > 30 ? "Critical" : overlapDays > 14 ? "Warning" : "Info";

                        newAlerts.Add(new ProjectAlert
                        {
                            ProjectId = a.ProjectId,
                            DeveloperId = developer.Id,
                            CommitmentId = a.Id,
                            RelatedCommitmentId = b.Id,
                            AlertType = "Overlap",
                            Severity = severity,
                            Message = $"{developer.User.DisplayName} tiene solapamiento de {overlapDays} días entre \"{a.Title}\" y \"{b.Title}\".",
                            ConflictStartDate = overlapStart,
                            ConflictEndDate = overlapEnd,
                        });
                    }
                }
            }

            // --- Rule 2: Delivery date conflicts (same QA/PRE/PROT dates) ---
            for (int i = 0; i < devCommitments.Count; i++)
            {
                for (int j = i + 1; j < devCommitments.Count; j++)
                {
                    var a = devCommitments[i];
                    var b = devCommitments[j];

                    var conflictingEnvs = GetDeliveryDateConflicts(a, b);
                    if (conflictingEnvs.Count == 0) continue;

                    if (AlertAlreadyExists(existingAlertKeys, "DeliveryConflict", a.Id, b.Id, developer.Id))
                        continue;

                    var envList = string.Join(", ", conflictingEnvs.Select(e => e.env));
                    newAlerts.Add(new ProjectAlert
                    {
                        ProjectId = a.ProjectId,
                        DeveloperId = developer.Id,
                        CommitmentId = a.Id,
                        RelatedCommitmentId = b.Id,
                        AlertType = "DeliveryConflict",
                        Severity = conflictingEnvs.Any(e => e.env == "PROT") ? "Critical" : "Warning",
                        Message = $"{developer.User.DisplayName} tiene entregas coincidentes en {envList} para \"{a.Title}\" y \"{b.Title}\".",
                        ConflictStartDate = conflictingEnvs.First().date,
                        ConflictEndDate = conflictingEnvs.Last().date,
                    });
                }
            }

            // --- Rule 3: Overload (3+ active commitments in same period) ---
            if (devCommitments.Count >= OverloadThreshold)
            {
                if (!AlertAlreadyExists(existingAlertKeys, "Overload", null, null, developer.Id))
                {
                    var earliestStart = devCommitments.Min(c => c.StartDate);
                    var latestEnd = devCommitments.Max(c => c.CommitmentDate);

                    newAlerts.Add(new ProjectAlert
                    {
                        ProjectId = devCommitments.First().ProjectId,
                        DeveloperId = developer.Id,
                        CommitmentId = devCommitments.First().Id,
                        AlertType = "Overload",
                        Severity = devCommitments.Count >= 4 ? "Critical" : "Warning",
                        Message = $"{developer.User.DisplayName} tiene {devCommitments.Count} compromisos activos simultáneos, lo que puede generar sobrecarga.",
                        ConflictStartDate = earliestStart,
                        ConflictEndDate = latestEnd,
                    });
                }
            }
        }

        if (newAlerts.Count > 0)
        {
            _db.ProjectAlerts.AddRange(newAlerts);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return newAlerts;
    }

    public static bool CommitmentsOverlap(Commitment a, Commitment b)
    {
        return a.StartDate < b.CommitmentDate && b.StartDate < a.CommitmentDate;
    }

    public static List<(string env, DateTime date)> GetDeliveryDateConflicts(Commitment a, Commitment b)
    {
        var conflicts = new List<(string env, DateTime date)>();

        if (a.QADate.HasValue && b.QADate.HasValue && a.QADate.Value.Date == b.QADate.Value.Date)
            conflicts.Add(("QA", a.QADate.Value));

        if (a.PREDate.HasValue && b.PREDate.HasValue && a.PREDate.Value.Date == b.PREDate.Value.Date)
            conflicts.Add(("PRE", a.PREDate.Value));

        if (a.PROTDate.HasValue && b.PROTDate.HasValue && a.PROTDate.Value.Date == b.PROTDate.Value.Date)
            conflicts.Add(("PROT", a.PROTDate.Value));

        return conflicts;
    }

    private static bool AlertAlreadyExists(
        List<ExistingAlertKey> existing,
        string alertType,
        int? commitmentId,
        int? relatedCommitmentId,
        int? developerId)
    {
        return existing.Any(e =>
            e.AlertType == alertType &&
            e.DeveloperId == developerId &&
            ((e.CommitmentId == commitmentId && e.RelatedCommitmentId == relatedCommitmentId) ||
             (e.CommitmentId == relatedCommitmentId && e.RelatedCommitmentId == commitmentId)));
    }

    private record ExistingAlertKey(string AlertType, int? CommitmentId, int? RelatedCommitmentId, int? DeveloperId);
}
