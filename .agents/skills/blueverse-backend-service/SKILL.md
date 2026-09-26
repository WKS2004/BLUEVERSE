---
name: blueverse-backend-service
description: Create or change a BLUEVERSE ASP.NET Core API, Auth, or internal backend service, including its contract, persistence, Docker integration, tests, CI discovery, and documentation. Do not use for client-only changes.
---

# BLUEVERSE backend service

Before changing any API, Auth or internal service endpoint—including a
controller/minimal route, health or OpenAPI route, test-only fixture, route
rename or route removal—read `docs/api/endpoint-catalog.md`. **MUST** update
`docs/api/endpoint-catalog.json` in the same change with the exact method/path,
public boundary, authorization, owner, operation, purpose, usage and source
path. Remove stale entries when routes are removed. Regenerate the Markdown
view with `python .agents/scripts/validate_endpoint_catalog.py --write-markdown`,
then run `.agents/scripts/validate_endpoint_catalog.py` and
the affected API, OpenAPI, authorization and service tests. A route change is
not complete while the catalog validator fails. Routine route work leaves
`.agents` guidance unchanged.

Read the root `AGENTS.md`, `.agents/routing.md`, and the architecture,
security, data-access, testing, Docker and validation rules. Verify the target
service and requirement before generating code. A reserved directory or ignored `bin/` and
`obj/` output is not a service and is not permission to create a sample app.
Read the applicable requirements and owning component/workflow documents;
preserve the same public contract for React and Flutter.

For a v1 member component, also follow `.agents/rules/v1-development.md`.
Implement the component's business logic and persistence in its own private
service. The existing `services/api` and `services/auth` receive only the
contract-authorized integration; preserve their current logical flows.

- Keep clients behind the public API and internal services private. Use `/api/`
  routes without an API-version path segment.
- Use ASP.NET Core 10 and the pinned SDK, DTO/application layers, async I/O,
  server validation, structured errors, OpenAPI and named permission checks.
- Use PostgreSQL through the service layer and EF Core migrations. Add
  constraints, indexes, audit fields, transactions and migration evidence when
  the implementation requires them.
- For provider-sensitive behavior, follow `blueverse-postgresql-efcore` and
  validate against real PostgreSQL; do not treat an InMemory test as provider
  evidence.
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
