using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Application;

public sealed class TargetStatusApplicationService(CoastalOperationsDbContext db)
{
    public async Task<OperationalStatusResponse> GetStatusAsync(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var type = OperationsValidation.NormalizeTargetType(targetType);
        if (targetId == Guid.Empty) throw Invalid("target_identity_invalid", "Target identity is invalid", "Provide a non-empty target ID.");
        var state = await db.TargetOperationalStates.AsNoTracking().SingleOrDefaultAsync(
            x => x.TargetType == type && x.TargetId == targetId,
            cancellationToken);
        if (state is null)
            throw new CoastalOperationsException(StatusCodes.Status404NotFound, "target_state_not_found", "Operational state not found", "No authoritative state has been established for this target.");
        return new OperationalStatusResponse(state.TargetType, state.TargetId, state.State, state.Version, state.UpdatedAt);
    }

    public async Task<OperationalHistoryResponse> GetHistoryAsync(
        string targetType,
        Guid targetId,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var type = OperationsValidation.NormalizeTargetType(targetType);
        if (targetId == Guid.Empty) throw Invalid("target_identity_invalid", "Target identity is invalid", "Provide a non-empty target ID.");
        if (pageSize is < 1 or > 100) throw Invalid("page_size_invalid", "Page size is invalid", "Use a page size from 1 to 100.");
        if (!OperationsValidation.TryReadCursor(cursor, out var cursorId))
            throw Invalid("cursor_invalid", "The history cursor is invalid", "Use the cursor returned by the previous page.");

        var query = db.OperationalHistory.AsNoTracking().Where(x => x.TargetType == type && x.TargetId == targetId);
        if (cursorId != Guid.Empty) query = query.Where(x => x.Id.CompareTo(cursorId) < 0);
        var page = await query.OrderByDescending(x => x.Id).Take(pageSize + 1).ToListAsync(cancellationToken);
        var hasMore = page.Count > pageSize;
        if (hasMore) page.RemoveAt(page.Count - 1);
        if (page.Count == 0 && cursorId == Guid.Empty)
        {
            var exists = await db.TargetOperationalStates.AsNoTracking().AnyAsync(
                x => x.TargetType == type && x.TargetId == targetId,
                cancellationToken);
            if (!exists)
                throw new CoastalOperationsException(StatusCodes.Status404NotFound, "target_history_not_found", "Operational history not found", "No authoritative state has been established for this target.");
        }

        var items = page.Select(x => new OperationalHistoryItem(x.Id, x.PreviousState, x.NewState, x.AssessmentId, x.DecisionId, x.ActorId, x.CreatedAt)).ToArray();
        return new OperationalHistoryResponse(type, targetId, items, hasMore && page.Count > 0 ? OperationsValidation.EncodeCursor(page[^1].Id) : null);
    }

    private static CoastalOperationsException Invalid(string code, string title, string detail) =>
        new(StatusCodes.Status422UnprocessableEntity, code, title, detail);
}
