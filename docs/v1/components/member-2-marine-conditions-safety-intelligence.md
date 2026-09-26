---
contract_id: v1.component.marine-safety
contract_type: business_component
release: v1
implementation_status: target_not_implemented
owner_label: member_2
requirements: "PROJECT_REQUIREMENTS.md sections 14, 15, 20-27, 31, 53"
non_crud_operation: deterministic_activity_suitability_assessment
minimum_meaningful_public_api_endpoints: 4
agent_contract: "../agents/member-2-marine-conditions-intelligence-agent.md"
---

# Member 2 — Marine Conditions & Safety Intelligence

**Contract status:** v1 target specification; the complete v1 component is
not implemented in the current foundation. **Ownership label:** Member 2,
which is a requirements label and not a confirmed identity.

## 1. Purpose and user outcome

This component acquires and presents trustworthy weather and marine context
for coastal activities, retains enough provenance to judge its timeliness, and
calculates activity-specific environmental suitability using deterministic
application rules. It answers: what conditions are relevant to this activity,
place and time; where did the information come from; when was it forecast or
observed and retrieved; what is missing or stale; and what does the configured
rule set conclude?

It provides decision support for tourists, operators and reviewers. It is not
a marine navigation system, professional maritime forecast, official closure
authority or autonomous safety decision-maker.

### 1.1 Cross-layer implementation overview

| Layer | Component responsibility and implementation contract |
|---|---|
| **Universal product idea** | Acquire weather and marine source data for an activity, location and requested period; validate and normalize it with provenance/freshness; then apply configured application rules to determine `SUITABLE`, `CAUTION`, `UNSUITABLE` or insufficient evidence. Source data, deterministic classification and optional AI explanation remain distinct. The period is an ordinary condition-query input; a planner-originated request preserves the planner's validated itinerary period. |
| **React Web** | Provide the same authorized condition, suitability, profile-management and history outcomes as Flutter through the public API. Use React 19/TypeScript/Vite/React Router, reusable pages/components, Tailwind utilities and existing request/state separation. Accept the location and period needed for a condition query using ordinary workflow inputs, then render server classifications, source, units, time and gaps; do not recompute them in the browser. See the [React component contract](../../v0/components/react-web-client.md), [UI integration guide](../../development/ui-integration.md), and [React state ADR](../../adr/ADR-0005-react-state-management.md). |
| **Flutter Mobile** | Provide those same outcomes with native Dart/Material UI, using the repository's UI/logic/data layers, repository/API service and view-model pattern. The condition workflow accepts its location and period as ordinary business inputs; no distinct sensor or device feature is assigned to Member 2. The server's condition evidence, classification, permission outcome and warnings remain equivalent to React. Do not reclassify locally or change a planner-originated period. See the [Flutter component contract](../../v0/components/flutter-client.md), [UI integration guide](../../development/ui-integration.md), and [Flutter state ADR](../../adr/ADR-0006-flutter-state-management.md). |
| **ASP.NET Core and data** | The public API validates requests and permissions. Backend-owned provider adapters retrieve only required values, validate shape/units/ranges/timestamps, normalize them and preserve provenance. Application code applies the deterministic activity profile. EF Core/PostgreSQL may persist profiles, validated snapshots, assessment history and audit data according to the accepted schema; cache-versus-persist is an implementation decision. |
| **Third-party integration** | Open-Meteo Weather and Marine APIs are Member 2's required v1 source and are called by the backend only. The integration must handle timeout, rate limits, schema/value errors, missing variables and stale data, minimize location data, and keep provider details/secrets out of both clients. Requests, fields, units, caching and freshness rules remain explicit implementation decisions; a provider response never changes the configured safety profile. |
| **Paired Agentic AI role** | The future Marine Conditions Intelligence Agent may summarize validated, time-aware conditions and the existing deterministic suitability result through read-only allowlisted tools. Before G07, this branch prepares only the typed adapter and safe unavailable status. After G07 the agent may contextualize evidence but cannot fetch arbitrary network data, choose thresholds or change a classification. |
| **Component relationships** | Member 2 uses Member 1's canonical activity identity/taxonomy and destination location, supplies sourced conditions and suitability to Member 3's eligibility/ranking workflow, and supplies evidence to Member 4's assessment. Member 1 owns map-provider lookups; Member 2 consumes the canonical location data and does not call the map provider. Member 2 does not own operational restrictions. See the [producer/consumer relationship map](../component-relationships.md#producer-consumer-and-authority-map). |

