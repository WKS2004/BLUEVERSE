using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Blueverse.CoastalOperations.Security;

public sealed class CoastalActorContextAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    CoastalActorContextEnvelopeVerifier verifier,
    CoastalActorContextReplayGuard replayGuard)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "CoastalActorContext";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var actorHeader = Request.Headers[CoastalActorContextHeaders.ActorId].ToString();
        var permissionsHeader = Request.Headers[CoastalActorContextHeaders.Permissions].ToString();
        var correlationHeader = Request.Headers[CoastalActorContextHeaders.CorrelationId].ToString();
        var issuedAtHeader = Request.Headers[CoastalActorContextHeaders.IssuedAt].ToString();
        var nonceHeader = Request.Headers[CoastalActorContextHeaders.Nonce].ToString();
        var signatureHeader = Request.Headers[CoastalActorContextHeaders.Signature].ToString();

        if (string.IsNullOrEmpty(actorHeader) && string.IsNullOrEmpty(permissionsHeader) &&
            string.IsNullOrEmpty(correlationHeader) && string.IsNullOrEmpty(issuedAtHeader) && string.IsNullOrEmpty(nonceHeader) && string.IsNullOrEmpty(signatureHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!verifier.TryValidate(
                Request.Method,
                Request.Path.ToUriComponent() + Request.QueryString.ToUriComponent(),
                actorHeader,
                permissionsHeader,
                correlationHeader,
                issuedAtHeader,
                nonceHeader,
                signatureHeader,
                out var actorId,
                out var permissions,
                out var expiresAt))
        {
            return Task.FromResult(AuthenticateResult.Fail("The API actor context is invalid or expired."));
        }

        if (!replayGuard.TryUse(nonceHeader, expiresAt))
            return Task.FromResult(AuthenticateResult.Fail("The API actor context has already been used."));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, actorId.ToString()),
            new("sub", actorId.ToString()),
            new("correlation_id", correlationHeader)
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public static class CoastalActorContextHeaders
{
    public const string ActorId = "X-Blueverse-Actor-Id";
    public const string Permissions = "X-Blueverse-Permissions";
    public const string CorrelationId = "X-Blueverse-Correlation-Id";
    public const string IssuedAt = "X-Blueverse-Issued-At";
    public const string Nonce = "X-Blueverse-Nonce";
    public const string Signature = "X-Blueverse-Signature";
}

public sealed class CoastalActorContextEnvelopeVerifier
{
    private readonly byte[] _key;

    public CoastalActorContextEnvelopeVerifier(IConfiguration configuration)
    {
        var configuredValue = configuration["COASTAL_OPERATIONS_CONTEXT_KEY"];
        if (string.IsNullOrWhiteSpace(configuredValue))
            throw new InvalidOperationException("COASTAL_OPERATIONS_CONTEXT_KEY must be configured.");
        try
        {
            _key = Convert.FromBase64String(configuredValue);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("COASTAL_OPERATIONS_CONTEXT_KEY must be valid Base64.", exception);
        }

        if (_key.Length < 32)
            throw new InvalidOperationException("COASTAL_OPERATIONS_CONTEXT_KEY must contain at least 32 random bytes encoded as Base64.");
    }

    public bool TryValidate(
        string method,
        string pathAndQuery,
        string actorHeader,
        string permissionsHeader,
        string correlationHeader,
        string issuedAtHeader,
        string nonce,
        string signatureHeader,
        out Guid actorId,
        out IReadOnlyList<string> permissions,
        out DateTimeOffset expiresAt)
    {
        actorId = Guid.Empty;
        permissions = [];
        expiresAt = DateTimeOffset.MinValue;
        if (!Guid.TryParseExact(actorHeader, "N", out actorId) || actorId == Guid.Empty ||
            !long.TryParse(issuedAtHeader, NumberStyles.None, CultureInfo.InvariantCulture, out var issuedAtSeconds) ||
            !Guid.TryParseExact(nonce, "N", out _) || nonce.Length != 32)
            return false;
        if (string.IsNullOrWhiteSpace(correlationHeader) || correlationHeader.Length > 128 ||
            correlationHeader.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or ':' or '-')))
            return false;

        DateTimeOffset issuedAt;
        try
        {
            issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        if (issuedAt < now.AddSeconds(-60) || issuedAt > now.AddSeconds(5)) return false;
        expiresAt = issuedAt.AddMinutes(2);

        string[] decodedPermissions;
        try
        {
            decodedPermissions = JsonSerializer.Deserialize<string[]>(Convert.FromBase64String(permissionsHeader)) ?? [];
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return false;
        }

        if (decodedPermissions.Length > 128 ||
            decodedPermissions.Any(permission => string.IsNullOrWhiteSpace(permission) ||
                                                 permission.Length > 128 ||
                                                 !permission.StartsWith("operations.", StringComparison.Ordinal)) ||
            decodedPermissions.Distinct(StringComparer.Ordinal).Count() != decodedPermissions.Length ||
            !decodedPermissions.SequenceEqual(decodedPermissions.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            return false;

        byte[] providedSignature;
        try
        {
            providedSignature = Convert.FromBase64String(signatureHeader);
        }
        catch (FormatException)
        {
            return false;
        }

        var canonical = string.Join('\n', method.ToUpperInvariant(), pathAndQuery, actorId.ToString("N"), permissionsHeader, correlationHeader, issuedAtHeader, nonce);
        var expectedSignature = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(canonical));
        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, providedSignature)) return false;

        permissions = decodedPermissions;
        return true;
    }
}

public sealed class CoastalActorContextReplayGuard
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _usedNonces = new(StringComparer.Ordinal);

    public bool TryUse(string nonce, DateTimeOffset expiresAt)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (key, expiry) in _usedNonces)
        {
            if (expiry <= now) _usedNonces.TryRemove(key, out _);
        }

        return _usedNonces.TryAdd(nonce, expiresAt);
    }
}
