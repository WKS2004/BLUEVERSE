using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Blueverse.CoastalPlanner.Models.Dtos;

namespace Blueverse.CoastalPlanner.Services;

/// <summary>
/// Projects the persisted coordinator state into the small public summary used by the planner UI.
/// Raw specialist reports, source identifiers, provider payloads, and credentials are never copied.
/// </summary>
internal static partial class AgentExecutionSummaryProjector
{
    private const int MaximumStateLength = 256_000;
    private static readonly HashSet<string> AgentIds = new(StringComparer.Ordinal)
    {
        "experience_biodiversity",
        "marine_conditions",
        "safety_operations"
    };
    private static readonly HashSet<string> ExecutionStatuses = new(StringComparer.Ordinal)
    {
        "PLANNING", "DELEGATING", "BLOCKED", "COMPLETED", "UNAVAILABLE", "SAFE_FAILURE"
    };
    private static readonly HashSet<string> StepStatuses = new(StringComparer.Ordinal)
    {
        "RUNNING", "SUCCEEDED", "BLOCKED", "INSUFFICIENT_DATA", "UNAVAILABLE", "INVALID_RESULT"
    };
    private static readonly HashSet<string> SafetyRecommendations = new(StringComparer.Ordinal)
    {
        "CONTINUE", "CONTINUE_WITH_CAUTION", "PROPOSE_ACTION", "INSUFFICIENT_DATA"
    };
    private static readonly HashSet<string> SafetyReviewSuggestions = new(StringComparer.Ordinal)
    {
        "NONE", "REVIEW_RECOMMENDED"
    };

