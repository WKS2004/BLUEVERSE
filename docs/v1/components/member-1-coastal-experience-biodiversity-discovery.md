---
contract_id: v1.component.experience-biodiversity
contract_type: business_component
release: v1
implementation_status: target_not_implemented
owner_label: member_1
requirements: "PROJECT_REQUIREMENTS.md sections 11, 12, 13, 29, 30, 53"
non_crud_operation: publication_and_availability_evaluation
minimum_meaningful_public_api_endpoints: 4
agent_contract: "../agents/member-1-coastal-experience-biodiversity-agent.md"
---

# Member 1 — Coastal Experience & Biodiversity Discovery

**Contract status:** v1 target specification. The current foundation does not
contain the complete v1 business implementation described here. **Ownership
label:** Member 1, as used by the frozen requirements. This label is not a
person or GitHub identity.

## 1. Purpose and user outcome

This component gives people a trustworthy way to find and manage coastal
destinations and marine activities, understand when a particular offering is
available, save experiences, and view relevant biodiversity predictions when
the separate model integration can provide them. The catalogue is specifically
about coastal tourism and operations; it is not a generic travel catalogue.

Its user-visible answer must make these questions clear:

- What coastal destination, activity or scheduled/participatory offering is
  being described?
- Is it valid, published, scheduled and available for the requested period?
- Where is it, and what discovery context is available?
- Is a biodiversity prediction available, what does it estimate, when and
  where was it produced, and what are its limitations?
- Is there an operational restriction from the authoritative operations
  component that changes whether the offering can be presented as usable?

### 1.1 Cross-layer implementation overview

| Layer | Component responsibility and implementation contract |
|---|---|
| **Universal product idea** | One authoritative coastal catalogue connects destinations, activities, destination-specific offerings, schedules, publication and current availability. People discover and save experiences; catalogue managers maintain them; biodiversity is optional, sourced context. Member 1 owns the BLUEVERSE map-provider integration that supports agreed location-aware discovery. Provider map/place data helps people find or view places; it never becomes the authoritative destination catalogue. Booking/payment inventory, generic travel search and an independent operational-restriction source are outside this component. |
| **React Web** | Implement the same authorized discovery, detail, management, favourite, map-assisted location and biodiversity outcomes through the public API. Use the repository's React 19/TypeScript/Vite/React Router structure, reusable route/page/components, Tailwind utilities and existing request/state separation. Nearby search has a usable manual location/destination path. Map-provider requests and credentials stay in Member 1's private service, reached through the public API; the eventual map presentation must use a provider-compatible approach that does not make the browser call the provider directly. Do not calculate authoritative availability in the browser. See the [React component contract](../../v0/components/react-web-client.md), [UI integration guide](../../development/ui-integration.md), and [React state ADR](../../adr/ADR-0005-react-state-management.md). |
| **Flutter Mobile** | Implement equivalent authorized outcomes in Dart with native Material widgets and the existing UI/logic/data separation, repository/API service and view-model pattern. Device location can improve nearby search when permission is granted; manual destination/location selection remains available when denied, unavailable or unsupported. Map-provider requests and credentials stay in Member 1's private service, reached through the public API; the eventual map presentation must use a provider-compatible approach that does not make the mobile client call the provider directly. Do not calculate availability locally. See the [Flutter component contract](../../v0/components/flutter-client.md), [UI integration guide](../../development/ui-integration.md), and [Flutter state ADR](../../adr/ADR-0006-flutter-state-management.md). |
| **Member 1 .NET service and data** | A separate internal ASP.NET Core service in Member 1's own `services/<component-service>/` subfolder owns catalogue/discovery operations, domain validation, persistence, schedules/availability/favourites and the map-provider adapter. Its EF Core/PostgreSQL records are owned by this service. The existing `services/api` remains the public boundary and receives only the authentication/permission, route/forwarding and typed-integration code needed to expose this service under `/api/...`; it contains none of Member 1's business rules or persistence. Member 1 consumes biodiversity results through Member 3's validated public API contract; Member 3's service owns the private IT3091 adapter. Clients use only `/api/...`; they never call providers, PostgreSQL or internal services. Agree service identifiers, public/internal route mapping, DTOs, actor/permission propagation, schema and map choices at G00. |
| **Map provider integration** | Member 1 owns the BLUEVERSE adapter and consumer contract for the selected map API. Its exact vendor and feature scope (for example map display, place lookup, geocoding or directions) are open decisions. All provider access is server-mediated under the assignment/repository boundary; keys remain server-side. Validate and normalize results, honor provider terms/attribution, and preserve manual/list discovery if the provider is unavailable. Provider results do not create, publish or overwrite canonical destinations automatically. |
| **Biodiversity ML integration** | Member 3 owns BLUEVERSE's backend adapter to the separate IT3091 inference service and its validated public prediction-result contract; the IT3091 workstream supplies the trained model and inference service. Member 1 owns the destination/activity-facing user experience and consumes only Member 3's public contract. Preserve genuine prediction provenance, model/version, query location/time, uncertainty and limitations. An outage is explicit unavailable context, never a guessed result. This integration is ordinary backend ML/API work, not Agentic AI, and is implemented before G07. |
| **Other external ownership** | Open-Meteo Weather and Marine API acquisition belongs to Member 2. It is independent of Member 1's map integration and Member 3's biodiversity inference adapter. |
| **Component relationships** | Member 1 is the canonical source of destination/activity/offering IDs and schedule/availability for Members 2 and 3 and the managed target/evidence referenced by Member 4. It consumes Member 4's current operational restriction when deriving effective usability. It provides the location context Member 3 may use for an optional biodiversity request and consumes the resulting validated Member 3 public contract for presentation. Member 2 owns activity suitability; Member 3 owns planning and the ML adapter; Member 4 owns operational restrictions. See the [producer/consumer relationship map](../component-relationships.md#producer-consumer-and-authority-map). |

