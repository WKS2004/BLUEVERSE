using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Blueverse.CoastalOperations.Data;

public sealed class CoastalOperationsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CoastalOperationsDbContext>
{
    public CoastalOperationsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=blueverse;Username=blueverse;Password=design-time-placeholder";
        var options = new DbContextOptionsBuilder<CoastalOperationsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new CoastalOperationsDbContext(options);
    }
}
