# Schema Design Status

The [Coastal Operations follow-up](../v1/coastal-operations-record-experience.md)
adds Assessment.Title/TimeZoneId, OperationalAlert.TimeZoneId and an IANA-seeded
TimeZoneLocations catalogue. Titled unlinked drafts retain an empty target
sentinel until linked; publication rejects it.

The final business schema is still deferred, but the current Auth foundation
defines a PostgreSQL/EF Core schema with checked-in migrations under
`services/auth/Data/Migrations`.
Auth, Coastal Operations, Coastal Planner, Marine Safety and Experience & Biodiversity have
PostgreSQL/EF Core schemas with checked-in migrations under their owning
service directories. Planning Coordination execution checkpoints use a
separate `coastal_planner.agent_execution_states` table keyed to Planner's
business workflow. This is Planner-owned persistence for AI-02, not a shared
AI runtime schema. It stores structured plan/step state and validated report
summaries only; it never stores hidden reasoning or credentials. Other shared
Agentic AI state remains deferred pending ADR-0008 and AI-00 acceptance.

The Coastal Operations service has a PostgreSQL-backed EF Core context with
the provisional `coastal_operations` default schema and checked-in migrations.
G00 acceptance was recorded from Adithya Gunawardana's team meeting report.
The service currently uses configured Compose database credentials. No live
PostgreSQL migration execution has been verified for this change.

## Coastal Operations tables (branch-local, provisional)

The current context defines these tables in `coastal_operations`:

| Table | Purpose and integrity controls |
|---|---|
| `Assessments` | Caller-owned business assessment/workflow, title (max 160, duplicate titles permitted), starting in `DRAFT`, target reference (empty only for unlinked drafts), UTC period and nullable selected zone, AI dependency outcome, bounded JSONB snapshots of optional Member 1–3 results collected on submission, initiator and optimistic version; cancellation tombstone; unique workflow ID and status/period/version/lifecycle constraints. |
| `AssessmentProposals` | Versioned proposal references and validity; unique assessment/version and a maximum 30-minute validity constraint. |
| `AssessmentEvidence` | Private image metadata, immutable assessment version, uploader, SHA-256, inspection state (AVAILABLE/EXPIRED/REMOVED), nullable RemovedAt/ContentDeletedAt and 365-day expiry; a removal-state check requires a timestamp for tombstones; content bytes live in the private evidence volume. |
| `ReviewerDecisions` | Human decision for a proposal version; unique proposal/version. |
| `TargetOperationalStates` | Member 4-owned state and optimistic version by target type/ID; unique target reference and state constraints. |
| `OperationalHistory` | Target state transitions with assessment/proposal/decision references and correlation ID. |
| `OperationalAlerts` | Draft and published alert content, target, severity, `PUBLIC`/`OPERATIONS` visibility, lifecycle, effective period and optimistic version; proposed drafts can be logically withdrawn with actor/time tombstone fields. |
| `AlertDecisions` | Audited publish/resolve/expire decisions. |
| `IdempotencyRecords` | Actor/operation/key-scoped request digest and original response; unique scope. |
| `OperationsAudit` | Resource/action/actor/correlation and timestamp; nullable actor name (100), record title (200), readable summary (512), required ActorRolesJson/ChangesJson JSONB arrays with an array-type check. New entries capture allowlisted before/after values and historical display identity atomically with the mutation. |
| `TimeZoneLocations` | IANA identifier primary key (max 100), country code/name, location, coordinates, description, source version and active flag. 419 seeded choices including UTC, from IANA 2026e zone.tab/iso3166.tab. Assessment/alert nullable zone references use restrictive foreign keys and indexes. Date-specific rules come from the OS, not a stored fixed offset. |

Migration `20261002081218_CoastalDetailedAudit` adds the audit snapshots above.
Existing events receive empty arrays and nullable details: no names, roles or
field history are reconstructed. Sensitive provider payloads, hidden reasoning,
credentials and image bytes are excluded. The same scoped audit access/retention
applies. Down retains original event rows but drops the new detail columns.
See [ADR-0027](../adr/ADR-0027-coastal-detailed-audit-snapshots.md). Generated SQL
and model consistency were checked; live PostgreSQL execution remains unverified.

Migration `20261001204558_CoastalDraftEvidenceRemoval` adds nullable removal and
content-deletion timestamps, allows REMOVED, and constrains its timestamp/state
pair. Existing rows remain unchanged. Draft removal commits evidence metadata,
assessment version and audit atomically before deleting private bytes. Pending
cleanup is retried in batches of 50 by the existing retention worker. Audit
queries retain removed evidence IDs; current details, publication snapshots and
attachment counts exclude removed images. Rollback maps removed rows to EXPIRED
because deleted private content cannot be recovered. Logs reuse existing tables
and indexes; they introduce no duplicate audit storage or content snapshots.
See [ADR-0026](../adr/ADR-0026-coastal-draft-evidence-removal.md).

