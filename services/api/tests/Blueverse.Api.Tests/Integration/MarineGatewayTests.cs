using Blueverse.Api.Integration;
using Blueverse.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Blueverse.Api.Tests.Integration;

public sealed class MarineGatewayTests
{
    private const string Key = "gateway-context-test-only-key-at-least-32-bytes";
    private sealed class AuthDouble : IAuthActorClient
    {
        public AuthActor Actor { get; set; } = new(Guid.NewGuid(), true, ["marine.profile.read"]);
        public Exception? Failure { get; set; }
        public string? Token { get; private set; }
        public int Calls { get; private set; }
        public Task<AuthActor> GetActorAsync(string token, CancellationToken ct)
        {
            Token = token; Calls++;
            return Failure is null ? Task.FromResult(Actor) : Task.FromException<AuthActor>(Failure);
        }
    }
    private static async Task<(DefaultHttpContext Context, bool Forwarded)> Run(AuthDouble auth, string path = "/api/marine/evaluate", string method = "POST", string? authorization = "Bearer caller-token", string? cookies = null, string? signingKey = Key)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method; context.Request.Path = path;
        context.Request.QueryString = new QueryString("?source=test");
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"latitude\":6}"));
        context.Response.Body = new MemoryStream();
        context.Request.Headers[MarineActorContext.Header] = "untrusted-client-value";
        if (authorization is not null) context.Request.Headers.Authorization = authorization;
        if (cookies is not null) context.Request.Headers.Cookie = cookies;
        var forwarded = false;
        var middleware = new MarineGatewayMiddleware(_ => { forwarded = true; return Task.CompletedTask; },
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { [MarineActorContext.KeySetting] = signingKey }).Build());
        await middleware.InvokeAsync(context, auth);
        return (context,forwarded);
    }
    [Fact, Trait("CaseId", "API-MARINE-001")]
    public async Task Current_actor_permissions_and_operation_are_signed_and_credentials_are_stripped()
    {
        var auth = new AuthDouble(); var (ctx,forwarded) = await Run(auth);
        Assert.True(forwarded); Assert.Equal("caller-token", auth.Token); Assert.Equal(1, auth.Calls);
        Assert.False(ctx.Request.Headers.ContainsKey("Authorization")); Assert.False(ctx.Request.Headers.ContainsKey("Cookie"));
        var principal = new JwtSecurityTokenHandler().ValidateToken(ctx.Request.Headers[MarineActorContext.Header].ToString(), MarineActorContext.Validation(Encoding.UTF8.GetBytes(Key)), out var token);
        Assert.Equal(auth.Actor.Id.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.True(principal.HasClaim("permission","marine.profile.read"));
        Assert.False(principal.HasClaim("permission","marine.profile.manage"));
        Assert.True(await MarineActorContext.MatchesAsync(principal,ctx.Request,default));
        Assert.True(token.ValidTo <= DateTime.UtcNow.AddSeconds(30));
    }
    [Theory, Trait("CaseId", "API-MARINE-002")]
    [InlineData("POST")] [InlineData("PUT")] [InlineData("DELETE")]
    public async Task Read_permission_cannot_manage_profiles(string method)
    {
        var (ctx,forwarded) = await Run(new AuthDouble(), "/api/marine/safety-profiles",method);
        Assert.False(forwarded); Assert.Equal(403,ctx.Response.StatusCode);
        Assert.False(ctx.Request.Headers.ContainsKey(MarineActorContext.Header));
        ctx.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(ctx.Response.Body);
        Assert.Equal(403,body.RootElement.GetProperty("status").GetInt32());
    }
    [Fact, Trait("CaseId", "API-MARINE-003")]
    public async Task Manage_permission_without_read_permission_is_denied()
    {
        var auth = new AuthDouble { Actor = new(Guid.NewGuid(),true,["marine.profile.manage"]) };
        var (ctx,forwarded) = await Run(auth,"/api/marine/safety-profiles");
        Assert.False(forwarded); Assert.Equal(403,ctx.Response.StatusCode);
    }
    [Fact, Trait("CaseId", "API-MARINE-004")]
    public async Task Profile_mutation_requires_both_current_grants()
    {
        var auth = new AuthDouble { Actor = new(Guid.NewGuid(),true,["marine.profile.read","marine.profile.manage"]) };
        var (ctx,forwarded) = await Run(auth,"/api/marine/safety-profiles");
        Assert.True(forwarded);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(ctx.Request.Headers[MarineActorContext.Header].ToString());
        Assert.Contains(jwt.Claims,c => c.Type == "permission" && c.Value == "marine.profile.manage");
    }
    [Theory, Trait("CaseId", "API-MARINE-005")]
    [InlineData(null)] [InlineData("Basic something")] [InlineData("Bearer ")]
    public async Task Missing_or_wrong_credentials_cannot_be_replaced_by_spoofed_context(string? header)
    {
        var auth = new AuthDouble();var (ctx,forwarded) = await Run(auth,authorization:header);
        Assert.False(forwarded);Assert.Equal(401,ctx.Response.StatusCode);Assert.Equal(0,auth.Calls);
    }
    [Fact, Trait("CaseId", "API-MARINE-006")]
    public async Task Revoked_sessions_fail_closed_before_domain_forwarding()
    {
        var (ctx,forwarded) = await Run(new AuthDouble { Failure = new ActorAuthenticationException() });
        Assert.False(forwarded);Assert.Equal(401,ctx.Response.StatusCode);
        Assert.False(ctx.Request.Headers.ContainsKey(MarineActorContext.Header));
    }
    [Fact, Trait("CaseId", "API-MARINE-007")]
    public async Task Unavailable_auth_returns_retryable_503_without_signed_authority()
    {
        var (ctx,forwarded) = await Run(new AuthDouble { Failure = new ActorDependencyException() });
        Assert.False(forwarded);Assert.Equal(503,ctx.Response.StatusCode);Assert.Equal("5",ctx.Response.Headers.RetryAfter);
        Assert.False(ctx.Request.Headers.ContainsKey(MarineActorContext.Header));
    }
    [Fact, Trait("CaseId", "API-MARINE-008")]
    public async Task Malformed_selected_account_never_falls_back_to_legacy_cookie()
    {
        var auth = new AuthDouble();var (ctx,forwarded) = await Run(auth,authorization:null,cookies:"blueverse_active_account_id=invalid; blueverse_access_token=legacy");
        Assert.False(forwarded);Assert.Equal(401,ctx.Response.StatusCode);Assert.Equal(0,auth.Calls);
    }
    [Fact, Trait("CaseId", "API-MARINE-009")]
    public async Task Bearer_precedes_selected_account_cookie()
    {
        var id = Guid.NewGuid();var auth = new AuthDouble();var (_,forwarded) = await Run(auth,cookies:$"blueverse_active_account_id={id}; blueverse_access_token_{id:N}=cookie-token");
        Assert.True(forwarded);Assert.Equal("caller-token",auth.Token);
    }
    [Fact, Trait("CaseId", "API-MARINE-010")]
    public async Task Health_is_anonymous_and_spoofed_context_is_removed()
    {
        var auth = new AuthDouble();var (ctx,forwarded) = await Run(auth,"/api/marine/health","GET",null);
        Assert.True(forwarded);Assert.Equal(0,auth.Calls);Assert.False(ctx.Request.Headers.ContainsKey(MarineActorContext.Header));
    }
    [Fact, Trait("CaseId", "API-MARINE-011")]
    public async Task Missing_actor_key_disables_only_marine_protected_operations()
    {
        var auth = new AuthDouble();
        var (marine, marineForwarded) = await Run(auth, signingKey: null);
        Assert.False(marineForwarded); Assert.Equal(503, marine.Response.StatusCode); Assert.Equal(0, auth.Calls);
        var (_, authForwarded) = await Run(auth, "/api/auth/me", "GET", signingKey: null);
        Assert.True(authForwarded); Assert.Equal(0, auth.Calls);
        var (_, healthForwarded) = await Run(auth, "/api/marine/health", "GET", signingKey: null);
        Assert.True(healthForwarded); Assert.Equal(0, auth.Calls);
    }
}
