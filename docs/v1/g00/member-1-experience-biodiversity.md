# G00 contract input — Member 1 Experience & Biodiversity

- **Owner:** Ushan Srinuka (`Ushan-Srinuka`)
- **Feature branch:** `features/experience-biodiversity`
- **Status:** Ushan's proposal for G00 review; shared-owner agreement is pending.
- **Gate effect:** This record does not accept G00 and does not authorize member implementation.

This record captures the Member 1 decisions and proposals needed to review the
G00 shared contracts. The four owners still need to agree the cross-component
items in this record and the other members' inputs before G00 can be accepted.
The service folder and project currently present on the feature branch are
untracked starter files, not evidence of a completed service.

## 1. Ownership and canonical identity

- Ushan's service is authoritative for destinations, activities, offerings,
  schedules, publication and availability, favourites, and the map-provider
  adapter. Each canonical destination, activity, offering and schedule uses
  an immutable UUID (`Guid`) as its cross-service identifier.
- Ushan owns the activity taxonomy and publishes stable activity IDs and
  category codes. Sanuda's service owns safety profiles and suitability;
  it references Ushan's activity IDs without copying the activity catalogue.
- Favourites are scoped to the authenticated user's UUID. A favourite may
  target a destination, activity or offering; the experience service enforces
  target existence and uniqueness. It does not store Auth user data or create
  a cross-service database foreign key.
- Wanshaja's service remains authoritative for operational restrictions.
  Ushan consumes restriction state and its version/effective time when
  evaluating availability; it stores no competing restriction state.
- Adithya's service owns the IT3091 adapter and validated biodiversity result
  contract. Ushan consumes its public result for presentation and stores no
  prediction as catalogue authority. A prediction never means observed
  presence, safety or availability.

**For shared agreement:** all four owners use opaque UUIDs for canonical
cross-component IDs and agree the activity category-code and operational
restriction references before consumers implement them.

## 2. Proposed service and persistence identity

| Item | Member 1 proposal |
|---|---|
| Service folder | `services/experience-biodiversity/` |
| .NET project/assembly | `Blueverse.ExperienceBiodiversity` |
| Compose service/DNS identity | `experience-biodiversity` |
| Container port | `8080`, private on the existing internal Docker network |
| Internal service prefix | `/internal/experiences`; only the public API and explicitly authorized private service callers reach it |
| PostgreSQL | Existing PostgreSQL 16 instance; service-owned `experience_biodiversity` schema and migrations |
| Database identity | Dedicated least-privilege service credential, supplied through environment/secret configuration |
| Health | Private `/health/live` for process liveness and `/health/ready` for required database readiness; optional Agentic AI state stays separate |

The service owns all catalogue business rules and persistence. Its relational
records use UUID primary keys, UTC instants (`timestamptz`), and
`created_at`/`updated_at` audit timestamps. Store an IANA time-zone identifier
with schedules for local interpretation; evaluate requested intervals as
half-open `[start, end)` ranges. `user_id` on favourites is an opaque Auth UUID,
not a local user record.

**For shared agreement:** settle the per-service schema and migration-history
ownership, database credential approach, internal network membership, service
host/port names, and UTC/time-zone/interval semantics for all components.

## 3. Proposed public capability and permission contract

These are G00 candidates, not implemented routes. `services/api` remains the
only client-facing API, authenticates and authorizes with the existing
role-to-permission model, and forwards only the approved actor/operation
context. The component service never calls Auth.

| Method and candidate public path | Capability | Proposed permission |
|---|---|---|
| `GET /api/experiences/destinations` | Browse/search published destinations with filters and pagination | `experience.catalog.read` |
| `GET /api/experiences/destinations/{destinationId:guid}` | Destination detail and its published experience context | `experience.catalog.read` |
| `GET /api/experiences/activities` | Read the canonical activity taxonomy | `experience.catalog.read` |
| `GET /api/experiences/offerings` | Browse/search published offerings with filters and pagination | `experience.catalog.read` |
| `GET /api/experiences/offerings/{offeringId:guid}` | Offering detail and current effective availability | `experience.catalog.read` |
| `GET /api/experiences/nearby` | Find nearby published destinations/offerings from validated `latitude`, `longitude` and `radiusMeters` query values supplied from a one-time location input | `experience.catalog.read` |
| `POST /api/experiences/destinations` and `PUT /api/experiences/destinations/{destinationId:guid}` | Create/update a destination | `experience.catalog.manage` |
| `POST /api/experiences/activities` and `PUT /api/experiences/activities/{activityId:guid}` | Create/update an activity and taxonomy fields | `experience.catalog.manage` |
| `POST /api/experiences/offerings` and `PUT /api/experiences/offerings/{offeringId:guid}` | Create/update an offering and its schedule | `experience.catalog.manage` |
| `POST /api/experiences/destinations/{destinationId:guid}/publication-evaluations`; `POST /api/experiences/activities/{activityId:guid}/publication-evaluations`; `POST /api/experiences/offerings/{offeringId:guid}/publication-evaluations` | Evaluate a requested publication transition and return accepted/blocked reasons without mutation | `experience.catalog.manage` |
| `PATCH /api/experiences/destinations/{destinationId:guid}/publication`; `PATCH /api/experiences/activities/{activityId:guid}/publication`; `PATCH /api/experiences/offerings/{offeringId:guid}/publication` | Apply an allowed publication transition after revalidation | `experience.catalog.manage` |
| `POST /api/experiences/availability/evaluations` | Evaluate current usability for a requested interval | `experience.catalog.read` |
| `POST /api/experiences/offerings/{offeringId:guid}/schedules` and `PUT /api/experiences/offerings/{offeringId:guid}/schedules/{scheduleId:guid}` | Add/update a schedule | `experience.catalog.manage` |
| `GET /api/experiences/favourites` | List only the caller's saved targets | `experience.favourites.manage` |
| `PUT /api/experiences/favourites/{targetType}/{targetId:guid}` | Idempotently save one of the caller's targets | `experience.favourites.manage` |
| `DELETE /api/experiences/favourites/{targetType}/{targetId:guid}` | Remove one of the caller's targets | `experience.favourites.manage` |

