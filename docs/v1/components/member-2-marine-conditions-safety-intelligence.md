---
contract_id: v1.component.marine-safety
contract_type: business_component
release: v1
implementation_status: backend_and_react_web_implemented_on_feature_branch
owner_label: member_2
owner_full_name: "Sanuda Abeysinghe"
owner_github_username: "sanudaabey"
feature_branch: "features/marine-safety"
agentic_ai_branch: "agentic-ai/marine-conditions"
requirements: "PROJECT_REQUIREMENTS.md sections 14, 15, 20-27, 31, 53"
non_crud_operation: deterministic_activity_suitability_assessment
minimum_meaningful_public_api_endpoints: 4
agent_contract: "../agents/member-2-marine-conditions-intelligence-agent.md"
---

# Sanuda Abeysinghe (Member 2) — Marine Conditions & Safety Intelligence

**Implementation status (2026-10-03):** the marine backend and React Web
workflows are implemented on `features/marine-safety`. The latest verification
passed all 117 marine service tests and all 198 React tests, including 41
marine cases. React lint and the shared UI-integration validator passed.
Backend tests use an in-memory database and deterministic Auth/provider
doubles; they do not verify the production Auth-table SQL against PostgreSQL.
Earlier live provider/PostgreSQL evidence is recorded in the G00 history.
Flutter and executable Agentic AI remain deferred; the component PR/merge and
G07 integration are not recorded.
**Assigned owner:** Sanuda Abeysinghe (`@sanudaabey`) — frozen-requirements
trace label Member 2. Feature branch: `features/marine-safety`; paired
Agentic AI branch: `agentic-ai/marine-conditions`.

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
| **React Web** | Implemented on this branch. `apps/web` provides condition lookup, server suitability, profile management and history through the public API: `/marine/conditions`, `/marine/history` and `/marine/safety-profiles` (React 19/TypeScript/Vite/React Router, reusable pages/components, Tailwind utilities, existing request/state separation). The condition query accepts location, optional requested UTC period and activity as ordinary workflow inputs, then renders server classifications, source, units, time and gaps; the browser never recomputes them. Registered under the `marine-conditions`, `marine-condition-history` and `marine-safety-profile-management` shared workflow IDs; see the [React component contract](../../v0/components/react-web-client.md), [UI integration guide](../../development/ui-integration.md), and [React state ADR](../../adr/ADR-0005-react-state-management.md). Flutter routes are registered under the same workflow IDs but the mobile surface is not implemented yet. |
| **Flutter Mobile** | Provide those same outcomes with native Dart/Material UI, using the repository's UI/logic/data layers, repository/API service and view-model pattern. The condition workflow accepts its location and period as ordinary business inputs; no distinct sensor or device feature is assigned to Sanuda Abeysinghe (Member 2). The server's condition evidence, classification, permission outcome and warnings remain equivalent to React. Do not reclassify locally or change a planner-originated period. See the [Flutter component contract](../../v0/components/flutter-client.md), [UI integration guide](../../development/ui-integration.md), and [Flutter state ADR](../../adr/ADR-0006-flutter-state-management.md). |
| **Sanuda Abeysinghe (Member 2) .NET service and data** | The implemented internal ASP.NET Core service is `services/marine-safety` (`Blueverse.MarineSafety`, Compose service `marine-safety`, container `blueverse-marine-safety`). It owns Open-Meteo acquisition, provider-response validation/normalization, deterministic suitability, condition/profile history, domain persistence and audit data. Its EF Core migration is `InitialMarineSafetySchema`. `services/api` forwards `/api/marine/{**catch-all}` to `http://marine-safety:8080`; it contains none of the component's condition logic or provider calls. Clients never call the member service or Open-Meteo directly. These identifiers and the public/private route mapping are frozen in the [Member 2 G00 record](../g00/member-2-marine-safety-decisions.md). |
| **Third-party integration** | Open-Meteo Weather and Marine APIs are the required source and are called by the backend only. The G00 record freezes the requested variables (`wind_speed_10m`, `precipitation`, `weather_code`, `wave_height`, `swell_wave_height`), UTC handling, nullable missing-field representation, snapshot persistence/reuse, a 60-minute default freshness limit, one transient-only retry and a 10-second per-attempt timeout. Provider responses never change the configured safety profile. Numeric threshold provenance and defensible profile criteria remain owner decisions; test fixtures do not count as live provider evidence. |
| **Paired Agentic AI role** | The future Marine Conditions Intelligence Agent may summarize validated, time-aware conditions and the existing deterministic suitability result through read-only allowlisted tools. The accepted Member 2 G00 decision defers the AI workflow and typed adapter; neither is implemented on this branch. After G07 the agent may contextualize evidence but cannot fetch arbitrary network data, choose thresholds or change a classification. |
| **Component relationships** | Sanuda Abeysinghe (Member 2) uses Ushan Srinuka's canonical activity identity/taxonomy and destination location, supplies sourced conditions and suitability to Adithya Gunawardana's eligibility/ranking workflow, and supplies evidence to Wanshaja Sooriyabandara's assessment. Ushan Srinuka (Member 1) owns map-provider lookups; Sanuda Abeysinghe (Member 2) consumes the canonical location data and does not call the map provider. Sanuda Abeysinghe (Member 2) does not own operational restrictions. See the [producer/consumer relationship map](../component-relationships.md#producer-consumer-and-authority-map). |

