namespace Blueverse.Auth.Services;

public sealed class AuthSessionOptions
{
    public const string SectionName = "AuthSession";

    public int DefaultLifetimeDays { get; set; } = 1;
    public int RememberMeLifetimeDays { get; set; } = 30;

    public TimeSpan GetLifetime(bool rememberMe)
    {
        var days = rememberMe ? RememberMeLifetimeDays : DefaultLifetimeDays;
        if (days is < 1 or > 30)
        {
            throw new InvalidOperationException(
                "AuthSession lifetime settings must be between 1 and 30 days.");
        }

        return TimeSpan.FromDays(days);
    }
}
