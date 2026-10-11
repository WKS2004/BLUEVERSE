using Blueverse.Contracts;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace Blueverse.MarineSafety.Tests.Integration;

public sealed class GatewayContextTests : IClassFixture<MarineSafetyWebApplicationFactory>
{
    private readonly MarineSafetyWebApplicationFactory _factory;
    public GatewayContextTests(MarineSafetyWebApplicationFactory factory) => _factory = factory;
    private async Task<HttpClient> PrivateClient()
    {
        _factory.ResetDatabase(); _factory.Provider = new StubOpenMeteoClient();
        var client = _factory.CreateDefaultClient();
        await _factory.SeedAsync(MarineSafetyTestSeed.SeedConfigurationAsync);
        return client;
    }
    private static async Task<HttpRequestMessage> ContextRequest(string target, string signedTarget, string method = "GET", string signedMethod = "GET", string body = "", string signedBody = "", string? key = null, string[]? permissions = null)
    {
        var uri = new Uri("http://localhost" + signedTarget);
        var context = new DefaultHttpContext(); context.Request.Method = signedMethod;
        context.Request.Path = uri.AbsolutePath; context.Request.QueryString = new QueryString(uri.Query);
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(signedBody));
        var token = MarineActorContext.Sign(MarineSafetyTestSeed.ReaderUserId, permissions ?? ["marine.profile.read"], context.Request,
            await MarineActorContext.BodyHashAsync(context.Request, default), Encoding.UTF8.GetBytes(key ?? MarineSafetyWebApplicationFactory.JwtSigningKey));
        var request = new HttpRequestMessage(new HttpMethod(method), target);
        request.Headers.Add(MarineActorContext.Header, token);
        if (body != "") request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }
    [Fact, Trait("CaseId", "M2-CONTEXT-001")]
    public async Task Private_service_does_not_accept_client_bearer_tokens()
    {
        using var client = await PrivateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId, ["marine.profile.read"]));
        using var response = await client.GetAsync("/api/marine/safety-profiles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await _factory.SeedAsync(async db => Assert.Empty(await db.SuitabilityAssessments.ToListAsync()));
    }
    [Fact, Trait("CaseId", "M2-CONTEXT-002")]
    public async Task Matching_gateway_context_authorizes_read()
    {
        using var client = await PrivateClient();
        using var request = await ContextRequest("/api/marine/safety-profiles", "/api/marine/safety-profiles");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty((await response.Content.ReadFromJsonAsync<List<SafetyProfileDto>>())!);
    }
    [Theory, Trait("CaseId", "M2-CONTEXT-003")]
    [InlineData("/api/marine/current?latitude=6&longitude=80", "/api/marine/current?latitude=7&longitude=80", "GET", "GET", "", "")]
    [InlineData("/api/marine/safety-profiles", "/api/marine/assessments", "GET", "GET", "", "")]
    [InlineData("/api/marine/safety-profiles", "/api/marine/safety-profiles", "POST", "GET", "{}", "")]
    [InlineData("/api/marine/evaluate", "/api/marine/evaluate", "POST", "POST", "{}", "{\"activityId\":\"changed\"}")]
    public async Task Tampered_operation_context_cannot_reach_domain_actions(string target, string signedTarget, string method, string signedMethod, string body, string signedBody)
    {
        using var client = await PrivateClient();
        using var request = await ContextRequest(target,signedTarget,method,signedMethod,body,signedBody);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, _factory.Provider.CallCount);
    }
    [Fact, Trait("CaseId", "M2-CONTEXT-004")]
    public async Task Foreign_signature_and_missing_permission_are_rejected()
    {
        using var client = await PrivateClient();
        using var forged = await ContextRequest("/api/marine/safety-profiles", "/api/marine/safety-profiles", key:"foreign-key-with-more-than-32-characters");
        using var forgedResponse = await client.SendAsync(forged);
        Assert.Equal(HttpStatusCode.Unauthorized, forgedResponse.StatusCode);
        using var denied = await ContextRequest("/api/marine/safety-profiles", "/api/marine/safety-profiles", permissions:[]);
        using var deniedResponse = await client.SendAsync(denied);
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
    }
}
