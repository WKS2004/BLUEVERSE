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
    public DbSet<BiodiversityPredictionCache> BiodiversityPredictions => Set<BiodiversityPredictionCache>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("coastal_planner");

        modelBuilder.Entity<PlanningWorkflow>(entity =>
        {
            entity.HasIndex(e => e.CreatedAtUtc);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.InitiatorUserId);
        });

        modelBuilder.Entity<Itinerary>(entity =>
        {
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
            entity.HasIndex(e => new { e.ItineraryId, e.OrderIndex });
        });

        modelBuilder.Entity<RecommendationSession>(entity =>
        {
            entity.HasIndex(e => e.WorkflowId);
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<BiodiversityPredictionCache>(entity =>
        {
            entity.HasIndex(e => new { e.DestinationId, e.ActivityId });
            entity.HasIndex(e => e.ExpiresAtUtc);
        });
    }
}
