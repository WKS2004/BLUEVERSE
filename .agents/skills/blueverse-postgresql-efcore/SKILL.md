---
name: blueverse-postgresql-efcore
description: Design, change, review or test BLUEVERSE PostgreSQL and EF Core persistence, including models, migrations, constraints, indexes, queries and provider-specific integration behavior. Do not use for client-only work.
---

# BLUEVERSE PostgreSQL and EF Core

Use this workflow for persistence changes. Read the root `AGENTS.md`,
`.agents/routing.md`, `.agents/rules/data-access.md`, and the owning
`blueverse-backend-service` or `blueverse-test-design` skill first.

## Workflow

1. Read `docs/database/README.md`, `docs/database/schema.md`, the target
   service configuration, migrations and local tests. Verify the service and
   model exist before changing them.
2. Preserve the repository path
   `ASP.NET Core → EF Core 10 → Npgsql → PostgreSQL 16`. Use the pinned
   `dotnet-ef` tool for migrations and inspect the generated SQL and data
   upgrade path.
3. Add deliberate keys, foreign keys, constraints, indexes, audit fields and
   transaction boundaries. Keep secrets and raw tokens out of database rows
   and diagnostic output.
4. Use no database or the existing isolated substitute only for tests whose
   behavior is provider-independent. Use real PostgreSQL for SQL translation,
   migrations, constraints, indexes, transactions, concurrency, retries and
   persistence assertions.
5. Keep existing tests as protected specifications. Ask before changing,
   deleting, skipping or weakening one; add requirement-based regression cases
   with stable IDs for new behavior.
6. Update database and testing documentation. If routes or client API targets
   change, also update and validate the endpoint catalog and UI integration
   contract as applicable.

## Completion checks

- `.agents/scripts/validate_agent_resources.py`
- Relevant `dotnet test` project and provider-specific test command
- Migration/model review and available PostgreSQL or Docker smoke checks
- `git diff --check`

Report blocked environment checks instead of treating them as successful.
