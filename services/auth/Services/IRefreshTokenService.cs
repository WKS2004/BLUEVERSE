namespace Blueverse.Auth.Services;

public interface IRefreshTokenService
{
    string CreateOpaqueToken();
    string CreateDeviceId();
    string CreateDeviceKey();
    string Hash(string value);
}
