using System.Text.Json;
using System.Security.Cryptography;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Blueverse.CoastalOperations.Application;

public sealed record StoredOutcome<T>(int StatusCode, T Body, string SerializedBody, bool Replayed);

public sealed class IdempotencyStore(CoastalOperationsDbContext db)
{
    public async Task<StoredOutcome<T>?> TryReplayAsync<T>(
        Guid actorId,
        string operation,
        string key,
        string digest,
        CancellationToken cancellationToken)
    {
        var record = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
            x => x.ActorId == actorId && x.Operation == operation && x.Key == key,
            cancellationToken);
        return record is null ? null : Replay<T>(record, digest);
    }

    public async Task<StoredOutcome<T>> SaveAsync<T>(
        Guid actorId,
        string operation,
        string key,
        string digest,
        int statusCode,
        T body,
        CancellationToken cancellationToken)
    {
        var serialized = JsonSerializer.Serialize(body, OperationsValidation.JsonOptions);
        if (serialized.Length > 65536)
        {
            throw new InvalidOperationException("The idempotent response exceeded its configured storage limit.");
        }

        db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Id = Guid.CreateVersion7(),
            ActorId = actorId,
            Operation = operation,
            Key = key,
            RequestDigest = digest,
            ResponseStatusCode = statusCode,
            ResponseBody = serialized,
            CreatedAt = DateTimeOffset.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new StoredOutcome<T>(statusCode, body, serialized, Replayed: false);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            var existing = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
                x => x.ActorId == actorId && x.Operation == operation && x.Key == key,
                cancellationToken);
            if (existing is not null) return Replay<T>(existing, digest);
            throw new CoastalOperationsException(
                StatusCodes.Status409Conflict,
                "concurrent_update",
                "The resource changed while the request was being applied",
                "Reload the current state and retry with a new Idempotency-Key.");
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
            var existing = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
                x => x.ActorId == actorId && x.Operation == operation && x.Key == key,
                cancellationToken);
            if (existing is null) throw;
            return Replay<T>(existing, digest);
        }
    }

    private static StoredOutcome<T> Replay<T>(IdempotencyRecord record, string digest)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(record.RequestDigest),
                System.Text.Encoding.ASCII.GetBytes(digest)))
        {
            throw new CoastalOperationsException(
                StatusCodes.Status409Conflict,
                "idempotency_key_reused",
                "Idempotency key conflicts with an earlier request",
                "Use a new Idempotency-Key when the request content changes.");
        }

        var body = JsonSerializer.Deserialize<T>(record.ResponseBody, OperationsValidation.JsonOptions)
            ?? throw new InvalidOperationException("The stored idempotent response could not be read.");
        return new StoredOutcome<T>(record.ResponseStatusCode, body, record.ResponseBody, Replayed: true);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
