---
contract_id: v1.component.experience-biodiversity
contract_type: business_component
release: v1
implementation_status: target_not_implemented
owner_label: member_1
requirements: "PROJECT_REQUIREMENTS.md sections 12, 13, 29, 30, 53"
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

## 2. Ownership boundary

Member 1 owns the BLUEVERSE experience catalogue and its discovery behavior:

- destinations and their coastal location/context;
- coastal activities such as surfing, snorkeling, diving, whale or dolphin
  watching, and boat activities;
- offerings that connect an activity to a destination and define how it may
  be used or attended;
- schedules, availability, and catalogue publication status;
- nearby discovery and personal saved experiences/favourites; and
- BLUEVERSE's integration and user-facing interpretation of biodiversity
  predictions produced by the separately developed IT3091 ML capability.

The component does **not** own weather acquisition or activity safety
classification (Member 2), recommendation and itinerary orchestration (Member
3), operational restriction state or approval (Member 4), user authentication,
the ML model itself, or regulatory/emergency authority. It consumes
Member 4's authoritative operational status. An experience record cannot
override a restriction by remaining published or available in this component.

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
| Activity | A coastal activity category with descriptive and practical participation context. An activity may have multiple offerings and activity-specific suitability rules owned by Member 2. |
| Offering | A managed instance of an activity at a destination. It carries the availability and operational context needed to decide whether it can be shown as usable for a requested time. |
| Schedule / availability | Time-related information for an offering. The implementation must distinguish a scheduled time from a general publication state and from current availability; booking or payment inventory is outside v1 scope. |
| Publication state | Controls catalogue visibility and management lifecycle. DRAFT, PUBLISHED, UNPUBLISHED and ARCHIVED are possible vocabulary, but the final state machine and allowed transitions must be specified and tested. |
| Favourite / saved experience | A user's association with a destination, activity or offering that supports save, revisit and removal without exposing one user's private list to another. Exact target types and uniqueness rules remain design decisions. |
| Biodiversity prediction | Context returned by the internal ML integration, linked to the queried location/area and prediction time. It may include focal species, occurrence probability, habitat-suitability interpretation, model version, timestamp, uncertainty and limitations. |
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
5. Each mutation is authorized and validated by ASP.NET Core, persisted by
   the owning service using EF Core/PostgreSQL, and auditable as required.

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

1. A relevant destination or location detail requests biodiversity context
   through ASP.NET Core, which calls the internal inference service.
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
7. Biodiversity results preserve source/model metadata and uncertainty. An
   unavailable or malformed response is represented as unavailable/invalid,
   never as a guessed prediction.
8. Favourites are scoped to the authorized user and must have a documented
   duplicate and stale-target policy.
9. Every mutation uses server-side authorization, validation, audit fields
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
- user-scoped favourite read/add/remove actions; and
- a biodiversity context request/result that is mediated by the backend and
  reports real model availability/provenance.

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
| Location-aware browse | Enter a location or select a destination for nearby results. | Use device location when granted; keep manual selection when denied or unavailable. |
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
| Member 3 — Smart Coastal Planner & Itinerary Management | Valid destinations, activities, offerings, schedules, availability and experience constraints. | Planner consumes authoritative candidate data and must apply marine and operational constraints too. |
| Member 4 — Coastal Operations, Advisories & Alerts | Current managed operational status/restriction relevant to an offering or session. | Member 4 owns restriction state and approved transitions. |
| IT3091 biodiversity inference | Query location and prediction result plus provenance, uncertainty and availability. | Internal service called through backend; never directly exposed to React/Flutter or treated as guaranteed presence. |
| Identity and authorization | Authenticated principal and effective permissions. | Public API/Auth contract is authoritative; no role check embedded only in the client. |

Cross-component dependencies use service/application contracts and stable
identifiers. Clients never call each other or internal service hostnames.

## 10. Failure, privacy and operational behavior

The component must distinguish invalid user input, no matching results,
unpublished content, no schedule/availability, a service failure, and
unavailable biodiversity inference. An unavailable optional prediction must
not turn into a normal-looking zero or stale value. External/provider and ML
errors should be recorded without storing credentials or unnecessary request
data.

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
- denied/unavailable device location still permits manual Flutter discovery;
- favourites cannot be read or changed by another user, and duplicate/stale
  target behavior matches the documented contract;
- schedule/time-zone boundaries and concurrent availability updates are
  covered;
- biodiversity returns an actual prediction when the inference service and
  model are available, preserves metadata/uncertainty, and reports unavailable
  or malformed inference without fabrication;
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
conceptual item type; biodiversity request granularity, caching and retention;
and audit-history retention. Each decision that changes architecture or a
public contract must be documented in the owning API/database/ADR material,
then reflected in implementation and tests.

## 13. Traceability

- Requirements: [sections 12, 13, 29, 30 and 53](../../../PROJECT_REQUIREMENTS.md).
- Paired AI role: [Coastal Experience & Biodiversity Agent](../agents/member-1-coastal-experience-biodiversity-agent.md).
- Related shared contracts: [v1 workflows](../workflows.md), [permissions and client parity](../cross-platform-and-permissions.md), [quality and delivery](../quality-and-delivery.md), [requirements coverage and readiness](../requirements-coverage-and-readiness.md), [Agentic AI safety](../../agentic-ai/safety.md), [endpoint catalog](../../api/endpoint-catalog.md), [UI integration](../../contracts/ui-integration.json).
