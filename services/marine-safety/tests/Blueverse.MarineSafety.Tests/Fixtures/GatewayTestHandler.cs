using Blueverse.Api.Integration;
using Blueverse.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Blueverse.MarineSafety.Tests.Fixtures;

internal sealed class GatewayTestHandler(MarineSafetyWebApplicationFactory owner) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var context = new DefaultHttpContext();
        context.RequestAborted = ct;
        context.Request.Method = request.Method.Method;
        context.Request.Path = request.RequestUri!.AbsolutePath;
        context.Request.QueryString = new QueryString(request.RequestUri.Query);
        foreach (var header in request.Headers) context.Request.Headers[header.Key] = header.Value.ToArray();
        var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(ct);
        context.Request.Body = new MemoryStream(body);
        context.Response.Body = new MemoryStream();
        HttpResponseMessage? forwarded = null;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { [MarineActorContext.KeySetting] = MarineSafetyWebApplicationFactory.JwtSigningKey }).Build();
        var middleware = new MarineGatewayMiddleware(async ctx =>
        {
            request.Headers.Remove(MarineActorContext.Header);
            if (ctx.Request.Headers.TryGetValue(MarineActorContext.Header, out var actor))
                request.Headers.TryAddWithoutValidation(MarineActorContext.Header, actor.ToString());
            request.Headers.Remove("Authorization");
            request.Headers.Remove("Cookie");
            forwarded = await base.SendAsync(request, ct);
        }, configuration);
        await middleware.InvokeAsync(context, new AuthDouble(owner));
        if (forwarded is not null) return forwarded;
        context.Response.Body.Position = 0;
        var response = new HttpResponseMessage((System.Net.HttpStatusCode)context.Response.StatusCode)
        { Content = new StringContent(await new StreamReader(context.Response.Body).ReadToEndAsync(ct), Encoding.UTF8, "application/problem+json") };
        foreach (var header in context.Response.Headers) response.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        return response;
    }

    private sealed class AuthDouble(MarineSafetyWebApplicationFactory owner) : IAuthActorClient
    {
        public async Task<AuthActor> GetActorAsync(string token, CancellationToken ct)
        {
            ClaimsPrincipal principal;
            try
            {
                var parameters = MarineActorContext.Validation(Encoding.UTF8.GetBytes(MarineSafetyWebApplicationFactory.JwtSigningKey));
                parameters.ValidIssuer = "Blueverse.Auth";
                parameters.ValidAudience = "Blueverse.Client";
                principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
            }
            catch (Exception ex) when (ex is ArgumentException or SecurityTokenException) { throw new ActorAuthenticationException(); }
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
                !int.TryParse(principal.FindFirstValue("token_version"), out var tokenVersion) ||
                !Guid.TryParse(principal.FindFirstValue("session_id"), out var sessionId) ||
                !int.TryParse(principal.FindFirstValue("session_version"), out var sessionVersion) ||
                !await owner.IdentityValidator.IsCurrentAsync(userId, tokenVersion, sessionId, sessionVersion, ct))
                throw new ActorAuthenticationException();
            var permissions = new List<string>();
            foreach (var code in new[] { "marine.profile.read", "marine.profile.manage" })
                if (await owner.Resolver.HasPermissionAsync(userId, code, ct)) permissions.Add(code);
            return new AuthActor(userId, true, permissions);
        }
    }
}
