using Microsoft.EntityFrameworkCore;

namespace Blueverse.ExperienceBiodiversity.Data;

public static class SyntheticDemoDataSeedCommand
{
    public static async Task<int> RunAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("Set ConnectionStrings__DefaultConnection to the local PostgreSQL connection before seeding.");
            return 2;
        }

        try
        {
            var options = new DbContextOptionsBuilder<ExperienceBiodiversityDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql
                    .MigrationsHistoryTable("__EFMigrationsHistory_ExperienceBiodiversity")
                    .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null))
                .Options;

            await using var context = new ExperienceBiodiversityDbContext(options);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            if (!await context.Database.CanConnectAsync(timeout.Token))
            {
                Console.Error.WriteLine("The configured local PostgreSQL database is unavailable.");
                return 1;
            }

            await context.Database.MigrateAsync(timeout.Token);
            var result = await SyntheticDemoDataSeeder.SeedAsync(context, DateTimeOffset.UtcNow, timeout.Token);
            Console.WriteLine(
                $"Added {result.TotalRecordsAdded} synthetic demo records " +
                $"({result.DestinationsAdded} destinations, {result.ActivitiesAdded} activities, " +
                $"{result.OfferingsAdded} offerings, {result.SchedulesAdded} schedules). Existing records were preserved.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Synthetic demo-data seeding timed out or was cancelled.");
            return 1;
        }
        catch (Exception exception)
        {
            // Do not print provider exception details because they may include local connection information.
            Console.Error.WriteLine($"Synthetic demo-data seeding failed ({exception.GetType().Name}). Check the local Development configuration and database availability.");
            return 1;
        }
    }
}
