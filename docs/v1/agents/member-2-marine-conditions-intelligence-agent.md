---
contract_id: v1.agent.marine-conditions
contract_type: agent_role
release: v1
implementation_status: target_not_implemented
owner_label: member_2
requirements: "PROJECT_REQUIREMENTS.md sections 15, 20-27, 31, 40"
business_component: "../components/member-2-marine-conditions-safety-intelligence.md"
---

# Member 2 — Marine Conditions Intelligence Agent

**Target status:** the repository does not contain an executable v1 agent,
runtime or allowlisted provider tools. This file defines its target role and
hand-off; it does not claim that provider integration or AI inference exists.

**Owning business component:** [Marine Conditions & Safety Intelligence](../components/member-2-marine-conditions-safety-intelligence.md).

**Implementation sequence:** do not implement an executable agent, model
call or tool before all four business components pass G07. After that gate,
follow the post-G07 sequence in the [member branch and integration workflow](../member-branch-workflow.md).
Before G07, the owning Member 2 feature branch prepares only the public
workflow contract, private backend adapter and not-connected/unavailable
behavior defined in the [member integration boundary](../agentic-ai-integration-boundary.md).
Implement this actual agent on an `agentic-ai/**` branch after G07.

## 1. Responsibility and role separation

Collect and interpret weather and marine context relevant to a validated
BLUEVERSE objective, destination/location, coastal activity and requested
period. Return a source-aware report that preserves provider timing,
freshness, unavailable fields and factors relevant to the objective.

This agent is an evidence summarizer and specialist, not:

- the Open-Meteo provider client (backend-owned service and tools make calls);
- the deterministic suitability evaluator (application/business logic owned
  by Member 2);
- a professional navigator, weather authority or operational approver; or
- the operations agent that recommends a managed state change.

The distinct skill is to interpret actual retrieved context and report its
limits. It must not invent physical thresholds, facts or authorization.

## 2. Invocation and input contract

The private orchestrator invokes this role for a validated plan step. The
public API has applied its existing authentication and permission checks, and
the owning member service has validated the request and created the shared
workflow record before private orchestration begins. The agent receives only
the context required for its step.

The logical typed input must carry, as relevant:

| Input concept | Meaning |
|---|---|
| Workflow/step correlation | Shared workflow ID/type, planned step identifier and expected report type. |
| Objective | Bounded reason for retrieving conditions, supplied as untrusted task data. |
| Location | Validated destination/location identifier or coordinates from the approved workflow input. The model cannot substitute arbitrary network locations. |
| Activity and period | The relevant activity/category and requested forecast or observation interval. |
| Required condition scope | Only the factors needed for that activity/workflow; the application configuration, not the model, decides the allowed field set. |
| Existing context | Optional authorized snapshot reference or known constraints, including what is not known. |
| Tool policy | Runtime-provided allowlist, per-tool limits and output schema. The agent cannot select new tools or raise limits. |

The requested period is supplied by the validated public workflow. The agent
may retrieve only that approved interval (or an explicitly authorized,
deterministic normalization of it); it must not independently select, widen,
shift or silently truncate the time window. For planner-originated work, the
candidate itinerary period remains the input authority.

These concepts do not freeze wire-level field names, DTO versions, units or
nullable annotations. The implementation schema must define them explicitly
and validate requests before provider access.

## 3. Allowlisted read tools

Candidate tool capabilities are not executable permissions, client routes or
arbitrary URLs. The backend mediates all access to the selected Open-Meteo
Weather and Marine APIs. Tool definitions and audit obligations are in the
[shared tool contract](../../agentic-ai/tools.md).

| Candidate tool | Purpose | Important validated result |
|---|---|---|
| `weather_forecast_lookup` | Retrieve only required weather forecast factors for the specified location and interval. | Provider/source, requested location, forecast timestamps, units, retrieval time, requested/returned fields and unavailable/invalid values. |
| `marine_forecast_lookup` | Retrieve only required marine forecast factors for the location and period. | Same provenance plus marine variables such as waves, swell, sea-surface temperature or currents only when required. |
| `condition_snapshot_lookup` | Retrieve a prior validated snapshot if allowed for the workflow. | Distinguish historical/stale evidence from fresh evidence; return original source and forecast/observation/retrieval times. |

Every actual tool must specify authorized agent, typed input/output,
validation, timeout, bounded retry, rate-limit handling, failure mapping,
allowed side effects (read-only for this role), workflow/step correlation,
elapsed time and safe audit summary. No raw
HTTP access or provider credentials are exposed to the model.

## 4. Structured Marine Conditions Report

Return a schema-validated report with the following semantic content:

| Output concept | Required meaning |
|---|---|
| Correlation/scope | Workflow and step correlation, requested location, activity if supplied and exact requested interval. |
| Source | Provider/source for each factor or group; do not claim data came from observation if it is forecast. |
| Environmental factors | Requested weather/marine values with source units or explicit normalized units, and their relevant forecast/observation time. |
| Retrieval metadata | Backend retrieval time and any source-issued generation/update timestamp available. |
| Freshness | Configured freshness assessment and what rule/configuration produced it; freshness does not guarantee correctness. |
| Missing/unavailable factors | Each requested variable that was not returned, invalid or unsupported, distinguished from a measured/forecast zero. |
| Objective relevance | Plain, source-grounded explanation of which factors matter to the supplied objective. It does not introduce threshold policy. |
| Uncertainty/limitation | Incomplete interval, coarse spatial coverage, missing data or any source limitation relevant to interpretation. |
| Outcome | Complete report, incomplete report, source unavailable, invalid result or configured failure state. |

