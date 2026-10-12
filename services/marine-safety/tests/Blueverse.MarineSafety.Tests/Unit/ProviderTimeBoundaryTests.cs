using System.Globalization;
using Microsoft.Extensions.Options;

namespace Blueverse.MarineSafety.Tests.Unit;

public sealed class ProviderTimeBoundaryTests
{
    [Fact]
    [Trait("CaseId", "M2-REPAIR-TIME-003")]
    public async Task Provider_rejects_extremes_before_transport_and_handles_safe_range_edges()
    {
        DateTime forecast = default;
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { hourly = new {
                time = new[] { forecast.ToString("O", CultureInfo.InvariantCulture) },
                wind_speed_10m = new[] { 15 }, precipitation = new[] { 0 }, weather_code = new[] { 0 },
                wave_height = new[] { 0.8 }, swell_wave_height = new[] { 0.7 }
            } })
        });
        var adapter = new OpenMeteoClient(new TestHttpClientFactory(handler),
            Options.Create(new OpenMeteoOptions()), NullLogger<OpenMeteoClient>.Instance);
        foreach (var invalid in new[] { DateTime.MinValue, DateTime.MaxValue })
            await Assert.ThrowsAsync<MarineTimeValidationException>(() => adapter.GetConditionsAsync(6.025m, 80.216m, invalid, default));
        Assert.Empty(handler.Requests);
        foreach (var edge in new[] {
            DateTime.SpecifyKind(DateTime.MinValue.AddHours(1), DateTimeKind.Utc),
            DateTime.SpecifyKind(DateTime.MaxValue.AddHours(-1), DateTimeKind.Utc) })
        {
            forecast = edge;
            var result = await adapter.GetConditionsAsync(6.025m, 80.216m, edge, default);
            Assert.Equal(edge, result.ForecastTimeUtc);
            Assert.Equal(15m, result.WindSpeed);
            Assert.Equal(0.8m, result.WaveHeight);
            Assert.Empty(result.MissingFields);
        }
        Assert.Equal(4, handler.Requests.Count);
    }
}