The availability request contains `offeringId`, `startsAt` and `endsAt` as
offset-qualified ISO-8601 instants. The response separates publication,
schedule and operational inputs and returns an effective result of
`AVAILABLE`, `UNAVAILABLE` or `UNKNOWN` plus reason codes. A missing, stale or
unreachable Wanshaja status produces `UNKNOWN`, never an optimistic result.
Provider and biodiversity context are optional enrichments and do not alter
this deterministic result. The proposed catalogue lifecycle is `DRAFT`,
`PUBLISHED` and `ARCHIVED`: unpublishing returns a published record to `DRAFT`,
and `ARCHIVED` is terminal. Publication evaluation checks catalogue-owned
completeness and schedule setup; transient Wanshaja restrictions do not change
publication state, but they affect the separate effective-availability
result.

Use the API's structured `ProblemDetails` convention for malformed input,
authorization failure, missing records and conflicting state. A valid
availability request with stale/missing operational data returns a successful
assessment with `UNKNOWN`; it is not represented as API liveness or database
failure.

**For shared agreement:** review the path/method set, exact DTO schemas,
permission codes, unpublished-record visibility, role grants, actor identity
envelope and error/status mapping. These proposals do not add catalog entries;
the endpoint catalog is updated when routes are implemented.

## 4. Cross-component, provider and time seams

- Availability evaluation accepts canonical offering UUID and a requested
  interval. Ushan derives publication and schedule eligibility, then consumes
  Wanshaja's read-only status/version/effective-time contract. Missing or stale
  status maps to `UNKNOWN`; Ushan never caches it as authoritative state.
- Ushan consumes biodiversity only through Adithya's public result capability.
  Adithya owns the request/response DTO, prediction validation and explicit
  unavailable/invalid outcomes. The request context may identify a canonical
  destination or activity and its location; it must not contain unnecessary
  personal/device-location data.
- Map requests and credentials stay server-side in Ushan's service. Provider
  responses are normalized, attribution is preserved, provider places remain
  non-canonical suggestions, and manual/list discovery remains available on
  provider failure. The vendor and enabled capability remain an owner decision
  before live integration acceptance.
- All schedule and cross-component event instants use UTC RFC 3339 values;
  local schedule interpretation carries an IANA time-zone ID. Interval
  boundaries are inclusive at start and exclusive at end.

**For shared agreement:** Member 3 confirms the biodiversity consumer contract;
Member 4 confirms restriction status, version, freshness and failure meanings;
all owners accept the timestamp and interval rules. The map feature schema is
finalized after vendor and feature selection, before live provider acceptance.

## 5. Proposed workflow, UI and dependency states

| Shared UI workflow ID | React route proposal | Flutter route proposal | Outcome |
|---|---|---|---|
| `experience-discovery` | `/experiences` | `/experiences` | Browse, search, detail and effective availability |
| `experience-catalogue-management` | `/experiences/manage` | `/experiences/manage` | Equivalent authorized catalogue and schedule management |
| `experience-favourites` | `/experiences/saved` | `/experiences/saved` | View, save and remove caller-owned targets |
| `experience-biodiversity-report` | `/experiences/reports/{workflowId}` | `/experiences/reports/{workflowId}` | View a sourced report or its explicit unavailable state |

The business request owns an opaque UUID `workflowId`; it is distinct from any
later AI execution record. Proposed dependency states are `NOT_CONNECTED`,
`UNAVAILABLE` and `AVAILABLE`; report outcomes remain separate from dependency
health. An absent/unavailable AI runtime produces no report and no fabricated
success. Keep API liveness and database readiness separate. The AI probe is
internal and bounded; expose its state through an authorized business workflow
status rather than changing `GET /api/health`.

**For shared agreement:** agree workflow IDs and React/Flutter paths across
owners, status vocabulary, request correlation, retryability, and actor/
permission propagation over a mutually authenticated private service channel.
The exact internal transport and trust mechanism remain a shared G00 decision.

## 6. Decisions deliberately deferred

G00 does not select a map vendor, approve provider licensing/attribution or
quotas, create production credentials, implement the IT3091 adapter, expose a
new endpoint, or implement a member workflow. Those decisions require the
owner's provider review and the applicable component acceptance evidence.
Actual Agentic AI agents, prompts, tools, model calls and orchestration remain
gated on G07.

## G00 review checklist

- [ ] Ushan confirms this proposal as the Member 1 input.
- [ ] Sanuda, Adithya and Wanshaja accept or return the cross-component items
      assigned to them above.
- [ ] All four owners accept shared IDs, public/private contracts, permissions,
      error/status semantics, time rules, service identities, database/network
      delivery and client workflow registrations.
- [ ] The maintainer records G00 acceptance in the readiness document and
      branch tracker only after the complete exit criteria are met.
