using Blueverse.Auth.Tests.Fixtures;

namespace Blueverse.Auth.Tests.Integration;

public sealed class AuthErrorBoundaryTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly AuthWebApplicationFactory _factory;

    public AuthErrorBoundaryTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("TestId", "AUTH-ERROR-001")]
    public async Task UnexpectedAuthExceptionsReturnSafeProblemDetails()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/test-only/auth-errors");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("https://tools.ietf.org/html/rfc7807", root.GetProperty("type").GetString());
        Assert.Equal("An internal error occurred", root.GetProperty("title").GetString());
        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("An unexpected error occurred processing your request.", root.GetProperty("detail").GetString());
        Assert.DoesNotContain("synthetic auth exception", body, StringComparison.OrdinalIgnoreCase);
    }
}
