# Schema Design Status

The final business schema is still deferred, but the current Auth foundation
defines a PostgreSQL/EF Core schema with checked-in migrations under
`services/auth/Data/Migrations`.

The Auth schema contains users, roles, permissions, device installations,
active sessions, session lifecycle logs, rotating refresh-token records and the
two many-to-many assignment tables. It includes unique email/name/code
constraints, one active session row per user/device pair, session-version
state, absolute expiration, foreign-key cascade behavior, token-version
revocation state, system-role protection and `CreatedAt`/`UpdatedAt` fields
where applicable. Auth enforces a maximum of five active account sessions for
one installation and five active sessions per account across installations.

## Auth session tables

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

The v1 target schema must support four member-owned domains:

- Coastal experiences: destinations, activities, offerings, schedules,
  statuses, favourites and relevant biodiversity context.
- Marine conditions and safety: sourced snapshots, freshness, activity
  safety profiles and deterministic suitability results.
- Coastal planning: recommendation requests, constraints, recommendations,
  itineraries and ordered items.
- Coastal operations: assessments, proposals, operational state, approval
  decisions, advisories, alerts and execution history.

Shared structured Agentic AI workflow state must retain only the objective,
plan, steps, outputs or auditable summaries, validation, errors, bounded
retries, approvals, result and timestamps required to operate and audit the
workflow. Do not store hidden reasoning, passwords, bearer tokens or
unnecessary sensitive data.

## Marine conditions and safety tables (implemented)

The marine-safety component service
(`services/marine-safety`, migration `InitialMarineSafetySchema`) owns the
first v1 domain tables in the shared `blueverse` database:

| Table | Keys, constraints and purpose |
|---|---|
| `MarineActivities` | `Id` PK, unique `Name`, `ActivityType`, `IsActive`, audit timestamps. Minimal locally-owned activity reference (Surfing, Snorkeling, Scuba Diving, whale/dolphin watching, coastal boat tour) to be replaced by Member 1's canonical taxonomy when that component merges. |
| `SafetyProfiles` | `Id` PK, `ActivityId` FK → `MarineActivities` (cascade), positive-limit CHECK constraints (`MaxWindSpeed`/`MaxWaveHeight`/`MaxSwellHeight` > 0), caution-band CHECKs (each optional `Caution*` < its hard limit), partial unique index enforcing one active profile per activity, monotonic `Version`, audit timestamps. |
| `ConditionSnapshots` | `Id` PK, `Latitude numeric(8,5)`/`Longitude numeric(9,5)`, `ForecastTime`/`RetrievedAt` timestamptz, nullable environmental values (`WindSpeed`, `WaveHeight`, `SwellHeight`, `Rain`, `WeatherCode` — null means unavailable, never zero), `Source`, `FreshnessStatus`, `MissingFields text[]`; location/forecast and retrieval indexes. Provider-derived and immutable through the public API. |
| `SuitabilityAssessments` | `Id` PK, `ActivityId` FK (restrict), `ProfileVersion`, request coordinates/time, `Result` (`SUITABLE`/`CAUTION`/`UNSUITABLE`/`UNKNOWN`), `Violations`/`CautionFactors`/`MissingFields` arrays, `Source`, `FreshnessStatus`, evidence references; activity/request-time and evaluation-time indexes. History intentionally has no FK to snapshots or profiles so it survives retention pruning and profile deletion. |

The marine service resolves caller permissions with a read-only query over the
Auth-owned identity tables; it does not map or write identity entities. Its two
marine permission grants (`marine.profile.read`, `marine.profile.manage`) are
seeded for the Admin role by the Auth seeder, so the marine service itself
never writes identity data.

See the [v1 component and agent index](../v1/README.md). Business-domain
schema should be introduced with the implementation rather than prematurely
in the v0 foundation.
