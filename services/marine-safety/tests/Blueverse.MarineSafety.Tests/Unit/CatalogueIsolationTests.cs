namespace Blueverse.MarineSafety.Tests.Unit;

public sealed class CatalogueIsolationTests
{
    private sealed class FailureTransport(Func<CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; return action(ct); }
    }

    [Fact, Trait("CaseId", "M2-ISOLATION-005")]
    public async Task Missing_configuration_reports_unavailable_without_transport()
    {
        using var handler = new FailureTransport(_ => throw new InvalidOperationException("Transport must not run."));
        var adapter = new ExperienceCatalogueClient(new HttpClient(handler));
        var error = await Assert.ThrowsAsync<CatalogueUnavailableException>(() => adapter.GetPublishedAsync(default));
        Assert.Equal("UNCONFIGURED", error.Reason);
        Assert.Equal(0, handler.Calls);
    }

    [Fact, Trait("CaseId", "M2-ISOLATION-006")]
    public async Task Timeout_is_bounded_and_caller_cancellation_is_preserved()
    {
        using var handler = new FailureTransport(async ct => { await Task.Delay(Timeout.Infinite, ct); return new HttpResponseMessage(); });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://catalogue.test"), Timeout = TimeSpan.FromMilliseconds(30) };
        var adapter = new ExperienceCatalogueClient(http);
        var timedOut = await Assert.ThrowsAsync<CatalogueUnavailableException>(() => adapter.GetPublishedAsync(default));
        Assert.Equal("TIMEOUT", timedOut.Reason);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.GetPublishedAsync(cancelled.Token));
        Assert.Equal(1, handler.Calls);
    }

    [Fact, Trait("CaseId", "M2-ISOLATION-008")]
    public async Task Unreachable_service_is_typed_unavailability_without_exposing_transport_diagnostics()
    {
        using var handler = new FailureTransport(_ => throw new HttpRequestException("Private network diagnostic"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://catalogue.test") };
        var error = await Assert.ThrowsAsync<CatalogueUnavailableException>(() => new ExperienceCatalogueClient(http).GetPublishedAsync(default));
        Assert.Equal("UNREACHABLE", error.Reason);
        Assert.DoesNotContain("Private network", error.Message);
        Assert.Equal(1, handler.Calls);
    }
}
