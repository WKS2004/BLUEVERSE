using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
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
/// Auth-owned identity tables (users, active sessions, user_roles,
/// role_permissions and permissions) on this service's relational connection
/// to the shared database. The query is read-only; identity data is never
/// written or cached here, so account, token/session and role changes take
/// effect on the next protected request.
/// </summary>
public sealed class IdentityPermissionResolver : IPermissionResolver
{
    private readonly MarineSafetyDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IdentityPermissionResolver(
        MarineSafetyDbContext dbContext,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal?.FindFirstValue("sub");
        if (principal?.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(subject, out var principalUserId) ||
            principalUserId != userId ||
            !int.TryParse(principal.FindFirstValue("token_version"), out var tokenVersion) ||
            !Guid.TryParse(principal.FindFirstValue("session_id"), out var sessionId) ||
            !int.TryParse(principal.FindFirstValue("session_version"), out var sessionVersion))
        {
            return false;
        }

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT 1
            FROM "Users" u
            JOIN "ActiveSessions" s ON s."UserId" = u."Id"
            JOIN "UserRoles" ur ON ur."UserId" = u."Id"
            JOIN "RolePermissions" rp ON rp."RoleId" = ur."RoleId"
            JOIN "Permissions" p ON p."Id" = rp."PermissionId"
            WHERE u."Id" = @userId
              AND u."IsActive" = TRUE
              AND u."TokenVersion" = @tokenVersion
              AND s."Id" = @sessionId
              AND s."SessionVersion" = @sessionVersion
              AND s."ExpiresAt" > @now
              AND p."Code" = @permission
            LIMIT 1
            """;

        AddParameter(command, "@userId", userId);
        AddParameter(command, "@tokenVersion", tokenVersion);
        AddParameter(command, "@sessionId", sessionId);
        AddParameter(command, "@sessionVersion", sessionVersion);
        AddParameter(command, "@now", DateTime.UtcNow);
        AddParameter(command, "@permission", permissionCode);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null && result != DBNull.Value;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