All authorized actions and statuses use the server's role-to-permission
contract and the shared public API/UI workflow IDs. The table summarizes the
system boundary; the following sections specify detailed data semantics,
profiles, journeys, provider failure, acceptance and remaining open decisions.

Implement this component within the [v1 shared-foundation and file-ownership
rules](../member-branch-workflow.md#shared-foundation-and-file-ownership):
keep Sanuda Abeysinghe's business behavior in its own internal service, preserve
existing API/Auth flows, and limit `services/api`, shared client, registry and
infrastructure edits to the exact integration entries this component needs.

The shared [Agentic AI implementation blueprint](../../agentic-ai/implementation-blueprint.md)
defines common model, tool, retrieval, security, recovery and evaluation
requirements for this component's paired agent.

## 2. Ownership and hard boundary

Sanuda Abeysinghe (Member 2) owns:

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
  weather/marine query. A planner-originated request uses Adithya Gunawardana's validated
  itinerary period; this does not create a separately assigned device
  capability.

The component does not own experience publication/availability (Ushan Srinuka (Member 1)),
recommendation and itinerary planning (Adithya Gunawardana (Member 3)), operational state or human
approval (Wanshaja Sooriyabandara (Member 4)), or the public client boundary. The Marine Conditions AI
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

The accepted Member 2 G00 decision explicitly defers the public AI
workflow/status contract and typed private backend adapter for the future
Marine Conditions Intelligence Agent. If a later accepted scope decision
reintroduces that pre-G07 work, expose only validated, minimal condition and
suitability evidence. Report not connected or unavailable if the Agentic AI
runtime is absent; never produce a placeholder report or let the adapter
change suitability rules.
Keep AI dependency health separate from API liveness, database readiness and
Open-Meteo provider health. Follow the shared
[member integration boundary](../agentic-ai-integration-boundary.md).

An AI model cannot invent physical thresholds or override a deterministic
`UNSUITABLE`, `UNKNOWN` or blocked outcome.

## 3. Users and permission behavior

Tourists and authorized operational users can inspect information relevant to
their workflow. A suitably permitted manager can configure safety profiles.
The marine service enforces the Auth-owned `marine.profile.read` and
`marine.profile.manage` permission codes through the role-to-permission model.
Profile and activity changes require both grants; reads and suitability
assessment require `marine.profile.read`. Auth seeds the grants for the Admin
role. Neither React nor Flutter, nor the marine service, grants authority by
checking a hard-coded role name.

The service accepts an Auth-issued JWT from an `Authorization: Bearer` header
or the Auth browser cookies, preferring an explicit bearer header when both
are present. When `blueverse_active_account_id` is present, it selects
`blueverse_access_token_<userIdN>`; the legacy
`blueverse_access_token` is accepted only when its subject matches that
selected account. A malformed selection fails closed. When no account is
selected, the legacy cookie remains supported for single-account clients. It
validates the shared signing key, issuer, audience, HS256 algorithm and token
lifetime. It also checks the current Auth account and active session against
`token_version`, `session_id`, `session_version` and session expiry. Every
protected operation resolves its permission from the current Auth role
assignments; JWT role and permission claims are informational snapshots and
never grant access. Expired or revoked identity sessions receive 401, while an
active caller without the required grant receives 403. Auth remains the sole
owner and writer of identity data.

The marine HTTP test host uses a deterministic permission resolver, so it
does not exercise the production Auth-table SQL; a PostgreSQL-backed
integration run is still needed for that evidence. See the [database schema
and test boundary](../../database/schema.md).

Both clients must display the same server-produced assessment and evidence to
callers with equivalent permissions. A client may format a chart or summary
for its screen, but it may not produce a competing suitability classification.

## 4. Data and domain concepts

These concepts define responsibility. The implemented PostgreSQL entities,
constraints and API contract are documented in [database schema](../../database/schema.md),
the [G00 decisions](../g00/member-2-marine-safety-decisions.md) and the
[endpoint catalog](../../api/endpoint-catalog.md). Any future schema changes
must be made in the owning service migration and reflected in those sources.

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

The component has more than four meaningful public API operations, including
the suitability assessment as a business operation beyond CRUD. Exact routes,
methods, DTOs, permissions, status codes and pagination are frozen in the
[G00 decision record](../g00/member-2-marine-safety-decisions.md) and listed
in the [endpoint catalog](../../api/endpoint-catalog.md). The capability list
below states the product scope. Keep implemented routes in the
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
| Select context | Choose or navigate from a destination/activity and submit the location and relevant period through the ordinary query workflow. When the request comes from Adithya Gunawardana (Member 3), preserve its validated itinerary period instead of asking the user to set a competing one. Invalid, unsupported or incomplete inputs receive actionable feedback. |
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

| Consumer | Contract provided by Sanuda Abeysinghe (Member 2) |
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

The Sanuda Abeysinghe (Member 2) owner must retain attributable ASP.NET Core, PostgreSQL/EF Core,
React, Flutter, agent, test, documentation and Git/PR evidence. Include a
genuine Open-Meteo integration result when the provider is available,
controlled provider fixtures for error paths, and real PostgreSQL evidence
for database-specific rules. The owner should be able to explain the
profile criteria and demonstrate the non-CRUD suitability assessment.

## 12. Decisions and remaining evidence

G00 and the implementation have frozen the service identity, API and gateway
routes, permission codes, UTC semantics, requested provider variables, units,
freshness and snapshot-reuse rules, suitability vocabulary, profile versioning,
error behavior, and bounded provider retry/timeout policy. See the [G00
decision record](../g00/member-2-marine-safety-decisions.md),
[database schema](../../database/schema.md) and [endpoint
catalog](../../api/endpoint-catalog.md); do not describe those choices as
open. The defensible source and approval process for numeric safety criteria
remain to be established, and the local activity references must map to
Member 1's canonical taxonomy when that service is integrated. Latest auth
SQL verification against PostgreSQL, Flutter parity, cross-component
verification on `dev`, the component PR/merge and G07 remain unrecorded gates.
The contract intentionally does not invent numeric safety thresholds.

## 13. Traceability

- Requirements: [sections 14, 15, 20–27, 31 and 53](../../../PROJECT_REQUIREMENTS.md).
- Paired AI role: [Marine Conditions Intelligence Agent](../agents/member-2-marine-conditions-intelligence-agent.md).
- Component work areas on one member branch: [Sanuda Abeysinghe (Member 2) phase plan](../phases/member-2-phase-plan.md); producer/consumer relationships: [component relationship map](../component-relationships.md); PR and G07 process: [member branch workflow](../member-branch-workflow.md).
- Related contracts: [shared v1 workflows](../workflows.md), [member Agentic AI integration boundary](../agentic-ai-integration-boundary.md), [permissions and client parity](../cross-platform-and-permissions.md), [quality and delivery](../quality-and-delivery.md), [requirements coverage and readiness](../requirements-coverage-and-readiness.md), [Agentic AI safety](../../agentic-ai/safety.md), [tool contract](../../agentic-ai/tools.md), [endpoint catalog](../../api/endpoint-catalog.md), [UI integration](../../contracts/ui-integration.json).
