namespace Blueverse.Auth.Services;

public sealed class DeviceAccountLimitExceededException : InvalidOperationException
{
    public DeviceAccountLimitExceededException(int maximumAccounts)
        : base($"A device can have at most {maximumAccounts} active accounts.")
    {
    }
}
