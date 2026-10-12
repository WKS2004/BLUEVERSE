namespace Blueverse.CoastalPlanner.Integration;

public sealed class BiodiversityCircuitBreaker
{
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private int _consecutiveFailures;
    private DateTimeOffset? _openedUntil;
    private bool _probeInFlight;

    public BiodiversityCircuitBreaker(IConfiguration configuration, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _failureThreshold = configuration.GetValue<int?>("PeerServices:BiodiversityFailureThreshold") ?? 3;
        _openDuration = TimeSpan.FromSeconds(configuration.GetValue<int?>("PeerServices:BiodiversityCircuitOpenSeconds") ?? 15);
        if (_failureThreshold is < 1 or > 20)
            throw new InvalidOperationException("PeerServices:BiodiversityFailureThreshold must be between 1 and 20.");
        if (_openDuration < TimeSpan.FromSeconds(1) || _openDuration > TimeSpan.FromMinutes(5))
            throw new InvalidOperationException("PeerServices:BiodiversityCircuitOpenSeconds must be between 1 and 300.");
    }

    public bool TryEnter(out TimeSpan retryAfter)
    {
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            if (_openedUntil is null)
            {
                retryAfter = TimeSpan.Zero;
                return true;
            }

            if (_openedUntil > now || _probeInFlight)
            {
                retryAfter = _openedUntil > now ? _openedUntil.Value - now : _openDuration;
                return false;
            }

            _probeInFlight = true;
            retryAfter = TimeSpan.Zero;
            return true;
        }
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
            _openedUntil = null;
            _probeInFlight = false;
        }
    }

    public void RecordFailure()
    {
        lock (_gate)
        {
            _probeInFlight = false;
            _consecutiveFailures++;
            if (_consecutiveFailures >= _failureThreshold)
            {
                _openedUntil = _timeProvider.GetUtcNow().Add(_openDuration);
            }
        }
    }
}
