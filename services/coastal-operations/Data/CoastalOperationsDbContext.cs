using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Data;

public sealed class CoastalOperationsDbContext(DbContextOptions<CoastalOperationsDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("coastal_operations");
    }
}