Migration `20261001123722_CoastalOperationsNamedDraftsAndTimeZones` adds the
catalogue, assessment Title and both selected-zone columns. It backfills titles
from trimmed objectives (first 160 characters), preserves existing UTC instants
and leaves legacy zones null. It does not invent producer references or create
dispatch records. New clients submit one zone plus two local times; server
validation resolves each instant and rejects invalid or ambiguous clock times.

Migration `20261001102110_CoastalOperationsPublicationDispatch` adds `AssessmentDispatches`:
one unique assessment envelope, immutable JSONB published payload, workflow and
actor/correlation IDs, delivery status/attempt count, finite lease/retry time,
created/updated timestamps and optimistic version. It is business delivery
state; no agent plan/steps/model execution is stored. Existing assessments are
not automatically redispatched by migration. See [ADR-0025](../adr/ADR-0025-coastal-assessment-publication-dispatch.md).

Migration `20260927211833_CoastalOperationsDraftLifecycles` adds the
assessment `DRAFT`/`CANCELLED` lifecycle constraints and cancellation
tombstone, the alert `WITHDRAWN` lifecycle and tombstone, and expands the
idempotency operation-key column to 64 characters for per-assessment draft
operations.

The dependency snapshots retain source, endpoint outcome, attempt/retry
counts, checked time and validated evidence fields. They do not make peer
services readiness dependencies. These tables and constraints are branch
implementation, not a shared contract freeze. Member 1 target-state handoff,
dedicated database-role setup and cross-component PostgreSQL acceptance remain
open pending shared G00.

```mermaid
erDiagram
    Assessments ||--o| AssessmentDispatches : queues_publication
    Assessments ||--o{ AssessmentProposals : has
    Assessments ||--o{ AssessmentEvidence : includes
    Assessments ||--o{ ReviewerDecisions : records
    AssessmentProposals ||--o{ ReviewerDecisions : reviewed_by
    Assessments ||--o{ OperationalHistory : affects
    AssessmentProposals ||--o{ OperationalHistory : authorizes
    ReviewerDecisions ||--o{ OperationalHistory : records
    Assessments o|--o{ OperationalAlerts : may_link
    OperationalAlerts ||--o{ AlertDecisions : receives
```

The Auth schema contains users, roles, permissions, device installations,
active sessions, session lifecycle logs, rotating refresh-token records and the
two many-to-many assignment tables. It includes unique email/name/code
constraints, one active session row per user/device pair, session-version
state, absolute expiration, foreign-key cascade behavior, token-version
revocation state, system-role protection and `CreatedAt`/`UpdatedAt` fields
where applicable. Auth enforces a maximum of five active account sessions for
one installation and five active sessions per account across installations.

## Experience & Biodiversity connection foundation

Experience & Biodiversity connects to the same PostgreSQL database and
currently uses the Compose-configured login shared with Auth. Its EF Core
context uses the service-owned `experience_biodiversity` schema; its migration
history is isolated in `public.__EFMigrationsHistory_ExperienceBiodiversity`.
The current integration branch includes an initial migration
(`20260927113951_InitialExperienceBiodiversitySchema`) for these tables:

| Table | Current purpose |
|---|---|
| `destinations` | Coastal destination identity, location, publication status and audit timestamps. |
| `activities` | Activity taxonomy identity, category, publication status and audit timestamps. |
| `offerings` | Destination/activity-linked experience details, pricing, capacity, publication status and optional planner levels. The `ExperienceLevels` PostgreSQL `text[]` accepts each of `BEGINNER`, `INTERMEDIATE` and `ADVANCED` at most once; null means the owner has not supplied a level. |
| `schedules` | Offering schedule intervals, time-zone identifiers and active state. |
| `favourites` | User-scoped saved destination, activity or offering references; user IDs remain opaque Auth UUIDs. |

This branch schema is implementation evidence, not acceptance of the shared
G00 data-ownership or migration-history contract. All four member domains now
have their own EF Core service migrations; shared Agentic AI workflow-state
tables remain deferred.

Migration `20261012155447_AddOfferingExperienceLevels` adds a nullable
`ExperienceLevels` array so existing offerings remain valid without guessed
skill requirements. Migration
`20261012160632_AddOfferingExperienceLevelConstraint` adds a PostgreSQL check
constraint that rejects unknown or duplicate level values. The catalogue API
normalizes accepted input to uppercase, and planner candidate selection omits
level-specific offerings when no level matches the traveller's declared level.
Migration `20261012161414_MoveExperienceBiodiversityIntoOwnedSchema` moves the
five Experience tables from `public` into `experience_biodiversity` without
dropping or recreating data. The shared database login remains an explicit G00
decision; this schema move does not establish separate database credentials.

