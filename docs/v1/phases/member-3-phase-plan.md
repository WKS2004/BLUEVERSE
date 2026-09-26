# Member 3 phase plan — Smart Coastal Planner & Itinerary Management

This plan divides the [Member 3 component contract](../components/member-3-smart-coastal-planner-itinerary-management.md)
into work areas for one complete component branch. The
[component relationship map](../component-relationships.md) describes its
producer/consumer contracts, and the
[branch and integration workflow](../member-branch-workflow.md) defines
parallel work, pull requests and the G07 gate. Work areas are not separate
branches or PRs and do not schedule other members.

Member 3 owns planning requests, workflow/result identity, personalization
constraints, deterministic recommendation assembly, persisted itineraries
and re-evaluation. The component includes ordinary application workflow
state; its LLM plan generation, delegation, tool use and Agentic AI
orchestration are deferred until after all four business components pass G07.

## Component work areas

| Area | Work |
|---:|---|
| 1 | Planning request, workflow and itinerary foundation |
| 2 | Deterministic candidate eligibility and recommendation |
| 3 | Itinerary lifecycle, re-evaluation and both-client workflow |
| 4 | Agentic backend boundary, component acceptance and handoff |

Use the single branch `features/coastal-planner` for all four work areas and
submit one complete feature PR to `dev`. Agree the Member 1 catalogue and
availability, Member 2 suitability, and Member 4 status contracts at G00.
Develop consumers against those contracts and controlled test doubles while
all component branches are in progress. Verify real provider/consumer
behavior on `dev` after the component PRs merge; fixtures alone are not
integration evidence.

The numbered phases below are contract/dependency work areas, not a required
implementation timeline or separate branch sequence. Implement all four as
one component on the single member branch; work areas may overlap where their
local technical dependencies permit. Use the component relationship map to
see cross-member producer/consumer dependencies.

## Phase 1 — Planning request, workflow and itinerary foundation

**Starts after:** the team agrees the shared workflow, resource and status
contracts at G00. Build the request/workflow/itinerary foundation on this
branch while Member 1, Member 2 and Member 4 implement their own components.

**Implement:**

- persisted planning request, validated objective/preferences/constraints,
  workflow ID/type/status, result version and timestamps;
- itinerary and item references to Member 1's canonical IDs, with
  user/resource ownership and server-side permission semantics;
- public API contract for request creation, status/result retrieval and
  itinerary retrieval/initial management, including the future private
  Planning & Coordination integration seam and its not-connected status;
- shared state behavior for validation failure, duplicate request,
  cancellation/expiry if required, and recoverable dependency status; and
- one workflow identity that React and Flutter can both use to retrieve the
  same authorized status and result.

This is ordinary ASP.NET Core application behavior. It does not invoke an
LLM, specialist agent, external provider, or free-form model output. The
backend adapter and availability result prepare access to the future planner;
they do not implement its runtime, plan generation or agent-owned execution
state. Define workflow state and persistence with the owning database/ADR
documentation. Keep the member-owned business request/status distinct from
the later Agentic AI plan/step execution state.

**Handoff:** stable request, status/result and itinerary reference schemas
that Member 4 can link to assessment records and both clients can retrieve.

## Phase 2 — Deterministic candidate eligibility and recommendation

**Local dependencies and shared contracts:** build on work area 1 and use the
Member 1 effective-availability, Member 2 deterministic-suitability and
Member 4 operational-status contracts agreed at G00. Implement with
contract-level test doubles while provider branches are in progress; verify
actual provider APIs after PRs merge to `dev`.

**Implement:**

- validate objective, preferences and constraints on the server;
- obtain candidates only from Member 1's published, valid and available
  destination/activity/offering results;
- preserve Member 2's deterministic result and exclude `UNSUITABLE`; express
  `UNKNOWN`, stale and missing required evidence as uncertainty or exclusion
  under the accepted policy;
- apply Member 4's authoritative current operational restrictions;
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
  Member 1/2/4 source-of-truth data;
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

## Phase 4 — Agentic backend boundary, component acceptance and handoff

**Local dependency and shared boundary:** complete work areas 1–3 on this
branch and use the shared Agentic integration contract agreed at G00.

**Verify the Agentic AI connection boundary:**

- authorized workflow initiation/status/results use the Member 3 public API;
- the private planner/orchestration adapter has server-side configuration,
  a bounded availability check and correlated dispatch outcome;
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

**Member 3 exit:** all deterministic domain behavior is accepted at G07. The
Planning & Coordination Agent begins only after G07; its schema and
orchestration work follow the post-gate AI queue.

## Progress record

Update the Member 3 row in the
[component branch status tracker](../member-branch-workflow.md#component-branch-status)
with branch/PR/merge status and integration evidence. Record work-area
milestones in the PR or the team's agreed contribution record. Do not
attribute Member 3 to a named person without a confirmed team ownership
record.
