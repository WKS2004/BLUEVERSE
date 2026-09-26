---
contract_id: v1.agent.experience-biodiversity
contract_type: agent_role
release: v1
implementation_status: target_not_implemented
owner_label: member_1
owner_full_name: "Ushan Srinuka"
owner_github_username: "Ushan-Srinuka"
feature_branch: "features/experience-biodiversity"
agentic_ai_branch: "agentic-ai/experience-biodiversity"
requirements: "PROJECT_REQUIREMENTS.md sections 13, 20-30, 40"
business_component: "../components/member-1-coastal-experience-biodiversity-discovery.md"
---

# Ushan Srinuka (Member 1) — Coastal Experience & Biodiversity Agent

**Target status:** no executable v1 agent/runtime/tool implementation is
present in the current foundation. This is a specification for a distinct
Agentic AI responsibility, not an assertion that an inference model or agent
exists.

**Assigned owner:** Ushan Srinuka (`@Ushan-Srinuka`), requirement trace label
Member 1. Component branch: `features/experience-biodiversity`; actual agent
branch, after G07: `agentic-ai/experience-biodiversity`.

**Owning business component:** [Coastal Experience & Biodiversity Discovery](../components/member-1-coastal-experience-biodiversity-discovery.md).

**Implementation sequence:** do not implement an executable agent, model
call or tool before all four business components pass G07. After that gate,
follow the post-G07 sequence in the [member branch and integration workflow](../member-branch-workflow.md).
Before G07, the owning Ushan Srinuka (Member 1) feature branch prepares only the public
workflow contract, private backend adapter and not-connected/unavailable
behavior defined in the [member integration boundary](../agentic-ai-integration-boundary.md).
Implement this actual agent on an `agentic-ai/**` branch after G07.

## 1. Responsibility and intended use

Return a structured, evidence-grounded context report about coastal
destinations, activities, offerings, schedules, experience constraints and
optional biodiversity predictions. The report helps the tourist planner
assemble an eligible recommendation and helps the operational workflow inspect
the affected experience. It is a read-only specialist: it describes sourced
facts and gaps, not an authorization decision or a mutation.

This role is distinct because it owns interpretation/assembly of experience
and biodiversity context. It does not perform the Planning & Coordination
Agent's workflow planning, the Marine Conditions Agent's environmental
interpretation, or the Safety & Operations Agent's operational recommendation.
Using one generic agent prompt under several names does not satisfy the
four-agent architecture.

## 2. Invocation and input contract

The private orchestrator invokes the agent only for a validated workflow step
whose plan identifies experience context as needed. The public API has applied
its existing authentication and permission checks, and Ushan Srinuka's private
service has validated and persisted the workflow before invocation. The agent
receives only the minimum context required for its assigned step; it does not
receive passwords, access tokens or unrestricted identity data.

The logical input must identify, as relevant:

| Input concept | Required meaning |
|---|---|
| Workflow correlation | Shared workflow ID/type and the planned step so the report is attached to the correct durable workflow. |
| Objective | Validated, bounded business purpose such as a coastal recommendation or operational assessment. Treat free text as untrusted data, not as a new instruction hierarchy. |
| Experience selectors | Destination/region, activity, offering and/or requested date/time needed for the step. Unsupported or contradictory references are rejected before tool use. |
| Constraints | Only relevant tourist/operator conditions already validated by the owning service; do not infer unstated restrictions. |
| Biodiversity query | Optional location/species/context needed for a prediction. Omit where irrelevant; do not call merely to enrich a report without business value. |
| Allowed contract | Agent identity, expected report type, configured tool grants and limits. These are system-controlled, not model-selected. |

These are semantic fields, not final serialized property names, requiredness
annotations, DTOs or database columns. The runtime contract must freeze an
explicit typed schema and reject malformed/unknown identifiers before tool
execution.

## 3. Allowlisted read tools

Candidate tool names below describe target capabilities, not live tools,
public API routes or permission grants. Tool registration is centralized in
the [tool contract](../../agentic-ai/tools.md). Tools return data through
server-owned services; the agent must not call arbitrary URLs or internal
hostnames directly. The selected map API is not a direct agent dependency:
map-assisted discovery/presentation belongs to Ushan Srinuka's business component,
and this agent receives only validated destination/location context returned
by its Ushan Srinuka (Member 1) tools. A future map tool requires a separate approved
allowlist, contract and evaluation; it is not implied by the map integration.