All authorized actions and statuses use the server's role-to-permission
contract and the shared public API/UI workflow IDs. The table summarizes the
system boundary; the following sections specify detailed data semantics,
profiles, journeys, provider failure, acceptance and choices still open.

The shared [Agentic AI implementation blueprint](../../agentic-ai/implementation-blueprint.md)
defines common model, tool, retrieval, security, recovery and evaluation
requirements for this component's paired agent.

## 2. Ownership and hard boundary

Member 2 owns:

- backend-mediated retrieval from the selected v1 provider, Open-Meteo Weather
  and Marine APIs;
- normalization and reporting of relevant condition information;
- condition snapshots and their source, location, period, retrieval and
  freshness metadata;
- the configurable deterministic safety-profile inputs for relevant coastal
  activity types;
- the non-CRUD assessment of activity suitability for a specified location
  and time; and
- validation and reporting of the requested period as part of the ordinary
  weather/marine query. A planner-originated request uses Member 3's validated
  itinerary period; this does not create a separately assigned device
  capability.

The component does not own experience publication/availability (Member 1),
recommendation and itinerary planning (Member 3), operational state or human
approval (Member 4), or the public client boundary. The Marine Conditions AI
agent may summarize verified evidence; it is not the suitability engine.
Environmental inputs, deterministic suitability and any AI interpretation
must remain three distinguishable stages:

```text
Provider observations / forecasts
                 ↓
Validated, time-aware condition report
                 ↓
Application-owned deterministic suitability
                 ↓
Optional AI explanation or recommendation
```

On `features/marine-conditions-safety`, implement the authorized public
workflow/status contract and typed private backend adapter through which the
future Marine Conditions Intelligence Agent can consume only the validated,
minimal condition and suitability evidence it needs. Before G07, report
not connected or unavailable if the Agentic AI runtime is absent; never
produce a placeholder report or let the adapter change suitability rules.
Keep AI dependency health separate from API liveness, database readiness and
Open-Meteo provider health. Follow the shared
[member integration boundary](../agentic-ai-integration-boundary.md).

An AI model cannot invent physical thresholds or override a deterministic
`UNSUITABLE`, `UNKNOWN` or blocked outcome.

## 3. Users and permission behavior

Tourists and authorized operational users can inspect information relevant to
their workflow. A suitably permitted manager can configure safety profiles.
The exact role-to-permission mapping and permission codes remain server-side
implementation decisions. Neither React nor Flutter may grant profile-edit,
assessment or operational authority based on a hard-coded role name.

Both clients must display the same server-produced assessment and evidence to
callers with equivalent permissions. A client may format a chart or summary
for its screen, but it may not produce a competing suitability classification.

## 4. Data and domain concepts

These concepts define responsibility, not exact EF entities or table names.
The PostgreSQL model and API schema are to be designed and documented during
implementation.

| Concept | Required meaning |
|---|---|
| Condition query | The requested destination/location, relevant activity, forecast or observation interval, and only the variables needed by the invoking BLUEVERSE workflow. |
| Provider result | Open-Meteo response fields after validation and normalization, with the provider/source identity and enough original context to explain units and interpretation. |
| Condition snapshot | A point-in-time record or retrievable report that identifies requested location, forecast/observation time, retrieval time, freshness, relevant factors and unavailable fields. Exact persistence versus cache policy is a design decision. |
| Activity safety profile | Deterministic configuration for one or more activity types. Its criteria must have a defensible source, explicit units and version/effective context as needed. |
| Suitability assessment | A server-calculated result for activity + location + relevant period based on validated inputs and the applicable profile, with evidence, missing-data reasons and profile/source context. |
| Assessment history | Prior queries and outcomes visible to permitted callers, with enough context to distinguish results over time and support audit/re-evaluation. |

Potential condition factors, only where they serve a real workflow, include
weather condition, wind, rainfall, wave height/direction/period, swell,
sea-surface temperature and ocean-current information. The implementation
must not request or persist unrelated fields simply because the provider
offers them.

## 5. Data-quality and time semantics

Every displayed or consumed report must preserve, where applicable:

- the provider/source;
- the requested destination or coordinates;
- whether the value is a forecast or observation;
- the forecast/observation time or interval;
- the time the backend retrieved it;
- the configured freshness assessment and its basis; and
- each expected but unavailable, invalid or unsupported field.

