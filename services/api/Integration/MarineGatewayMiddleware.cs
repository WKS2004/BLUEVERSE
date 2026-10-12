using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Blueverse.Contracts;

namespace Blueverse.Api.Integration;

public sealed record AuthActor(Guid Id, bool IsActive, List<string>? Permissions);
public sealed class ActorAuthenticationException : Exception;
public sealed class ActorDependencyException : Exception;
public interface IAuthActorClient
{
    Task<AuthActor> GetActorAsync(string token, CancellationToken cancellationToken);
}

public sealed class AuthActorClient(HttpClient client) : IAuthActorClient
{
    public async Task<AuthActor> GetActorAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new ActorAuthenticationException();
            if (!response.IsSuccessStatusCode) throw new ActorDependencyException();
            var actor = await response.Content.ReadFromJsonAsync<AuthActor>(cancellationToken);
            if (actor is null || actor.Id == Guid.Empty || actor.Permissions is null || actor.Permissions.Any(string.IsNullOrWhiteSpace)) throw new ActorDependencyException();
            if (!actor.IsActive) throw new ActorAuthenticationException();
            return actor;
        }
        catch (HttpRequestException) { throw new ActorDependencyException(); }
        catch (JsonException) { throw new ActorDependencyException(); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new ActorDependencyException(); }
    }
}

// Only Marine Safety integration is affected. Auth retains token/session and permission ownership.
public sealed class MarineGatewayMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context, IAuthActorClient auth)
    {
        if (!context.Request.Path.StartsWithSegments("/api/marine")) { await next(context); return; }
        context.Request.Headers.Remove(MarineActorContext.Header);
        if (context.Request.Path == "/api/marine/health" || context.Request.Path.StartsWithSegments("/api/marine/swagger"))
        {
            await next(context); return;
        }
        byte[] key;
        try { key = MarineActorContext.ReadKey(configuration[MarineActorContext.KeySetting]); }
        catch (InvalidOperationException)
        {
            context.Response.Headers.RetryAfter = "5";
            await Error(context, 503, "Marine safety access is temporarily unavailable."); return;
        }
        var token = MarineActorContext.ReadCallerToken(context.Request);
        if (string.IsNullOrWhiteSpace(token)) { await Error(context, 401, "Sign in to use marine safety."); return; }
        AuthActor actor;
        try { actor = await auth.GetActorAsync(token, context.RequestAborted); }
        catch (ActorAuthenticationException) { await Error(context, 401, "Your account session is no longer active."); return; }
        catch (ActorDependencyException)
        {
            context.Response.Headers.RetryAfter = "5";
            await Error(context, 503, "Account verification is unavailable.", "auth", "ACCOUNT_VERIFICATION_UNAVAILABLE"); return;
        }
        if (!actor.IsActive || actor.Id == Guid.Empty || actor.Permissions is null)
        { await Error(context, 401, "Your account session is no longer active."); return; }
        var manage = context.Request.Path.StartsWithSegments("/api/marine/safety-profiles") && context.Request.Method is not ("GET" or "HEAD" or "OPTIONS");
        if (!actor.Permissions.Contains("marine.profile.read", StringComparer.Ordinal) ||
            (manage && !actor.Permissions.Contains("marine.profile.manage", StringComparer.Ordinal)))
        { await Error(context, 403, "You do not have permission for this marine safety action."); return; }
        string hash;
        try { hash = await MarineActorContext.BodyHashAsync(context.Request, context.RequestAborted); }
        catch (IOException) { await Error(context, 413, "The request is too large."); return; }
        context.Request.Headers[MarineActorContext.Header] = MarineActorContext.Sign(actor.Id, actor.Permissions, context.Request, hash, key);
        // The component receives only its signed actor context, never the caller's credentials.
        context.Request.Headers.Remove("Authorization");
        context.Request.Headers.Remove("Cookie");
        await next(context);
    }

    private static Task Error(HttpContext context, int status, string detail,
        string dependency = "marine-safety", string code = "MARINE_ACCESS_UNAVAILABLE")
    {
        context.Response.StatusCode = status;
        if (status == 503)
            return context.Response.WriteAsJsonAsync(new { status, title = "Marine Safety Access", detail,
                availability = "UNAVAILABLE", dependency, code, reason = "UNAVAILABLE", retryable = true,
                missingFields = new[] { dependency == "auth" ? "verifiedActor" : "verifiedAccess" }, data = (object?)null },
                options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
        return context.Response.WriteAsJsonAsync(new { status, title = "Marine Safety Access", detail },
            options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
