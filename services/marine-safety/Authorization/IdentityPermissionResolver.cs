using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Blueverse.MarineSafety.Data;

namespace Blueverse.MarineSafety.Authorization;

/// <summary>
/// Resolves whether a user currently holds a permission code. Production
/// reads the shared identity schema owned by the Auth service; tests provide
/// deterministic doubles.
/// </summary>
public interface IPermissionResolver
{
    Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken);
}

/// <summary>
/// Production resolver. Executes one parameterized query against the
/// Auth-owned identity tables (users → user_roles → role_permissions →
/// permissions) on this service's relational connection to the shared
/// database. The query is read-only; identity data is never written or cached
/// here, so a role change takes effect on the next request exactly as the
/// Auth service's own permission handler guarantees.
/// </summary>
public sealed class IdentityPermissionResolver : IPermissionResolver
{
    private readonly MarineSafetyDbContext _dbContext;

    public IdentityPermissionResolver(MarineSafetyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT 1
            FROM "Users" u
            JOIN "UserRoles" ur ON ur."UserId" = u."Id"
            JOIN "RolePermissions" rp ON rp."RoleId" = ur."RoleId"
            JOIN "Permissions" p ON p."Id" = rp."PermissionId"
            WHERE u."Id" = @userId
              AND u."IsActive" = TRUE
              AND p."Code" = @permission
            LIMIT 1
            """;

        var userIdParameter = command.CreateParameter();
        userIdParameter.ParameterName = "@userId";
        userIdParameter.Value = userId;
        command.Parameters.Add(userIdParameter);

        var permissionParameter = command.CreateParameter();
        permissionParameter.ParameterName = "@permission";
        permissionParameter.Value = permissionCode;
        command.Parameters.Add(permissionParameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null && result != DBNull.Value;
    }
}
