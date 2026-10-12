using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using Blueverse.CoastalOperations.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Blueverse.CoastalOperations.Tests;

public sealed class CoastalActorContextAuthenticationTests
{
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
    private static string KeyBase64 => Convert.ToBase64String(Key);

    [Fact(DisplayName = "COASTAL-AUTH-CONTEXT-001 valid signed actor context authenticates the actor and sorted permissions")]
    [Trait("TestId", "COASTAL-AUTH-CONTEXT-001")]
    public async Task ValidEnvelopeAuthenticatesWithClaims()
    {
        var actor = Guid.NewGuid();
        var permissions = new[] { "operations.alerts.read", "operations.assessments.read" };
        var headers = SignedHeaders("GET", "/api/operations/alerts?cursor=first", actor, permissions);
        var context = Context("GET", "/api/operations/alerts", "?cursor=first", headers);
        var handler = CreateHandler(new CoastalActorContextReplayGuard());
        await handler.InitializeAsync(Scheme(), context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(actor.ToString(), result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(actor.ToString(), result.Principal.FindFirstValue("sub"));
        Assert.Equal("coastal-correlation", result.Principal.FindFirstValue("correlation_id"));
        Assert.Equal(permissions, result.Principal.FindAll("permission").Select(claim => claim.Value).ToArray());
    }

    [Fact(DisplayName = "COASTAL-AUTH-CONTEXT-002 no actor context produces no authentication result")]
    [Trait("TestId", "COASTAL-AUTH-CONTEXT-002")]
    public async Task MissingEnvelopeReturnsNoResult()
    {
        var handler = CreateHandler(new CoastalActorContextReplayGuard());
        await handler.InitializeAsync(Scheme(), Context("GET", "/api/operations/alerts", string.Empty, new Dictionary<string, string>()));

        Assert.True((await handler.AuthenticateAsync()).None);
    }

    [Theory(DisplayName = "COASTAL-AUTH-CONTEXT-003 signature binds actor context to method and path")]
    [Trait("TestId", "COASTAL-AUTH-CONTEXT-003")]
    [InlineData("POST", "/api/operations/alerts")]
    [InlineData("GET", "/api/operations/targets")]
    [InlineData("GET", "/api/operations/alerts?cursor=changed")]
    public void SignatureTamperingIsRejected(string method, string pathAndQuery)
    {
        var actor = Guid.NewGuid();
        var signed = SignedHeaders("GET", "/api/operations/alerts?cursor=first", actor, ["operations.alerts.read"]);
        var valid = Verifier().TryValidate(
            method,
            pathAndQuery,
            signed[CoastalActorContextHeaders.ActorId],
            signed[CoastalActorContextHeaders.Permissions],
            signed[CoastalActorContextHeaders.CorrelationId],
            signed[CoastalActorContextHeaders.IssuedAt],
            signed[CoastalActorContextHeaders.Nonce],
            signed[CoastalActorContextHeaders.Signature],
            out _, out _, out _);

        Assert.False(valid);
    }

    [Theory(DisplayName = "COASTAL-AUTH-CONTEXT-004 malformed and unsafe signed envelopes are rejected")]
    [Trait("TestId", "COASTAL-AUTH-CONTEXT-004")]
    [InlineData("actor")]
    [InlineData("empty-actor")]
    [InlineData("nonce")]
    [InlineData("correlation")]
    [InlineData("stale-time")]
    [InlineData("future-time")]
    [InlineData("permissions-base64")]
    [InlineData("permissions-json")]
    [InlineData("permission-prefix")]
    [InlineData("permissions-unsorted")]
    [InlineData("permissions-duplicate")]
    [InlineData("permission-too-long")]
    [InlineData("too-many-permissions")]
    [InlineData("signature-base64")]
    public void InvalidEnvelopeFieldsAreRejected(string scenario)
    {
        var headers = SignedHeaders("GET", "/api/operations/alerts", Guid.NewGuid(), ["operations.alerts.read"]);
        switch (scenario)
        {
            case "actor": headers[CoastalActorContextHeaders.ActorId] = "not-a-guid"; break;
            case "empty-actor": headers[CoastalActorContextHeaders.ActorId] = Guid.Empty.ToString("N"); break;
            case "nonce": headers[CoastalActorContextHeaders.Nonce] = "not-a-guid"; break;
            case "correlation": headers[CoastalActorContextHeaders.CorrelationId] = "unsafe value"; break;
            case "stale-time": headers[CoastalActorContextHeaders.IssuedAt] = DateTimeOffset.UtcNow.AddSeconds(-61).ToUnixTimeSeconds().ToString(); break;
            case "future-time": headers[CoastalActorContextHeaders.IssuedAt] = DateTimeOffset.UtcNow.AddSeconds(6).ToUnixTimeSeconds().ToString(); break;
            case "permissions-base64": headers[CoastalActorContextHeaders.Permissions] = "%%%"; break;
            case "permissions-json": headers[CoastalActorContextHeaders.Permissions] = Convert.ToBase64String(Encoding.UTF8.GetBytes("{}")); break;
            case "permission-prefix": ReplacePermissions(headers, ["users.read"]); break;
            case "permissions-unsorted": ReplacePermissions(headers, ["operations.targets.read", "operations.alerts.read"]); break;
            case "permissions-duplicate": ReplacePermissions(headers, ["operations.alerts.read", "operations.alerts.read"]); break;
            case "permission-too-long": ReplacePermissions(headers, ["operations." + new string('x', 128)]); break;
            case "too-many-permissions": ReplacePermissions(headers, Enumerable.Range(0, 129).Select(index => $"operations.permission{index:D3}").ToArray()); break;
            case "signature-base64": headers[CoastalActorContextHeaders.Signature] = "%%%"; break;
        }

        var valid = Verifier().TryValidate(
            "GET", "/api/operations/alerts", headers[CoastalActorContextHeaders.ActorId],
            headers[CoastalActorContextHeaders.Permissions], headers[CoastalActorContextHeaders.CorrelationId],
            headers[CoastalActorContextHeaders.IssuedAt], headers[CoastalActorContextHeaders.Nonce],
            headers[CoastalActorContextHeaders.Signature], out _, out _, out _);

        Assert.False(valid);
    }

    [Fact(DisplayName = "COASTAL-AUTH-CONTEXT-005 verifier rejects missing invalid and short signing keys")]
    [Trait("TestId", "COASTAL-AUTH-CONTEXT-005")]
    public void SigningKeyConfigurationIsValidated()
    {
        Assert.Throws<InvalidOperationException>(() => new CoastalActorContextEnvelopeVerifier(Configuration(null)));
        Assert.Throws<InvalidOperationException>(() => new CoastalActorContextEnvelopeVerifier(Configuration("not-base64")));
        Assert.Throws<InvalidOperationException>(() => new CoastalActorContextEnvelopeVerifier(Configuration(Convert.ToBase64String(new byte[31]))));
    }

    [Fact(DisplayName = "COASTAL-AUTH-CONTEXT-006 request nonce is accepted only once")]
    [Trait("TestId", "COASTAL-AUTH-CONTEXT-006")]
    public async Task HandlerRejectsReplayedEnvelope()
    {
        var replay = new CoastalActorContextReplayGuard();
        var headers = SignedHeaders("GET", "/api/operations/alerts", Guid.NewGuid(), ["operations.alerts.read"]);
        var first = CreateHandler(replay);
        await first.InitializeAsync(Scheme(), Context("GET", "/api/operations/alerts", string.Empty, headers));
        var accepted = await first.AuthenticateAsync();
        var second = CreateHandler(replay);
        await second.InitializeAsync(Scheme(), Context("GET", "/api/operations/alerts", string.Empty, headers));
        var rejected = await second.AuthenticateAsync();

        Assert.True(accepted.Succeeded);
        Assert.True(rejected.Failure is not null);
        Assert.Contains("already been used", rejected.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "COASTAL-AUTH-CONTEXT-007 concurrent replay checks allow exactly one request")]
    [Trait("TestId", "COASTAL-AUTH-CONTEXT-007")]
    public void ConcurrentNonceUseHasOneWinner()
    {
        var guard = new CoastalActorContextReplayGuard();
        var nonce = Guid.NewGuid().ToString("N");
        var expiry = DateTimeOffset.UtcNow.AddMinutes(1);
        var accepted = 0;

        Parallel.For(0, 64, _ =>
        {
            if (guard.TryUse(nonce, expiry)) Interlocked.Increment(ref accepted);
        });

        Assert.Equal(1, accepted);
        Assert.False(guard.TryUse(nonce, expiry));
    }

    [Fact(DisplayName = "COASTAL-AUTH-CONTEXT-008 expired nonce entries are pruned before a later request")]
    [Trait("TestId", "COASTAL-AUTH-CONTEXT-008")]
    public void ExpiredNonceCanBeReusedAfterExpiryPruning()
    {
        var guard = new CoastalActorContextReplayGuard();
        var nonce = Guid.NewGuid().ToString("N");

        Assert.True(guard.TryUse(nonce, DateTimeOffset.UtcNow.AddSeconds(-1)));
        Assert.True(guard.TryUse(nonce, DateTimeOffset.UtcNow.AddMinutes(1)));
    }

    [Fact(DisplayName = "COASTAL-AUTH-CONTEXT-009 signed display identity creates name/role snapshots without changing permissions")]
    [Trait("TestId", "COASTAL-AUTH-CONTEXT-009")]
    public async Task SignedDisplayIdentityBecomesVerifiedClaims()
    {
        var actor = Guid.NewGuid();
        var headers = SignedHeaders("GET", "/api/operations/alerts", actor, ["operations.alert.read"]);
        var snapshot = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new { name = "Coastal Steward", roles = new[] { "Field Officer" } }));
        headers[CoastalActorContextHeaders.Identity] = snapshot;
        var canonical = string.Join('\n', "GET", "/api/operations/alerts", headers[CoastalActorContextHeaders.ActorId], headers[CoastalActorContextHeaders.Permissions], headers[CoastalActorContextHeaders.CorrelationId], headers[CoastalActorContextHeaders.IssuedAt], headers[CoastalActorContextHeaders.Nonce], snapshot);
        headers[CoastalActorContextHeaders.Signature] = Convert.ToBase64String(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes(canonical)));
        var handler = CreateHandler(new CoastalActorContextReplayGuard());
        await handler.InitializeAsync(Scheme(), Context("GET", "/api/operations/alerts", string.Empty, headers));
        var result = await handler.AuthenticateAsync();
        Assert.True(result.Succeeded); Assert.Equal("Coastal Steward", result.Principal!.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("Field Officer", Assert.Single(result.Principal.FindAll(ClaimTypes.Role)).Value);
        Assert.Equal("operations.alert.read", Assert.Single(result.Principal.FindAll("permission")).Value);
    }

