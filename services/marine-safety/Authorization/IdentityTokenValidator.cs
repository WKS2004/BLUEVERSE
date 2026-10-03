using Blueverse.MarineSafety.Data;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.MarineSafety.Authorization;

/// <summary>Checks the token's account and active Auth session against current state.</summary>
public interface IIdentityTokenValidator
{
    Task<bool> IsCurrentAsync(
        Guid userId,
        int tokenVersion,
        Guid sessionId,
        int sessionVersion,
        CancellationToken cancellationToken);
}

/// <summary>
/// Read-only validation of Auth-owned user and active-session state. Auth
/// remains the sole writer and owner of these records.
/// </summary>
public sealed class IdentityTokenValidator : IIdentityTokenValidator
{
    private readonly MarineSafetyDbContext _dbContext;

    public IdentityTokenValidator(MarineSafetyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> IsCurrentAsync(
        Guid userId,
        int tokenVersion,
        Guid sessionId,
        int sessionVersion,
        CancellationToken cancellationToken)
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
            JOIN "ActiveSessions" s ON s."UserId" = u."Id"
            WHERE u."Id" = @userId
              AND u."IsActive" = TRUE
              AND u."TokenVersion" = @tokenVersion
              AND s."Id" = @sessionId
              AND s."SessionVersion" = @sessionVersion
              AND s."ExpiresAt" > @now
            LIMIT 1
            """;

        AddParameter(command, "@userId", userId);
        AddParameter(command, "@tokenVersion", tokenVersion);
        AddParameter(command, "@sessionId", sessionId);
        AddParameter(command, "@sessionVersion", sessionVersion);
        AddParameter(command, "@now", DateTime.UtcNow);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null && result != DBNull.Value;
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
