using Microsoft.EntityFrameworkCore;
using Blueverse.ExperienceBiodiversity.Models;

namespace Blueverse.ExperienceBiodiversity.Data;

public sealed class ExperienceBiodiversityDbContext : DbContext
{
    public ExperienceBiodiversityDbContext(DbContextOptions<ExperienceBiodiversityDbContext> options)
        : base(options)
    {
    }

    public DbSet<Destination> Destinations => Set<Destination>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Offering> Offerings => Set<Offering>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Favourite> Favourites => Set<Favourite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Destination>(entity =>
        {
            entity.ToTable("destinations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Region).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => new { e.Status, e.Region });
            entity.HasIndex(e => new { e.Latitude, e.Longitude });
        });

        modelBuilder.Entity<Activity>(entity =>
        {
            entity.ToTable("activities");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<Offering>(entity =>
        {
            entity.ToTable("offerings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Price).HasPrecision(10, 2);
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();

            entity.HasOne(e => e.Destination)
                .WithMany(d => d.Offerings)
                .HasForeignKey(e => e.DestinationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Activity)
                .WithMany(a => a.Offerings)
                .HasForeignKey(e => e.ActivityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.DestinationId, e.Status });
            entity.HasIndex(e => new { e.ActivityId, e.Status });
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.ToTable("schedules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TimeZoneId).HasMaxLength(100).IsRequired();

            entity.HasOne(e => e.Offering)
                .WithMany(o => o.Schedules)
                .HasForeignKey(e => e.OfferingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.OfferingId, e.StartsAt, e.EndsAt, e.IsActive });
        });

        modelBuilder.Entity<Favourite>(entity =>
        {
            entity.ToTable("favourites");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.TargetType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.TargetId).IsRequired();

            entity.HasIndex(e => new { e.UserId, e.TargetType, e.TargetId }).IsUnique();
        });
    }
}
