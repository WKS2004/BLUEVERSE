using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Blueverse.CoastalOperations.Application;

/// <summary>
/// Dispatches a minimized, authenticated operational assessment to the private
/// planning coordinator and projects its response into safe user-facing output.
/// </summary>
public sealed class HttpAssessmentProposalPort(
    IHttpClientFactory clients,
    IOptions<AgenticAiOptions> agenticOptions,
    IOptions<ComponentDependencyOptions> componentOptions,
    CoastalOperationsDbContext db) : IAssessmentProposalPort
{
    public const string HttpClientName = "CoastalAgenticAi";
    private const string HealthPath = "/internal/agentic/agents/planning-coordination/health";
    private const string DispatchPath = "/internal/agentic/agents/planning-coordination/dispatch";
    private const int MaxResponseBytes = 64 * 1024;
    private const int MaxCatalogueBytes = 512 * 1024;
    private static readonly Guid UrlNamespace = Guid.Parse("6ba7b811-9dad-11d1-80b4-00c04fd430c8");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AssessmentAiAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
    {
        if (!TryServiceBase(out var serviceBase)) return AssessmentAiAvailability.NotConnected;
        try
        {
            using var request = CreateAuthenticatedRequest(HttpMethod.Get, new Uri(serviceBase!, HealthPath));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var response = await clients.CreateClient(HttpClientName)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) return AssessmentAiAvailability.Unavailable;
            var body = await ReadLimitedBodyAsync(response, MaxResponseBytes, timeout.Token);
            using var document = JsonDocument.Parse(body);
            if (!TryString(document.RootElement, "status", 32, out var status))
                return AssessmentAiAvailability.Unavailable;
            return status switch
            {
                "AVAILABLE" => AssessmentAiAvailability.Available,
                "NOT_CONNECTED" => AssessmentAiAvailability.NotConnected,
                _ => AssessmentAiAvailability.Unavailable
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AssessmentAiAvailability.Unavailable;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or UriFormatException)
        {
            return AssessmentAiAvailability.Unavailable;
        }
    }

    public Task<ProposalDispatchOutcome> DispatchAsync(Guid workflowId, Guid assessmentId, CancellationToken cancellationToken) =>
        Task.FromResult(new ProposalDispatchOutcome("INVALID_RESULT", false));

    public async Task<ProposalDispatchOutcome> DispatchAsync(
        PublishedAssessmentDispatch request,
        CancellationToken cancellationToken)
    {
        if (!TryServiceBase(out var serviceBase) || string.IsNullOrWhiteSpace(agenticOptions.Value.ServiceKey))
            return new("UNAVAILABLE", false);
        if (request.DispatchId == Guid.Empty || request.WorkflowId == Guid.Empty || request.AssessmentId == Guid.Empty ||
            request.TargetId == Guid.Empty || request.PeriodEndsAt <= request.PeriodStartsAt ||
            request.PeriodEndsAt <= DateTimeOffset.UtcNow)
            return new("INVALID_RESULT", false);

        var targetType = request.TargetType.Trim().ToUpperInvariant();
        if (targetType is not ("DESTINATION" or "ACTIVITY" or "OFFERING"))
            return new("UNAVAILABLE", true);

        var state = await db.TargetOperationalStates.AsNoTracking().SingleOrDefaultAsync(
            item => item.TargetType == targetType && item.TargetId == request.TargetId,
            cancellationToken);
        if (state is null || state.Version < 1 || !IsKnownState(targetType, state.State))
            return new("UNAVAILABLE", true);

        var experienceBase = componentOptions.Value.Member1Experience?.BaseAddress;
        if (!TryBaseUri(experienceBase, out var experienceUri)) return new("UNAVAILABLE", false);

        ExperienceContext? context;
        try
        {
            context = await LoadExperienceContextAsync(experienceUri!, request, targetType, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or UriFormatException)
        {
            return new("UNAVAILABLE", true);
        }
        if (context is null) return new("UNAVAILABLE", true);

        var observedAt = DateTimeOffset.UtcNow;
        var validUntil = observedAt.AddMinutes(15);
        if (request.PeriodEndsAt < validUntil) validUntil = request.PeriodEndsAt;
        if (validUntil <= observedAt) return new("UNAVAILABLE", true);

        var plannerReference = request.SourceWorkflowId ?? request.WorkflowId;
        var reportReferences = new[] { plannerReference, request.AssessmentId, request.DispatchId };
        if (reportReferences.Any(reference => reference == Guid.Empty) || reportReferences.Distinct().Count() != 3)
            return new("INVALID_RESULT", false);

        var allowedActions = AllowedActions(targetType, state.State);
        var body = new
        {
            contractVersion = 1,
            workflowId = request.WorkflowId,
            workflowType = "OPERATIONAL_ASSESSMENT",
            objective = request.Objective,
            timeRange = new { startsAt = request.PeriodStartsAt, endsAt = request.PeriodEndsAt },
            agentContext = new
            {
                destinationId = context.DestinationId,
                activityId = context.ActivityId,
                activityName = context.ActivityName,
                latitude = context.Latitude,
                longitude = context.Longitude,
                offeringId = context.OfferingId
            },
            assessment = new
            {
                assessmentId = request.AssessmentId,
                dispatchId = request.DispatchId,
                target = new { type = targetType, id = request.TargetId, currentState = state.State, stateVersion = state.Version },
                policyVersion = "coastal-operations.transitions.v1",
                allowedActions,
                reports = new object[]
                {
                    Report(plannerReference, "planner_report", "UNKNOWN", "coastal-operations", observedAt, validUntil, new Dictionary<string, object?>()),
                    Report(request.AssessmentId, "operations_context", "VALID", "coastal-operations", observedAt, validUntil,
                        new Dictionary<string, object?> { ["operationalStatus"] = state.State }),
                    Report(request.DispatchId, "safety_profile", "UNKNOWN", "coastal-operations.transition-policy", observedAt, validUntil,
                        new Dictionary<string, object?>())
                }
            }
        };

        var content = JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions);
        if (content.Length > 64_000) return new("INVALID_RESULT", false);
        try
        {
            using var message = CreateAuthenticatedRequest(HttpMethod.Post, new Uri(serviceBase!, DispatchPath));
            message.Headers.TryAddWithoutValidation("X-Correlation-ID", SafeCorrelationId(request.CorrelationId, request.DispatchId));
            message.Content = new ByteArrayContent(content);
            message.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(agenticOptions.Value.DispatchTimeoutSeconds, 5, 180)));
            using var response = await clients.CreateClient(HttpClientName)
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                return new("UNAVAILABLE", true);
            if (!response.IsSuccessStatusCode) return new("INVALID_RESULT", false);

            var responseBytes = await ReadLimitedBodyAsync(response, MaxResponseBytes, timeout.Token);
            var summary = ValidateResponse(responseBytes, request, state, allowedActions, reportReferences);
            return summary is null ? new("INVALID_RESULT", false) : new("ACCEPTED", false, summary);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new("UNAVAILABLE", true);
        }
        catch (InvalidDataException)
        {
            return new("INVALID_RESULT", false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or UriFormatException)
        {
            return new("UNAVAILABLE", true);
        }
    }

    private async Task<ExperienceContext?> LoadExperienceContextAsync(
        Uri baseUri,
        PublishedAssessmentDispatch request,
        string targetType,
        CancellationToken cancellationToken)
    {
        var queryName = targetType switch
        {
            "DESTINATION" => "destinationId",
            "ACTIVITY" => "activityId",
            "OFFERING" => "offeringId",
            _ => throw new InvalidDataException("Unsupported target type.")
        };
        var catalogueUri = new Uri(baseUri, $"/api/experiences/catalogue?{queryName}={request.TargetId:D}");
        using var message = new HttpRequestMessage(HttpMethod.Get, catalogueUri);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var response = await clients.CreateClient(ComponentDependencyCollector.HttpClientName)
            .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            throw new HttpRequestException("Experience catalogue is temporarily unavailable.");
        if (!response.IsSuccessStatusCode) return null;

        var bytes = await ReadLimitedBodyAsync(response, MaxCatalogueBytes, timeout.Token);
        using var document = JsonDocument.Parse(bytes);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return null;
        var candidates = new List<ExperienceContext>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!TryCatalogueItem(item, request, targetType, out var candidate)) continue;
            candidates.Add(candidate!);
        }
        return candidates.OrderBy(item => item.AvailableUntil)
            .ThenBy(item => item.OfferingId)
            .FirstOrDefault();
    }

    private static bool TryCatalogueItem(JsonElement item, PublishedAssessmentDispatch request, string targetType, out ExperienceContext? context)
    {
        context = null;
        if (item.ValueKind != JsonValueKind.Object ||
            !TryGuid(item, "destinationId", out var destinationId) ||
            !TryGuid(item, "activityId", out var activityId) ||
            !TryGuid(item, "offeringId", out var offeringId) ||
            !TryString(item, "activityName", 100, out var activityName) ||
            !TryNumber(item, "latitude", out var latitude) || latitude is < -90 or > 90 ||
            !TryNumber(item, "longitude", out var longitude) || longitude is < -180 or > 180 ||
            !TryString(item, "availabilityStatus", 32, out var availability) || availability != "AVAILABLE" ||
            !TryDate(item, "availableFrom", out var availableFrom) ||
            !TryDate(item, "availableUntil", out var availableUntil) ||
            availableFrom > request.PeriodStartsAt || availableUntil < request.PeriodEndsAt)
            return false;

        var matches = targetType switch
        {
            "DESTINATION" => destinationId == request.TargetId,
            "ACTIVITY" => activityId == request.TargetId,
            "OFFERING" => offeringId == request.TargetId,
            _ => false
        };
        if (!matches || destinationId == Guid.Empty || activityId == Guid.Empty || offeringId == Guid.Empty) return false;
        context = new ExperienceContext(destinationId, activityId, activityName.Trim(), latitude, longitude,
            offeringId, availableUntil);
        return true;
    }

    private static AssessmentAgentExecutionSummaryResponse? ValidateResponse(
        byte[] responseBytes,
        PublishedAssessmentDispatch request,
        TargetOperationalState targetState,
        IReadOnlyList<string> allowedActions,
        IReadOnlyList<Guid> inputReferences)
    {
        using var document = JsonDocument.Parse(responseBytes);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !TryInt(root, "contractVersion", out var contractVersion) || contractVersion != 1 ||
            !TryGuid(root, "workflowId", out var workflowId) || workflowId != request.WorkflowId ||
            !TryString(root, "status", 32, out var status) || status != "COMPLETED" ||
            !root.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array || steps.GetArrayLength() != 3 ||
            (root.TryGetProperty("errorCode", out var rootError) && rootError.ValueKind != JsonValueKind.Null) ||
            !root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object ||
            !TryGuid(result, "workflowId", out var resultWorkflowId) || resultWorkflowId != request.WorkflowId ||
            !TryString(result, "objectiveSummary", 500, out var objectiveSummary) || !IsSafeText(objectiveSummary, 500) ||
            !result.TryGetProperty("safetyProposal", out var safetyProposal) || safetyProposal.ValueKind != JsonValueKind.Object)
            return null;

        var expectedTypes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["experience_biodiversity"] = "ExperienceBiodiversityContextReport.v1",
            ["marine_conditions"] = "MarineConditionsReport.v1",
            ["safety_operations"] = "SafetyOperationsRecommendation.v1"
        };
        var parsedSteps = new Dictionary<string, AssessmentAgentStepResponse>(StringComparer.Ordinal);
        var stepIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var uniqueStepIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in steps.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Object ||
                !TryString(step, "agentId", 64, out var agentId) || !expectedTypes.ContainsKey(agentId) ||
                parsedSteps.ContainsKey(agentId) ||
                !TryString(step, "stepId", 64, out var stepId) || !IsSafeIdentifier(stepId) || !uniqueStepIds.Add(stepId) ||
                !TryString(step, "status", 32, out var stepStatus) || stepStatus != "SUCCEEDED" ||
                !TryString(step, "reportType", 128, out var reportType) || reportType != expectedTypes[agentId] ||
                !TryString(step, "summary", 1000, out var stepSummary) || !IsSafeText(stepSummary, 1000) ||
                !TryStringArray(step, "sourceReferences", 32, 256, out var sources) || sources.Count == 0 ||
                !TryStringArray(step, "uncertaintyNotes", 32, 500, out var uncertainties) ||
                !TryDate(step, "producedAtUtc", out var producedAt))
                return null;

            string? errorCode = null;
            if (step.TryGetProperty("errorCode", out var error) && error.ValueKind != JsonValueKind.Null)
            {
                if (error.ValueKind != JsonValueKind.String || error.GetString() is not { Length: > 0 and <= 64 } value || !IsSafeIdentifier(value))
                    return null;
                errorCode = value;
            }
            parsedSteps[agentId] = new AssessmentAgentStepResponse(agentId, stepStatus, stepSummary, sources, uncertainties, errorCode);
            stepIds[agentId] = stepId;
        }
        if (parsedSteps.Count != expectedTypes.Count) return null;

        var safety = safetyProposal;
        if (!TryString(safety, "schemaVersion", 64, out var schemaVersion) || schemaVersion != "safety-operations.recommendation.v1" ||
            !TryGuid(safety, "workflowId", out var safetyWorkflowId) || safetyWorkflowId != request.WorkflowId ||
            !TryString(safety, "stepId", 64, out var safetyStepId) || safetyStepId != stepIds["safety_operations"] ||
            !TryGuid(safety, "assessmentId", out var safetyAssessmentId) || safetyAssessmentId != request.AssessmentId ||
            !TryGuid(safety, "targetId", out var safetyTargetId) || safetyTargetId != request.TargetId ||
            !TryString(safety, "recommendation", 32, out var recommendation) ||
            recommendation is not ("CONTINUE" or "CONTINUE_WITH_CAUTION" or "PROPOSE_ACTION" or "INSUFFICIENT_DATA") ||
            !TryString(safety, "summary", 1000, out var safetySummary) || !IsSafeText(safetySummary, 1000) ||
            !TryString(safety, "reviewSuggestion", 32, out var reviewSuggestion) || reviewSuggestion is not ("NONE" or "REVIEW_RECOMMENDED") ||
            !TryStringArray(safety, "evidenceReferences", 5, 128, out var evidenceReferences) || evidenceReferences.Count != 5 ||
            !TryStringArray(safety, "uncertainties", 10, 300, out var safetyUncertainties))
            return null;

        var actionElement = safety.TryGetProperty("proposedAction", out var actionValue) ? actionValue : default;
        string? proposedAction = null;
        if (actionElement.ValueKind == JsonValueKind.String)
        {
            proposedAction = actionElement.GetString();
            if (string.IsNullOrWhiteSpace(proposedAction) || proposedAction.Length > 32 || !IsSafeIdentifier(proposedAction)) return null;
        }
        else if (actionElement.ValueKind != JsonValueKind.Null) return null;
        if ((recommendation == "PROPOSE_ACTION") != (proposedAction is not null) ||
            (proposedAction is not null && (!allowedActions.Contains(proposedAction, StringComparer.Ordinal) ||
                !OperationsValidation.IsAllowedTransition(targetState.TargetType, targetState.State, proposedAction))))
            return null;

        var allowedEvidence = new HashSet<Guid>(inputReferences);
        foreach (var agentId in new[] { "experience_biodiversity", "marine_conditions" })
            allowedEvidence.Add(CreateVersion5(UrlNamespace, $"blueverse:{request.WorkflowId:D}:{stepIds[agentId]}:{agentId}"));
        var parsedEvidence = new List<Guid>(evidenceReferences.Count);
        foreach (var reference in evidenceReferences)
        {
            if (!Guid.TryParse(reference, out var parsed) || !allowedEvidence.Contains(parsed)) return null;
            parsedEvidence.Add(parsed);
        }
        if (parsedEvidence.Distinct().Count() != parsedEvidence.Count)
            return null;

        var safetyProducedAt = DateTimeOffset.UtcNow;
        foreach (var value in steps.EnumerateArray())
        {
            if (TryString(value, "agentId", 64, out var agentId) && agentId == "safety_operations" &&
                TryDate(value, "producedAtUtc", out var produced)) safetyProducedAt = produced;
        }
        var summarySteps = new List<AssessmentAgentStepResponse>(4)
        {
            new("planning_coordination", "SUCCEEDED", objectiveSummary, [], [])
        };
        summarySteps.AddRange(new[] { "experience_biodiversity", "marine_conditions", "safety_operations" }
            .Select(agentId => parsedSteps[agentId]));

        var proposal = new AssessmentSafetyProposalResponse(
            safetyAssessmentId, safetyTargetId, targetState.Version, recommendation, proposedAction,
            safetySummary, evidenceReferences, safetyUncertainties, reviewSuggestion, safetyProducedAt);
        var summary = new AssessmentAgentExecutionSummaryResponse("COMPLETED", objectiveSummary, summarySteps, proposal);
        return JsonSerializer.SerializeToUtf8Bytes(summary, JsonOptions).Length <= 32_768 ? summary : null;
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("X-Blueverse-Service-Key", agenticOptions.Value.ServiceKey);
        return request;
    }

    private bool TryServiceBase(out Uri? serviceBase)
    {
        serviceBase = null;
        var options = agenticOptions.Value;
        return options.Enabled && !string.IsNullOrWhiteSpace(options.ServiceKey) && TryBaseUri(options.BaseUrl, out serviceBase);
    }

    private static bool TryBaseUri(string? value, out Uri? uri)
    {
        uri = null;
        return Uri.TryCreate(value, UriKind.Absolute, out var parsed) &&
               parsed.Scheme is "http" or "https" && string.IsNullOrEmpty(parsed.UserInfo) &&
               string.IsNullOrEmpty(parsed.Query) && string.IsNullOrEmpty(parsed.Fragment) &&
               (uri = new Uri(parsed.GetLeftPart(UriPartial.Authority) + "/")).IsAbsoluteUri;
    }

    private static object Report(
        Guid reference, string category, string status, string source, DateTimeOffset observedAt,
        DateTimeOffset validUntil, IReadOnlyDictionary<string, object?> facts) => new
    {
        reference,
        category,
        status,
        source,
        observedAt,
        validUntil,
        facts
    };

    private static IReadOnlyList<string> AllowedActions(string targetType, string currentState)
    {
        var candidates = targetType == "SESSION"
            ? new[] { "OPEN", "CAUTION", "TEMPORARILY_SUSPENDED", "CANCELLED", "COMPLETED" }
            : new[] { "OPEN", "CAUTION", "TEMPORARILY_SUSPENDED" };
        return candidates.Where(next => OperationsValidation.IsAllowedTransition(targetType, currentState, next)).ToArray();
    }

    private static bool IsKnownState(string targetType, string value) =>
        value is "OPEN" or "CAUTION" or "TEMPORARILY_SUSPENDED" ||
        (targetType == "SESSION" && value is ("CANCELLED" or "COMPLETED"));

    private static async Task<byte[]> ReadLimitedBodyAsync(HttpResponseMessage response, int maxBytes, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is long length && length > maxBytes)
            throw new InvalidDataException("Response exceeds its size limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (output.Length + read > maxBytes) throw new InvalidDataException("Response exceeds its size limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static bool TryGuid(JsonElement element, string property, out Guid value)
    {
        value = Guid.Empty;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var item) &&
               item.ValueKind == JsonValueKind.String && item.TryGetGuid(out value) && value != Guid.Empty;
    }

    private static bool TryInt(JsonElement element, string property, out int value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var item) &&
               item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out value);
    }

    private static bool TryString(JsonElement element, string property, int maxLength, out string value)
    {
        value = string.Empty;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var item) ||
            item.ValueKind != JsonValueKind.String) return false;
        var parsed = item.GetString();
        if (parsed is null || parsed.Length is 0 || parsed.Length > maxLength || parsed != parsed.Trim()) return false;
        value = parsed;
        return true;
    }

    private static bool TryNumber(JsonElement element, string property, out double value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var item) &&
               item.ValueKind == JsonValueKind.Number && item.TryGetDouble(out value) && double.IsFinite(value);
    }

    private static bool TryDate(JsonElement element, string property, out DateTimeOffset value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var item) &&
               item.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(item.GetString(), CultureInfo.InvariantCulture,
                   DateTimeStyles.RoundtripKind, out value);
    }

    private static bool TryStringArray(JsonElement element, string property, int maxItems, int maxLength, out IReadOnlyList<string> values)
    {
        values = [];
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var array) ||
            array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > maxItems) return false;
        var items = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { Length: > 0 } value ||
                value.Length > maxLength || value != value.Trim() || value.Any(char.IsControl)) return false;
            items.Add(value);
        }
        values = items;
        return true;
    }

    private static bool IsSafeIdentifier(string value) =>
        value.Length is > 0 and <= 128 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':');

    private static bool IsSafeText(string value, int maxLength) =>
        value.Length is > 0 && value.Length <= maxLength && !value.Any(char.IsControl) &&
        !value.Contains("http://", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("https://", StringComparison.OrdinalIgnoreCase);

    private static string SafeCorrelationId(string value, Guid dispatchId) =>
        value.Length is > 0 and <= 80 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or ':' or '-')
            ? value
            : $"assessment:{dispatchId:N}";

    private static Guid CreateVersion5(Guid namespaceId, string name)
    {
        var namespaceBytes = Convert.FromHexString(namespaceId.ToString("N"));
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(input, 0);
        nameBytes.CopyTo(input, namespaceBytes.Length);
        var digest = SHA1.HashData(input);
        digest[6] = (byte)((digest[6] & 0x0F) | 0x50);
        digest[8] = (byte)((digest[8] & 0x3F) | 0x80);
        var hex = Convert.ToHexString(digest.AsSpan(0, 16)).ToLowerInvariant();
        return Guid.ParseExact($"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}", "D");
    }

    private sealed record ExperienceContext(
        Guid DestinationId,
        Guid ActivityId,
        string ActivityName,
        double Latitude,
        double Longitude,
        Guid OfferingId,
        DateTimeOffset AvailableUntil);
}
