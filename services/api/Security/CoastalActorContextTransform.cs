using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using Yarp.ReverseProxy.Transforms;

namespace Blueverse.Api.Security;

public static class CoastalActorContextTransform
{
    public const string ActorHeader = "X-Blueverse-Actor-Id";
    public const string PermissionsHeader = "X-Blueverse-Permissions";
    public const string CorrelationIdHeader = "X-Blueverse-Correlation-Id";
    public const string IssuedAtHeader = "X-Blueverse-Issued-At";
    public const string NonceHeader = "X-Blueverse-Nonce";
    public const string SignatureHeader = "X-Blueverse-Signature";

    public static byte[] ReadKey(string? configuredValue)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
            throw new InvalidOperationException("COASTAL_OPERATIONS_CONTEXT_KEY must be configured.");

        try
        {
            var key = Convert.FromBase64String(configuredValue);
            if (key.Length < 32)
                throw new InvalidOperationException("COASTAL_OPERATIONS_CONTEXT_KEY must contain at least 32 random bytes encoded as Base64.");
            return key;
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("COASTAL_OPERATIONS_CONTEXT_KEY must be valid Base64.", exception);
        }
    }

    public static void Apply(RequestTransformContext context, byte[] key)
    {
        var principal = context.HttpContext.User;

        foreach (var header in new[] { ActorHeader, PermissionsHeader, CorrelationIdHeader, IssuedAtHeader, NonceHeader, SignatureHeader })
            RequestTransform.RemoveHeader(context, header);
        RequestTransform.RemoveHeader(context, "Authorization");
        RequestTransform.RemoveHeader(context, "Cookie");

        if (principal.Identity?.IsAuthenticated != true) return;

        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                      principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(subject, out var actorId) || actorId == Guid.Empty) return;

        var permissions = principal.FindAll("permission")
            .Select(claim => claim.Value)
            .Where(value => value.StartsWith("operations.", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var permissionsBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(permissions)));
        var correlationId = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Guid.NewGuid().ToString("N");
        var pathAndQuery = context.Path.ToUriComponent() + context.HttpContext.Request.QueryString.ToUriComponent();
        var canonical = Canonical(context.HttpContext.Request.Method, pathAndQuery, actorId, permissionsBase64, correlationId, issuedAt, nonce);
        var signature = Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(canonical)));

        RequestTransform.AddHeader(context, ActorHeader, actorId.ToString("N"));
        RequestTransform.AddHeader(context, PermissionsHeader, permissionsBase64);
        RequestTransform.AddHeader(context, CorrelationIdHeader, correlationId);
        RequestTransform.AddHeader(context, IssuedAtHeader, issuedAt);
        RequestTransform.AddHeader(context, NonceHeader, nonce);
        RequestTransform.AddHeader(context, SignatureHeader, signature);
    }

    public static string Canonical(string method, string pathAndQuery, Guid actorId, string permissionsBase64, string correlationId, string issuedAt, string nonce) =>
        string.Join('\n', method.ToUpperInvariant(), pathAndQuery, actorId.ToString("N"), permissionsBase64, correlationId, issuedAt, nonce);
}
