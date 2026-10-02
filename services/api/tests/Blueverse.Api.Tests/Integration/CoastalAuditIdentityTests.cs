using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Blueverse.Api.Security;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Transforms;

namespace Blueverse.Api.Tests.Integration;

public sealed class CoastalAuditIdentityTests
{
    [Fact(DisplayName = "API-AUDIT-IDENTITY-001 verified identity replaces spoofed header and participates in the signature")]
    [Trait("TestId", "API-AUDIT-IDENTITY-001")]
    public void SignedIdentityUsesVerifiedClaims()
    {
        var actor = Guid.NewGuid(); var key = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", actor.ToString()), new("name", "Coastal Steward"), new("role", "Field Officer"), new(ClaimTypes.Role, "Field Officer"), new("permission", "operations.audit.read")], "verified")) };
        http.Request.Method = "GET"; http.Request.Path = "/api/operations/logs/assessments";
        var proxy = new HttpRequestMessage(); proxy.Headers.Add(CoastalActorContextTransform.IdentityHeader, "spoofed"); proxy.Headers.Add("Authorization", "Bearer must-not-forward");
        var transform = new RequestTransformContext { HttpContext = http, ProxyRequest = proxy, Path = http.Request.Path };
        CoastalActorContextTransform.Apply(transform, key);
        string Header(string name) => proxy.Headers.GetValues(name).Single();
        var identity = Header(CoastalActorContextTransform.IdentityHeader);
        using var snapshot = JsonDocument.Parse(Convert.FromBase64String(identity));
        Assert.Equal("Coastal Steward", snapshot.RootElement.GetProperty("name").GetString());
        Assert.Equal("Field Officer", Assert.Single(snapshot.RootElement.GetProperty("roles").EnumerateArray()).GetString());
        Assert.False(proxy.Headers.Contains("Authorization"));
        var canonical = CoastalActorContextTransform.Canonical("GET", http.Request.Path, actor, Header(CoastalActorContextTransform.PermissionsHeader), Header(CoastalActorContextTransform.CorrelationIdHeader), Header(CoastalActorContextTransform.IssuedAtHeader), Header(CoastalActorContextTransform.NonceHeader), identity);
        Assert.Equal(Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(canonical))), Header(CoastalActorContextTransform.SignatureHeader));
    }
}
