using Blueverse.Api.Integration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Yarp.ReverseProxy.Forwarder;

namespace Blueverse.Api.Tests.Integration;

public sealed class MarineAvailabilityTests
{
    private sealed class ProxyError(ForwarderError error = ForwarderError.Request) : IForwarderErrorFeature
    {
        public ForwarderError Error => error;
        public Exception Exception => new HttpRequestException("Private destination diagnostic must not escape.");
    }
    [Theory, Trait("CaseId", "API-MARINE-AVAIL-001")]
    [InlineData("GET", true)] [InlineData("POST", false)] [InlineData("PUT", false)] [InlineData("DELETE", false)]
    public async Task Missing_destination_reports_data_unavailable_and_ambiguous_writes_are_not_retryable(string method, bool retryable)
    {
        foreach (var error in new[] { ForwarderError.Request, ForwarderError.RequestTimedOut })
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Path = "/api/marine/safety-profiles";
            context.Response.Body = new MemoryStream();
            await new MarineProxyAvailabilityMiddleware(ctx =>
            {
                ctx.Response.StatusCode = 502;
                ctx.Features.Set<IForwarderErrorFeature>(new ProxyError(error));
                return Task.CompletedTask;
            }).InvokeAsync(context);
            Assert.Equal(503, context.Response.StatusCode);
            Assert.Equal("application/problem+json", context.Response.ContentType);
            Assert.Equal(retryable ? "5" : "", context.Response.Headers.RetryAfter.ToString());
            context.Response.Body.Position = 0;
            using var body = await JsonDocument.ParseAsync(context.Response.Body);
            Assert.Equal("UNAVAILABLE", body.RootElement.GetProperty("availability").GetString());
            Assert.Equal("marine-safety", body.RootElement.GetProperty("dependency").GetString());
            Assert.Equal("MARINE_SERVICE_UNAVAILABLE", body.RootElement.GetProperty("code").GetString());
            Assert.Equal(error == ForwarderError.RequestTimedOut ? "TIMEOUT" : "UNREACHABLE", body.RootElement.GetProperty("reason").GetString());
            Assert.Equal("marineSafetyData", Assert.Single(body.RootElement.GetProperty("missingFields").EnumerateArray()).GetString());
            Assert.Equal(503, body.RootElement.GetProperty("status").GetInt32());
            Assert.Equal(retryable, body.RootElement.GetProperty("retryable").GetBoolean());
            Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("data").ValueKind);
            Assert.DoesNotContain("Private destination", body.RootElement.GetRawText());
            if (!retryable) Assert.Contains("outcome could not be confirmed", body.RootElement.GetProperty("detail").GetString());
        }
    }

    [Theory, Trait("CaseId", "API-MARINE-AVAIL-002")]
    [InlineData("/api/auth/me", true)]
    [InlineData("/api/marine/activities", false)]
    public async Task Other_routes_and_valid_upstream_unavailable_bodies_are_preserved(string path, bool proxyFailure)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        await new MarineProxyAvailabilityMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 503;
            ctx.Response.Headers.RetryAfter = "17";
            if (proxyFailure) ctx.Features.Set<IForwarderErrorFeature>(new ProxyError());
            return Task.CompletedTask;
        }).InvokeAsync(context);
        Assert.Equal(503, context.Response.StatusCode);
        Assert.Equal("17", context.Response.Headers.RetryAfter.ToString());
        Assert.Equal(0, context.Response.Body.Length);
    }

    private sealed class UnavailableAuth : IAuthActorClient
    {
        public Task<AuthActor> GetActorAsync(string token, CancellationToken ct) => Task.FromException<AuthActor>(new ActorDependencyException());
    }
    [Fact, Trait("CaseId", "API-MARINE-AVAIL-003")]
    public async Task Missing_identity_data_remains_fail_closed_with_structured_availability()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/marine/current";
        context.Request.Headers.Authorization = "Bearer test-caller";
        context.Response.Body = new MemoryStream();
        var forwarded = false;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["MARINE_ACTOR_SIGNING_KEY"] = "availability-test-only-signing-key-at-least-32-bytes" }).Build();
        await new MarineGatewayMiddleware(_ => { forwarded = true; return Task.CompletedTask; }, config).InvokeAsync(context, new UnavailableAuth());
        Assert.False(forwarded);
        Assert.Equal(503, context.Response.StatusCode);
        Assert.Equal("5", context.Response.Headers.RetryAfter.ToString());
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("auth", body.RootElement.GetProperty("dependency").GetString());
        Assert.Equal("UNAVAILABLE", body.RootElement.GetProperty("availability").GetString());
        Assert.Equal("verifiedActor", body.RootElement.GetProperty("missingFields")[0].GetString());
        Assert.False(context.Request.Headers.ContainsKey("X-Blueverse-Marine-Actor"));
    }
}
