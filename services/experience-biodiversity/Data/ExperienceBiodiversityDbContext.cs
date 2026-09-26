using Microsoft.EntityFrameworkCore;

namespace Blueverse.ExperienceBiodiversity.Data;

public sealed class ExperienceBiodiversityDbContext : DbContext
{
    public ExperienceBiodiversityDbContext(
        DbContextOptions<ExperienceBiodiversityDbContext> options)
        : base(options)
    {
    }
}
