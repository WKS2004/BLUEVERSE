# Data-access and PostgreSQL rule

Use this rule for EF Core models, migrations, queries, persistence services,
database constraints or indexes, PostgreSQL integration tests, and database
documentation. Read it with `architecture.md`, `security.md`, `testing.md`,
`documentation.md` and `validation.md` when the change crosses those areas.

## Authoritative boundary

The current relational data path is:

```text
ASP.NET Core service → EF Core 10 → Npgsql.EntityFrameworkCore.PostgreSQL → PostgreSQL 16
```

PostgreSQL is authoritative. Clients never connect to PostgreSQL, and data
access remains behind the owning backend or service layer. Do not substitute
SQL Server, SQLite or another provider unless the user explicitly requests a
separate, justified capability.

The implemented Auth persistence model, migrations and PostgreSQL operating
notes are documented in [`docs/database/README.md`](../../docs/database/README.md)
and [`docs/database/schema.md`](../../docs/database/schema.md). Do not treat a
reserved domain or Agentic AI schema as implemented.

## Change workflow

1. Confirm the target service, model and current migration state before
   editing code. Read the database docs and the owning service tests.
2. Use the repository-pinned `dotnet-ef` tool for migration creation and
   inspection. Review generated SQL and the upgrade path for existing data.
3. Model keys, foreign keys, constraints, indexes, audit fields, retention and
   transaction boundaries explicitly. Do not rely on application checks when a
   database constraint is required for integrity.
4. Keep asynchronous I/O, bounded retries and transaction scope appropriate
   to the operation. Never log passwords, tokens, connection strings or hidden
   Agentic AI reasoning.
5. Update the owning tests and database documentation in the same change.
   Update architecture or an ADR when the persistence boundary materially
   changes.
6. If the change adds or changes an endpoint, follow the endpoint-catalog rule
   as well; database work does not exempt route or contract documentation.

## Test-provider decision

| Situation | Required test provider |
|---|---|
| Pure domain, validation or mapping logic where provider behavior is irrelevant | No database, or the existing isolated test substitute when it is sufficient |
| PostgreSQL SQL translation, type mapping, constraints, indexes, migrations, transactions, locking, concurrency or retry behavior | Real PostgreSQL through Compose, an explicitly enabled database fixture, or an approved disposable container |
| Service persistence or HTTP integration that claims PostgreSQL behavior | Real PostgreSQL with complete assertions for response, state, persistence and side effects |
| Existing protected Auth tests | Preserve them; do not replace or weaken them without explicit user permission |

InMemory is not evidence that PostgreSQL behavior is correct, but its existing
use for deterministic provider-independent tests must not be removed blindly.
When Testcontainers is introduced, use the repository-selected PostgreSQL
image/version, ephemeral credentials, cleanup and an explicit Docker-available
precondition. Do not add a runtime dependency merely to make a guidance task
pass.

## Safety and completion

- Keep connection strings and credentials in environment variables or approved
  secret management; never commit them to code, tests, `.agents` or docs.
- Do not grant an agent write-enabled database MCP access by default. Any
  external database tool must be optional, read-only by default and separately
  authorized for mutations.
- Do not claim a migration, schema, service or integration test is complete
  because a skill, Compose service or document exists. Report unavailable
  Docker, PostgreSQL, SDK or credential prerequisites exactly.
- Run the narrowest applicable checks first. For guidance changes, run the
  agent-resource validator; for source/schema/test changes, also run the
  relevant project tests and database/migration checks.
