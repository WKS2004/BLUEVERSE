using Blueverse.Api.Integration;
using System.Net;
using System.Text;

namespace Blueverse.Api.Tests.Integration;

public sealed class AuthActorClientTests
{
    private sealed class Transport(string body, HttpStatusCode status = HttpStatusCode.OK, Exception? failure = null) : HttpMessageHandler
    {
        public Uri? Target { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Target = request.RequestUri;
            Assert.Equal(HttpMethod.Get,request.Method);
            Assert.Equal("Bearer",request.Headers.Authorization!.Scheme);
            Assert.Equal("caller-token",request.Headers.Authorization.Parameter);
            return failure is null ? Task.FromResult(new HttpResponseMessage(status)
            { Content = new StringContent(body,Encoding.UTF8,"application/json") }) : Task.FromException<HttpResponseMessage>(failure);
        }
    }
    [Fact, Trait("CaseId", "API-ACTOR-001")]
    public async Task Current_auth_contract_is_read_from_existing_me_endpoint()
    {
        var id = Guid.NewGuid();
        var transport = new Transport($$"""{"id":"{{id}}","isActive":true,"permissions":["marine.profile.read"],"roles":["Custom role"]}""");
        var actor = await new AuthActorClient(new HttpClient(transport) { BaseAddress = new Uri("http://auth.test/") }).GetActorAsync("caller-token",default);
        Assert.Equal(id,actor.Id);Assert.True(actor.IsActive);Assert.Equal("marine.profile.read",Assert.Single(actor.Permissions!));
        Assert.Equal("/api/auth/me",transport.Target!.AbsolutePath);
    }
    [Theory, Trait("CaseId", "API-ACTOR-002")]
    [InlineData("null")] [InlineData("[]")] [InlineData("{}")] [InlineData("not-json")]
    [InlineData("{\"id\":\"11111111-1111-1111-1111-111111111111\",\"isActive\":true}")]
    [InlineData("{\"id\":\"11111111-1111-1111-1111-111111111111\",\"isActive\":true,\"permissions\":[\"\"]}")]
    public async Task Malformed_auth_response_cannot_grant_authority(string body)
    {
        var adapter = new AuthActorClient(new HttpClient(new Transport(body)) { BaseAddress = new Uri("http://auth.test/") });
        await Assert.ThrowsAsync<ActorDependencyException>(() => adapter.GetActorAsync("caller-token",default));
    }
    [Theory, Trait("CaseId", "API-ACTOR-003")]
    [InlineData(401)] [InlineData(403)]
    public async Task Rejected_session_is_an_authentication_failure(int status)
    {
        var adapter = new AuthActorClient(new HttpClient(new Transport("{}",(HttpStatusCode)status)) { BaseAddress = new Uri("http://auth.test/") });
        await Assert.ThrowsAsync<ActorAuthenticationException>(() => adapter.GetActorAsync("caller-token",default));
    }
    [Fact, Trait("CaseId", "API-ACTOR-004")]
    public async Task Dependency_timeout_is_unavailable()
    {
        var adapter = new AuthActorClient(new HttpClient(new Transport("",failure:new TaskCanceledException())) { BaseAddress = new Uri("http://auth.test/") });
        await Assert.ThrowsAsync<ActorDependencyException>(() => adapter.GetActorAsync("caller-token",default));
    }
    [Fact, Trait("CaseId", "API-ACTOR-005")]
    public async Task Inactive_account_cannot_receive_actor_context()
    {
        var adapter = new AuthActorClient(new HttpClient(new Transport("""{"id":"11111111-1111-1111-1111-111111111111","isActive":false,"permissions":["marine.profile.read"]}""")) { BaseAddress = new Uri("http://auth.test/") });
        await Assert.ThrowsAsync<ActorAuthenticationException>(() => adapter.GetActorAsync("caller-token",default));
    }
}