Exact property names, measurement conversions, outcome enum, confidence
representation and report version are implementation decisions. Preserve
units and timestamps without silently changing them. A generated summary
cannot replace raw structured evidence needed by deterministic validation.

## 5. Interpretation and non-authority rules

1. Never infer or invent a weather/marine value absent from validated tool
   output.
2. Do not mark a prior snapshot current because it was just retrieved from a
   cache; use the forecast/observation time and configured freshness rule.
3. Distinguish zero from missing, null, unsupported, malformed and failed.
4. Do not combine values from different locations, periods, sources or units
   without an explicit, tested deterministic normalization rule.
5. Do not invent a surfing, snorkeling, diving, boating or other physical
   safety limit. Thresholds belong to the configured Member 2 activity
   profiles and deterministic evaluator.
6. Do not assign or override `SUITABLE`, `CAUTION`, `UNSUITABLE` or `UNKNOWN`.
   If the workflow includes an existing deterministic result, report it as
   that result and do not contradict it.
7. Do not authorize operation continuation, suspension, cancellation or alert
   publication. Those belong to deterministic validation, human approval and
   the Member 4 API execution flow.
8. Explain that conditions are decision support and not professional marine
   navigation guidance.

## 6. Workflow participation

The Planning & Coordination Agent delegates the marine-context step when the
objective depends on marine/weather conditions. For the tourist workflow,
the report and deterministic suitability constrain candidate recommendation.
For the canonical operational assessment, the Safety & Operations Agent
receives this report alongside experience and current operational context.
Deterministic application validation rechecks evidence freshness and the
activity profile after the agent's summary is generated.

The agent output is persisted in the shared workflow only as structured
business output or an auditable summary, with source/error/timing information
needed to explain the result. Hidden reasoning is not workflow data. A
successful tool call alone does not prove the report is valid; schema,
provenance, period, location and required factors must all be checked.

## 7. Security and recovery

Provider responses and user objectives are untrusted. Prompt injection or
malicious provider content cannot modify instructions, widen the query,
choose arbitrary coordinates/URLs, grant tools, ask for secrets, bypass the
safety profile, or cause an operational action. Validate schema, location,
period, units, ranges and timestamps in backend code before downstream use.

Handle invalid arguments, invalid coordinates/time range, provider timeout,
HTTP/provider errors, rate limits, malformed response, schema drift,
unavailable variables, stale snapshots, mixed/unknown units and exhausted
bounded retries. Record the structured failure or missing factors. If
recovery is not possible, the workflow reports safe failure/insufficient
evidence and performs no protected side effect. Do not downsample or replace
missing values with a language-model guess.

Persist only necessary report values or reference/summary, source and timing,
tool outcome, retry/error and workflow timestamps. Never persist API secrets,
passwords, access tokens or hidden chain-of-thought. Tool audit records must
avoid duplicating sensitive context without a business need.

## 8. Validation and evaluation contract

Using deterministic fixtures, verify:

- the correct allowlisted provider tool is used for location, activity and
  interval;
- only factors required by a real BLUEVERSE objective are fetched;
- weather and marine data preserve provider, source units, requested location,
  forecast/observation times and retrieval time;
- zero, missing, null, unsupported and invalid values remain distinguishable;
- stale/cache data is explicitly marked and cannot support a fresh positive
  result where freshness is required;
- invalid timestamps, units, coordinates, provider payload and schema drift
  are rejected or surfaced as unavailable;
- timeout, provider outage, rate limits and retry exhaustion are recorded
  with no fabricated values or unsafe effect;
- prompt injection and instructions hidden in provider fields do not change
  instructions, tools or permissions;
- natural-language interpretation never changes configured numeric profiles
  or deterministic suitability; and
- the report's uncertainty remains visible to the planner and reviewer in the
  tourist and assessed workflows.

The role must be visibly distinct in the golden assessment and tourist
recommendation. Fixture evaluation, live provider evidence and live-model
evidence must be reported separately. A plausible explanation without
structured evidence does not pass.

## 9. Implementation decisions

Define exact provider request mappings, field/unit normalization, typed report
version, freshness calculation, source timestamp interpretation, snapshots
and cache retention, rate-limit/backoff policy, retry limits, valid coordinate
and period ranges, error schema, and what failures map to `UNKNOWN` versus
workflow failure. Safety thresholds and operation permission cannot be
delegated to this agent.

## 10. Related contracts

- Requirements: [sections 14–15, 20–27, 31 and 40](../../../PROJECT_REQUIREMENTS.md).
- Owning component: [Marine Conditions & Safety Intelligence](../components/member-2-marine-conditions-safety-intelligence.md).
- Shared behavior: [canonical workflows](../workflows.md), [requirements coverage and readiness](../requirements-coverage-and-readiness.md), [Agentic AI architecture](../../agentic-ai/architecture.md), [implementation blueprint](../../agentic-ai/implementation-blueprint.md), [tools](../../agentic-ai/tools.md), [safety](../../agentic-ai/safety.md), [evaluation](../../agentic-ai/evaluation.md).
