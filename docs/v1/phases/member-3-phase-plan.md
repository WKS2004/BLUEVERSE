# Adithya Gunawardana (Member 3) phase plan — Smart Coastal Planner & Itinerary Management

**Assigned owner:** Adithya Gunawardana (`@AdithyaGunawardana`). Feature
branch: `features/coastal-planner`; Agentic AI branch after G07:
`agentic-ai/planning-coordination`.

This plan divides the [Adithya Gunawardana (Member 3) component contract](../components/member-3-smart-coastal-planner-itinerary-management.md)
into work areas for one complete component branch. The
[component relationship map](../component-relationships.md) describes its
producer/consumer contracts, and the
[branch and integration workflow](../member-branch-workflow.md) defines
parallel work, pull requests and the G07 gate. Work areas are not separate
branches or PRs and do not schedule other members.

The [shared foundation and file-ownership rules](../member-branch-workflow.md#shared-foundation-and-file-ownership)
apply to every phase: keep this component additive, preserve existing API/Auth
flows, implement Adithya Gunawardana's business logic in its own `services/` microservice,
and limit `services/api` to integration code. Minimize shared React, Flutter,
route-registry and infrastructure edits.

Adithya Gunawardana (Member 3) owns planning requests, workflow/result identity, personalization
constraints, deterministic recommendation assembly, persisted itineraries,
re-evaluation and BLUEVERSE's backend-mediated adapter/public result contract
for the separately supplied IT3091 biodiversity inference service. Ushan Srinuka (Member 1)
owns the experience-facing presentation and consumes the validated Adithya Gunawardana (Member 3)
contract. The component includes ordinary application workflow state and ML
service integration; its LLM plan generation, delegation, tool use and
Agentic AI orchestration are deferred until after all four business
components pass G07.

## Component work areas

| Area | Work |
|---:|---|
| 1 | Planning request, workflow and itinerary foundation |
| 2 | Deterministic candidate eligibility and recommendation |
| 3 | Itinerary lifecycle, re-evaluation and both-client workflow |
| 4 | Backend-mediated biodiversity ML adapter and public result contract |
| 5 | Agentic backend boundary, component acceptance and handoff |

Use the single branch `features/coastal-planner` for all five work areas and
submit one complete feature PR to `dev`. Agree the Ushan Srinuka (Member 1) catalogue and
availability, Sanuda Abeysinghe (Member 2) suitability, and Wanshaja Sooriyabandara (Member 4) status contracts at G00.
Develop consumers against those contracts and controlled test doubles while
all component branches are in progress. Verify real provider/consumer
behavior on `dev` after the component PRs merge; fixtures alone are not
integration evidence.

The numbered phases below are contract/dependency work areas, not a required
implementation timeline or separate branch sequence. Implement all five as
one component on the single member branch; work areas may overlap where their
local technical dependencies permit. Use the component relationship map to
see cross-member producer/consumer dependencies.

## Phase 1 — Planning request, workflow and itinerary foundation

**Starts after:** the team agrees the shared workflow, resource and status
contracts at G00. Build the request/workflow/itinerary foundation on this
branch while Ushan Srinuka (Member 1), Sanuda Abeysinghe (Member 2) and Wanshaja Sooriyabandara (Member 4) implement their own components.

**Implement:**

- persisted planning request, validated objective/preferences/constraints,
  workflow ID/type/status, result version and timestamps;
- itinerary and item references to Ushan Srinuka's canonical IDs, with
  user/resource ownership and server-side permission semantics;
- public API contract for request creation, status/result retrieval and
  itinerary retrieval/initial management, including the future private
  Planning & Coordination integration seam and its not-connected status;
- shared state behavior for validation failure, duplicate request,
  cancellation/expiry if required, and recoverable dependency status; and
- one workflow identity that React and Flutter can both use to retrieve the
  same authorized status and result.

This is ordinary Adithya Gunawardana (Member 3) component-service behavior exposed through the
public API. It does not invoke an LLM, specialist agent, external provider, or
free-form model output. The service adapter and availability result prepare
access to the future planner;
they do not implement its runtime, plan generation or agent-owned execution
state. Define workflow state and persistence with the owning database/ADR
documentation. Keep the member-owned business request/status distinct from
the later Agentic AI plan/step execution state.

**Handoff:** stable request, status/result and itinerary reference schemas
that Wanshaja Sooriyabandara (Member 4) can link to assessment records and both clients can retrieve.

## Phase 2 — Deterministic candidate eligibility and recommendation

**Local dependencies and shared contracts:** build on work area 1 and use the
Ushan Srinuka (Member 1) effective-availability, Sanuda Abeysinghe (Member 2) deterministic-suitability and
Wanshaja Sooriyabandara (Member 4) operational-status contracts agreed at G00. Implement with
contract-level test doubles while provider branches are in progress; verify
actual provider APIs after PRs merge to `dev`.

**Implement:**

- validate objective, preferences and constraints on the server;
- obtain candidates only from Ushan Srinuka's published, valid and available
  destination/activity/offering results;
- preserve Sanuda Abeysinghe's deterministic result and exclude `UNSUITABLE`; express
  `UNKNOWN`, stale and missing required evidence as uncertainty or exclusion
  under the accepted policy;
- apply Wanshaja Sooriyabandara's authoritative current operational restrictions;
- document deterministic ranking, tie behavior and reason/source references;
- ensure retries, client formatting or later prose cannot reintroduce an
  excluded candidate; and
- persist result/status/history under the shared workflow identity.

No LLM summary or planner agent is part of this phase. The recommendation
must be independently useful and safe before agentic assembly is added.

**Handoff:** structured recommendation with eligibility, exclusions,
evidence references and uncertainty that can seed an itinerary and be
reviewed consistently by both clients.

## Phase 3 — Itinerary lifecycle, re-evaluation and clients

**Local dependency:** build on work area 2's recommendation and candidate
result contracts.

**Implement:**

- create an itinerary from an accepted recommendation or eligible selected
  items;
- add, remove, reorder and edit permitted schedule/context without replacing
  Ushan Srinuka, Sanuda Abeysinghe and Wanshaja Sooriyabandara source-of-truth data;
- provide accessible React date/time inputs and Flutter native date/time
  pickers for planning preferences and itinerary schedule edits; agree date,
  local time, duration, time-zone, daylight-saving and API serialization
  semantics before freezing the shared request contract;
- define ownership, duplicates, stale/invalid item, ordering, concurrent
  update and idempotency behavior;
- request re-evaluation when requested or when a defined source change makes
  it relevant; compare old/new results and explain removed, changed or
  uncertain items; and
- implement the complete planning, result/status, itinerary and
  re-evaluation journeys in both React and Flutter, including loading, empty,
  denied, stale and dependency-failure paths.

This is the planner's date/time input, not a notification/reminder system.
Follow the shared [device-capability contract](../device-capabilities.md).

Any confirmation required before updating a saved itinerary must be explicit
in the product/API contract. A saved itinerary is never a source of truth for
availability, safety or operations state.

**Handoff:** each authorized user can complete the same recommendation and
itinerary outcome from either client through the public API.

## Phase 4 — Backend-mediated biodiversity ML adapter and public result contract

**Starts after:** the G00 decision defines the Ushan Srinuka (Member 1) canonical location
reference and consumer need, Adithya Gunawardana's public/private boundaries, and the
IT3091 service request/result contract. Ushan Srinuka (Member 1) can implement its user-facing
presentation against the agreed result schema and controlled available/
unavailable fixtures while this adapter is in progress.

**Implement inside Adithya Gunawardana's component service, with only the required
public route/authorization integration in `services/api`, before G07:**

- a typed private adapter in Adithya Gunawardana's .NET service calls the IT3091
  inference service; no client or agent receives its private host or
  credentials;
- validate the minimum location/species/context needed for the query and
  associate each response with the request location and time;
- validate required fields, numeric ranges, timestamps, model/version and
  provenance before constructing the public result; preserve uncertainty and
  limitations supplied by the service;
- define explicit `available`, `not requested`, `unavailable` and invalid or
  equivalent documented states so no absent, malformed, stale or failed
  response becomes zero, a guess or a false current prediction;
- bound connection/read timeouts and retry policy, avoid unapproved cache or
  retention, minimize location precision and keep provider details/secrets
  server-side;
- expose the validated result through Adithya Gunawardana's (Member 3) public API capability that
  Ushan Srinuka (Member 1) can consume for its destination/activity experience. Name and
  register exact routes/DTOs only when implemented; do not add target routes
  to the live catalog in advance; and
- keep prediction context optional. It may enrich an experience or planning
  report, but cannot decide safety, eligibility, operational status or
  approval and cannot override Ushan Srinuka, Sanuda Abeysinghe and Wanshaja Sooriyabandara authoritative data.

This adapter is not an AI agent and does not implement an LLM, RAG pipeline,
prompt, agent tool execution or AI-owned workflow state. IT3091 supplies the
separate trained model and inference service. The real adapter path must
return a genuine model-backed result when that service is available;
fixtures provide deterministic contract and failure evidence but do not
replace the integration.

**Handoff:** Adithya Gunawardana (Member 3) provides validated sourced prediction context or the
documented unavailable/invalid state. Ushan Srinuka (Member 1) renders the public contract;
the future Ushan Srinuka (Member 1) agent may consume it only through a separately approved,
allowlisted integration after G07.

## Phase 5 — Agentic backend boundary, component acceptance and handoff

**Local dependency and shared boundary:** complete work areas 1–4 on this
branch and use the shared Agentic integration contract agreed at G00.

**Verify the Agentic AI connection boundary:**

- authorized workflow initiation/status/results use the Adithya Gunawardana (Member 3) public API;
- the private planner/orchestration adapter has server-side configuration,
  a bounded availability check and correlated dispatch outcome;
- the ordinary IT3091 prediction adapter and public result contract remain a
  separate Adithya Gunawardana (Member 3) service integration and are not used as the Agentic
  runtime's health signal;
- an absent or unreachable runtime is reported as not connected/unavailable
  without fabricating a plan, while deterministic recommendations and
  itineraries remain usable; and
- no production planning agent, prompts/model calls, specialist delegation,
  tool execution or AI-owned execution state is included before G07.

Follow [Member feature integration with Agentic AI](../agentic-ai-integration-boundary.md)
for the shared status, readiness and safe-failure contract.

**Verify and close:**

- meaningful relational data, migrations, at least four meaningful public
  operations and the re-evaluation non-CRUD operation;
- candidate exclusions for unpublished/unavailable, restricted,
  `UNSUITABLE`, unknown, stale and missing required evidence;
- itinerary ownership, duplicates, source changes, re-evaluation and
  concurrency behavior;
- shared workflow ID/status/result semantics and parity across React and
  Flutter;
- catalog/UI registry, OpenAPI, database/ER documentation, tests and Git/PR
  evidence.

**Adithya Gunawardana (Member 3) exit:** deterministic domain behavior and the genuine IT3091
integration are accepted at G07. The Planning & Coordination Agent begins
only after all four member components pass G07; its schema and orchestration
work follow its assigned `agentic-ai/planning-coordination` branch.

## Progress record

Update the Adithya Gunawardana (Member 3) row in the
[component branch status tracker](../member-branch-workflow.md#component-branch-status)
with branch/PR/merge status and integration evidence. Record work-area
milestones in the PR or the team's agreed contribution record. The canonical
[owner map](../../project/ai-team-members.md) records the exact identity and
branches.