Forecast time and retrieval time are different facts. “Current” must not mean
merely “most recently retrieved.” The technical contract must define time-zone
normalization, interval boundaries, freshness windows by use case, and how
provider revisions affect a cached snapshot. A stale result may be useful as
history but must not silently pass as current. Preserve units and normalize
only through an explicit, tested conversion. Unrecognized units, malformed
timestamps, out-of-range values or provider schema changes cannot be treated
as valid evidence.

## 6. Deterministic suitability operation

The required business operation evaluates a specified activity, location and
relevant time using configured application rules. Illustrative result states
are `SUITABLE`, `CAUTION`, `UNSUITABLE` and `UNKNOWN`; implementation may refine
their names but must preserve their meaning.

The rule evaluator must:

1. identify the applicable activity profile and its version/effective
   configuration;
2. verify that required provider data exists, is valid and meets freshness
   requirements for that profile;
3. evaluate factors using documented units, thresholds and combination
   logic;
4. return a deterministic classification plus the factors/rules responsible
   for the result; and
5. use `UNKNOWN` or a documented equivalent when evidence is insufficient to
   support a positive classification.

`CAUTION` and `UNSUITABLE` must have distinct, documented meanings. A missing
required factor cannot be converted into a safe value. If a profile changes,
the implementation must define how old assessments remain interpretable and
whether they are recalculated. The LLM may explain a result in plain language
but cannot change the input data, threshold or result.

### Safety profile management

Profile criteria are configured by an authorized person through a permission-
gated server workflow. The source and unit of every criterion must be clear;
changes need validation and appropriate history. The implementation must
define default behavior for an activity with no profile, profile conflicts,
effective dates, and whether a profile can be activated without review. Do not
invent live numeric thresholds in this target document.

## 7. Public API capability contract

The component must have at least four meaningful public API endpoints and the
suitability assessment as a business operation beyond CRUD. The capability
list below does not prescribe URL paths. Exact routes, methods, DTOs,
permission codes, status codes and pagination conventions belong in the
implementation. Add implemented routes to the
[endpoint catalog](../../api/endpoint-catalog.md), under `/api/...` and with
no version path segment.

The public operations need to cover the following capabilities:

- obtain relevant current/forecast marine and weather information for a
  destination or location, activity and requested time;
- inspect a condition snapshot/history and its source, timestamps, freshness,
  missing fields and warnings;
- run deterministic activity suitability assessment and retrieve its
  supporting factors/result;
- search/filter/sort/page relevant condition or assessment history where
  appropriate; and
- read and permission-manage activity-specific safety profiles.

Open-Meteo access remains backend-mediated. The client and agents do not use a
provider API key or arbitrary URL. Every user-facing screen/workflow must be
registered in the shared [UI integration registry](../../contracts/ui-integration.json).

## 8. React and Flutter user experience

Both clients must support the complete authorized Component B workflow:

| User task | Required experience in both clients |
|---|---|
| Select context | Choose or navigate from a destination/activity and submit the location and relevant period through the ordinary query workflow. When the request comes from Member 3, preserve its validated itinerary period instead of asking the user to set a competing one. Invalid, unsupported or incomplete inputs receive actionable feedback. |
| Read conditions | See relevant weather/marine factors with units, forecast/observation time, retrieval time, source and freshness. |
| Understand gaps | Identify unavailable variables, stale information, provider outage, and the effect those gaps have on the assessment. |
| Read suitability | See the backend classification and meaningful explanation of contributing factors; clients do not reclassify. |
| Inspect history | Review permitted past assessments and distinguish them from current evidence. |
| Manage profile | Users with the required permission can inspect and change profiles through validated controls. Other callers cannot mutate them. |
| Navigate | Move between destination/activity detail and related marine information without losing relevant context. |

Search, filters, sorting and pagination should be provided where they improve
history and profile management. Responsive layouts may differ. Both clients
must preserve the same facts, warnings, classification, permission behavior
and recovery choices. The requested condition period remains part of the
normal query contract. It is not a separate device capability, and a
planner-originated period is preserved without a competing input.

## 9. Agent and component handoffs

| Consumer | Contract provided by Member 2 |
|---|---|
| Coastal Experience & Biodiversity Agent | Activity context may identify which suitability evidence applies, but this agent does not own environmental classification. |
| Planning & Coordination Agent / planner | Structured condition report and deterministic suitability for candidate activities and requested periods. |
| Safety & Operations Agent | Sourced marine evidence, freshness/gaps, and a deterministic result that the operations recommendation must not contradict. |
| Human-facing React/Flutter workflows | The same public API decision and evidence, including warnings and result time. |

