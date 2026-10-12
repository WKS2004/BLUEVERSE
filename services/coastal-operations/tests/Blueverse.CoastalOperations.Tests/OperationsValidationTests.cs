using Blueverse.CoastalOperations.Application;
using Microsoft.AspNetCore.Http;

namespace Blueverse.CoastalOperations.Tests;

public sealed class OperationsValidationTests
{
    [Theory(DisplayName = "COASTAL-VALIDATION-001 accepted target types normalize to their canonical values")]
    [Trait("TestId", "COASTAL-VALIDATION-001")]
    [InlineData("destination", "DESTINATION")]
    [InlineData(" Activity ", "ACTIVITY")]
    [InlineData("OFFERING", "OFFERING")]
    [InlineData("session", "SESSION")]
    public void TargetTypeIsTrimmedAndNormalized(string input, string expected) =>
        Assert.Equal(expected, OperationsValidation.NormalizeTargetType(input));

    [Theory(DisplayName = "COASTAL-VALIDATION-002 unknown target types are rejected")]
    [Trait("TestId", "COASTAL-VALIDATION-002")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("BEACH")]
    [InlineData("ACTIVITIES")]
    public void UnknownTargetTypeIsRejected(string? input) =>
        AssertInvalid("target_type_invalid", () => OperationsValidation.NormalizeTargetType(input));

    [Theory(DisplayName = "COASTAL-VALIDATION-003 supported alert severities normalize")]
    [Trait("TestId", "COASTAL-VALIDATION-003")]
    [InlineData("low", "LOW")]
    [InlineData(" Moderate ", "MODERATE")]
    [InlineData("HIGH", "HIGH")]
    [InlineData("critical", "CRITICAL")]
    public void SeverityIsTrimmedAndNormalized(string input, string expected) =>
        Assert.Equal(expected, OperationsValidation.NormalizeSeverity(input));

    [Theory(DisplayName = "COASTAL-VALIDATION-004 invalid alert severities are rejected")]
    [Trait("TestId", "COASTAL-VALIDATION-004")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("INFO")]
    [InlineData("EXTREME")]
    public void InvalidSeverityIsRejected(string? input) =>
        AssertInvalid("alert_severity_invalid", () => OperationsValidation.NormalizeSeverity(input));

    [Theory(DisplayName = "COASTAL-VALIDATION-005 supported alert visibility values normalize")]
    [Trait("TestId", "COASTAL-VALIDATION-005")]
    [InlineData("public", "PUBLIC")]
    [InlineData(" OPERATIONS ", "OPERATIONS")]
    public void VisibilityIsTrimmedAndNormalized(string input, string expected) =>
        Assert.Equal(expected, OperationsValidation.NormalizeAlertVisibility(input));

    [Theory(DisplayName = "COASTAL-VALIDATION-006 unsupported visibility values are rejected")]
    [Trait("TestId", "COASTAL-VALIDATION-006")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("PRIVATE")]
    public void UnsupportedVisibilityIsRejected(string? input) =>
        AssertInvalid("alert_visibility_invalid", () => OperationsValidation.NormalizeAlertVisibility(input));

