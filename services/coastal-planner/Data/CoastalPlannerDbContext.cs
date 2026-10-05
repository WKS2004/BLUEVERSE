using Microsoft.EntityFrameworkCore;
using Blueverse.CoastalPlanner.Data.Entities;

namespace Blueverse.CoastalPlanner.Data;

public class CoastalPlannerDbContext : DbContext
{
    public CoastalPlannerDbContext(DbContextOptions<CoastalPlannerDbContext> options) : base(options)
    {
    }

    public DbSet<PlanningWorkflow> PlanningWorkflows => Set<PlanningWorkflow>();
    public DbSet<Itinerary> Itineraries => Set<Itinerary>();
    public DbSet<ItineraryItem> ItineraryItems => Set<ItineraryItem>();
    public DbSet<RecommendationSession> Recommendations => Set<RecommendationSession>();
    public DbSet<ItineraryEvaluation> ItineraryEvaluations => Set<ItineraryEvaluation>();
    public DbSet<BiodiversityPredictionCache> BiodiversityPredictions => Set<BiodiversityPredictionCache>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("coastal_planner");
        modelBuilder.Entity<ItineraryEvaluation>(entity =>
        {
            entity.ToTable("itinerary_evaluations");
            entity.HasIndex(e => new { e.ItineraryId, e.EvaluatedAtUtc });
            entity.HasOne<Itinerary>().WithMany().HasForeignKey(e => e.ItineraryId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlanningWorkflow>(entity =>
        {
            entity.ToTable("planning_workflows", "coastal_planner", table =>
                table.HasCheckConstraint("CK_planning_workflows_status",
                    "\"Status\" IN ('PENDING', 'PROCESSING', 'COMPLETED', 'FAILED')"));
            entity.HasIndex(e => e.CreatedAtUtc);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.InitiatorUserId);
        });

        modelBuilder.Entity<Itinerary>(entity =>
        {
            entity.Property(e => e.TimeZone).HasDefaultValue("Asia/Colombo");
            entity.ToTable("itineraries", "coastal_planner", table =>
                table.HasCheckConstraint("CK_itineraries_date_range", "\"EndsAtUtc\" > \"StartsAtUtc\""));
            entity.HasIndex(e => e.OwnerUserId);
            entity.HasIndex(e => e.CreatedAtUtc);
            entity.Property(e => e.ConcurrencyVersion).IsConcurrencyToken();

            entity.HasMany(e => e.Items)
                  .WithOne(i => i.Itinerary)
                  .HasForeignKey(i => i.ItineraryId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItineraryItem>(entity =>
        {
            entity.Property(e => e.TimeZone).HasDefaultValue("Asia/Colombo");
            entity.ToTable("itinerary_items", "coastal_planner", table =>
            {
                table.HasCheckConstraint("CK_itinerary_items_order_non_negative", "\"OrderIndex\" >= 0");
                table.HasCheckConstraint("CK_itinerary_items_date_range", "\"ScheduledEndUtc\" > \"ScheduledStartUtc\"");
            });
            entity.HasIndex(e => new { e.ItineraryId, e.OrderIndex }).IsUnique();
        });

        modelBuilder.Entity<RecommendationSession>(entity =>
        {
            entity.Property(e => e.Outcome).HasDefaultValue("LEGACY_RESULT");
            entity.ToTable("recommendations", "coastal_planner", table =>
            {
                table.HasCheckConstraint("CK_recommendations_date_range", "\"EndsAtUtc\" > \"StartsAtUtc\"");
                table.HasCheckConstraint("CK_recommendations_duration_positive", "\"DurationHours\" > 0");
            });
            entity.HasIndex(e => e.WorkflowId).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasOne<PlanningWorkflow>()
                .WithMany()
                .HasForeignKey(e => e.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BiodiversityPredictionCache>(entity =>
        {
            entity.ToTable("biodiversity_predictions_cache");
            entity.HasIndex(e => new { e.DestinationId, e.ActivityId });
            entity.HasIndex(e => e.ExpiresAtUtc);
        });
    }
}
