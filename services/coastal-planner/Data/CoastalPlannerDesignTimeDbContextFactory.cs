using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Blueverse.CoastalPlanner.Data;

public sealed class CoastalPlannerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CoastalPlannerDbContext>
{
    public CoastalPlannerDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__DefaultConnection before running Coastal Planner EF Core tools.");
        }

        var options = new DbContextOptionsBuilder<CoastalPlannerDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new CoastalPlannerDbContext(options);
    }
}
