namespace Blueverse.Auth.Services;

/// <summary>
/// Archives expired sessions and their refresh-token associations. Requests
/// enforce expiry synchronously; this job keeps ActiveSessions limited to
/// current sessions between requests.
/// </summary>
public sealed class SessionCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionCleanupHostedService> _logger;

    public SessionCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<SessionCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        do
        {
            try
            {
                await CleanupExpiredSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Expired authentication-session cleanup failed; it will retry later.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var archivedCount = await authService.ArchiveExpiredSessionsAsync(cancellationToken);
        if (archivedCount > 0)
        {
            _logger.LogInformation("Archived {SessionCount} expired authentication sessions", archivedCount);
        }
    }
}