    [GeneratedRegex("https?://|www\\.|\\b(?:api[_ -]?key|access[_ -]?token|password|secret|credential|authorization)\\b\\s*[:=]|\\bbearer\\s+[A-Za-z0-9._~+/-]+=*|AIza[0-9A-Za-z_-]{20,}|\\b[A-Za-z0-9_-]{16,}\\.[A-Za-z0-9_-]{16,}\\.[A-Za-z0-9_-]{16,}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeTextPattern();

    [GeneratedRegex("^[A-Z][A-Z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeCodePattern();

    public static PlannerAgentExecutionSummaryDto? Project(Guid workflowId, string persistedStatus, string? stateJson)
    {
        if (workflowId == Guid.Empty || string.IsNullOrWhiteSpace(stateJson) || stateJson.Length > MaximumStateLength ||
            !ExecutionStatuses.Contains(persistedStatus))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(stateJson, new JsonDocumentOptions { MaxDepth = 32 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryGet(root, "contractVersion", out var contractVersion) ||
                contractVersion.ValueKind != JsonValueKind.Number || !contractVersion.TryGetInt32(out var version) || version != 1 ||
                !TryGet(root, "workflowId", out var stateWorkflowId) ||
                stateWorkflowId.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(stateWorkflowId.GetString(), out var parsedWorkflowId) || parsedWorkflowId != workflowId ||
                !TryGet(root, "status", out var stateStatus) ||
                stateStatus.ValueKind != JsonValueKind.String || stateStatus.GetString() != persistedStatus ||
                !TryGet(root, "steps", out var rawSteps) || rawSteps.ValueKind != JsonValueKind.Array || rawSteps.GetArrayLength() > 12)
            {
                return null;
            }

            var steps = new List<PlannerAgentStepDto>();
            foreach (var rawStep in rawSteps.EnumerateArray())
            {
                if (rawStep.ValueKind != JsonValueKind.Object ||
                    !ReadRequiredString(rawStep, "agentId", 100, out var agentId) || !AgentIds.Contains(agentId) ||
                    !ReadRequiredString(rawStep, "status", 100, out var stepStatus) || !StepStatuses.Contains(stepStatus) ||
                    !TryGet(rawStep, "sourceReferences", out var rawReferences) ||
                    !TryReadStringList(rawReferences, maximumCount: 32, maximumItemLength: 256, out var references) ||
                    stepStatus == "SUCCEEDED" && references.Count == 0 ||
                    !TryGet(rawStep, "uncertaintyNotes", out var rawUncertainties) ||
                    !TryReadStringList(rawUncertainties, maximumCount: 32, maximumItemLength: 500, out var uncertaintyTexts))
                {
                    return null;
                }

                var summary = ReadOptionalSafeText(rawStep, "summary", 1_000);
                var safeUncertainties = uncertaintyTexts
                    .Select(text => SafeText(text, 500))
                    .Where(text => text is not null)
                    .Select(text => text!)
                    .ToList();
                var errorCode = ReadOptionalCode(rawStep, "errorCode");
                var safeReferences = Enumerable.Range(1, references.Count)
                    .Select(index => $"evidence-{index}")
                    .ToList();

                steps.Add(new PlannerAgentStepDto(
                    AgentId: agentId,
                    Status: stepStatus,
                    Summary: summary,
                    SourceCount: references.Count,
                    Uncertainties: safeUncertainties,
                    SourceReferences: safeReferences,
                    UncertaintyNotes: safeUncertainties,
                    ErrorCode: errorCode));
            }

            var objective = ReadOptionalSafeText(TryGet(root, "result", out var result) ? result : default,
                "objectiveSummary", 2_000) ??
                ReadOptionalSafeText(TryGet(root, "plan", out var plan) ? plan : default,
                    "objectiveSummary", 2_000) ?? string.Empty;
            var safetyProposal = TryGet(root, "result", out result) && result.ValueKind == JsonValueKind.Object &&
                TryGet(result, "safetyProposal", out var rawProposal)
                    ? ReadSafetyProposal(rawProposal, steps, root, workflowId)
                    : null;

            return new PlannerAgentExecutionSummaryDto(
                Status: persistedStatus,
                ObjectiveSummary: objective,
                Steps: steps,
                SafetyProposal: safetyProposal);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static PlannerSafetyProposalDto? ReadSafetyProposal(
        JsonElement envelope,
        IReadOnlyCollection<PlannerAgentStepDto> steps,
        JsonElement state,
        Guid expectedWorkflowId)
    {
        if (envelope.ValueKind != JsonValueKind.Object ||
            !TryGet(envelope, "workflowId", out var proposalWorkflowValue) ||
            proposalWorkflowValue.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(proposalWorkflowValue.GetString(), out var proposalWorkflowId) || proposalWorkflowId != expectedWorkflowId ||
            !ReadRequiredString(envelope, "stepId", 128, out var proposalStepId) ||
            !ReadRequiredString(envelope, "agentId", 100, out var proposalAgentId) || proposalAgentId != "safety_operations" ||
            !ReadRequiredString(envelope, "reportType", 100, out var reportType) || reportType != "SafetyOperationsRecommendation.v1" ||
            !ReadRequiredString(envelope, "status", 100, out var envelopeStatus) || envelopeStatus != "SUCCEEDED" ||
            !TryGet(envelope, "report", out var raw) || raw.ValueKind != JsonValueKind.Object ||
            !ReadRequiredString(raw, "schemaVersion", 100, out var schemaVersion) || schemaVersion != "safety-operations.recommendation.v1" ||
            !TryGet(raw, "workflowId", out var reportWorkflowValue) ||
            reportWorkflowValue.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(reportWorkflowValue.GetString(), out var reportWorkflowId) || reportWorkflowId != expectedWorkflowId ||
            !ReadRequiredString(raw, "stepId", 128, out var reportStepId) || reportStepId != proposalStepId ||
            !TryGet(raw, "assessmentId", out var assessmentIdValue) ||
            !TryGet(raw, "targetId", out var targetIdValue) ||
            assessmentIdValue.ValueKind != JsonValueKind.String || !Guid.TryParse(assessmentIdValue.GetString(), out var assessmentId) || assessmentId == Guid.Empty ||
            targetIdValue.ValueKind != JsonValueKind.String || !Guid.TryParse(targetIdValue.GetString(), out var targetId) || targetId == Guid.Empty ||
            !TryGet(raw, "targetStateVersion", out var stateVersionValue) ||
            stateVersionValue.ValueKind != JsonValueKind.Number || !stateVersionValue.TryGetInt32(out var stateVersion) || stateVersion < 1 ||
            !ReadRequiredCode(raw, "recommendation", 100, out var recommendation) || !SafetyRecommendations.Contains(recommendation) ||
            !ReadRequiredCode(raw, "reviewSuggestion", 100, out var reviewSuggestion) || !SafetyReviewSuggestions.Contains(reviewSuggestion) ||
            !TryGet(raw, "evidenceReferences", out var rawEvidenceReferences) ||
            !TryReadStringList(rawEvidenceReferences, maximumCount: 12, maximumItemLength: 256, out var evidenceReferences) ||
            !TryGet(raw, "uncertainties", out var rawUncertainties) ||
            !TryReadStringList(rawUncertainties, maximumCount: 10, maximumItemLength: 300, out var uncertainties))
        {
            return null;
        }

        var summary = ReadOptionalSafeText(raw, "summary", 1_000);
        if (summary is null || !TryGet(raw, "proposedAction", out var proposedActionValue))
        {
            return null;
        }

        string? proposedAction = null;
        if (proposedActionValue.ValueKind == JsonValueKind.String)
        {
            proposedAction = proposedActionValue.GetString();
            if (proposedAction is null || proposedAction.Length > 100 || !SafeCodePattern().IsMatch(proposedAction))
            {
                return null;
            }
        }
        else if (proposedActionValue.ValueKind != JsonValueKind.Null)
        {
            return null;
        }

        var safetyStep = steps.FirstOrDefault(step => step.AgentId == "safety_operations" && step.Status == "SUCCEEDED");
        if (safetyStep is null || !TryGet(state, "steps", out var rawSteps) || rawSteps.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var rawSafetyStep = rawSteps.EnumerateArray().FirstOrDefault(step =>
            step.ValueKind == JsonValueKind.Object &&
            ReadOptionalSafeText(step, "agentId", 100) == "safety_operations" &&
            ReadOptionalSafeText(step, "stepId", 128) == proposalStepId);
        var timestampText = rawSafetyStep.ValueKind == JsonValueKind.Object
            ? ReadOptionalSafeText(envelope, "producedAtUtc", 64)
            : null;
        if (timestampText is null || !HasExplicitOffset(timestampText) ||
            !DateTimeOffset.TryParse(timestampText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
        {
            return null;
        }

        var safeUncertainties = uncertainties
            .Select(text => SafeText(text, 300))
            .Where(text => text is not null)
            .Select(text => text!)
            .ToList();
        return new PlannerSafetyProposalDto(
            AssessmentId: assessmentId.ToString("D"),
            TargetId: targetId.ToString("D"),
            TargetStateVersion: stateVersion,
            Recommendation: recommendation,
            ProposedAction: proposedAction,
            Summary: summary,
            EvidenceReferences: Enumerable.Range(1, evidenceReferences.Count).Select(index => $"evidence-{index}").ToList(),
            Uncertainties: safeUncertainties,
            ReviewSuggestion: reviewSuggestion,
            ProducedAtUtc: timestamp.UtcDateTime);
    }

    private static bool TryGet(JsonElement value, string propertyName, out JsonElement property)
    {
        property = default;
        return value.ValueKind == JsonValueKind.Object && value.TryGetProperty(propertyName, out property);
    }

    private static bool ReadRequiredString(JsonElement value, string propertyName, int maximumLength, out string text)
    {
        text = string.Empty;
        if (!TryGet(value, propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        text = property.GetString() ?? string.Empty;
        return text.Length is > 0 && text.Length <= maximumLength;
    }

    private static bool ReadRequiredCode(JsonElement value, string propertyName, int maximumLength, out string code)
    {
        code = string.Empty;
        return ReadRequiredString(value, propertyName, maximumLength, out code) && SafeCodePattern().IsMatch(code);
    }

    private static string? ReadOptionalCode(JsonElement value, string propertyName)
    {
        if (!TryGet(value, propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var code = property.GetString();
        return code is not null && code.Length <= 64 && SafeCodePattern().IsMatch(code) ? code : null;
    }

    private static string? ReadOptionalSafeText(JsonElement value, string propertyName, int maximumLength)
    {
        if (!TryGet(value, propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? SafeText(property.GetString(), maximumLength) : null;
    }

    private static string? SafeText(string? text, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximumLength || UnsafeTextPattern().IsMatch(text))
        {
            return null;
        }

        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool TryReadStringList(JsonElement value, int maximumCount, int maximumItemLength, out List<string> items)
    {
        items = [];
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > maximumCount)
        {
            return false;
        }

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var text = item.GetString();
            if (string.IsNullOrWhiteSpace(text) || text.Length > maximumItemLength)
            {
                return false;
            }

            items.Add(text);
        }

        return true;
    }

    private static bool HasExplicitOffset(string value) =>
        value.EndsWith('Z') || value.EndsWith('z') ||
        Regex.IsMatch(value, "[+-][0-9]{2}:[0-9]{2}$", RegexOptions.CultureInvariant);
}
