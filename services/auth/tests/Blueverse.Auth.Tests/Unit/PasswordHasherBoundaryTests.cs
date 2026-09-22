namespace Blueverse.Auth.Tests.Unit;

public sealed class PasswordHasherBoundaryTests
{
    [Fact]
    [Trait("TestId", "AUTH-PASSWORD-002")]
    public void EmptyPasswordsCannotBeHashedAndMalformedHashesNeverVerify()
    {
        var hasher = new PasswordHasherService();

        Assert.Throws<ArgumentException>(() => hasher.HashPassword(string.Empty));
        Assert.Throws<ArgumentException>(() => hasher.HashPassword("   "));
        Assert.False(hasher.VerifyPassword("Password-123!", string.Empty));
        Assert.False(hasher.VerifyPassword("Password-123!", "not-a-pbkdf2-hash"));
        Assert.False(hasher.VerifyPassword("Password-123!", "100000.invalid.invalid"));
        Assert.False(hasher.VerifyPassword("", "100000.invalid.invalid"));
    }
}
