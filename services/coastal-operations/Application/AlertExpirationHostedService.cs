using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Application;

public sealed class AlertExpirationHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<AlertExpirationHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await ExpireDueAlertsAsync(stoppingToken);
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ExpireDueAlertsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CoastalOperationsDbContext>();
            var now = DateTimeOffset.UtcNow;
            var dueAlerts = await db.OperationalAlerts
                .Where(x => x.Lifecycle == "ACTIVE" && x.ValidUntil <= now)
                .OrderBy(x => x.ValidUntil)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            foreach (var alert in dueAlerts)
            {
                var previousVersion = alert.Version;
                alert.Lifecycle = "EXPIRED";
                alert.Version++;
                alert.UpdatedAt = now;
                db.AlertDecisions.Add(new AlertDecision
                {
                    Id = Guid.CreateVersion7(),
                    AlertId = alert.Id,
                    Decision = "EXPIRE",
                    ActorId = Guid.Empty,
                    ExpectedVersion = previousVersion,
                    DecidedAt = now
                });
                db.OperationsAudit.Add(new OperationsAuditEntry
                {
                    Id = Guid.CreateVersion7(),
                    ResourceType = "alert",
                    ResourceId = alert.Id,
                    Action = "EXPIRED",
                    ActorId = Guid.Empty,
                    CorrelationId = "system:alert-expiry",
                    CreatedAt = now
                });
            }

            if (dueAlerts.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Expired {AlertCount} Coastal Operations alerts.", dueAlerts.Count);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent reviewer transition won. The next bounded sweep rechecks remaining due alerts.
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Coastal Operations alert expiry sweep failed.");
        }
    }
}
