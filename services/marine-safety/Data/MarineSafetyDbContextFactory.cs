using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Blueverse.MarineSafety.Data;

/// <summary>
/// Design-time factory so `dotnet ef` can create marine-safety migrations
/// without a running PostgreSQL instance, mirroring the Auth service factory.
/// </summary>
public sealed class MarineSafetyDbContextFactory : IDesignTimeDbContextFactory<MarineSafetyDbContext>
{
    public MarineSafetyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MARINE_SAFETY_MIGRATION_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=blueverse;Username=blueverse;Password=design-time-only";

        var options = new DbContextOptionsBuilder<MarineSafetyDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new MarineSafetyDbContext(options);
    }
}