Client workflows use the same public API contract and shared workflow IDs
where status tracking applies, use the server's role-to-permission model, and
are registered in the shared
[UI integration registry](../../contracts/ui-integration.json). The table is
an at-a-glance boundary map; the sections below define the full journeys,
invariants, API capability, failures and acceptance evidence.

Implement this component within the [v1 shared-foundation and file-ownership
rules](../member-branch-workflow.md#shared-foundation-and-file-ownership):
keep Member 1's business behavior in its own internal service, preserve
existing API/Auth flows, and limit `services/api`, shared client, registry and
infrastructure edits to the exact integration entries this component needs.

The shared [Agentic AI implementation blueprint](../../agentic-ai/implementation-blueprint.md)
defines common model, tool, retrieval, security, recovery and evaluation
requirements for this component's paired agent.

## 2. Ownership boundary

Member 1 owns the BLUEVERSE experience catalogue and its discovery behavior:

- destinations and their coastal location/context;
- coastal activities such as surfing, snorkeling, diving, whale or dolphin
  watching, and boat activities;
- offerings that connect an activity to a destination and define how it may
  be used or attended;
- schedules, availability, and catalogue publication status;
- nearby discovery and personal saved experiences/favourites; and
- BLUEVERSE's map-provider adapter and map-assisted discovery contract, with
  the provider and exact map features chosen before implementation; and
- the user-facing interpretation and presentation of sourced biodiversity
  prediction context obtained through Member 3's public contract. The
  BLUEVERSE IT3091 adapter belongs to Member 3; the separate IT3091 workstream
  supplies the model and inference service.

The component does **not** own weather acquisition or activity safety
classification (Member 2), recommendation and itinerary orchestration or the
biodiversity inference adapter (Member 3), operational restriction state or
approval (Member 4), user authentication, the ML model or inference service
itself, or regulatory/emergency authority.
It still consumes Member 4's authoritative operational status. An experience
record cannot override a restriction by remaining published or available in
this component.

On `features/coastal-experience-biodiversity`, implement the authorized
public workflow/status contract and typed private backend adapter that will
let the future Experience & Biodiversity Agent request this component's
validated catalogue/availability context. Before G07, the adapter reports
not connected or unavailable when the private Agentic AI runtime is absent;
it does not execute an agent or fabricate a report. Keep this dependency
status distinct from API liveness, database readiness and biodiversity ML
availability. Follow the shared [member integration boundary](../agentic-ai-integration-boundary.md).

## 3. Users and permission behavior

The product serves tourists and the people responsible for maintaining
BLUEVERSE's coastal catalogue. The requirements also expect authorized
operational users to inspect context when relevant to an assessment.
Capabilities are granted by the server's role-to-permission model, not by
client-side assumptions about role names. Therefore:

- a person with browse permission can discover and inspect public/published
  content;
- a person with catalogue-management permission can perform the allowed
  create, update, publication, archive, activity, offering, schedule or
  availability actions;
- a person can create, view or remove only their own favourites, subject to
  the server contract; and
- an operational restriction is read from the owning operations service and
  is not editable through this component.

Exact permission codes, visibility rules for unpublished records, and whether
some management operations have separate permissions are implementation
decisions. React and Flutter must show the same actions to any caller with the
same effective permissions.

## 4. Domain concepts and information meaning

These are conceptual data responsibilities, not a frozen database schema or
DTO. The database design must select exact tables, keys, relationships,
constraints, indexes, audit columns, and PostgreSQL types.

| Concept | Meaning and required relationships |
|---|---|
| Destination | A managed coastal place with a stable identity and enough location and descriptive context for detail, search, and nearby discovery. Activities and biodiversity queries may refer to it. |
| Map-provider result | External map/place/geocoding data returned through Member 1's server-side adapter for the map features the team selects. It is untrusted discovery/display context, not a destination record; it cannot publish or silently overwrite BLUEVERSE-owned names, coordinates, visibility or business state. Exact result fields depend on the selected provider and feature scope. |
| Activity | A coastal activity category with descriptive and practical participation context. An activity may have multiple offerings and activity-specific suitability rules owned by Member 2. |
| Offering | A managed instance of an activity at a destination. It carries the availability and operational context needed to decide whether it can be shown as usable for a requested time. |
| Schedule / availability | Time-related information for an offering. The implementation must distinguish a scheduled time from a general publication state and from current availability; booking or payment inventory is outside v1 scope. |
| Publication state | Controls catalogue visibility and management lifecycle. DRAFT, PUBLISHED, UNPUBLISHED and ARCHIVED are possible vocabulary, but the final state machine and allowed transitions must be specified and tested. |
| Favourite / saved experience | A user's association with a destination, activity or offering that supports save, revisit and removal without exposing one user's private list to another. Exact target types and uniqueness rules remain design decisions. |
| Biodiversity prediction | Optional context returned through Member 3's validated public result contract, linked to the queried canonical location/area and prediction time. Member 3 owns the internal IT3091 adapter. A genuine result may include focal species, occurrence probability, habitat-suitability interpretation, model version, timestamp, uncertainty and limitations. |
| Availability assessment | A business result for whether the selected experience can be discovered/used for the requested time after publication, schedule, availability and authoritative operational state are considered. It is the required non-CRUD operation. |

An occurrence probability is an estimate, not a guarantee of species presence.
Habitat suitability is a model interpretation, not proof of observed presence.
The UI and agent must preserve model version, query location and time, result
time, uncertainty/limitations, and unavailable status where supplied.

## 5. Required journeys

### 5.1 Catalogue management

1. An authorized manager creates or edits a destination and its coastal
   location and descriptive information.
2. The manager creates or edits activities and destination-specific
   offerings, then maintains schedules and availability.
3. The manager reviews validation feedback and attempts only a permitted
   publication-state transition.
4. The public discovery view reflects the resulting state according to the
   finalized visibility rules. Archived or otherwise non-published records
   cannot leak into ordinary public discovery.
5. `services/api` authenticates/authorizes and routes each operation to the
   private Member 1 service. That service validates and persists the mutation
   with EF Core/PostgreSQL and writes required audit data.

### 5.2 Browse, search and nearby discovery

1. A tourist browses or searches published destinations and activities.
2. Filters, sorting, and pagination are applied through the public API so
   results are consistent across clients.
3. A person can open a destination, activity or offering detail and inspect
   its relevant schedule and current availability.
4. Nearby discovery uses an explicitly supplied or device-provided location
   and returns geographically relevant results under the finalized search
   rules. The location is used only for the requested discovery purpose; the
   product must not imply ongoing tracking.
5. If location permission is denied on Flutter, a practical manual location
   or destination-selection path remains available. React must provide an
   equivalent location-entry path; device affordances may differ.
6. If the selected map API supplies map display, place lookup, geocoding or
   another location feature, both clients consume that capability through the
   public API, which integrates the private Member 1 service. Provider-returned places are candidate discovery
   context; a catalogue manager must validate and create/update an
   authoritative destination through the normal management workflow.
7. A provider outage, quota/rate limit, ambiguous result or no-match response
   must leave a usable list/manual search path. A map view is a presentation
   aid and does not determine publication, availability, safety or operational
   permission.

### 5.3 Favourites

1. A signed-in caller saves or removes an eligible destination, activity or
   offering.
2. The caller revisits their saved list and can navigate from a saved item to
   its current detail and availability.
3. A saved item that is no longer published, available or operationally
   permitted must not be represented as currently usable merely because it
   remains in the saved list.
4. Anonymous behavior, favourite target types and deletion/retention behavior
   must be decided in the public contract rather than silently assumed by a
   client.

### 5.4 Biodiversity context

1. A relevant destination/activity or location detail requests biodiversity
   context through Member 3's documented public prediction capability. Member
   1 does not address the private IT3091 service or implement a second ML
   adapter. Member 3's backend calls the internal inference service.
2. A genuine prediction is shown with enough provenance to explain what it
   estimates and the uncertainty or limitations returned by the model.
3. If the service or trained model is unavailable, BLUEVERSE reports an
   explicit unavailable state. It does not substitute a fabricated value or
   falsely imply that a past result is current.
4. Other experience and planning workflows may continue only when biodiversity
   is optional for that particular decision; its absence must remain visible
   wherever it affects interpretation.

## 6. Business invariants and validation

The owning service enforces these rules on every relevant read or write; a
client's disabled button is not enforcement:

1. A result shown as publicly discoverable must satisfy the final publication
   visibility policy.
2. An offering cannot be called available for a period solely because its
   destination or activity is published. Schedule, availability and any
   applicable restrictions also matter.
3. An invalid, unpublished, unavailable or operationally restricted offering
   is not an eligible recommendation candidate.
4. Member 4's operational state is authoritative for operational restrictions.
   Member 1 may display that state but may not maintain an independent value
   that conflicts with it.
5. Schedule and availability comparisons must use a defined time-zone and
   boundary convention. The selected convention is a technical decision that
   must be documented and covered at interval boundaries.
6. Location queries must validate coordinates/region inputs and must not
   return misleading nearby results when the input or location data is
   invalid or absent.
7. Map-provider output is untrusted input: validate and normalize it before
   use; do not let it silently create or overwrite a canonical destination.
   Provider attribution, license and use limits must be followed. A provider
   failure never implies that no destination exists.
8. Biodiversity results preserve source/model metadata and uncertainty. An
   unavailable or malformed response is represented as unavailable/invalid,
   never as a guessed prediction.
9. Favourites are scoped to the authorized user and must have a documented
   duplicate and stale-target policy.
10. Every mutation uses server-side authorization, validation, audit fields
   and transaction boundaries where the operation requires atomic changes.

### Publication and availability operation

The non-CRUD operation must evaluate whether an experience can be published,
shown, or treated as available in a specified context. It must state which
conditions it evaluated and why the result was accepted or blocked. The
requirements give `DRAFT → PUBLISHED → ARCHIVED` as an example, not a final
state machine. Before implementation, define transitions, unpublish semantics,
archive behavior, validation failures, and how operational restrictions
interact with publication. Do not conflate catalogue publication with
real-time availability or with Member 4's operational state.

## 7. Public API capability contract

The component must implement at least four meaningful public API endpoints
and at least one operation beyond CRUD. This is a capability inventory, not a
route proposal. Exact paths, HTTP methods, DTOs, authorization codes, paging
conventions and status codes are set during implementation and recorded in
the [endpoint catalog](../../api/endpoint-catalog.md). New routes use the
public `/api/...` boundary without a version segment.

The public contract must cover, as applicable:

- destination/activity/offerings discovery, details, search and management;
- schedule and availability management and current availability queries;
- the publication/availability business operation;
- nearby discovery with validated location input;
- any selected map lookup/display-support operation through an ASP.NET Core
  adapter, with a provider-independent public response and safe unavailable
  behavior (exact routes are chosen only when implemented);
- user-scoped favourite read/add/remove actions; and
- a biodiversity context request/result consumed through Member 3's public
  API capability, with explicit unavailable status rather than fabricated
  content. Its exact route and DTO are owned by Member 3 and are registered
  only when implemented.

Do not register internal ML endpoints as client routes. The React and Flutter
screens that implement these workflows must be represented in
[`ui-integration.json`](../../contracts/ui-integration.json), each linked to
its public API operation. The catalog remains the evidence for routes that
actually exist; this target contract does not create routes by naming them.

## 8. React and Flutter experience parity

Both clients must let every authorized caller complete the same business
outcome. At minimum, verify equivalent capability for:

| Capability | React Web | Flutter Mobile |
|---|---|---|
| Discover | Search, filter, sort, paginate, inspect destination/activity/offering detail and availability. | Same results and filters; fit browse and detail interaction to a small screen. |
| Location-aware browse | Enter a location or select a destination for nearby results; use agreed map-provider features through the public API. | Use device location when granted; keep manual selection when denied or unavailable; use the same API-mediated map-provider capabilities and safe fallback. |
| Catalogue management | Perform every management action authorized by the API. | Perform those same authorized actions, with mobile-appropriate forms. |
| Favourites | Save, revisit and remove the caller's eligible saved experiences. | Same saved-item behavior and current status. |
| Biodiversity | Present prediction/unavailable state, provenance and uncertainty. | Present the same meaning and caveats in a readable mobile layout. |
| Feedback | Show loading, empty, validation, stale/unavailable, denied and recoverable failure states. | Show equivalent states and a usable recovery path. |

Neither client should calculate business availability or operational
eligibility independently. Both render the authoritative public API result,
explain stale or missing information, and prevent duplicate submissions where
practical while still relying on server-side idempotency/validation.

## 9. Cross-component and external contracts

| Collaborator | Information exchanged | Boundary |
|---|---|---|
| Member 2 — Marine Conditions & Safety Intelligence | Activity identity/context and suitability evidence may constrain recommendations or operational review. | Member 2 owns marine data and deterministic environmental suitability. Member 1 must not calculate a second result. |
| Member 3 — Smart Coastal Planner & Itinerary Management | Valid destinations, activities, offerings, schedules, availability, experience constraints and location context for an optional prediction request. | Planner consumes authoritative candidate data and must apply marine and operational constraints too. Member 3 owns the IT3091 backend adapter and exposes only validated prediction context through its public contract; Member 1 renders that context. |
| Member 4 — Coastal Operations, Advisories & Alerts | Current managed operational status/restriction relevant to an offering or session. | Member 4 owns restriction state and approved transitions. |
| Selected map API | Provider-backed map/place/geocoding/display data for Member 1's agreed location-discovery features. | The private Member 1 service owns the adapter. `services/api` is the only client-facing boundary; provider results are untrusted, non-authoritative discovery context. Vendor and exact feature scope remain open. |
| IT3091 biodiversity inference | Member 3 sends minimal validated prediction inputs and receives the model result. Member 1 consumes the resulting public context. | Member 3's internal adapter calls the IT3091 service; clients and agents do not call it directly. Preserve provenance, uncertainty and unavailable/invalid states; a prediction is never guaranteed presence or safety evidence. |
| Open-Meteo | Weather and marine forecast/observation inputs. | Member 2 owns the adapter and deterministic suitability; this integration is not part of Member 1's map or Member 3's biodiversity adapter. |
| Identity and authorization | Authenticated principal and effective permissions. | Public API/Auth contract is authoritative; no role check embedded only in the client. |

Cross-component dependencies use service/application contracts and stable
identifiers. Clients never call each other or internal service hostnames.

## 10. Failure, privacy and operational behavior

The component must distinguish invalid user input, no matching results,
unpublished content, no schedule/availability, map-provider no-match or
unavailability, a service failure, and Member 3's unavailable biodiversity
result. An unavailable optional prediction must not turn into a normal-looking
zero or stale value. Map errors and Member 3's public unavailable status
should be recorded without credentials, full sensitive payloads or
unnecessary request data. Provider keys remain in server-side secret
configuration. Use only location precision needed for the
requested operation, disclose/request device location only when needed, and
avoid persistent tracking or raw-location retention without a documented
business need. Follow provider terms, attribution and quota limits.

Collect only location and preference data needed for the requested task. Do
not infer persistent GPS consent from a one-time nearby search. Enforce
ownership of saved lists, protect management actions, and avoid exposing
private/unpublished records via search or agent tools. Keep audit history for
material management changes according to the repository's data/audit
contract.

## 11. Acceptance and evidence checklist

Implementation evidence must demonstrate all of the following, not merely a
successful build:

- destination, activity, offering, schedule and availability relationships
  are valid in PostgreSQL/EF Core, with constraints and migrations;
- authorized management can create/edit and perform permitted publication
  transitions; invalid transitions and invalid/duplicate relationships are
  rejected with useful structured errors;
- public discovery excludes records outside the publication policy and
  filters unavailable or operationally restricted candidates;
- search/filter/sort/pagination work consistently in both clients;
- nearby discovery handles valid, missing, malformed and boundary locations;
- map-provider adapter responses are schema/coordinate validated and
  normalized; provider attribution/usage requirements are met; outage,
  timeout, quota/rate-limit, malformed and ambiguous/no-result cases preserve
  useful manual/list discovery without exposing provider credentials;
- denied/unavailable device location still permits manual Flutter discovery;
- favourites cannot be read or changed by another user, and duplicate/stale
  target behavior matches the documented contract;
- schedule/time-zone boundaries and concurrent availability updates are
  covered;
- Member 1 renders a valid prediction fixture with its provenance and
  uncertainty, and renders Member 3's unavailable/invalid result without
  presenting it as a prediction;
- Member 1 calls only the approved Member 3 public contract for prediction
  context and never addresses IT3091 or a private service directly; Member 3
  owns evidence for the genuine model-backed request/response and failure
  behavior;
- equivalent authorized actions and outcomes are available in React and
  Flutter; denied actions remain denied even if a client is manipulated;
- agent consumers receive structured, current context and do not make a
  blocked experience recommendable; and
- the route catalog, UI integration registry, source, migrations, tests and
  operational docs agree.

For each feature, the component owner records identifiable backend, database,
React, Flutter, test, documentation and Git evidence in the repository's
individual contribution process. Automated test expectations come from the
requirements, not from the current implementation.

## 12. Decisions to finalize during implementation

The following choices are deliberately not invented here: final entity/table
and DTO names; exact endpoints and permission codes; destination coordinate
and search-radius semantics; publication and availability state machines;
schedule time-zone representation; whether favourites can target each
conceptual item type; map provider/vendor and exact feature scope (display,
tiles, place search, geocoding, directions or other functions); whether its
terms support the required ASP.NET-mediated access and chosen client-rendering
approach; key restrictions, attribution, quotas, caching, timeouts and failure
fallback; biodiversity request granularity, caching and retention; and
audit-history retention. Do not ship direct client-to-provider calls under
the current architecture. Each decision that changes architecture or a
public contract must be documented in the owning API/database/ADR material,
then reflected in implementation and tests. The map-provider ownership and
ASP.NET Core boundary are recorded in
[ADR-0017](../../adr/ADR-0017-map-provider-integration-boundary.md). ML
adapter ownership and the Member 1 consumer boundary are recorded in
[ADR-0019](../../adr/ADR-0019-biodiversity-inference-integration-ownership.md).

## 13. Traceability

- Requirements: [sections 11–13, 29, 30 and 53](../../../PROJECT_REQUIREMENTS.md).
- Paired AI role: [Coastal Experience & Biodiversity Agent](../agents/member-1-coastal-experience-biodiversity-agent.md).
- Component work areas on one member branch: [Member 1 phase plan](../phases/member-1-phase-plan.md); producer/consumer relationships: [component relationship map](../component-relationships.md); PR and G07 process: [member branch workflow](../member-branch-workflow.md).
- Related shared contracts: [v1 workflows](../workflows.md), [member Agentic AI integration boundary](../agentic-ai-integration-boundary.md), [permissions and client parity](../cross-platform-and-permissions.md), [quality and delivery](../quality-and-delivery.md), [requirements coverage and readiness](../requirements-coverage-and-readiness.md), [Agentic AI safety](../../agentic-ai/safety.md), [endpoint catalog](../../api/endpoint-catalog.md), [UI integration](../../contracts/ui-integration.json).
- Device/location contract: [v1 device capabilities](../device-capabilities.md) defines one-time Flutter GPS use, React's equivalent location input, manual fallback, consent and data-minimization behavior.
