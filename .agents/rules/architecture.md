# Architecture rules

- Preserve `apps/web` and `apps/mobile` → public ASP.NET Core API → PostgreSQL.
- Keep `docs/contracts/ui-integration.json` as the canonical mapping from a
  shared workflow ID to its React route, Flutter route and public API endpoint
  references. The two clients connect through the workflow contract, never by
  calling one another.
- Clients may use the gateway/public API only; never call internal Auth or
  Agentic AI services directly and never expose database access to clients.
- Prefer literal relative `/api/...` request paths. Absolute API hosts must be
  listed in `publicApi.allowedAbsoluteHosts`, and dynamic network targets that
  CI cannot resolve to the registered public contract are invalid.
- Keep `edge-nginx` as the local Docker entry point. Do not add `/api/v1`-style
  path segments; follow the existing `/api/...` convention.
- Keep role → permission authorization. Do not replace permission checks with
  hard-coded role names or client-only business rules.
- Keep DTOs, application/service layers, async I/O, validation, structured
  errors and OpenAPI at the public API boundary.
- Use `docs/api/endpoint-catalog.md` as the fast inventory of implemented
  frontend, gateway, public API, internal, test-only and Agentic AI routes.
  For every route addition, update, rename, move or removal, update its JSON
  source and regenerate the Markdown view in the same change; it complements,
  but does not replace, the UI workflow registry. Run the endpoint validator
  before completion.
- Avoid speculative services in the v0 foundation. A material boundary,
  persistence or orchestration decision requires an ADR and matching tests.

Before changing a boundary, read the relevant `docs/architecture/`,
`docs/adr/`, security and Agentic AI documents and verify the target service
actually exists.

For any UI route or API request change, also read
[`docs/development/ui-integration.md`](../../docs/development/ui-integration.md)
and the endpoint catalog, update the catalog in the same change, then run the
endpoint and UI integration contract validators.
