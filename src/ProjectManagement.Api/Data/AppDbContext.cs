using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Models;

namespace ProjectManagement.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Developer> Developers => Set<Developer>();
    public DbSet<ProductOwner> ProductOwners => Set<ProductOwner>();
    public DbSet<Commitment> Commitments => Set<Commitment>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Deliverable> Deliverables => Set<Deliverable>();
    public DbSet<EnvironmentDelivery> EnvironmentDeliveries => Set<EnvironmentDelivery>();
    public DbSet<ProjectDocument> ProjectDocuments => Set<ProjectDocument>();
    public DbSet<AIExtraction> AIExtractions => Set<AIExtraction>();
    public DbSet<AIProposalRegistration> AIProposalRegistrations => Set<AIProposalRegistration>();
    public DbSet<ProjectAlert> ProjectAlerts => Set<ProjectAlert>();
    public DbSet<AzureDevOpsWorkItem> AzureDevOpsWorkItems => Set<AzureDevOpsWorkItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ===== User =====
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(300);
            entity.Property(u => u.Role).HasMaxLength(50).IsRequired();
        });

        // ===== Developer =====
        modelBuilder.Entity<Developer>(entity =>
        {
            entity.HasOne(d => d.User)
                .WithOne(u => u.Developer)
                .HasForeignKey<Developer>(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(d => d.Specialty).HasMaxLength(50);
            entity.Property(d => d.Seniority).HasMaxLength(50);
        });

        // ===== ProductOwner =====
        modelBuilder.Entity<ProductOwner>(entity =>
        {
            entity.HasOne(po => po.User)
                .WithOne(u => u.ProductOwner)
                .HasForeignKey<ProductOwner>(po => po.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(po => po.Department).HasMaxLength(200);
        });

        // ===== Project =====
        modelBuilder.Entity<Project>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(300).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(2000);
            entity.Property(p => p.Product).HasMaxLength(200);
            entity.Property(p => p.Status).HasMaxLength(50).IsRequired();
        });

        // ===== ProjectMember =====
        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.HasOne(pm => pm.Project)
                .WithMany(p => p.Members)
                .HasForeignKey(pm => pm.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pm => pm.User)
                .WithMany(u => u.ProjectMemberships)
                .HasForeignKey(pm => pm.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(pm => new { pm.ProjectId, pm.UserId }).IsUnique();
            entity.Property(pm => pm.Role).HasMaxLength(50).IsRequired();
        });

        // ===== Commitment =====
        modelBuilder.Entity<Commitment>(entity =>
        {
            entity.HasOne(c => c.Project)
                .WithMany(p => p.Commitments)
                .HasForeignKey(c => c.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Developer)
                .WithMany(d => d.Commitments)
                .HasForeignKey(c => c.DeveloperId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.ProductOwner)
                .WithMany(po => po.Commitments)
                .HasForeignKey(c => c.ProductOwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(c => c.Title).HasMaxLength(500).IsRequired();
            entity.Property(c => c.Description).HasMaxLength(4000);
            entity.Property(c => c.Status).HasMaxLength(50).IsRequired();
            entity.Property(c => c.Priority).HasMaxLength(50).IsRequired();
            entity.Property(c => c.Notes).HasMaxLength(4000);
        });

        // ===== Milestone =====
        modelBuilder.Entity<Milestone>(entity =>
        {
            entity.HasOne(m => m.Project)
                .WithMany(p => p.Milestones)
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(m => m.Title).HasMaxLength(300).IsRequired();
            entity.Property(m => m.Description).HasMaxLength(2000);
            entity.Property(m => m.Status).HasMaxLength(50).IsRequired();
        });

        // ===== Deliverable =====
        modelBuilder.Entity<Deliverable>(entity =>
        {
            entity.HasOne(d => d.Project)
                .WithMany(p => p.Deliverables)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Commitment)
                .WithMany()
                .HasForeignKey(d => d.CommitmentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(d => d.Title).HasMaxLength(500).IsRequired();
            entity.Property(d => d.Description).HasMaxLength(4000);
            entity.Property(d => d.Status).HasMaxLength(50).IsRequired();
        });

        // ===== EnvironmentDelivery =====
        modelBuilder.Entity<EnvironmentDelivery>(entity =>
        {
            entity.HasOne(ed => ed.Commitment)
                .WithMany(c => c.EnvironmentDeliveries)
                .HasForeignKey(ed => ed.CommitmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ed => ed.Deliverable)
                .WithMany(d => d.EnvironmentDeliveries)
                .HasForeignKey(ed => ed.DeliverableId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(ed => ed.Environment).HasMaxLength(20).IsRequired();
            entity.Property(ed => ed.Status).HasMaxLength(50).IsRequired();
            entity.Property(ed => ed.Notes).HasMaxLength(2000);
        });

        // ===== ProjectDocument =====
        modelBuilder.Entity<ProjectDocument>(entity =>
        {
            entity.HasOne(pd => pd.Project)
                .WithMany(p => p.Documents)
                .HasForeignKey(pd => pd.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pd => pd.UploadedBy)
                .WithMany()
                .HasForeignKey(pd => pd.UploadedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(pd => pd.Title).HasMaxLength(300).IsRequired();
            entity.Property(pd => pd.Description).HasMaxLength(2000);
            entity.Property(pd => pd.DocumentType).HasMaxLength(50).IsRequired();
            entity.Property(pd => pd.OriginalFileName).HasMaxLength(300);
            entity.Property(pd => pd.FileSizeBytes);
            entity.Property(pd => pd.IsDeleted).HasDefaultValue(false);
            entity.Property(pd => pd.ProcessingStatus)
                .HasMaxLength(50)
                .IsRequired()
                .HasDefaultValue("Prepared");
            entity.Property(pd => pd.FilePath).HasMaxLength(1000);
            entity.Property(pd => pd.Url).HasMaxLength(2000);
            entity.HasIndex(pd => pd.IsDeleted);
            entity.HasOne<ProjectDocument>()
                .WithMany()
                .HasForeignKey(pd => pd.SourceDocumentId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<AIExtraction>()
                .WithMany()
                .HasForeignKey(pd => pd.SourceAnalysisId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(pd => pd.SourceAnalysisId)
                .IsUnique()
                .HasFilter("\"SourceAnalysisId\" IS NOT NULL AND \"IsDeleted\" = 0");
            entity.HasIndex(pd => pd.SourceDocumentId);
        });

        // ===== AIExtraction =====
        modelBuilder.Entity<AIExtraction>(entity =>
        {
            entity.HasOne(ai => ai.Project)
                .WithMany()
                .HasForeignKey(ai => ai.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(ai => ai.Commitment)
                .WithMany()
                .HasForeignKey(ai => ai.CommitmentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(ai => ai.ReviewedBy)
                .WithMany()
                .HasForeignKey(ai => ai.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(ai => ai.SourceType).HasMaxLength(50).IsRequired();
            entity.Property(ai => ai.SourceReference).HasMaxLength(500);
            entity.Property(ai => ai.Status).HasMaxLength(50).IsRequired();
            entity.Property(ai => ai.ProcessingError).HasMaxLength(1000);
        });

        modelBuilder.Entity<AIProposalRegistration>(entity =>
        {
            entity.HasOne(registration => registration.AIExtraction)
                .WithMany(extraction => extraction.ProposalRegistrations)
                .HasForeignKey(registration => registration.AIExtractionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(registration => registration.Project)
                .WithMany()
                .HasForeignKey(registration => registration.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(registration => new { registration.AIExtractionId, registration.ProposalId })
                .IsUnique();
            entity.Property(registration => registration.ProposalId).HasMaxLength(100).IsRequired();
            entity.Property(registration => registration.EntityType).HasMaxLength(50).IsRequired();
        });

        // ===== ProjectAlert =====
        modelBuilder.Entity<ProjectAlert>(entity =>
        {
            entity.HasOne(pa => pa.Project)
                .WithMany(p => p.Alerts)
                .HasForeignKey(pa => pa.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pa => pa.Developer)
                .WithMany()
                .HasForeignKey(pa => pa.DeveloperId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(pa => pa.Commitment)
                .WithMany()
                .HasForeignKey(pa => pa.CommitmentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(pa => pa.RelatedCommitment)
                .WithMany()
                .HasForeignKey(pa => pa.RelatedCommitmentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(pa => pa.AlertType).HasMaxLength(50).IsRequired();
            entity.Property(pa => pa.Severity).HasMaxLength(50).IsRequired();
            entity.Property(pa => pa.Status).HasMaxLength(50).IsRequired().HasDefaultValue("Open");
            entity.Property(pa => pa.Message).HasMaxLength(1000).IsRequired();
        });

        // ===== AzureDevOpsWorkItem =====
        modelBuilder.Entity<AzureDevOpsWorkItem>(entity =>
        {
            entity.HasOne(wi => wi.Project)
                .WithMany(p => p.WorkItems)
                .HasForeignKey(wi => wi.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(wi => wi.Commitment)
                .WithMany()
                .HasForeignKey(wi => wi.CommitmentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(wi => new { wi.DevOpsOrganization, wi.DevOpsProject, wi.DevOpsWorkItemId }).IsUnique();
            entity.Property(wi => wi.DevOpsOrganization).HasMaxLength(200).IsRequired();
            entity.Property(wi => wi.DevOpsProject).HasMaxLength(200).IsRequired();
            entity.Property(wi => wi.WorkItemType).HasMaxLength(50).IsRequired();
            entity.Property(wi => wi.Title).HasMaxLength(500).IsRequired();
            entity.Property(wi => wi.State).HasMaxLength(50);
            entity.Property(wi => wi.AssignedTo).HasMaxLength(200);
            entity.Property(wi => wi.Url).HasMaxLength(2000);
        });

        // ===== AuditLog =====
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasOne(al => al.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(al => al.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(al => al.Action).HasMaxLength(50).IsRequired();
            entity.Property(al => al.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(al => al.IpAddress).HasMaxLength(50);
        });
    }
}
