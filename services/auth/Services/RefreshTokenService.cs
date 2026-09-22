using System.Security.Cryptography;
using System.Text;

namespace Blueverse.Auth.Services;

public sealed class RefreshTokenService : IRefreshTokenService
{
    public string CreateOpaqueToken() => CreateBase64UrlSecret(32);

    public string CreateDeviceId() => Guid.NewGuid().ToString("N");

    public string CreateDeviceKey() => CreateBase64UrlSecret(32);

    public string Hash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static string CreateBase64UrlSecret(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
