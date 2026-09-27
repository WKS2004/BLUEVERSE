using System.ComponentModel.DataAnnotations;

namespace Blueverse.ExperienceBiodiversity.DTOs;

public sealed record AvailabilityEvaluationRequest(
    [Required] Guid OfferingId,
    [Required] DateTimeOffset StartsAt,
    [Required] DateTimeOffset EndsAt);

public sealed record OfferingSummaryDto(
    Guid OfferingId,
    string OfferingTitle,
    Guid DestinationId,
    string DestinationName,
    string DestinationStatus,
    Guid ActivityId,
    string ActivityName,
    string ActivityStatus,
    string OfferingStatus);

public sealed record OperationalRestrictionContextDto(
    bool HasRestriction,
    string? RestrictionType,
    string? Severity,
    string? Reason,
    DateTimeOffset? EffectiveUntil,
    string SourceStatus,
    bool Responded = true,
    string? TargetEndpoint = null,
    int AttemptsCount = 1,
    long LatencyMs = 0,
    string? RemoteStatus = "RESPONDED",
    string? Message = null);

public sealed record AvailabilityEvaluationResponse(
    Guid OfferingId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Status, // AVAILABLE, UNAVAILABLE, UNKNOWN
    IReadOnlyList<string> ReasonCodes,
    OfferingSummaryDto Offering,
    OperationalRestrictionContextDto? OperationalRestriction,
    DateTimeOffset EvaluatedAt);
