# G00 contract input — Member 1 Experience & Biodiversity

- **Owner:** Ushan Srinuka (`Ushan-Srinuka`)
- **Feature branch:** `features/experience-biodiversity`
- **Status:** Ushan's proposal for G00 review; shared-owner agreement is pending.
- **Gate effect:** This record does not accept G00 or establish component acceptance. The feature branch now contains partial implementation; that source does not record shared-owner agreement.

This record captures the Member 1 decisions and proposals for review at G00.
The four owners still need to agree the cross-component items in this record
and the other members' inputs before G00 can be accepted. The feature branch
now contains component source, including an EF Core model/migration and public
gateway routes. That implementation is evidence of branch work only; it does
not settle or approve shared G00 contracts.

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
| Public API route prefix | `/api/experiences`; YARP forwards this path unchanged to the private `experience-biodiversity` service |
| PostgreSQL | Existing PostgreSQL 16 instance and the same configured database used by Auth; a branch migration creates five component tables while shared data ownership remains pending |
| Database identity | Uses the same `POSTGRES_USER` and `POSTGRES_PASSWORD` configuration as Auth; no separate database or service login |
| Health | `GET /api/experiences/health` through the API gateway; anonymous service/database readiness with `200` when connected and `503` when unavailable, matching Auth |
| Swagger/OpenAPI | `/api/experiences/swagger/{documentName}/swagger.json` through the API gateway |
| Gateway security | API and component service validate Auth-issued JWTs; management routes require catalogue-management permission claims, while published reads and health/OpenAPI routes are anonymous |

The service owns all catalogue business rules and persistence. Its relational
records use UUID primary keys, UTC instants (`timestamptz`), and
`created_at`/`updated_at` audit timestamps. Store an IANA time-zone identifier
with schedules for local interpretation; evaluate requested intervals as
half-open `[start, end)` ranges. `user_id` on favourites is an opaque Auth UUID,
not a local user record.

The service registers an EF Core context, performs a bounded database
connection check and applies its migration at startup. Its initial migration
creates `destinations`, `activities`, `offerings`, `schedules` and
`favourites` in PostgreSQL's default schema, using the shared database
credentials and a separate migration-history table. Shared G00 data ownership
and migration-history decisions remain pending.

The API gateway and private component service validate Auth-issued JWTs. The
gateway protects management routes with the existing role-to-permission
claims; published catalogue/map reads and health/OpenAPI routes remain
anonymous. This records current source behavior and does not convert Member 1's
proposed contracts below into shared-owner decisions.

**For shared agreement:** settle the shared database/schema and per-service
migration-history approach, confirm the database credential approach, internal
network membership, service host/port names, any later database-readiness
contract, and UTC/time-zone/interval semantics for all components.

## 3. Proposed public capability and permission contract

These remain Ushan's proposed G00 contract decisions. Matching branch routes
are present, but route presence does not record shared agreement. `services/api`
remains the only client-facing API and uses the existing role-to-permission
model; the component service validates forwarded Auth-issued tokens locally
and never calls Auth.

| Method and candidate public path | Capability | Implemented / Proposed permission |
|---|---|---|
| `GET /api/experiences/destinations` | Browse/search published destinations with filters and pagination | Anonymous for published records; `experiences.catalogue.read` or `experiences.catalogue.manage` for other publication states |
| `GET /api/experiences/destinations/{destinationId:guid}` | Destination detail and its published experience context | Anonymous for published records; `experiences.catalogue.read` or `experiences.catalogue.manage` for non-published records |
| `GET /api/experiences/activities` | Read the canonical activity taxonomy | Anonymous for published records; `experiences.catalogue.read` or `experiences.catalogue.manage` for other publication states |
| `GET /api/experiences/offerings` | Browse/search published offerings with filters and pagination | Anonymous for published offerings with published parents; `experiences.catalogue.read` or `experiences.catalogue.manage` for other publication states |
| `GET /api/experiences/offerings/{offeringId:guid}` | Offering detail and schedule information | Anonymous for published offerings with published parents; `experiences.catalogue.read` or `experiences.catalogue.manage` for non-published records |
| `GET /api/experiences/nearby` | Find nearby published destinations/offerings from validated place name/keyword or coordinates | Anonymous; results include published destinations only |
| `POST /api/experiences/destinations` and `PUT /api/experiences/destinations/{destinationId:guid}` | Create/update a destination | `experiences.catalogue.manage` |
| `DELETE /api/experiences/destinations/{destinationId:guid}` | Delete a destination | `experiences.catalogue.manage` |
| `POST /api/experiences/activities` and `PUT /api/experiences/activities/{activityId:guid}` | Create/update an activity and taxonomy fields | `experiences.catalogue.manage` |
| `DELETE /api/experiences/activities/{activityId:guid}` | Delete an activity and cascade child offerings | `experiences.catalogue.manage` |
| `POST /api/experiences/offerings` and `PUT /api/experiences/offerings/{offeringId:guid}` | Create/update an offering and its schedule | `experiences.catalogue.manage` |
| `DELETE /api/experiences/offerings/{offeringId:guid}` | Delete an offering and related schedule slots | `experiences.catalogue.manage` |
| `POST /api/experiences/destinations/{destinationId:guid}/publication-evaluations`; `POST /api/experiences/activities/{activityId:guid}/publication-evaluations`; `POST /api/experiences/offerings/{offeringId:guid}/publication-evaluations` | Evaluate a requested publication transition and return accepted/blocked reasons without mutation | `experiences.catalogue.manage` |
| `PATCH /api/experiences/destinations/{destinationId:guid}/publication`; `PATCH /api/experiences/activities/{activityId:guid}/publication`; `PATCH /api/experiences/offerings/{offeringId:guid}/publication` | Apply an allowed publication transition after revalidation | `experiences.catalogue.manage` |
| `POST /api/experiences/availability/evaluations` | Evaluate current usability for a requested interval | `experiences.catalogue.read` |
| `POST /api/experiences/offerings/{offeringId:guid}/schedules` and `PUT /api/experiences/offerings/{offeringId:guid}/schedules/{scheduleId:guid}` | Add/update a schedule | `experiences.catalogue.manage` |
| `DELETE /api/experiences/offerings/{offeringId:guid}/schedules/{scheduleId:guid}` | Delete a schedule slot | `experiences.catalogue.manage` |
| `GET /api/experiences/favourites` | List only the caller's saved targets | Authenticated caller; records are scoped to that caller's UUID |
| `PUT /api/experiences/favourites/{targetType}/{targetId:guid}` | Idempotently save one of the caller's targets | Authenticated caller; records are scoped to that caller's UUID |
| `DELETE /api/experiences/favourites/{targetType}/{targetId:guid}` | Remove one of the caller's targets | Authenticated caller; records are scoped to that caller's UUID |

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
- Place search, provider configuration and credential-bearing map requests stay
  behind Ushan's service and the public API. The current Flutter implementation
  gets a MapLibre style from `GET /api/experiences/map/config`, then the renderer
  fetches public OpenFreeMap style/vector-tile resources from that configured
  HTTPS host only. React uses MapLibre GL JS with the same configured style and
  bounded tile-rendering exception. Neither client sends device location or
  user data with tile requests. Current-location coordinates stay in client
  memory and go only to the public nearby endpoint after the user taps the
  location control; the location marker is rendered locally. Provider results
  remain non-canonical suggestions and manual/list discovery remains available
  on provider failure. The shared provider, terms, attribution, quota and
  rendering-boundary decisions remain proposals pending G00 review.
