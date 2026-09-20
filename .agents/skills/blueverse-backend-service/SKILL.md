---
name: blueverse-backend-service
description: Create or change a BLUEVERSE ASP.NET Core API, Auth, or internal backend service, including its contract, persistence, Docker integration, tests, CI discovery, and documentation. Do not use for client-only changes.
---

# BLUEVERSE backend service

Read the root `AGENTS.md`, `.agents/routing.md`, and the architecture,
security, testing, Docker and validation rules. Verify the target service and
requirement before generating code. A reserved directory or ignored `bin/` and
`obj/` output is not a service and is not permission to create a sample app.

- Keep clients behind the public API and internal services private. Use `/api/`
  routes without an API-version path segment.
- Use ASP.NET Core 10 and the pinned SDK, DTO/application layers, async I/O,
  server validation, structured errors, OpenAPI and named permission checks.
- Use PostgreSQL through the service layer and EF Core migrations. Add
  constraints, indexes, audit fields, transactions and migration evidence when
  the implementation requires them.
- A new service needs source, authoritative tests, matching CI discovery,
  Docker/health wiring and synchronized docs/ADR material as applicable.
- `dotnet-webapi` and `optimizing-ef-core-queries` are supplementary. This
  skill and BLUEVERSE rules win if their generic guidance conflicts with the
  repository contract.
- Cover normal, invalid, boundary, authorization, dependency-failure,
  cancellation, retry/idempotency, concurrency and persistence behavior where
  relevant. Do not modify an existing test without explicit user permission.

Run focused restore/build/test checks first, then affected Docker, contract and
integration checks. Report absent projects, tools or credentials exactly.