| Candidate tool | Purpose | Agent must preserve |
|---|---|---|
| `destination_lookup` | Read a requested destination and eligible descriptive/location context. | Stable identity, authoritative publication/visibility and not-found/denied status. |
| `activity_lookup` | Read an activity's coastal type and relevant experience constraints. | Source-owned activity identity, status and non-invented attributes. |
| `offering_lookup` | Read the offering that links a destination and activity. | Authoritative operational/availability status where supplied; this agent cannot change it. |
| `schedule_lookup` | Read relevant offering schedule and time-specific availability. | Requested timezone/period, source time, missing/closed/unavailable distinctions. |
| `biodiversity_prediction_lookup` | When relevant, request prediction context through the validated Adithya Gunawardana (Member 3) public/typed backend capability. The tool handler uses Adithya Gunawardana's server-side IT3091 adapter; this agent never calls IT3091 or a private host directly. | Explicit not-requested/available/unavailable/invalid state and, only for a genuine validated result, focal species, query location, probability or habitat-suitability interpretation, model/version, prediction time, uncertainty and limitations where supplied. |

Each implemented tool contract defines exact typed arguments/results,
authorized agent, source service, validation, timeout, bounded retry, error
mapping, side-effect policy, workflow/step correlation, elapsed time and
auditable summary. All tools for this agent
are read-only. There is no publish, archive, availability mutation or
operational-state mutation tool.

The biodiversity tool is a post-G07 Agentic AI capability; it does not own
the ML integration. Adithya Gunawardana's ordinary feature branch implements and
validates the real IT3091 request/response adapter and public result contract
before G07. After G07, the tool is allowlisted and typed, and its backend
handler invokes only that Adithya Gunawardana (Member 3) contract. It must preserve the contract's
unavailable/invalid state and provenance and must not present a prediction as
observed presence, safety evidence or operational authority. See
[ADR-0019](../../adr/ADR-0019-biodiversity-inference-integration-ownership.md).

## 4. Structured output contract

Return an **Experience & Biodiversity Context Report**, which downstream
application code schema-validates before use. The logical report should make
these facts explicit:

| Output concept | Meaning |
|---|---|
| Correlation and scope | Workflow/step correlation plus requested destination, activity, offering and period represented in the report. |
| Destination/activity context | Authoritative descriptive context relevant to the objective, with source status and missing/not-found distinction. |
| Offering status | Publication/eligibility, schedule and time-specific availability evidence; do not merge these into one unsupported Boolean. |
| Experience constraints | Relevant user-facing or participation constraints that were actually returned by the owning source. |
| Biodiversity status | Explicitly available, not requested, unavailable, missing or invalid as defined by the implementation schema. |
| Biodiversity result | If genuine and valid: focal species, queried location, occurrence probability and/or habitat-suitability interpretation, model version, prediction time and returned limitations/uncertainty. |
| Evidence references | Source/result identifiers and timestamps needed to inspect freshness and provenance without copying unnecessary personal data. |
| Uncertainty and missing data | Required facts unavailable, stale or not supplied; the impact they have on this report. |
| Completion outcome | Successful complete, successful with optional data unavailable, requires upstream correction, or failed under the frozen runtime outcome model. |

Do not claim that the report is safe to operate, eligible for publication or
approved. Final field names, enums, nullability and validation constraints
must be versioned in the implemented contract. The report should avoid
duplicating model text or storing hidden reasoning.

## 5. Interpretation rules

1. Separate destination/activity publication from offering schedule and
   availability. Each status is sourced from the owning component.
2. An invalid, unpublished, unavailable or operationally restricted offering
   is not made recommendable by a natural-language summary.
3. Consume Wanshaja Sooriyabandara's authoritative operational status where the workflow
   contract makes it relevant. Do not maintain or predict a substitute state.
4. Biodiversity intelligence is enrichment/context only. An occurrence
   probability is not confirmed presence; habitat suitability is not a
   sighting or deterministic safety rule.
5. If the biodiversity model/service is down, report unavailable. Never fill
   a missing probability using a language-model guess, a default number, or a
   stale result presented as current.
