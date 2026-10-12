using Blueverse.CoastalPlanner.Data;
using Blueverse.CoastalPlanner.Data.Entities;
using Blueverse.CoastalPlanner.Models.Dtos;
using Blueverse.CoastalPlanner.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

// Explicit real-provider runner, matching the existing Auth opt-in convention.
// Every run owns one new database; never migrate, reset or delete a user's database.
public sealed class PostgresPlannerTests
{
    [Fact]
    [Trait("TestId", "PLANNER-POSTGRES-001")]
    public async Task Migrations_reorder_concurrency_constraints_and_history_cascade_use_real_PostgreSQL()
    {
        var password = Environment.GetEnvironmentVariable("BLUEVERSE_PLANNER_POSTGRES_PASSWORD");
        if (string.IsNullOrEmpty(password)) throw new InvalidOperationException("Set BLUEVERSE_PLANNER_POSTGRES_PASSWORD and enable this suite explicitly.");
        var settings = new NpgsqlConnectionStringBuilder {
            Host = Environment.GetEnvironmentVariable("BLUEVERSE_PLANNER_POSTGRES_HOST") ?? "127.0.0.1",
            Username = Environment.GetEnvironmentVariable("BLUEVERSE_PLANNER_POSTGRES_USER") ?? "blueverse",
            Password = password, Database = "postgres", Timeout = 5 };
        var database = "blueverse_planner_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(settings.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        settings.Database = database;
        var options = new DbContextOptionsBuilder<CoastalPlannerDbContext>().UseNpgsql(settings.ConnectionString, p => p.EnableRetryOnFailure()).Options;
        try
        {
            await using var db = new CoastalPlannerDbContext(options);
            await db.GetService<IMigrator>().MigrateAsync("20260927135138_InitialCoastalPlannerSchema");
            var legacyId = Guid.NewGuid();
            var legacyOwner = Guid.NewGuid();
            var legacyStart = DateTime.UtcNow.AddDays(1);
            var legacyEnd = legacyStart.AddHours(2);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO coastal_planner.itineraries (\"ItineraryId\", \"OwnerUserId\", \"Title\", \"StartsAtUtc\", \"EndsAtUtc\", \"ConcurrencyVersion\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES ({legacyId}, {legacyOwner}, 'Existing trip', {legacyStart}, {legacyEnd}, 1, {legacyStart}, {legacyStart})");
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var legacy = await db.Itineraries.SingleAsync(i => i.ItineraryId == legacyId);
            Assert.Equal("Asia/Colombo", legacy.TimeZone);
            Assert.Equal("Existing trip", legacy.Title);
            Assert.Equal(legacyOwner, legacy.OwnerUserId);
            Assert.Equal(1, legacy.ConcurrencyVersion);
            db.Itineraries.Remove(legacy);
            await db.SaveChangesAsync();
            var start = DateTime.UtcNow.AddDays(1);
            var owner = Guid.NewGuid();
            var service = new CoastalPlannerService(db, new TestPeerServicesClient(), NullLogger<CoastalPlannerService>.Instance);
            var stops = Enumerable.Range(0, 2).Select(i => new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), $"Experience {i + 1}", i, start.AddHours(i), start.AddHours(i + 1))).ToList();
            var trip = await service.CreateItineraryAsync(new("Real provider trip", null, start, start.AddHours(4), stops), owner);
            var reversed = new UpdateItineraryRequestDto("Reordered", null, start, start.AddHours(4), 1,
                [stops[1] with { ItemId = trip.Items[1].ItemId, OrderIndex = 0 }, stops[0] with { ItemId = trip.Items[0].ItemId, OrderIndex = 1 }]);
            var updated = await service.UpdateItineraryAsync(trip.ItineraryId, reversed, owner);
            Assert.Equal(trip.Items[1].ItemId, updated!.Items[0].ItemId);
            Assert.Equal(2, updated.ConcurrencyVersion);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.UpdateItineraryAsync(trip.ItineraryId, reversed, owner));
            await using var independent = new CoastalPlannerDbContext(options);
            var earlier = await independent.Itineraries.SingleAsync(i => i.ItineraryId == trip.ItineraryId);
            await service.ReEvaluateItineraryAsync(trip.ItineraryId, owner, new());
            earlier.Title = "Stale writer"; earlier.ConcurrencyVersion++;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => independent.SaveChangesAsync());
            db.ChangeTracker.Clear();
            var duplicate = new ItineraryItem { ItemId = Guid.NewGuid(), ItineraryId = trip.ItineraryId, DestinationId = Guid.NewGuid(), ActivityId = Guid.NewGuid(), Title = "Duplicate order", OrderIndex = 0, ScheduledStartUtc = start, ScheduledEndUtc = start.AddHours(1) };
            db.ItineraryItems.Add(duplicate);
            var conflict = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(conflict.InnerException).SqlState);
            db.ChangeTracker.Clear();
            duplicate.ItemId = Guid.NewGuid(); duplicate.OrderIndex = 3; duplicate.ScheduledEndUtc = start;
            db.ItineraryItems.Add(duplicate);
            var invalid = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(invalid.InnerException).SqlState);
            db.ChangeTracker.Clear();
            Assert.Single(await db.ItineraryEvaluations.ToListAsync());
            Assert.True(await service.DeleteItineraryAsync(trip.ItineraryId, owner));
            Assert.Empty(await db.ItineraryItems.ToListAsync());
            Assert.Empty(await db.ItineraryEvaluations.ToListAsync());
        }
        finally
        {
            await using var clear = new NpgsqlConnection(settings.ConnectionString); NpgsqlConnection.ClearPool(clear);
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