## Auth session tables

Bootstrap permission identity is the unique permission code. Preferred seed
GUIDs are used only when free; if another member's existing code occupies one,
Auth allocates a new GUID for the missing code. Existing identities, descriptions,
role assignments and audit timestamps are preserved, and repeated seeding is
idempotent. This is an essential member-integration correction for reused Marine/
Experiences seed IDs, with no schema or authentication-flow change.

| Table | Important fields and constraints |
|---|---|
| `DeviceInstallations` | Server-issued `DeviceId` primary key, hashed `DeviceKeyHash`, legacy marker, last-seen and revocation timestamps. The device key is never stored in plaintext. |
| `ActiveSessions` | Only currently active session rows; unique `(UserId, DeviceId)`, JWT `SessionVersion`, `CreatedAt`, `LastSeenAt`, absolute `ExpiresAt`, `RememberMe` and a foreign key to the installation. |
| `UserSessionLogs` | Append-only ended-session records with the original lifecycle fields, `EndedAt` and bounded `EndReason`; never used for authentication. |
| `RefreshTokens` | Unique SHA-256 token hash, session/family IDs, issued and expiry timestamps, consumed/revoked timestamps, replacement link and revocation reason. Each row references exactly one active session or session log. Raw refresh tokens are never persisted. |

`20260921033902_AuthSessionLifecycle` backfills legacy device-installation
rows from existing `UserSessions` before adding the installation foreign key
and gives pre-existing sessions a one-day migration expiration.

`20260921133008_ActiveSessionsAndSessionLogs` separates active and ended
sessions without losing history. Existing valid rows are moved to
`ActiveSessions`; revoked or expired rows are moved to `UserSessionLogs`; and
refresh-token references are transferred to the corresponding log rows when a
session is archived.

## Coastal Planner tables

`services/coastal-planner/Data/Migrations` owns the `coastal_planner` schema.
Actor IDs are stored as opaque UUID references; the planner does not add
cross-service foreign keys to Auth or other components.

| Table | Important fields and constraints |
|---|---|
| `planning_workflows` | Workflow ID, actor ID, type, status and lifecycle timestamps; indexes on actor, status and creation time; status check limits values to `PENDING`, `PROCESSING`, `COMPLETED` and `FAILED`. |
| `agent_execution_states` | One AI execution checkpoint per Planner workflow; stores bounded structured state JSON, current AI status, correlation ID and update time. Workflow ID is both primary key and cascading FK to `planning_workflows`; status is constrained to implemented execution states. The agent has no database access. |
| `recommendations` | Unique workflow ID, owner ID, destination, UTC window, duration, candidate snapshot and uncertainty-note snapshot; checks enforce a positive window and duration; indexed by owner. |
| `itineraries` | Owner ID, title, optional description, UTC window, optimistic `ConcurrencyVersion`, and created/updated timestamps; check enforces end after start; indexed by owner and creation time. |
| `itinerary_items` | Ordered destination/activity/offering reference, UTC schedule, last known availability/suitability/operations states, and advisory; cascades from its itinerary; unique `(ItineraryId, OrderIndex)` and checks enforce a non-negative order and positive schedule interval. New items begin as `UNKNOWN`. |
| `biodiversity_predictions_cache` | Destination/activity, validated provider and model/version, prediction timestamp, expiration, prediction snapshot, uncertainty text and limitations; indexed by destination/activity/model version. Cache lookup requires the configured expected model version; TTL is six hours pending provider approval. |
| `itinerary_evaluations` | Evaluation ID, owning itinerary ID, UTC review time and structured result snapshot with previous/current evidence; indexed by itinerary/time; cascades from its itinerary. |

`20261005163837_ItineraryReviewHistory` adds itinerary/item `TimeZone`
columns, recommendation `Outcome` and the review table. Legacy rows retain
their IDs, ownership, versions and UTC times; their display zone defaults to
`Asia/Colombo` and legacy recommendation outcomes remain `LEGACY_RESULT`.
New search outcomes distinguish matches, no matches and unavailable evidence.
Itinerary edits and reviews increment the optimistic version. Reorder updates
use unused temporary orders in a transaction before applying final orders.

The workflow owns at most one recommendation result. Itinerary reads and
mutations are scoped by `OwnerUserId`; recommendation and workflow reads are
scoped by their owner/initiator IDs. Cached predictions expire six hours after
the source inference timestamp and are never synthesized when the ML endpoint
is unavailable.

```mermaid
erDiagram
    planning_workflows ||--o| recommendations : produces
    itineraries ||--o{ itinerary_items : contains
    itineraries ||--o{ itinerary_evaluations : records
```

The eventual schema documentation must include:

- ER diagram
- table definitions
- keys
- relationships
- constraints
- indexes
- audit fields
- migration strategy
- seed data
- Agentic AI workflow-state tables, if required