The dedicated [Marine Conditions Intelligence Agent](../agents/member-2-marine-conditions-intelligence-agent.md)
retrieves and interprets this context under allowlisted tools. The deterministic
suitability evaluator is ordinary application logic, not a fifth agent or an
LLM prompt. The agent can contextualize a result but must not set the profile,
edit a threshold or authorize operations.

## 10. Provider failure and security behavior

Handle provider timeout, service outage, rate limitation, invalid response,
schema drift, unsupported or unavailable variables, invalid coordinates,
missing periods and stale data. The system should use bounded retry only where
safe and useful. After recovery is exhausted, return an explicit failure or
`UNKNOWN` outcome as appropriate, retain no invented condition, and avoid any
protected side effect. Failed attempts and relevant source/error metadata may
be audited without storing secrets.

Provider content is untrusted input. Validate shape, units, ranges, location
and timestamps before persistence or assessment. External content cannot
change application instructions, grant a tool, bypass authorization or
approval, or authorize an operation. Secrets and hidden AI reasoning are
never stored. Present a plain-language decision-support limitation so users
do not treat BLUEVERSE as professional navigation information.

Before calling Open-Meteo, validate and minimize the location and time data
shared with that provider. Do not send account identity, favourites,
itinerary notes or other personal information unrelated to the forecast.
Document any coordinate precision and retention choice in the integration
contract, and keep provider credentials out of clients and Git.

## 11. Acceptance and evidence checklist

The implementation must produce evidence for each concern below:

- Open-Meteo weather and marine access is mediated through backend services;
- requests include only variables needed by an actual v1 workflow;
- provider responses preserve source, units, requested location, forecast or
  observation time and backend retrieval time;
- malformed timestamps, values, units, missing factors, invalid locations,
  timeout, rate limitation, outage and schema changes are handled safely;
- stale and historical data cannot be confused with a fresh decision input;
- each activity profile has documented criteria and validated configuration;
- configured rules produce independently testable SUITABLE, CAUTION,
  UNSUITABLE and insufficient-evidence outcomes;
- missing profiles and missing required fields cannot yield a fabricated
  positive suitability result;
- changing an LLM explanation cannot alter suitability or protected state;
- profile reads/changes, history and reports respect public API permissions;
- React and Flutter submit equivalent direct-query periods, reject unsupported
  bounds without silently changing the request, and preserve a planner-
  supplied candidate period without opening a competing selector;
- React and Flutter show the same server outcome, source and limitations;
- the Marine Conditions agent uses its distinct read-only contract; and
- route catalog, UI registry, implementation, migrations, tests and docs agree.

The Member 2 owner must retain attributable ASP.NET Core, PostgreSQL/EF Core,
React, Flutter, agent, test, documentation and Git/PR evidence. Include a
genuine Open-Meteo integration result when the provider is available,
controlled provider fixtures for error paths, and real PostgreSQL evidence
for database-specific rules. The owner should be able to explain the
profile criteria and demonstrate the non-CRUD suitability assessment.

## 12. Decisions to finalize during implementation

Exact endpoint paths/methods, provider-client library, query/cache strategy,
persist-versus-cache rules, coordinate precision and radius, units and
conversion policy, time-zone handling, activity profile schema, evidence
thresholds and defensible source, freshness limits, no-profile behavior,
classification vocabulary, profile versioning, history retention, and
provider retry/rate policies must be decided in implementation documentation
and tested. Numeric safety thresholds are deliberately not specified here.

## 13. Traceability

- Requirements: [sections 14, 15, 20–27, 31 and 53](../../../PROJECT_REQUIREMENTS.md).
- Paired AI role: [Marine Conditions Intelligence Agent](../agents/member-2-marine-conditions-intelligence-agent.md).
- Component work areas on one member branch: [Member 2 phase plan](../phases/member-2-phase-plan.md); producer/consumer relationships: [component relationship map](../component-relationships.md); PR and G07 process: [member branch workflow](../member-branch-workflow.md).
- Related contracts: [shared v1 workflows](../workflows.md), [member Agentic AI integration boundary](../agentic-ai-integration-boundary.md), [permissions and client parity](../cross-platform-and-permissions.md), [quality and delivery](../quality-and-delivery.md), [requirements coverage and readiness](../requirements-coverage-and-readiness.md), [Agentic AI safety](../../agentic-ai/safety.md), [tool contract](../../agentic-ai/tools.md), [endpoint catalog](../../api/endpoint-catalog.md), [UI integration](../../contracts/ui-integration.json).
