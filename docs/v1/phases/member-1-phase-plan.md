# Member 1 phase plan — Coastal Experience & Biodiversity Discovery

This plan divides the complete [Member 1 component
contract](../components/member-1-coastal-experience-biodiversity-discovery.md)
into work areas for one complete component branch. The
[component relationship map](../component-relationships.md) describes its
producer/consumer contracts, and the
[branch and integration workflow](../member-branch-workflow.md) defines
parallel work, pull requests and the G07 gate. Work areas are not separate
branches or PRs and do not schedule other members. Component requirements
remain the source of behavior.

The [shared foundation and file-ownership rules](../member-branch-workflow.md#shared-foundation-and-file-ownership)
apply to every phase: keep this component additive, preserve existing API/Auth
flows, implement Member 1's business logic in its own `services/` microservice,
and limit `services/api` to integration code. Minimize shared React, Flutter,
route-registry and infrastructure edits.

Member 1 owns destinations, activities, offerings, schedules, publication and
availability, discovery, favourites, the selected map-provider adapter, and
the user-facing biodiversity context surface. Member 3 owns BLUEVERSE's
backend adapter to the separate IT3091 inference API and its validated public
prediction contract; IT3091 supplies the model/inference service. Member 1
consumes the Member 3 public contract and does not call the private service.
Member 1 does not own environmental conditions/suitability, itinerary
recommendations, the ML adapter, or operational restriction state.

## Component work areas

| Area | Work |
|---:|---|
| 1 | Catalogue identities and relational foundation |
| 2 | Catalogue lifecycle, schedule and effective availability |
| 3 | Search, map-assisted nearby discovery and favourites |
| 4 | Agentic backend boundary, component acceptance and consumer handoff |

Use the single branch `features/coastal-experience-biodiversity` for all four
work areas and submit one complete feature PR to `dev`. Agree shared schemas
and semantics at G00, implement against those contracts while other members'
branches are in progress, then verify live provider/consumer behavior on
`dev` after the component PRs merge. Member 1 can build its biodiversity
presentation against the agreed Member 3 contract and controlled fixtures
while Member 3 implements the inference adapter; verify the live consumer and
provider integration on `dev`. Do not wait for another member to finish
their component before completing this branch.
React and Flutter must deliver the same permitted actions and server-owned
outcomes in each applicable workflow.

The numbered phases below are contract/dependency work areas, not a required
implementation timeline or separate branch sequence. Implement all four as
one component on the single member branch; work areas may overlap where their
local technical dependencies permit. Use the component relationship map to
see cross-member producer/consumer dependencies.

## Phase 1 — Catalogue identities and relational foundation

**Starts after:** the team agrees the shared IDs, ownership and boundary
contracts at G00. No other member's implementation must be complete.

**Implement:**

- stable destination, activity, offering and schedule/availability identity
  and their ownership relationships;
- coastal location fields sufficient for the agreed destination and nearby
  query contracts, with validated coordinates and privacy-minimal use;
- PostgreSQL/EF Core entities, keys, foreign keys, uniqueness, indexes,
  audit fields, useful seed data and the first migration;
- a public API read/detail contract for the canonical catalogue records; and
- the initial React and Flutter catalogue/detail path using the same IDs and
  public API. Register each UI and API operation in the shared registries.

Do not add an operational-state copy, a second suitability evaluator,
booking/payment inventory, direct client access to ML, or guessed Member 2
safety profiles. Exact routes, DTOs, lifecycle names and permission codes are
implementation decisions; define them in source and the owning contracts.

**Handoff:** publish canonical destination/activity/offering IDs, relationships
and activity taxonomy. Member 2 uses the taxonomy for profiles; Member 4
references these IDs for managed operational targets; Member 3 references
them in requests and itinerary items.

## Phase 2 — Catalogue lifecycle, schedule and effective availability

**Local dependency and cross-component contract:** build on work area 1 and
the Member 4 status contract agreed at G00. Implement the consumer against
that contract and controlled test doubles while Member 4 develops its branch;
verify live status integration after the component PRs merge to `dev`.

**Implement:**

- permission-checked destination, activity and offering create/update and
  publication/archive transitions;
- schedule, time-zone, interval-boundary and availability rules, explicitly
  distinguishing publication, scheduled availability and operational state;
- the non-CRUD publication/availability evaluation with a reasoned result;
- discovery/detail responses that do not call unpublished, invalid,
  unavailable or operationally restricted offerings usable; and
- server-side consumption of Member 4's current authoritative status. If that
  status is unavailable or stale, preserve the defined uncertainty/failure
  result; do not maintain a competing Member 1 status field.

React and Flutter management actions must follow the same API permissions.
Both clients display the same effective availability result rather than
calculating it independently.

**Handoff:** expose stable catalogue, publication, schedule and effective
availability semantics for Members 3 and 4. An initial Member 4 status may be
used as a controlled test fixture only in tests, not as a production source.

## Phase 3 — Search, map-assisted nearby discovery and favourites

**Local dependency:** build on work area 2's read and effective-availability
contracts. The selected provider and exact map feature scope must be agreed in
the architecture/API decision before live integration. Use controlled
provider responses while the provider contract is being implemented.

**Implement:**

- consistent browse, detail, search, filters, sorting and pagination;
- nearby discovery with validated coordinate/radius input and no implied
  continuous location tracking;
- the selected map API integration through a server-side adapter owned by
  Member 1's internal .NET service and reached by clients through the public
  API; the supported feature may be map display support, place lookup,
  geocoding, directions or another explicitly selected capability, but the
  provider and exact scope are not assumed by this plan;
- provider-response normalization, coordinate/schema validation, required
  attribution/license behavior, server-side credential handling, bounded
  timeout/rate-limit handling and a usable list/manual-search fallback;
- no direct React/Flutter-to-provider request and no silent conversion of a
  provider place result into a canonical BLUEVERSE destination;
- Flutter device-location permission handling and a usable manual
  destination/location choice when permission is denied or unavailable;
- equivalent React location entry;
- signed-in, user-scoped favourite add/list/remove behavior, including
  duplicate and stale-target policy; and
- equivalent accessible success, empty, denied, invalid and recoverable
  failure states in both clients.

Use the shared [device-capability contract](../device-capabilities.md) for
one-time, user-initiated readings; React may offer browser geolocation but
must always preserve manual search. This is discovery input, not background
tracking. GPS itself does not wait for the map vendor choice; rendered-map or
place-provider features do.

**Handoff:** tourists and permitted catalogue managers can complete the same
discovery and saved-experience outcomes from React and Flutter; downstream
components can query the same public API results.

## Phase 4 — Agentic backend boundary, component acceptance and handoff

**Local dependency and shared boundary:** complete work areas 1–3 on this
branch and use the shared Agentic integration contract agreed at G00.

**Implement and verify the Agentic AI integration boundary:**

- the Member 1 public API workflow initiation/status contract, authorization,
  validation and business request identity needed to request the future
  Experience & Biodiversity report;
- a typed private backend adapter that can later dispatch through the
  approved Agentic AI service boundary, with service configuration kept
  server-side;
- explicit not-connected/unavailable status, bounded health/dispatch
  handling, safe retryability and correlated failure evidence when the
  private Agentic AI service is absent or cannot respond; and
- no production agent, prompt/model call, tool execution or fabricated report
  before G07. This is an integration seam only; the real biodiversity ML
  adapter belongs to Member 3, is separate from Agentic AI, and must be ready
  before G07. Member 1 consumes its validated public result contract.

Follow [Member feature integration with Agentic AI](../agentic-ai-integration-boundary.md)
for shared states, health semantics and acceptance evidence.

**Verify and close:**

- catalogue ownership, four meaningful public operations and the
  publication/availability business operation;
- relationships, migrations, constraints, indexes, transaction/audit needs,
  boundary-time behavior and real PostgreSQL evidence where required;
- Member 4 restriction consumption and the Member 3 candidate contract,
  without duplicating their state or rules;
- React/Flutter role-action parity, the GPS-denied fallback, map-provider
  failure fallback, location validation, stale status and correct rendering
  of Member 3's available/unavailable biodiversity result;
- route catalog, UI registry, OpenAPI, component docs, tests and attributable
  PR evidence.

**Member 1 exit:** the component meets its contract without an executable
Member 1 agent. The agent remains deferred until the team accepts G07.

## Progress record

Update the Member 1 row in the
[component branch status tracker](../member-branch-workflow.md#component-branch-status)
with branch/PR/merge status and integration evidence. Record work-area
milestones in the PR or the team's agreed contribution record. Do not infer a
person's identity from the Member 1 label.
