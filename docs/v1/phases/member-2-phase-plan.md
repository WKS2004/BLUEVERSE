# Sanuda Abeysinghe (Member 2) phase plan — Marine Conditions & Safety Intelligence

**Assigned owner:** Sanuda Abeysinghe (`@sanudaabey`). Feature branch:
`features/marine-safety`; Agentic AI branch after G07:
`agentic-ai/marine-conditions`.

This plan divides the [Sanuda Abeysinghe (Member 2) component contract](../components/member-2-marine-conditions-safety-intelligence.md)
into work areas for one complete component branch. The
[component relationship map](../component-relationships.md) describes its
producer/consumer contracts, and the
[branch and integration workflow](../member-branch-workflow.md) defines
parallel work, pull requests and the G07 gate. Work areas are not separate
branches or PRs and do not schedule other members.

The [shared foundation and file-ownership rules](../member-branch-workflow.md#shared-foundation-and-file-ownership)
apply to every phase: keep this component additive, preserve existing API/Auth
flows, implement Sanuda Abeysinghe's business logic in its own `services/` microservice,
and limit `services/api` to integration code. Minimize shared React, Flutter,
route-registry and infrastructure edits.

Sanuda Abeysinghe (Member 2) owns backend-mediated weather/marine acquisition, normalized
condition data and history, provenance/freshness, activity safety profiles,
and deterministic suitability. The Marine Conditions Intelligence Agent is
a separate post-G07 feature. It cannot choose thresholds or override the
application's deterministic result.

## Component work areas

| Area | Work |
|---:|---|
| 1 | Open-Meteo acquisition and normalized condition contract |
| 2 | Safety profiles and deterministic suitability |
| 3 | Condition, profile and suitability surfaces in both clients |
| 4 | Agentic backend boundary, consumer verification and component closeout |

Use the single branch `features/marine-safety` for all four work
areas and submit one complete feature PR to `dev`. Agree the activity,
condition and suitability contracts at G00, implement against those contracts
while the other component branches are in progress, then verify live
provider/consumer behavior on `dev` after the component PRs merge. Provider
access, marine data, suitability and future Agentic AI access remain behind
ASP.NET Core; neither client calls Open-Meteo, a model provider or an internal
service.

The numbered phases below are contract/dependency work areas, not a required
implementation timeline or separate branch sequence. Implement all four as
one component on the single member branch; work areas may overlap where their
local technical dependencies permit. Use the component relationship map to
see cross-member producer/consumer dependencies.

## Phase 1 — Open-Meteo acquisition and normalized condition contract

**Starts after:** the team agrees the shared IDs, activity reference and
provider boundary at G00. Acquisition and normalization can proceed in
parallel with all other component branches; activity-specific interpretation
uses the agreed Ushan Srinuka (Member 1) taxonomy contract without waiting for its code.

**Implement:**

- a server-side adapter inside Sanuda Abeysinghe's internal .NET service for only the
  Weather and Marine API variables needed by a real v1 workflow;
- validated location, activity reference and forecast/observation interval
  handling, with no unrelated account, favourite or itinerary data sent to
  the provider;
- normalized units and time representation while retaining provider/source,
  requested location, forecast/observation time and backend retrieval time;
- explicit missing, unsupported, stale, malformed, rate-limited, timed-out
  and provider-unavailable results;
- a documented decision on snapshot persistence versus caching, freshness,
  bounded retry and provider limits; and
- a versionable public application contract and persistence/history behavior
  that does not expose provider hostnames or credentials to clients.

Do not choose profile thresholds in this phase. Preserve enough validated
source evidence for the later suitability evaluator and both clients.

**Handoff:** publish the normalized condition query/result schema and its
freshness and missing-data meaning. Adithya Gunawardana (Member 3) and Wanshaja Sooriyabandara (Member 4) may use controlled
fixtures until the production API is accepted.

## Phase 2 — Safety profiles and deterministic suitability

**Local dependency and shared contract:** build on work area 1 and use the
Ushan Srinuka (Member 1) activity taxonomy agreed at G00. Contract tests can run while the
Ushan Srinuka (Member 1) branch is still in progress.

**Implement:**

- versioned activity safety profiles with criterion provenance, units,
  effective dates and permission-checked management;
- required/optional condition factors, freshness rules, combination logic,
  no-profile behavior and deterministic handling of absent evidence;
- the non-CRUD assessment operation returning a classification and the
  factors/evidence that produced it;
- `UNKNOWN` or an equally explicit unresolved result when evidence is
  insufficient; missing values must never default to safe; and
- persistence/history sufficient to explain a prior result after a profile
  or source-data change.

Numeric criteria must be configured and supported by defensible sources;
examples in prose do not establish thresholds. A language model must not
select or alter them.

**Handoff:** give Adithya Gunawardana and Wanshaja Sooriyabandara an assessment tied to activity, location,
requested period, source/freshness, missing factors, profile version and
deterministic result.

## Phase 3 — Condition, profile and suitability surfaces

**Local dependency:** build on work areas 1 and 2. The clients and API may be
developed together on this branch against their reviewed schemas; verify
their integrated behavior before the complete component PR is ready.

**Implement in both clients:**

- select the relevant destination/activity/location/time and inspect current
  or forecast conditions using the ordinary condition-query inputs. When the
  query comes from a planner candidate, preserve Adithya Gunawardana's validated period
  instead of asking for a competing period;
- show actionable feedback when the requested period is unsupported by the
  validated API/provider contract; and
- show units, source, observation/forecast time, retrieval time, freshness,
  missing fields and stale/unavailable warnings;
- show only the server-calculated suitability result and its contributing
  factors; and
- allow authorized profile management and condition/assessment history with
  clear validation, empty, denied and failure states.

React and Flutter may arrange the views differently. Values, classification,
permission behavior, evidence and uncertainty must have the same meaning.
The condition-query period is ordinary business input, distinct from Adithya
Gunawardana's (Member 3) planning and itinerary schedule; it is not a separate
device-feature assignment for Sanuda Abeysinghe (Member 2).

**Handoff:** the same public API outcome and recovery choice are usable in
both clients, and the route/API registry metadata agrees with the screens.

## Phase 4 — Agentic backend boundary, consumer verification and closeout

**Shared integration relationship:** prepare this component's typed future-AI
access boundary and verify consumer-facing contracts against the schemas
agreed at G00. Adithya Gunawardana (Member 3) and Wanshaja Sooriyabandara (Member 4) develop their consumers on their own
branches; prove live provider-consumer integration on `dev` after the feature
PRs merge. Their completion is not a prerequisite for this branch.

**Implement and verify the Agentic AI integration boundary:**

- a public Sanuda Abeysinghe (Member 2) workflow initiation/status contract with permission,
  validation, business request identity and references to normalized
  condition/suitability evidence;
- a typed private adapter for the future Marine Conditions specialist report,
  configured server-side and restricted to the validated weather/marine
  evidence needed by that role;
- a server-side availability check and safe `not connected`/unavailable
  result for an absent, unreachable or timing-out AI dependency; and
- no production agent, model call, tool execution, agent-selected threshold
  or output that overrides the deterministic suitability service before G07.

Follow [Member feature integration with Agentic AI](../agentic-ai-integration-boundary.md)
for shared states, health semantics and acceptance evidence.

**Verify and close:**

- condition source, units, requested location, timestamps, freshness, missing
  values and profile version survive each consumer handoff;
- `UNSUITABLE` and `UNKNOWN` are not turned into permissive results by
  recommendation, proposal, retry or client formatting;
- provider timeout/rate limit/schema change, malformed values, missing
  factors, stale history and retry exhaustion have deterministic evidence;
- React and Flutter render equal authorized actions and identical server
  decisions; and
- migrations, database/provider evidence, tests, routes, API docs and PR
  handoff records satisfy the component contract.

**Sanuda Abeysinghe (Member 2) exit:** the deterministic service and both client surfaces are
complete before G07. The Sanuda Abeysinghe (Member 2) specialist agent remains deferred until
after G07.

## Progress record

Update the Sanuda Abeysinghe (Member 2) row in the
[component branch status tracker](../member-branch-workflow.md#component-branch-status)
with branch/PR/merge status and integration evidence. Record work-area
milestones in the PR or the team's agreed contribution record. The canonical
[owner map](../../project/ai-team-members.md) records the exact identity and
branches.
