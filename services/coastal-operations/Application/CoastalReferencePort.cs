using Blueverse.CoastalOperations.Contracts;

namespace Blueverse.CoastalOperations.Application;

// The owning catalogue/planner contracts are not implemented on this branch.
// Future adapters must enforce actor-scoped, bounded, validated named choices.
public interface ICoastalReferencePort
{
    Task<CoastalReferenceOptions> GetTargetsAsync(Guid actorId, CancellationToken cancellationToken);
    Task<CoastalReferenceOptions> GetPlansAsync(Guid actorId, CancellationToken cancellationToken);
}

public sealed class DisconnectedCoastalReferencePort : ICoastalReferencePort
{
    public Task<CoastalReferenceOptions> GetTargetsAsync(Guid actorId, CancellationToken cancellationToken) =>
        Task.FromResult(new CoastalReferenceOptions("NOT_CONNECTED", []));
    public Task<CoastalReferenceOptions> GetPlansAsync(Guid actorId, CancellationToken cancellationToken) =>
        Task.FromResult(new CoastalReferenceOptions("NOT_CONNECTED", []));
}