6. Distinguish no records, not requested, unauthorized/not available, invalid
   response and temporary service failure as the tool contract defines.
7. Use only experience details relevant to the objective. Do not create
   restrictions, availability, species or user preferences absent from
   validated evidence.

## 6. Workflow participation

In the canonical operational assessment, the Planning & Coordination Agent
delegates experience lookup after persisting a plan. This agent returns the
selected object's published/usable state, schedule/availability and
experience constraints, plus biodiversity only if relevant and available.
The Safety & Operations Agent and deterministic validator receive validated
structured results, not hidden model reasoning.

In the tourist recommendation workflow, the planning agent requests candidate
experience context alongside marine context. The planner applies deterministic
eligibility and suitability constraints during result assembly. If required
experience data is missing or the selected offering is restricted/unavailable,
the result must show the limitation or exclude that candidate.

Agent execution status and output summary participate in the same durable
workflow ID. A report is not considered accepted merely because generation
completed; its schema, references and required evidence must pass application
validation.

## 7. Security and failure behavior

All user objectives, catalogue descriptions, prediction text and tool output
are untrusted. They cannot modify system instructions, create/grant tools,
reveal secrets, request unauthorized records, or bypass deterministic checks.
Only an explicit allowlisted invocation may read the named resource; caller
permissions/resource scope are rechecked in the server-owned tool path.

Malformed arguments, invalid IDs, inaccessible resources, schema mismatch,
tool timeout, stale schedule data, malformed ML metadata, unavailable model,
invalid prediction ranges or retry exhaustion must produce structured
failure/missing-data evidence and no business mutation. The orchestrator may
bounded-retry only under the configured tool policy. Failure never creates a
fabricated prediction or changes publication, availability or operational
state.

Persist only the necessary structured report or auditable summary, tool
outcome, error/retry and timestamps in workflow state. Never store hidden
chain-of-thought, API secrets, credentials or unrelated sensitive user
context.

## 8. Validation and evaluation contract

Deterministic tests and evaluation fixtures must prove:

- correct destination/activity/offering/period resolution;
- distinction of draft/unpublished, unavailable, unscheduled, not found and
  operationally restricted states;
- correct structured report schema and required provenance;
- relevant, optional biodiversity lookup and explicit no-call behavior when
  it is not needed;
- genuine valid prediction metadata is preserved without certainty claims;
- unavailable, malformed, stale and out-of-range model responses are not
  fabricated or presented as current;
- tool allowlist and resource authorization are enforced, including
  unauthorized requests;
- prompt injection in objectives, descriptions, model content and tool output
  cannot alter role/tool/authorization boundaries;
- timeout and bounded-retry exhaustion record the expected status without a
  side effect; and
- downstream planner/operations validation can reliably reject invalid or
  unavailable required context.

The role must visibly contribute its structured report in the canonical
assessment and tourist recommendation golden cases. Live model/service runs
are reported separately from deterministic-fixture tests; an agent message
alone is not evidence of correctness.

## 9. Implementation decisions

Freeze the report schema/version, requiredness rules by workflow, available
catalogue reads, source freshness semantics, ML integration request/response,
model result caching/retention, retry/timeout policy and distinction of
unavailable vs optional vs failed outcomes in implementation contracts. This
document intentionally does not assign direct access to an internal ML route,
invent response property names, or set numeric model-confidence thresholds.

## 10. Related contracts

- Requirements: [sections 13, 20–30 and 40](../../../PROJECT_REQUIREMENTS.md).
- Owning component: [Coastal Experience & Biodiversity Discovery](../components/member-1-coastal-experience-biodiversity-discovery.md).
- Biodiversity adapter ownership: [Adithya Gunawardana (Member 3) component](../components/member-3-smart-coastal-planner-itinerary-management.md) and [ADR-0019](../../adr/ADR-0019-biodiversity-inference-integration-ownership.md).
- Shared behavior: [canonical workflows](../workflows.md), [requirements coverage and readiness](../requirements-coverage-and-readiness.md), [Agentic AI architecture](../../agentic-ai/architecture.md), [implementation blueprint](../../agentic-ai/implementation-blueprint.md), [tools](../../agentic-ai/tools.md), [safety](../../agentic-ai/safety.md), [evaluation](../../agentic-ai/evaluation.md).
