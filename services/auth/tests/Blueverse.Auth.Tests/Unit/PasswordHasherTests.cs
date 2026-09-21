namespace Blueverse.Auth.Tests.Unit;

public sealed class PasswordHasherTests
{
    [Fact]
    [Trait("TestId", "AUTH-PASSWORD-001")]
    public void HashesAreSaltedAndWrongPasswordsDoNotVerify()
    {
        var hasher = new PasswordHasherService();
        var firstHash = hasher.HashPassword("CorrectPassword-123!");
        var secondHash = hasher.HashPassword("CorrectPassword-123!");

        Assert.NotEqual(firstHash, secondHash);
        Assert.True(hasher.VerifyPassword("CorrectPassword-123!", firstHash));
        Assert.False(hasher.VerifyPassword("WrongPassword-123!", firstHash));
    }
}