    private static CoastalActorContextEnvelopeVerifier Verifier() => new(Configuration(KeyBase64));

    private static IConfiguration Configuration(string? key) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["COASTAL_OPERATIONS_CONTEXT_KEY"] = key })
        .Build();

    private static Dictionary<string, string> SignedHeaders(string method, string pathAndQuery, Guid actor, string[] permissions)
    {
        var permissionsHeader = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(permissions));
        var correlation = "coastal-correlation";
        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var nonce = Guid.NewGuid().ToString("N");
        var actorHeader = actor.ToString("N");
        var canonical = string.Join('\n', method.ToUpperInvariant(), pathAndQuery, actorHeader, permissionsHeader, correlation, issuedAt, nonce);
        var signature = Convert.ToBase64String(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes(canonical)));
        return new Dictionary<string, string>
        {
            [CoastalActorContextHeaders.ActorId] = actorHeader,
            [CoastalActorContextHeaders.Permissions] = permissionsHeader,
            [CoastalActorContextHeaders.CorrelationId] = correlation,
            [CoastalActorContextHeaders.IssuedAt] = issuedAt,
            [CoastalActorContextHeaders.Nonce] = nonce,
            [CoastalActorContextHeaders.Signature] = signature
        };
    }

    private static void ReplacePermissions(IDictionary<string, string> headers, string[] permissions)
    {
        headers[CoastalActorContextHeaders.Permissions] = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(permissions));
        // These negative cases are rejected by permission validation before signature verification.
    }

    private static DefaultHttpContext Context(string method, string path, string query, IReadOnlyDictionary<string, string> headers)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        foreach (var (name, value) in headers) context.Request.Headers[name] = value;
        return context;
    }

    private static AuthenticationScheme Scheme() => new(
        CoastalActorContextAuthenticationHandler.SchemeName,
        CoastalActorContextAuthenticationHandler.SchemeName,
        typeof(CoastalActorContextAuthenticationHandler));

    private static CoastalActorContextAuthenticationHandler CreateHandler(CoastalActorContextReplayGuard replayGuard) => new(
        new FixedOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions()),
        NullLoggerFactory.Instance,
        UrlEncoder.Default,
        Verifier(),
        replayGuard);

    private sealed class FixedOptionsMonitor<TOptions>(TOptions currentValue) : IOptionsMonitor<TOptions> where TOptions : class
    {
        public TOptions CurrentValue => currentValue;
        public TOptions Get(string? name) => currentValue;
        public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
    }
}
