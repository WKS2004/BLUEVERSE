namespace Blueverse.Auth.Tests.Unit;

public sealed class JwtConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    [Trait("TestId", "AUTH-CONFIG-001")]
    public void MissingOrShortSigningKeysAreRejected(string? signingKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SIGNING_KEY"] = signingKey,
                ["Jwt:ExpiryHours"] = "1"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new JwtTokenService(configuration));
    }
}