- All schedule and cross-component event instants use UTC RFC 3339 values;
  local schedule interpretation carries an IANA time-zone ID. Interval
  boundaries are inclusive at start and exclusive at end.

**For shared agreement:** Member 3 confirms the biodiversity consumer contract;
Member 4 confirms restriction status, version, freshness and failure meanings;
all owners accept the timestamp and interval rules. Owners review whether the
current OpenFreeMap/MapLibre rendering in both clients meets the shared
provider, terms, attribution and public-API boundary before
accepting the map feature contract.

## 5. Proposed workflow, UI and dependency states

| Shared UI workflow ID | React route proposal | Flutter route proposal | Outcome |
|---|---|---|---|
| `experience-discovery` | `/experiences` | `/experiences` | Browse, search, detail and effective availability |
| `experience-catalogue-management` | `/experiences/manage` | `/experiences/manage` | Equivalent authorized catalogue and schedule management |
| `experience-favourites` | `/experiences/saved` | `/experiences/favourites` | View, save and remove caller-owned targets |

For this member's mobile proposal, approximate device location is requested only
after the user taps **Near Me**. The coordinates are used for the public nearby
API request and are not persisted by the client; manual place search and list
browsing remain available when permission is denied or location is unavailable.
Both Coastal Maps use the registered `GET /api/experiences/map/config`
contract to obtain the MapLibre style and provider attribution, then render
OpenFreeMap vector tiles and published catalogue destinations at their stored
coordinates. MapLibre Native on Flutter and MapLibre GL JS on React request
tiles only from the HTTPS OpenFreeMap host returned by that API configuration.
Neither client sends device location or user data with tile requests. Location
is requested only after the user taps the location control; coordinates remain
in client memory, go only to the public nearby API and appear as a locally
rendered marker. Place search and nearby requests continue through the public
API. Selecting a destination marker shows its location details and an action to
open the registered detail route. These are maps for exploration, not
turn-by-turn routing. The shared-owner G00 agreement remains pending; see the
bounded implementation decision recorded in ADR-0017.

Biodiversity results are presented as model predictions, with source, model
version, uncertainty and disclaimer visible. Unknown or unavailable results
remain explicit; predicted likelihood is never labelled as observed presence.
Availability checks use an exact active schedule window or an explicitly
selected local window in `Asia/Colombo`, converted to UTC instants for the
public API. The current API request does not accept party size, so the mobile
client does not imply that a party-size check was performed. Catalogue managers
can add, edit, activate/deactivate and delete offering schedule windows.
Catalogue management requests `DRAFT`, `PUBLISHED` and `ARCHIVED` records with
separate permission-checked list queries; public discovery continues to use the
published-only reads.

Report presentation is deferred until G07 and a reviewed public workflow/API
contract exist. Before then, the mobile component does not claim to provide an
AI-generated report. Keep API liveness and database readiness separate. Any
future AI probe remains internal and bounded; expose its state through an
authorized business workflow status rather than changing `GET /api/health`.

**For shared agreement:** agree workflow IDs and React/Flutter paths across
owners, status vocabulary, request correlation, retryability, and actor/
permission propagation over a mutually authenticated private service channel.
The exact internal transport and trust mechanism remain a shared G00 decision.

## 6. Decisions deliberately deferred

G00 does not ratify the current OpenFreeMap/MapLibre Flutter and React
implementations, approve provider licensing/attribution or quotas, create
production credentials, implement the IT3091 adapter, expose a new endpoint,
or accept the member workflow. Those decisions require the owners' provider
review and the applicable component acceptance evidence.
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
