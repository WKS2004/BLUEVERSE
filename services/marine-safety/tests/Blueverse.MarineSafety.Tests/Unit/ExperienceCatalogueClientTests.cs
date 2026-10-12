using Blueverse.MarineSafety.Services;
using System.Text;

namespace Blueverse.MarineSafety.Tests.Unit;

public sealed class ExperienceCatalogueClientTests
{
    private sealed class Transport(string payload, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? Requested { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requested = request.RequestUri;
            Assert.Null(request.Headers.Authorization);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(payload, Encoding.UTF8, "application/json") });
        }
    }
    [Fact, Trait("CaseId", "M2-CATALOG-001")]
    public async Task Published_contract_preserves_id_category_and_utc_dates()
    {
        var id = Guid.NewGuid();
        var transport = new Transport($$"""[{"id":"{{id}}","code":"surf-01","name":"Surfing","category":"{{new string('a',100)}}","status":"PUBLISHED","createdAt":"2026-09-01T10:00:00+05:30","updatedAt":"2026-09-02T10:00:00+05:30"}]""");
        var adapter = new ExperienceCatalogueClient(new HttpClient(transport) { BaseAddress = new Uri("http://catalogue.test/") });
        var row = Assert.Single(await adapter.GetPublishedAsync(default));
        Assert.Equal(id, row.Id); Assert.Equal("Surfing", row.Name); Assert.Equal(100, row.ActivityType.Length);
        Assert.True(row.IsActive); Assert.Equal(new DateTime(2026,9,1,4,30,0,DateTimeKind.Utc), row.CreatedAt);
        Assert.Equal("/api/experiences/activities", transport.Requested!.AbsolutePath);
    }
    [Theory, Trait("CaseId", "M2-CATALOG-002")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("not-json")]
    [InlineData("[{\"id\":\"00000000-0000-0000-0000-000000000000\"}]")]
    [InlineData("[{\"id\":\"11111111-1111-1111-1111-111111111111\",\"code\":\"surf\",\"name\":\"Surfing\",\"status\":\"DRAFT\",\"createdAt\":\"2026-09-01T00:00:00Z\",\"updatedAt\":\"2026-09-02T00:00:00Z\"}]")]
    public async Task Invalid_catalogue_is_unavailable(string payload)
    {
        var adapter = new ExperienceCatalogueClient(new HttpClient(new Transport(payload)) { BaseAddress = new Uri("http://catalogue.test/") });
        await Assert.ThrowsAsync<CatalogueUnavailableException>(() => adapter.GetPublishedAsync(default));
    }
    [Fact, Trait("CaseId", "M2-CATALOG-003")]
    public async Task Empty_published_catalogue_is_valid()
    {
        var adapter = new ExperienceCatalogueClient(new HttpClient(new Transport("[]")) { BaseAddress = new Uri("http://catalogue.test/") });
        Assert.Empty(await adapter.GetPublishedAsync(default));
    }
    [Theory, Trait("CaseId", "M2-CATALOG-004")]
    [InlineData(404)] [InlineData(503)] [InlineData(401)]
    public async Task Upstream_failure_has_no_cached_success(int status)
    {
        var adapter = new ExperienceCatalogueClient(new HttpClient(new Transport("[]", (HttpStatusCode)status)) { BaseAddress = new Uri("http://catalogue.test/") });
        var error = await Assert.ThrowsAsync<CatalogueUnavailableException>(() => adapter.GetPublishedAsync(default));
        Assert.Equal("UPSTREAM_UNAVAILABLE", error.Reason);
    }
    [Fact, Trait("CaseId", "M2-CATALOG-005")]
    public async Task Duplicate_canonical_ids_are_invalid()
    {
        var row = """{"id":"11111111-1111-1111-1111-111111111111","code":"surf","name":"Surfing","status":"PUBLISHED","createdAt":"2026-09-01T00:00:00Z","updatedAt":"2026-09-02T00:00:00Z"}""";
        var adapter = new ExperienceCatalogueClient(new HttpClient(new Transport($"[{row},{row}]")) { BaseAddress = new Uri("http://catalogue.test/") });
        await Assert.ThrowsAsync<CatalogueUnavailableException>(() => adapter.GetPublishedAsync(default));
    }
}
