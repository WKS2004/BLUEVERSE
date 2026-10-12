namespace Blueverse.CoastalOperations.Application;

public sealed class AssessmentEvidenceRetentionHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<AssessmentEvidenceRetentionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireDueEvidenceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Coastal Operations evidence retention pass failed.");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task ExpireDueEvidenceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var evidence = scope.ServiceProvider.GetRequiredService<AssessmentEvidenceApplicationService>();
        var expiredCount = await evidence.ExpireBatchAsync(DateTimeOffset.UtcNow, cancellationToken);
        if (expiredCount > 0)
            logger.LogInformation("Expired {EvidenceCount} Coastal Operations evidence attachments.", expiredCount);
    }
}
