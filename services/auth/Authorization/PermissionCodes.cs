namespace Blueverse.Auth.Authorization;

public static class PermissionCodes
{
    public const string UserRead = "auth.user.read";
    public const string UserManage = "auth.user.manage";
    public const string UserCreate = "auth.user.create";
    public const string UserUpdate = "auth.user.update";
    public const string UserDelete = "auth.user.delete";
    public const string RoleRead = "auth.role.read";
    public const string RoleManage = "auth.role.manage";
    public const string RoleCreate = "auth.role.create";
    public const string RoleUpdate = "auth.role.update";
    public const string RoleDelete = "auth.role.delete";
    public const string PermissionRead = "auth.permission.read";
    public const string SystemRoleManage = "auth.role.system.manage";

    public static IReadOnlyList<string> LegacyGrantAliases(string permissionCode) => permissionCode switch
    {
        UserCreate or UserUpdate or UserDelete => [UserManage],
        RoleCreate or RoleUpdate or RoleDelete => [RoleManage],
        _ => []
    };
}
