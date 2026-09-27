using System.ComponentModel.DataAnnotations;
using Blueverse.CoastalOperations.Contracts;

namespace Blueverse.CoastalOperations.Tests;

public sealed class ContractDataAnnotationTests
{
    [Fact(DisplayName = "COASTAL-CONTRACT-001 assessment creation contract accepts its declared boundary lengths")]
    [Trait("TestId", "COASTAL-CONTRACT-001")]
    public void AssessmentRequestAcceptsMaximumDeclaredLengths()
    {
        var request = new CreateAssessmentRequest
        {
            TargetType = new string('A', 32),
            TargetId = Guid.NewGuid(),
            PeriodStartsAt = new string('s', 64),
            PeriodEndsAt = new string('e', 64),
            Objective = new string('o', 2000)
        };

        Assert.Empty(Validate(request));
    }

    [Theory(DisplayName = "COASTAL-CONTRACT-002 assessment creation rejects missing and oversized fields")]
    [Trait("TestId", "COASTAL-CONTRACT-002")]
    [InlineData("target-type", "TargetType", "")]
    [InlineData("period-start", "PeriodStartsAt", "")]
    [InlineData("period-end", "PeriodEndsAt", "")]
    [InlineData("objective", "Objective", "")]
    [InlineData("target-type-too-long", "TargetType", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("period-start-too-long", "PeriodStartsAt", "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")]
    [InlineData("period-end-too-long", "PeriodEndsAt", "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee")]
    [InlineData("objective-too-long", "Objective", "oversized")]
    public void AssessmentRequestRejectsRequiredAndMaximumViolations(string field, string expectedMember, string invalidValue)
    {
        var request = new CreateAssessmentRequest
        {
            TargetType = "ACTIVITY",
            TargetId = Guid.NewGuid(),
            PeriodStartsAt = "2026-10-01T08:00:00Z",
            PeriodEndsAt = "2026-10-01T10:00:00Z",
            Objective = "Assessment"
        };
        request = field switch
        {
            "target-type" or "target-type-too-long" => new CreateAssessmentRequest
            {
                TargetType = invalidValue, TargetId = request.TargetId, PeriodStartsAt = request.PeriodStartsAt,
                PeriodEndsAt = request.PeriodEndsAt, Objective = request.Objective
            },
            "period-start" or "period-start-too-long" => new CreateAssessmentRequest
            {
                TargetType = request.TargetType, TargetId = request.TargetId, PeriodStartsAt = invalidValue,
                PeriodEndsAt = request.PeriodEndsAt, Objective = request.Objective
            },
            "period-end" or "period-end-too-long" => new CreateAssessmentRequest
            {
                TargetType = request.TargetType, TargetId = request.TargetId, PeriodStartsAt = request.PeriodStartsAt,
                PeriodEndsAt = invalidValue, Objective = request.Objective
            },
            "objective" => new CreateAssessmentRequest
            {
                TargetType = request.TargetType, TargetId = request.TargetId, PeriodStartsAt = request.PeriodStartsAt,
                PeriodEndsAt = request.PeriodEndsAt, Objective = invalidValue
            },
            "objective-too-long" => new CreateAssessmentRequest
            {
                TargetType = request.TargetType, TargetId = request.TargetId, PeriodStartsAt = request.PeriodStartsAt,
                PeriodEndsAt = request.PeriodEndsAt, Objective = new string('o', 2001)
            },
            _ => request
        };

        var errors = Validate(request);
        Assert.Contains(errors, result => result.MemberNames.Contains(expectedMember));
    }

    [Theory(DisplayName = "COASTAL-CONTRACT-003 assessment queue enforces inclusive page-size boundaries")]
    [Trait("TestId", "COASTAL-CONTRACT-003")]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    public void AssessmentPageSizeRangeIsEnforced(int pageSize, bool expectedValid)
    {
        var results = Validate(new AssessmentListQuery { PageSize = pageSize });
        Assert.Equal(expectedValid, results.Count == 0);
    }

    [Fact(DisplayName = "COASTAL-CONTRACT-004 reviewer decisions require supported field ranges")]
    [Trait("TestId", "COASTAL-CONTRACT-004")]
    public void ReviewerDecisionContractRejectsEmptyIdsAndOutOfRangeVersions()
    {
        var invalid = new ReviewerDecisionRequest
        {
            ProposalId = Guid.Empty,
            ProposalVersion = 0,
            ExpectedTargetStateVersion = -1,
            Decision = string.Empty,
            Explanation = new string('x', 1001)
        };

        Assert.NotEmpty(Validate(invalid));
        var valid = new ReviewerDecisionRequest
        {
            ProposalId = Guid.NewGuid(), ProposalVersion = 1, ExpectedTargetStateVersion = 0,
            Decision = "APPROVE", Explanation = new string('x', 1000)
        };
        Assert.Empty(Validate(valid));
    }

    [Fact(DisplayName = "COASTAL-CONTRACT-005 alert request lengths and required fields are validated")]
    [Trait("TestId", "COASTAL-CONTRACT-005")]
    public void AlertContractsRejectMissingAndOversizedContent()
    {
        var invalid = new CreateAlertRequest
        {
            TargetType = "ACTIVITY", TargetId = Guid.NewGuid(), Title = new string('t', 161),
            Description = "Description", Severity = "LOW", Visibility = "OPERATIONS",
            ValidFrom = "2026-10-01T00:00:00Z", ValidUntil = "2026-10-02T00:00:00Z"
        };

        Assert.NotEmpty(Validate(invalid));
        Assert.NotEmpty(Validate(new CreateAlertRequest()));
        var valid = new UpdateAlertRequest
        {
            ExpectedVersion = 1, Title = new string('t', 160), Description = new string('d', 4000),
            Severity = "CRITICAL", Visibility = null,
            ValidFrom = new string('s', 64), ValidUntil = new string('e', 64)
        };
        Assert.Empty(Validate(valid));
    }

    [Fact(DisplayName = "COASTAL-CONTRACT-006 evidence upload requires an image form field")]
    [Trait("TestId", "COASTAL-CONTRACT-006")]
    public void EvidenceUploadRejectsMissingImage()
    {
        Assert.NotEmpty(Validate(new AssessmentEvidenceUploadRequest()));
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }
}
