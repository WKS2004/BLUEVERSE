# Architecture rules

- Preserve `apps/web` and `apps/mobile` → public ASP.NET Core API → PostgreSQL.
- Clients may use the gateway/public API only; never call internal Auth or
  Agentic AI services directly and never expose database access to clients.
- Keep `edge-nginx` as the local Docker entry point. Do not add `/api/v1`-style
  path segments; follow the existing `/api/...` convention.
- Keep role → permission authorization. Do not replace permission checks with
  hard-coded role names or client-only business rules.
- Keep DTOs, application/service layers, async I/O, validation, structured
  errors and OpenAPI at the public API boundary.
- Avoid speculative services in the v0 foundation. A material boundary,
  persistence or orchestration decision requires an ADR and matching tests.

Before changing a boundary, read the relevant `docs/architecture/`,
`docs/adr/`, security and Agentic AI documents and verify the target service
actually exists.
