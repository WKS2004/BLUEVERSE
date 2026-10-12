# Schema Design Status

The complete four-component v1 business schema is still being delivered, but
the current Auth foundation defines a PostgreSQL/EF Core schema with
checked-in migrations under `services/auth/Data/Migrations`.

The Auth schema contains users, roles, permissions, device installations,
active sessions, session lifecycle logs, rotating refresh-token records and the
two many-to-many assignment tables. It includes unique email/name/code
constraints, one active session row per user/device pair, session-version
state, absolute expiration, foreign-key cascade behavior, token-version
revocation state, system-role protection and `CreatedAt`/`UpdatedAt` fields
where applicable. Auth enforces a maximum of five active account sessions for
one installation and five active sessions per account across installations.

## Experience & Biodiversity connection foundation

Experience & Biodiversity connects to the same PostgreSQL database and with
the same configured login as Auth. Its EF Core context uses PostgreSQL's
default `public` schema; its migrations are tracked in
`__EFMigrationsHistory_ExperienceBiodiversity` in that same schema. No second
database, service-specific login or SQL provisioning file is used. The Member
1 feature branch includes an initial migration
(`20260927113951_InitialExperienceBiodiversitySchema`) for these tables:

| Table | Current purpose |
|---|---|
| `destinations` | Coastal destination identity, location, publication status and audit timestamps. |
| `activities` | Activity taxonomy identity, category, publication status and audit timestamps. |
| `offerings` | Destination/activity-linked experience details, pricing, capacity and publication status. |
| `schedules` | Offering schedule intervals, time-zone identifiers and active state. |
| `favourites` | User-scoped saved destination, activity or offering references; user IDs remain opaque Auth UUIDs. |

This branch schema is implementation evidence, not acceptance of the shared
G00 data-ownership or migration-history contract. The other member domains and
shared Agentic AI workflow-state schema remain future work.

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
unnecessary sensitive data. Exact tables, keys and migrations remain design
work; none of these v1 domain tables are claimed as implemented.

See the [v1 component and agent index](../v1/README.md). Business-domain
schema should be introduced with the implementation rather than prematurely
in the v0 foundation.
