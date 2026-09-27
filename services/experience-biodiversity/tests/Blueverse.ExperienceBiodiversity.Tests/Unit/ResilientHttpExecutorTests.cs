using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Blueverse.ExperienceBiodiversity.Services;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Unit;

public sealed class ResilientHttpExecutorTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public int CallCount { get; private set; }

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    [Trait("CaseId", "EXP-RESIL-001")]
    public async Task Null_Or_Empty_Url_Returns_Not_Configured()
    {
        using var client = new HttpClient();
        var executor = new ResilientHttpExecutor(client, NullLogger<ResilientHttpExecutor>.Instance);

        var result = await executor.ExecuteGetAsync(null, TimeSpan.FromSeconds(1));
        Assert.False(result.Responded);
        Assert.Equal("NOT_CONFIGURED", result.RemoteStatus);
        Assert.Equal(0, result.AttemptsCount);
        Assert.Contains("not configured", result.Message);
    }

    [Fact]
    [Trait("CaseId", "EXP-RESIL-002")]
    public async Task Successful_Response_Returns_Responded_True_And_Content()
    {
        var mockHandler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"ok\"}")
        });

        using var client = new HttpClient(mockHandler);
        var executor = new ResilientHttpExecutor(client, NullLogger<ResilientHttpExecutor>.Instance);

        var result = await executor.ExecuteGetAsync("http://remote-service/api/test", TimeSpan.FromSeconds(1), maxRetries: 2);
        Assert.True(result.Responded);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("RESPONDED", result.RemoteStatus);
        Assert.Equal(1, result.AttemptsCount);
        Assert.Equal(1, mockHandler.CallCount);
        Assert.Contains("status", result.Content);
    }

    [Fact]
    [Trait("CaseId", "EXP-RESIL-003")]
    public async Task Transient_500_Retries_Up_To_Max_Retries()
    {
        var mockHandler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Server Error")
        });

        using var client = new HttpClient(mockHandler);
        var executor = new ResilientHttpExecutor(client, NullLogger<ResilientHttpExecutor>.Instance);

        // maxRetries = 2 -> total attempts = 3
        var result = await executor.ExecuteGetAsync(
            "http://remote-service/api/error",
            TimeSpan.FromSeconds(1),
            maxRetries: 2,
            initialRetryDelay: TimeSpan.FromMilliseconds(10));

        Assert.True(result.Responded); // Received HTTP 500
        Assert.Equal(500, result.StatusCode);
        Assert.Equal(3, result.AttemptsCount);
        Assert.Equal(3, mockHandler.CallCount);
        Assert.Equal("HTTP_ERROR", result.RemoteStatus);
    }

    [Fact]
    [Trait("CaseId", "EXP-RESIL-004")]
    public async Task Client_Error_404_Does_Not_Retry()
    {
        var mockHandler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Not Found")
        });

        using var client = new HttpClient(mockHandler);
        var executor = new ResilientHttpExecutor(client, NullLogger<ResilientHttpExecutor>.Instance);

        var result = await executor.ExecuteGetAsync(
            "http://remote-service/api/notfound",
            TimeSpan.FromSeconds(1),
            maxRetries: 2);

        Assert.True(result.Responded);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(1, result.AttemptsCount); // Not retried
        Assert.Equal(1, mockHandler.CallCount);
    }
}
