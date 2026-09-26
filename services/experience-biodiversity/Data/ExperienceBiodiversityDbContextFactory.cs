using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Blueverse.ExperienceBiodiversity.Data;

public sealed class ExperienceBiodiversityDbContextFactory
    : IDesignTimeDbContextFactory<ExperienceBiodiversityDbContext>
{
    public ExperienceBiodiversityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "EXPERIENCE_BIODIVERSITY_MIGRATION_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=blueverse;Username=blueverse;Password=design-time-only";

        var options = new DbContextOptionsBuilder<ExperienceBiodiversityDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsHistoryTable(
                    "__EFMigrationsHistory_ExperienceBiodiversity"))
            .Options;

        return new ExperienceBiodiversityDbContext(options);
    }
}
