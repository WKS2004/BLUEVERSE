---
name: blueverse-backend-service
description: Create or change a BLUEVERSE ASP.NET Core API, Auth, or internal backend service, including its contract, persistence, Docker integration, tests, CI discovery, and documentation. Do not use for client-only changes.
---

# BLUEVERSE backend service

Read the root `AGENTS.md`, `.agents/routing.md`, and the architecture,
security, testing, Docker, and validation rules. Verify the target service and
requirement before generating code; missing reserved paths are not permission
to create a sample service.

- Keep clients behind the public API and internal services private. Use the
  existing `/api/...` convention without an API-version path segment.
- Use ASP.NET Core 10, the pinned SDK, DTO/application layers, async I/O,
  validation, structured errors, OpenAPI, and named permission checks.
- Use PostgreSQL through the service layer and EF Core migrations. Add relevant
  constraints, indexes, audit fields, transactions, and migration tests.
- A new service includes `services/<name>`, `test/services/<name>`, matching CI
  discovery, `infrastructure/docker/<name>/Dockerfile`, health/readiness, and
  synchronized docs/ADR material when applicable.
- Test normal, invalid, boundary, authorization, dependency-failure,
  cancellation, retry/idempotency, concurrency, and persistence behavior where
  relevant. Do not modify an existing test without explicit user permission.

Run focused restore/build/test checks first, then affected Docker, contract,
and integration checks. Report missing infrastructure or credentials exactly.
