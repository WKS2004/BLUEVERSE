using Blueverse.CoastalOperations.Domain;

namespace Blueverse.CoastalOperations.Application;

public static class AlertVisibilityPolicy
{
    public static IQueryable<OperationalAlert> Apply(
        IQueryable<OperationalAlert> alerts,
        bool canManage,
        DateTimeOffset now) => canManage
        ? alerts
        : alerts.Where(alert => alert.Visibility == "PUBLIC" &&
                                alert.Lifecycle == "ACTIVE" &&
                                alert.ValidFrom <= now &&
                                alert.ValidUntil > now);
}
