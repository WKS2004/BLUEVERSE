using Microsoft.EntityFrameworkCore;
using Blueverse.MarineSafety.Models;

namespace Blueverse.MarineSafety.Data;

public class MarineSafetyDbContext : DbContext
{
    public MarineSafetyDbContext(DbContextOptions<MarineSafetyDbContext> options) : base(options)
    {
    }

    public DbSet<MarineActivity> MarineActivities => Set<MarineActivity>();
    public DbSet<SafetyProfile> SafetyProfiles => Set<SafetyProfile>();
    public DbSet<ConditionSnapshot> ConditionSnapshots => Set<ConditionSnapshot>();
    public DbSet<SuitabilityAssessmentRecord> SuitabilityAssessments => Set<SuitabilityAssessmentRecord>();

    // Permission resolution runs through IdentityPermissionResolver using a
    // read-only, parameterized query over the Auth-owned identity tables; it
    // intentionally does not map identity entities into this context.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Locally-owned activity reference (placeholder for Member 1's
        // canonical taxonomy; see MarineActivity). No FK to other services.
        modelBuilder.Entity<MarineActivity>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.Name).IsUnique();
            entity.Property(a => a.Name).IsRequired().HasMaxLength(128);
            entity.Property(a => a.ActivityType).IsRequired().HasMaxLength(64);
            entity.Property(a => a.IsActive).IsRequired();
            entity.Property(a => a.CreatedAt).IsRequired();
        });

        // Safety profiles: one active profile per activity, every limit a
        // positive decimal with fixed precision. Caution limits are optional
        // and must sit below their hard limit when present.
        modelBuilder.Entity<SafetyProfile>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => new { p.ActivityId, p.IsActive })
                .HasFilter("\"IsActive\"")
                .IsUnique();
            entity.Property(p => p.ActivityId).IsRequired();
            entity.Property(p => p.MaxWindSpeed).HasPrecision(6, 2);
            entity.Property(p => p.MaxWaveHeight).HasPrecision(5, 2);
            entity.Property(p => p.MaxSwellHeight).HasPrecision(5, 2);
            entity.Property(p => p.CautionWindSpeed).HasPrecision(6, 2);
            entity.Property(p => p.CautionWaveHeight).HasPrecision(5, 2);
            entity.Property(p => p.CautionSwellHeight).HasPrecision(5, 2);
            entity.Property(p => p.IsActive).IsRequired();
            entity.Property(p => p.Version).IsRequired();
            entity.Property(p => p.CreatedAt).IsRequired();
            entity.Property(p => p.UpdatedAt).IsRequired();

            entity.HasOne(p => p.Activity)
                .WithMany(a => a.SafetyProfiles)
                .HasForeignKey(p => p.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable("SafetyProfiles", table =>
            {
                table.HasCheckConstraint("CK_SafetyProfiles_MaxWindSpeed_Positive", "\"MaxWindSpeed\" > 0");
                table.HasCheckConstraint("CK_SafetyProfiles_MaxWaveHeight_Positive", "\"MaxWaveHeight\" > 0");
                table.HasCheckConstraint("CK_SafetyProfiles_MaxSwellHeight_Positive", "\"MaxSwellHeight\" > 0");
                table.HasCheckConstraint(
                    "CK_SafetyProfiles_CautionWindSpeed_BelowMax",
                    "\"CautionWindSpeed\" IS NULL OR \"CautionWindSpeed\" < \"MaxWindSpeed\"");
                table.HasCheckConstraint(
                    "CK_SafetyProfiles_CautionWaveHeight_BelowMax",
                    "\"CautionWaveHeight\" IS NULL OR \"CautionWaveHeight\" < \"MaxWaveHeight\"");
                table.HasCheckConstraint(
                    "CK_SafetyProfiles_CautionSwellHeight_BelowMax",
                    "\"CautionSwellHeight\" IS NULL OR \"CautionSwellHeight\" < \"MaxSwellHeight\"");
            });
        });

        // Provider-derived condition snapshots. Rows are immutable through the
        // public API; the location/forecast index serves the "nearest snapshot
        // for a place and time" query and the retrieval index serves history.
        modelBuilder.Entity<ConditionSnapshot>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => new { s.Latitude, s.Longitude, s.ForecastTime });
            entity.HasIndex(s => new { s.Latitude, s.Longitude, s.RetrievedAt });
            entity.HasIndex(s => s.RetrievedAt);
            entity.Property(s => s.Latitude).HasPrecision(8, 5);
            entity.Property(s => s.Longitude).HasPrecision(9, 5);
            entity.Property(s => s.ForecastTime).IsRequired();
            entity.Property(s => s.RetrievedAt).IsRequired();
            entity.Property(s => s.WindSpeed).HasPrecision(6, 2);
            entity.Property(s => s.WaveHeight).HasPrecision(5, 2);
            entity.Property(s => s.SwellHeight).HasPrecision(5, 2);
            entity.Property(s => s.Rain).HasPrecision(6, 2);
            entity.Property(s => s.Source).IsRequired().HasMaxLength(64);
            entity.Property(s => s.FreshnessStatus).IsRequired().HasMaxLength(16);
        });

        // Suitability assessment history. No FK to ConditionSnapshot or
        // SafetyProfile rows: history must survive provider-retention pruning
        // and later profile deletion, so it stores the version and evidence
        // references by value.
        modelBuilder.Entity<SuitabilityAssessmentRecord>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => new { r.ActivityId, r.RequestedTime });
            entity.HasIndex(r => r.EvaluatedAt);
            entity.Property(r => r.ActivityId).IsRequired();
            entity.Property(r => r.ProfileVersion).IsRequired();
            entity.Property(r => r.Latitude).HasPrecision(8, 5);
            entity.Property(r => r.Longitude).HasPrecision(9, 5);
            entity.Property(r => r.RequestedTime).IsRequired();
            entity.Property(r => r.EvaluatedAt).IsRequired();
            entity.Property(r => r.Result).IsRequired().HasMaxLength(16);
            entity.Property(r => r.Source).IsRequired().HasMaxLength(64);
            entity.Property(r => r.FreshnessStatus).IsRequired().HasMaxLength(16);
            entity.Property(r => r.SafetyProfileId).IsRequired();
            entity.Property(r => r.ConditionSnapshotId).IsRequired();
            entity.Property(r => r.CreatedAt).IsRequired();

            entity.HasOne(r => r.Activity)
                .WithMany()
                .HasForeignKey(r => r.ActivityId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        SeedReferenceActivities(modelBuilder);
    }

    private static void SeedReferenceActivities(ModelBuilder modelBuilder)
    {
        var seedDate = new DateTime(2026, 9, 26, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<MarineActivity>().HasData(
            new MarineActivity
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333301"),
                Name = "Surfing",
                ActivityType = "Surfing",
                IsActive = true,
                CreatedAt = seedDate
            },
            new MarineActivity
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333302"),
                Name = "Snorkeling",
                ActivityType = "Snorkeling",
                IsActive = true,
                CreatedAt = seedDate
            },
            new MarineActivity
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333303"),
                Name = "Scuba Diving",
                ActivityType = "Diving",
                IsActive = true,
                CreatedAt = seedDate
            },
            new MarineActivity
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333304"),
                Name = "Whale and Dolphin Watching",
                ActivityType = "BoatTour",
                IsActive = true,
                CreatedAt = seedDate
            },
            new MarineActivity
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333305"),
                Name = "Coastal Boat Tour",
                ActivityType = "BoatTour",
                IsActive = true,
                CreatedAt = seedDate
            });
    }
}
