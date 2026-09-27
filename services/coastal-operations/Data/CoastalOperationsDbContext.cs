using Blueverse.CoastalOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Data;

public sealed class CoastalOperationsDbContext(DbContextOptions<CoastalOperationsDbContext> options)
    : DbContext(options)
{
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentProposal> AssessmentProposals => Set<AssessmentProposal>();
    public DbSet<AssessmentEvidence> AssessmentEvidence => Set<AssessmentEvidence>();
    public DbSet<ReviewerDecision> ReviewerDecisions => Set<ReviewerDecision>();
    public DbSet<TargetOperationalState> TargetOperationalStates => Set<TargetOperationalState>();
    public DbSet<OperationalHistoryEntry> OperationalHistory => Set<OperationalHistoryEntry>();
    public DbSet<OperationalAlert> OperationalAlerts => Set<OperationalAlert>();
    public DbSet<AlertDecision> AlertDecisions => Set<AlertDecision>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OperationsAuditEntry> OperationsAudit => Set<OperationsAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("coastal_operations");

        modelBuilder.Entity<Assessment>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Assessments_WorkflowStatus", "\"WorkflowStatus\" IN ('SUBMITTED','PROPOSAL_READY','PENDING_APPROVAL','REVISION_REQUESTED','REJECTED','APPROVED','EXECUTED','BLOCKED','SAFE_FAILURE')");
                table.HasCheckConstraint("CK_Assessments_AiDependencyStatus", "\"AiDependencyStatus\" IN ('NOT_CONNECTED','UNAVAILABLE','AVAILABLE')");
                table.HasCheckConstraint("CK_Assessments_AiDispatchOutcome", "\"AiDispatchOutcome\" IN ('NOT_REQUESTED','NOT_STARTED','SUCCEEDED','UNAVAILABLE','INVALID_RESULT')");
                table.HasCheckConstraint("CK_Assessments_ComponentDependencies", "jsonb_typeof(\"ComponentDependenciesJson\") = 'array'");
                table.HasCheckConstraint("CK_Assessments_Period", "\"PeriodEndsAt\" > \"PeriodStartsAt\"");
                table.HasCheckConstraint("CK_Assessments_Version", "\"Version\" > 0");
            });
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.WorkflowId).IsUnique();
            entity.HasIndex(x => new { x.InitiatedBy, x.Id });
            entity.HasIndex(x => new { x.WorkflowStatus, x.Id });
            entity.Property(x => x.TargetType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Objective).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.WorkflowStatus).HasMaxLength(32).IsRequired();
            entity.Property(x => x.AiDependencyStatus).HasMaxLength(24).IsRequired();
            entity.Property(x => x.AiDispatchOutcome).HasMaxLength(24).IsRequired();
            entity.Property(x => x.ComponentDependenciesJson).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasMany(x => x.Proposals).WithOne(x => x.Assessment).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Decisions).WithOne(x => x.Assessment).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Evidence).WithOne(x => x.Assessment).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssessmentEvidence>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_AssessmentEvidence_Version", "\"AssessmentVersion\" > 0");
                table.HasCheckConstraint("CK_AssessmentEvidence_ByteLength", "\"ByteLength\" BETWEEN 1 AND 5242880");
                table.HasCheckConstraint("CK_AssessmentEvidence_InspectionStatus", "\"InspectionStatus\" IN ('AVAILABLE','EXPIRED')");
                table.HasCheckConstraint("CK_AssessmentEvidence_Expiry", "\"ExpiresAt\" > \"UploadedAt\"");
            });
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.AssessmentId, x.UploadedAt });
            entity.HasIndex(x => new { x.AssessmentId, x.Id }).IsUnique();
            entity.HasIndex(x => new { x.InspectionStatus, x.ExpiresAt });
            entity.Property(x => x.MediaType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ContentSha256).HasMaxLength(64).IsRequired();
            entity.Property(x => x.InspectionStatus).HasMaxLength(16).IsConcurrencyToken().IsRequired();
        });

        modelBuilder.Entity<AssessmentProposal>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_AssessmentProposals_Validity",
                "\"ExpiresAt\" > \"CreatedAt\" AND \"ExpiresAt\" <= \"CreatedAt\" + INTERVAL '30 minutes'"));
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.AssessmentId, x.ProposalVersion }).IsUnique();
            entity.Property(x => x.ValidationStatus).HasMaxLength(24).IsRequired();
            entity.Property(x => x.TargetType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.ProposedState).HasMaxLength(32);
            entity.Property(x => x.ProposalVersion).IsConcurrencyToken();
        });

        modelBuilder.Entity<ReviewerDecision>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ReviewerDecisions_Decision", "\"Decision\" IN ('APPROVE','REJECT','REQUEST_REVISION')");
                table.HasCheckConstraint("CK_ReviewerDecisions_ProposalVersion", "\"ProposalVersion\" > 0");
            });
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProposalId, x.ProposalVersion }).IsUnique();
            entity.HasIndex(x => new { x.AssessmentId, x.DecidedAt });
            entity.Property(x => x.Decision).HasMaxLength(24).IsRequired();
            entity.Property(x => x.Explanation).HasMaxLength(1000);
            entity.Property(x => x.WorkflowStatusAfterDecision).HasMaxLength(32).IsRequired();
            entity.Property(x => x.AppliedOperationalState).HasMaxLength(32);
            entity.HasOne(x => x.Proposal).WithMany(x => x.Decisions).HasForeignKey(x => x.ProposalId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TargetOperationalState>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_TargetOperationalStates_State", "\"State\" IN ('OPEN','CAUTION','TEMPORARILY_SUSPENDED') OR (\"TargetType\" = 'SESSION' AND \"State\" IN ('CANCELLED','COMPLETED'))");
                table.HasCheckConstraint("CK_TargetOperationalStates_Version", "\"Version\" > 0");
            });
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TargetType, x.TargetId }).IsUnique();
            entity.Property(x => x.TargetType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.State).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<OperationalHistoryEntry>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TargetType, x.TargetId, x.Id });
            entity.Property(x => x.TargetType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.PreviousState).HasMaxLength(32).IsRequired();
            entity.Property(x => x.NewState).HasMaxLength(32).IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
            entity.HasOne<Assessment>().WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AssessmentProposal>().WithMany().HasForeignKey(x => x.ProposalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ReviewerDecision>().WithMany().HasForeignKey(x => x.DecisionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OperationalAlert>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_OperationalAlerts_Severity", "\"Severity\" IN ('LOW','MODERATE','HIGH','CRITICAL')");
                table.HasCheckConstraint("CK_OperationalAlerts_Visibility", "\"Visibility\" IN ('OPERATIONS','PUBLIC')");
                table.HasCheckConstraint("CK_OperationalAlerts_Lifecycle", "\"Lifecycle\" IN ('PROPOSED','ACTIVE','RESOLVED','EXPIRED','SUPERSEDED')");
                table.HasCheckConstraint("CK_OperationalAlerts_Period", "\"ValidUntil\" > \"ValidFrom\"");
                table.HasCheckConstraint("CK_OperationalAlerts_Version", "\"Version\" > 0");
            });
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Lifecycle, x.Id });
            entity.HasIndex(x => new { x.TargetType, x.TargetId, x.Lifecycle });
            entity.HasIndex(x => new { x.Visibility, x.Lifecycle, x.ValidFrom, x.ValidUntil });
            entity.Property(x => x.TargetType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Severity).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Visibility).HasMaxLength(16).HasDefaultValue("OPERATIONS").IsRequired();
            entity.Property(x => x.Lifecycle).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne(x => x.Assessment).WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AlertDecision>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_AlertDecisions_Decision", "\"Decision\" IN ('PUBLISH','RESOLVE','EXPIRE')");
                table.HasCheckConstraint("CK_AlertDecisions_ExpectedVersion", "\"ExpectedVersion\" > 0");
            });
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.AlertId, x.DecidedAt });
            entity.Property(x => x.Decision).HasMaxLength(16).IsRequired();
            entity.HasOne<OperationalAlert>().WithMany().HasForeignKey(x => x.AlertId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ActorId, x.Operation, x.Key }).IsUnique();
            entity.Property(x => x.Operation).HasMaxLength(48).IsRequired();
            entity.Property(x => x.Key).HasMaxLength(128).IsRequired();
            entity.Property(x => x.RequestDigest).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ResponseBody).HasMaxLength(65536).IsRequired();
        });

        modelBuilder.Entity<OperationsAuditEntry>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ResourceType, x.ResourceId, x.CreatedAt });
            entity.Property(x => x.ResourceType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(48).IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
        });
    }
}
