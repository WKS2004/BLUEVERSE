# Database Foundation

PostgreSQL infrastructure is present in Compose. The Auth EF Core model,
migrations and application persistence code are checked in under
`services/auth`. The Experience & Biodiversity feature branch also contains
an EF Core model and initial migration for five component tables; shared G00
data-ownership agreement and integrated acceptance remain pending.

PostgreSQL is the authoritative relational database.
The [v0 persistence component](../v0/components/postgresql-ef-core.md)
summarizes the implemented Auth model and its verification boundary.

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
database, PostgreSQL role or schema-provisioning SQL file is used. The
[`ExperienceBiodiversityDbContext`](../../services/experience-biodiversity/Data/ExperienceBiodiversityDbContext.cs)
uses PostgreSQL's default `public` schema, as Auth does. Its migration history
uses the separate `__EFMigrationsHistory_ExperienceBiodiversity` table in that
same schema, so the two EF contexts track their migrations independently. The
initial migration creates `destinations`, `activities`, `offerings`,
`schedules` and `favourites`; it is applied by the component service at startup.
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

The Auth suite uses an isolated EF Core provider for deterministic tests whose
behavior does not depend on PostgreSQL. This is not evidence for PostgreSQL
SQL translation, migrations, constraints, indexes, transactions, locking or
concurrency. Those behaviors require the explicitly enabled real PostgreSQL
test path documented in [`services/auth/tests/README.md`](../../services/auth/tests/README.md)
or an approved disposable PostgreSQL fixture.

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
- Use the repository-pinned `dotnet-ef` tool manifest when creating or
  inspecting migrations
- Review generated migration SQL and the existing-data upgrade path before
  applying a migration; document rollback or forward-recovery expectations for
  changes that can affect existing rows
- do not store passwords or tokens in domain tables
- do not store hidden Agentic AI reasoning
