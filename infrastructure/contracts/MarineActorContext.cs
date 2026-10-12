using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace Blueverse.Contracts;

// Linked into the gateway and owning service; contains no domain behavior.
public static class MarineActorContext
{
    public const string Header = "X-Blueverse-Marine-Actor";
    public const string Issuer = "Blueverse.Api";
    public const string Audience = "Blueverse.MarineSafety";
    public const string KeySetting = "MARINE_ACTOR_SIGNING_KEY";

    public static byte[] ReadKey(string? value)
    {
        var key = Encoding.UTF8.GetBytes(value ?? "");
        if (key.Length < 32) throw new InvalidOperationException($"{KeySetting} must be at least 32 UTF-8 bytes.");
        return key;
    }

    public static async Task<string> BodyHashAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.EnableBuffering(bufferThreshold: 30 * 1024, bufferLimit: 1024 * 1024);
        var hash = await SHA256.HashDataAsync(request.Body, cancellationToken);
        request.Body.Position = 0;
        return Convert.ToHexString(hash);
    }

    public static string Sign(Guid actorId, IEnumerable<string> permissions, HttpRequest request, string bodyHash, byte[] key)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, actorId.ToString()),
            new("method", request.Method),
            new("target", request.PathBase + request.Path + request.QueryString),
            new("body_hash", bodyHash),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(permissions.Distinct(StringComparer.Ordinal).Select(p => new Claim("permission", p)));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            Issuer, Audience, claims, now, now.AddSeconds(30),
            new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)));
    }

    public static TokenValidationParameters Validation(byte[] key) => new()
    {
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true, ValidIssuer = Issuer, ValidateAudience = true, ValidAudience = Audience,
        ValidateLifetime = true, ClockSkew = TimeSpan.Zero, ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
    };

    public static async Task<bool> MatchesAsync(ClaimsPrincipal principal, HttpRequest request, CancellationToken cancellationToken) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"), out var id) && id != Guid.Empty &&
        principal.FindFirstValue("method") == request.Method &&
        principal.FindFirstValue("target") == request.PathBase + request.Path + request.QueryString &&
        principal.FindFirstValue("body_hash") == await BodyHashAsync(request, cancellationToken);

    public static string? ReadCallerToken(HttpRequest request)
    {
        if (request.Headers.ContainsKey("Authorization"))
        {
            var header = request.Headers.Authorization.ToString();
            return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
        }
        var legacy = request.Cookies["blueverse_access_token"];
        if (!request.Cookies.TryGetValue("blueverse_active_account_id", out var selection)) return legacy;
        if (!Guid.TryParse(selection, out var selected)) return null;
        var token = request.Cookies[$"blueverse_access_token_{selected:N}"];
        if (!string.IsNullOrWhiteSpace(token)) return token;
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(legacy ?? "");
            return Guid.TryParse(jwt.Subject, out var subject) && subject == selected ? legacy : null;
        }
        catch (ArgumentException) { return null; }
        catch (SecurityTokenException) { return null; }
    }
}
