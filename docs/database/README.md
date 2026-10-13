# Database Foundation

PostgreSQL infrastructure is present in Compose. The Auth EF Core model,
migrations and application persistence code are checked in under `services/auth`.
Coastal Operations has an EF Core `DbContext`, domain
entities and migrations under `services/coastal-operations`. At startup it
applies its migrations to the provisional `coastal_operations` schema using
the Compose PostgreSQL connection. Shared acceptance of schema provisioning,
dedicated role/credentials and migration ownership remains pending G00; the
current local Compose connection uses the configured application database
credentials rather than a dedicated Coastal Operations role. On
2026-09-27, the branch image applied its checked-in evidence/alert-visibility
migration to the existing local Compose PostgreSQL database, and
`/api/operations/health/ready` returned HTTP 200. This verifies the current
local schema upgrade and connection only; it does not establish production
role provisioning, concurrency behavior or cross-component acceptance. Marine
Safety owns its domain model and migrations under
`services/marine-safety`; Experience & Biodiversity owns its model and
migrations for five component tables under `services/experience-biodiversity`.
Coastal Planner owns its model and migrations under `services/coastal-planner`.
Its `agent_execution_states` table stores minimized AI plan and progress data,
keyed to an existing Planner workflow and persisted only through Planner's
private service-key endpoint. The Agentic AI runtime has no database
credentials.
Each service applies only its own migrations. G00 acceptance was recorded
from Adithya Gunawardana's team meeting report, and G07 acceptance from his
explicit instruction. These records do not establish new live database or
provider evidence.

PostgreSQL is the authoritative relational database.
The [v0 persistence component](../v0/components/postgresql-ef-core.md)
summarizes the implemented Auth model and its verification boundary.
The Coastal Planner feature branch also owns its PostgreSQL schema and
migrations in `services/coastal-planner/Data/Migrations`; its deployment
applies them at startup. Planner persistence stores opaque actor UUIDs and
keeps foreign keys within the planner schema.

## Local service

```text
postgres:5432
```

The development Compose configuration publishes PostgreSQL with the host
mapping `5432:5432`, bound on all host interfaces. Use it only on a trusted
development network with the local database secret and host firewall. When
promoting Compose from `dev` to `main`, change the mapping to
`127.0.0.1:5432:5432` for loopback-only host access. Both forms reserve host
port `5432`, so changing the bind address does not fix a port collision with
another local PostgreSQL process. The DHI volume is mounted at
`/var/lib/postgresql`, allowing the image to manage its versioned `16/data`
directory. This is for local development only; production databases must
remain privately managed.

Use these pgAdmin4 connection settings:

```text
Host: 127.0.0.1
Port: 5432
Database: blueverse
Username: blueverse
Password: the POSTGRES_PASSWORD value from .env
```

## Experience & Biodiversity connection

The internal `experience-biodiversity` service connects directly to the same
PostgreSQL database as Auth through EF Core and Npgsql. Both services use the
configured `POSTGRES_DB`, `POSTGRES_USER` and `POSTGRES_PASSWORD` values;
Compose supplies `Host=postgres` for the internal database network. No second
database or PostgreSQL role is configured for this component yet. The schema is
created and migrated by EF Core rather than a separate provisioning SQL file.
The
[`ExperienceBiodiversityDbContext`](../../services/experience-biodiversity/Data/ExperienceBiodiversityDbContext.cs)
uses the service-owned `experience_biodiversity` schema. Its migration history
uses the separate `public.__EFMigrationsHistory_ExperienceBiodiversity` table,
so the Auth and Experience contexts track their migrations independently. The
initial migration creates `destinations`, `activities`, `offerings`,
`schedules` and `favourites`; `20261012155447_AddOfferingExperienceLevels`
adds nullable planner level metadata to existing offerings, and
`20261012160632_AddOfferingExperienceLevelConstraint` constrains the stored
array to unique values from `BEGINNER`, `INTERMEDIATE` and `ADVANCED`. Migration
`20261012161414_MoveExperienceBiodiversityIntoOwnedSchema` moves the existing
five component tables from `public`, preserving their data, indexes and foreign
keys. These migrations are applied by the component service at startup.
These tables are branch implementation evidence and do not imply that the
shared G00 database/schema ownership decision has been accepted.
When running the service directly on the host, set
`ConnectionStrings__DefaultConnection` to the same connection values but use
`Host=127.0.0.1` instead of the Compose-only `Host=postgres`. Supply the
password through the local environment or approved secret store; do not put a
real connection string in `appsettings.json` or source control. The service
fails startup when this setting is missing.

The design-time factory reads
`EXPERIENCE_BIODIVERSITY_MIGRATION_CONNECTION` for EF migration commands. Set
it to a real connection string before applying migrations; its default value
uses a placeholder password and is safe only for generating migration files.

At startup the service performs a bounded connection check and applies its
EF Core migrations, following the Auth service pattern. Process liveness
remains independent from database availability. No manual database
provisioning step is required for either new or existing PostgreSQL volumes.

## Agent fast path

For a database question, start with this document and
[`schema.md`](schema.md), then use
[`../../.agents/rules/data-access.md`](../../.agents/rules/data-access.md) and
the `blueverse-postgresql-efcore` workflow. Inspect the owning service source
only when the requested detail is absent, the documentation disagrees with
the implementation, or the user asks for source-level verification.

## Test-provider boundary

The Auth and Coastal Planner suites use isolated EF Core providers for
deterministic tests whose behavior does not depend on PostgreSQL. This is not
evidence for PostgreSQL SQL translation, migrations, constraints, indexes,
transactions, locking or concurrency. Those behaviors require a real
PostgreSQL test path or an approved disposable PostgreSQL fixture. Coastal
Planner's generated migration SQL is reviewed with the pinned `dotnet-ef`
tool; a live PostgreSQL apply check is still required before integration.

## Rules

- use EF Core migrations
- normalize relational data appropriately
- define primary/foreign keys
- use constraints and indexes
- maintain `CreatedAt` / `UpdatedAt` where appropriate
- use transactions for multi-step operations that require atomicity
- Auth device installations use server-issued opaque IDs and hashed device
  keys; legacy client IDs are marked explicitly while they migrate
- Auth sessions use the `ActiveSessions` table with a unique `(UserId, DeviceId)`
  key and indexed active-device lookups; ended sessions move to the indexed
  `UserSessionLogs` archive with an end timestamp and reason
- Auth refresh-token rows store only hashes and support rotation/reuse audit;
  each token references exactly one active session or archived session log; no
  bearer token or device key is stored in plaintext
- seed only deliberate development/reference data
- Auth applies `services/auth/Data/Migrations` at startup before bootstrap
  administrator seeding
- Coastal Operations applies `services/coastal-operations/Data/Migrations`
  at startup before reporting database readiness; its schema and provisioning
  remain provisional pending shared G00 acceptance
- Use the repository-pinned `dotnet-ef` tool manifest when creating or
  inspecting migrations
- Review generated migration SQL and the existing-data upgrade path before
  applying a migration; document rollback or forward-recovery expectations for
  changes that can affect existing rows
- do not store passwords or tokens in domain tables
- do not store hidden Agentic AI reasoning
