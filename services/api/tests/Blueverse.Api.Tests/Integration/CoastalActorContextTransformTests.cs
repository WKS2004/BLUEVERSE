using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Blueverse.Api.Security;
using Blueverse.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Blueverse.Api.Tests.Integration;

public sealed class CoastalActorContextTransformTests
{
    private const string SigningKey = "api-test-only-signing-key-with-at-least-32-bytes";

    [Fact(DisplayName = "API-ACTOR-CONTEXT-001 Proxy signs actor and permission context without forwarding client credentials")]
    [Trait("TestId", "API-ACTOR-CONTEXT-001")]
    public async Task CoastalProxySignsAuthenticatedActorContextAndStripsClientCredentials()
    {
        await using var destination = await ActorContextDestination.StartAsync();
        var actorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var contextKey = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var encodedContextKey = Convert.ToBase64String(contextKey);
        var overrides = new Dictionary<string, string?>
        {
            ["COASTAL_OPERATIONS_CONTEXT_KEY"] = encodedContextKey,
            ["ReverseProxy:Clusters:coastal-operations:Destinations:coastal-operations:Address"] = destination.Address
        };

        using var factory = new ApiWebApplicationFactory(overrides);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/operations/assessments?cursor=next");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(actorId));
        request.Headers.TryAddWithoutValidation(CoastalActorContextTransform.ActorHeader, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        request.Headers.TryAddWithoutValidation(CoastalActorContextTransform.PermissionsHeader, "client-controlled");
        request.Headers.TryAddWithoutValidation("Cookie", "coastal_test_context=client-controlled");

        using var authenticationRequest = new HttpRequestMessage(HttpMethod.Get, "/api/test-only/auth");
        authenticationRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(actorId));
        using var authenticationResponse = await client.SendAsync(authenticationRequest);
        Assert.Equal(HttpStatusCode.OK, authenticationResponse.StatusCode);
        Assert.True((await authenticationResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("authenticated").GetBoolean());

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("/api/operations/assessments", root.GetProperty("path").GetString());
        Assert.Equal("?cursor=next", root.GetProperty("query").GetString());
        Assert.Equal(actorId.ToString("N"), root.GetProperty("actorId").GetString());
        Assert.False(root.GetProperty("authorizationPresent").GetBoolean());
        Assert.False(root.GetProperty("cookiePresent").GetBoolean());

        var permissionsBase64 = root.GetProperty("permissions").GetString()!;
        var permissionsJson = Encoding.UTF8.GetString(Convert.FromBase64String(permissionsBase64));
        var permissions = JsonSerializer.Deserialize<string[]>(permissionsJson)!;
        Assert.Equal(["operations.alert.read", "operations.assessment.read"], permissions);

        var correlationId = root.GetProperty("correlationId").GetString()!;
        var issuedAt = root.GetProperty("issuedAt").GetString()!;
        var nonce = root.GetProperty("nonce").GetString()!;
        var signature = Convert.FromBase64String(root.GetProperty("signature").GetString()!);
        var canonical = CoastalActorContextTransform.Canonical(
            "GET",
            "/api/operations/assessments?cursor=next",
            actorId,
            permissionsBase64,
            correlationId,
            issuedAt,
            nonce);
        var expectedSignature = HMACSHA256.HashData(contextKey, Encoding.UTF8.GetBytes(canonical));

        Assert.NotEmpty(correlationId);
        Assert.True(Guid.TryParseExact(nonce, "N", out _));
        Assert.True(long.TryParse(issuedAt, out var issuedAtSeconds));
        Assert.InRange(
            Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - issuedAtSeconds),
            0,
            60);
        Assert.True(CryptographicOperations.FixedTimeEquals(expectedSignature, signature));
    }

    private static string CreateToken(Guid actorId)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, actorId.ToString()),
                new Claim("permission", "operations.assessment.read"),
                new Claim("permission", "operations.alert.read"),
                new Claim("permission", "auth.permission.read")
            ]),
            Issuer = "Blueverse.Auth",
            Audience = "Blueverse.Client",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private sealed class ActorContextDestination : IAsyncDisposable
    {
        private readonly WebApplication _application;

        private ActorContextDestination(WebApplication application, string address)
        {
            _application = application;
            Address = address;
        }

        public string Address { get; }

        public static async Task<ActorContextDestination> StartAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(ActorContextDestination).Assembly.GetName().Name,
                EnvironmentName = "Testing"
            });
            builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");

            var application = builder.Build();
            application.Run(async context =>
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    path = context.Request.Path.Value,
                    query = context.Request.QueryString.Value,
                    actorId = context.Request.Headers[CoastalActorContextTransform.ActorHeader].ToString(),
                    permissions = context.Request.Headers[CoastalActorContextTransform.PermissionsHeader].ToString(),
                    correlationId = context.Request.Headers[CoastalActorContextTransform.CorrelationIdHeader].ToString(),
                    issuedAt = context.Request.Headers[CoastalActorContextTransform.IssuedAtHeader].ToString(),
                    nonce = context.Request.Headers[CoastalActorContextTransform.NonceHeader].ToString(),
                    signature = context.Request.Headers[CoastalActorContextTransform.SignatureHeader].ToString(),
                    authorizationPresent = context.Request.Headers.ContainsKey("Authorization"),
                    cookiePresent = context.Request.Headers.ContainsKey("Cookie")
                }, context.RequestAborted);
            });

            await application.StartAsync();
            var server = application.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.SingleOrDefault();
            if (string.IsNullOrWhiteSpace(address))
            {
                await application.DisposeAsync();
                throw new InvalidOperationException("The actor-context destination did not start.");
            }

            return new ActorContextDestination(application, address.TrimEnd('/') + "/");
        }

        public async ValueTask DisposeAsync()
        {
            await _application.StopAsync();
            await _application.DisposeAsync();
        }
    }
}