The eventual v1 target schema must support four member-owned domains:

- Coastal experiences: destinations, activities, offerings, schedules,
  statuses, favourites and relevant biodiversity context.
- Marine conditions and safety: sourced snapshots, freshness, activity
  safety profiles and deterministic suitability results.
- Coastal planning: recommendation workflows and result snapshots,
  itineraries and ordered items (implemented in the Coastal Planner feature
  branch; cross-component acceptance is still pending).
- Coastal operations: assessments, proposals, operational state, approval
  decisions, advisories, alerts and execution history.

Shared structured Agentic AI workflow state must retain only the objective,
plan, steps, outputs or auditable summaries, validation, errors, bounded
retries, approvals, result and timestamps required to operate and audit the
workflow. Do not store hidden reasoning, passwords, bearer tokens or
unnecessary sensitive data. The component schemas and migrations remain owned
by their services; shared G00/G07 acceptance and integrated deployment are
separate gates.

See the [v1 component and agent index](../v1/README.md). Business-domain
schema should be introduced with its owning implementation rather than
prematurely in the v0 foundation.


## Planner provider evidence — 2026-10-12

The opt-in `PLANNER-POSTGRES-001` check passed against the local DHI PostgreSQL
16 container. It upgraded a legacy itinerary, applied pending migrations,
preserved ownership/title/version, reordered stable item IDs under the unique
index, rejected stale writes and invalid constraints, and checked review/item
cascade deletion. The runner created and dropped only its uniquely named test
database. No application volume reset or production database access occurred.
## Marine conditions and safety tables (implemented)

The marine-safety component service
(`services/marine-safety`, migrations `InitialMarineSafetySchema` and
`SafetyProfileReviewAndAssessmentEvidence`) owns the first v1 domain tables in
the shared `blueverse` database:

| Table | Keys, constraints and purpose |
|---|---|
| `MarineActivities` | `Id` PK, nonunique indexed `Name`, `ActivityType` (100 characters), `IsActive`, audit timestamps. Non-authoritative projection of Member 1 published activities. Legacy rows retain historic references; current availability is always checked through the catalogue, never this table. |
| `SafetyProfiles` | `Id` PK, `ActivityId` FK → `MarineActivities` (cascade), positive-limit CHECK constraints (`MaxWindSpeed`/`MaxWaveHeight`/`MaxSwellHeight` > 0), caution-band CHECKs (each optional `Caution*` < its hard limit), partial unique index enforcing one active profile per activity, unique `(ActivityId, Version)`, per-factor source and rationale, author/reviewer IDs and review/effective timestamps. Drafts are inactive; approval is required before evaluation can use a version. |
| `ConditionSnapshots` | `Id` PK, `Latitude numeric(8,5)`/`Longitude numeric(9,5)`, `ForecastTime`/`RetrievedAt` timestamptz, nullable environmental values (`WindSpeed`, `WaveHeight`, `SwellHeight`, `Rain`, `WeatherCode` — null means unavailable, never zero), `Source`, `FreshnessStatus`, `MissingFields text[]`; location/forecast and retrieval indexes. Provider-derived and immutable through the public API. |
| `SuitabilityAssessments` | `Id` PK, `ActivityId` FK (restrict), captured activity name, profile/snapshot IDs and version, request/forecast/evaluation times, `Result` (`SUITABLE`/`CAUTION`/`UNSUITABLE`/`UNKNOWN`), violations/caution/missing arrays, copied condition values and retrieval metadata, copied limits and per-factor criteria citations, source/freshness and `EvidenceCompleteness`; activity/request-time and evaluation-time indexes. History intentionally has no FK to snapshots or profiles so it survives retention pruning and profile deletion. |

`SafetyProfileReviewAndAssessmentEvidence` leaves pre-existing profile
provenance/review fields null and marks older assessments
`LEGACY_INCOMPLETE`. It does not infer citations, reviewer identity, historic
condition values or the criteria applied from mutable current rows. An existing
profile must be replaced by a cited draft and reviewed before it can support a
new assessment; old assessment records remain available with their evidence
gaps visible.

The public API uses Auth `/api/auth/me` for current account/session and
role-permission resolution. It signs a short-lived request-bound actor context
for Marine Safety, which enforces the permissions without reading Auth tables.
The Marine in-memory HTTP host exercises the production gateway middleware
with Auth/provider/catalogue transport doubles. PostgreSQL constraints and
migrations still require separate provider-backed evidence.

`CanonicalActivityProjection` preserves rows while removing the local unique
name rule and expanding category length from 64 to 100. Downgrade requires
reconciling duplicate names and long categories before shrinking constraints.

See the [v1 component and agent index](../v1/README.md). Each component service
owns its business schema; introduce future schemas with their service
implementations rather than prematurely in the v0 foundation.