    [Fact(DisplayName = "COASTAL-VALIDATION-007 RFC3339 offsets are normalized to UTC")]
    [Trait("TestId", "COASTAL-VALIDATION-007")]
    public void ExplicitOffsetsAreConvertedToUtc()
    {
        var period = OperationsValidation.ParsePeriod("2026-10-01T13:30:00+05:30", "2026-10-01T10:00:00Z");

        Assert.Equal(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero), period.StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero), period.EndsAt);
        Assert.Equal(TimeSpan.Zero, period.StartsAt.Offset);
        Assert.Equal(TimeSpan.Zero, period.EndsAt.Offset);
    }

    [Theory(DisplayName = "COASTAL-VALIDATION-008 missing, malformed or offsetless periods are rejected")]
    [Trait("TestId", "COASTAL-VALIDATION-008")]
    [InlineData(null, "2026-10-01T10:00:00Z")]
    [InlineData("2026-10-01T08:00:00", "2026-10-01T10:00:00Z")]
    [InlineData("not-a-date", "2026-10-01T10:00:00Z")]
    [InlineData("2026-10-01T08:00:00Z", "2026-10-01T10:00:00")]
    public void InvalidOrOffsetlessPeriodIsRejected(string? start, string? end) =>
        AssertInvalid("period_offset_required", () => OperationsValidation.ParsePeriod(start, end));

    [Theory(DisplayName = "COASTAL-VALIDATION-009 end must follow start")]
    [Trait("TestId", "COASTAL-VALIDATION-009")]
    [InlineData("2026-10-01T08:00:00Z", "2026-10-01T08:00:00Z")]
    [InlineData("2026-10-01T09:00:00Z", "2026-10-01T08:00:00Z")]
    public void NonPositivePeriodIsRejected(string start, string end) =>
        AssertInvalid("period_invalid", () => OperationsValidation.ParsePeriod(start, end));

    [Theory(DisplayName = "COASTAL-VALIDATION-010 decisions normalize only among the caller supplied choices")]
    [Trait("TestId", "COASTAL-VALIDATION-010")]
    [InlineData(" approve ", "APPROVE")]
    [InlineData("reject", "REJECT")]
    public void DecisionNormalizesFromAllowedSet(string input, string expected) =>
        Assert.Equal(expected, OperationsValidation.NormalizeDecision(input, "APPROVE", "REJECT"));

    [Theory(DisplayName = "COASTAL-VALIDATION-011 unsupported and missing decisions fail closed")]
    [Trait("TestId", "COASTAL-VALIDATION-011")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("RESOLVE")]
    public void UnsupportedDecisionIsRejected(string? input) =>
        AssertInvalid("decision_invalid", () => OperationsValidation.NormalizeDecision(input, "APPROVE", "REJECT"));

    [Fact(DisplayName = "COASTAL-VALIDATION-012 idempotency key whitespace is trimmed and the 128 character limit is inclusive")]
    [Trait("TestId", "COASTAL-VALIDATION-012")]
    public void IdempotencyKeyBoundaryIsAccepted()
    {
        Assert.Equal("key-1", OperationsValidation.ValidateIdempotencyKey(" key-1 "));
        Assert.Equal(new string('a', 128), OperationsValidation.ValidateIdempotencyKey(new string('a', 128)));
    }

    [Theory(DisplayName = "COASTAL-VALIDATION-013 missing, oversized and unsafe idempotency keys are rejected")]
    [Trait("TestId", "COASTAL-VALIDATION-013")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("has spaces")]
    [InlineData("slash/key")]
    public void InvalidIdempotencyKeyIsRejected(string? input) =>
        AssertInvalid("idempotency_key_invalid", () => OperationsValidation.ValidateIdempotencyKey(input));

    [Fact(DisplayName = "COASTAL-VALIDATION-014 idempotency key over maximum length is rejected")]
    [Trait("TestId", "COASTAL-VALIDATION-014")]
    public void OverlongIdempotencyKeyIsRejected() =>
        AssertInvalid("idempotency_key_invalid", () => OperationsValidation.ValidateIdempotencyKey(new string('a', 129)));

    [Fact(DisplayName = "COASTAL-VALIDATION-015 cursors round-trip and empty cursor starts a new page")]
    [Trait("TestId", "COASTAL-VALIDATION-015")]
    public void CursorRoundTrips()
    {
        var expected = Guid.NewGuid();
        Assert.True(OperationsValidation.TryReadCursor(null, out var empty));
        Assert.Equal(Guid.Empty, empty);
        Assert.True(OperationsValidation.TryReadCursor(OperationsValidation.EncodeCursor(expected), out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory(DisplayName = "COASTAL-VALIDATION-016 malformed cursors are rejected")]
    [Trait("TestId", "COASTAL-VALIDATION-016")]
    [InlineData("%%%")]
    [InlineData("bm90LWEtZ3VpZA==")]
    public void MalformedCursorIsRejected(string cursor) =>
        Assert.False(OperationsValidation.TryReadCursor(cursor, out _));

    [Fact(DisplayName = "COASTAL-VALIDATION-021 absent or blank optional cursors mean the first page")]
    [Trait("TestId", "COASTAL-VALIDATION-021")]
    public void EmptyOptionalCursorMeansFirstPage()
    {
        Assert.True(OperationsValidation.TryReadCursor(null, out var absent));
        Assert.Equal(Guid.Empty, absent);
        Assert.True(OperationsValidation.TryReadCursor("  ", out var blank));
        Assert.Equal(Guid.Empty, blank);
    }

    [Theory(DisplayName = "COASTAL-VALIDATION-017 supported nonterminal operational transitions are allowed")]
    [Trait("TestId", "COASTAL-VALIDATION-017")]
    [InlineData("OPEN", "CAUTION")]
    [InlineData("OPEN", "TEMPORARILY_SUSPENDED")]
    [InlineData("CAUTION", "OPEN")]
    [InlineData("CAUTION", "TEMPORARILY_SUSPENDED")]
    [InlineData("TEMPORARILY_SUSPENDED", "OPEN")]
    [InlineData("TEMPORARILY_SUSPENDED", "CAUTION")]
    [InlineData("OPEN", "CANCELLED")]
    [InlineData("CAUTION", "COMPLETED")]
    public void AllowedTransitionsMatchTargetStateMachine(string current, string next)
    {
        var targetType = next is "CANCELLED" or "COMPLETED" ? "SESSION" : "ACTIVITY";
        Assert.True(OperationsValidation.IsAllowedTransition(targetType, current, next));
    }

    [Theory(DisplayName = "COASTAL-VALIDATION-018 terminal and unchanged states cannot transition")]
    [Trait("TestId", "COASTAL-VALIDATION-018")]
    [InlineData("ACTIVITY", "OPEN", "OPEN")]
    [InlineData("ACTIVITY", "OPEN", "CANCELLED")]
    [InlineData("SESSION", "CANCELLED", "OPEN")]
    [InlineData("SESSION", "COMPLETED", "CAUTION")]
    public void InvalidTransitionsAreRejected(string targetType, string current, string next) =>
        Assert.False(OperationsValidation.IsAllowedTransition(targetType, current, next));

    [Theory(DisplayName = "COASTAL-VALIDATION-019 high impact state classification is explicit")]
    [Trait("TestId", "COASTAL-VALIDATION-019")]
    [InlineData("ACTIVITY", "TEMPORARILY_SUSPENDED", true)]
    [InlineData("SESSION", "CANCELLED", true)]
    [InlineData("ACTIVITY", "CANCELLED", false)]
    [InlineData("SESSION", "CAUTION", false)]
    public void HighImpactStatesRequireReviewAsDefined(string targetType, string state, bool expected) =>
        Assert.Equal(expected, OperationsValidation.IsHighImpactState(targetType, state));

    [Fact(DisplayName = "COASTAL-VALIDATION-020 request digest is deterministic and changes with request data")]
    [Trait("TestId", "COASTAL-VALIDATION-020")]
    public void RequestDigestTracksSerializedRequestContent()
    {
        var request = new { TargetType = "ACTIVITY", Objective = "Review" };
        var sameRequest = new { TargetType = "ACTIVITY", Objective = "Review" };
        var changedRequest = new { TargetType = "ACTIVITY", Objective = "Publish" };

        Assert.Equal(OperationsValidation.RequestDigest(request), OperationsValidation.RequestDigest(sameRequest));
        Assert.NotEqual(OperationsValidation.RequestDigest(request), OperationsValidation.RequestDigest(changedRequest));
    }

    private static void AssertInvalid(string expectedCode, Action action)
    {
        var exception = Assert.Throws<CoastalOperationsException>(action);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
        Assert.Equal(expectedCode, exception.Code);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }
}
