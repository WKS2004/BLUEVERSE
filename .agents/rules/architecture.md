# Architecture rules

- Preserve `apps/web` and `apps/mobile` → public ASP.NET Core API → PostgreSQL.
- Give React Web and Flutter Mobile equal authorized role, capability and
  workflow-action coverage. Neither has stakeholder priority or design
  emphasis. Read the applicable requirements and owning component document
  when planning a feature.
- Keep `docs/contracts/ui-integration.json` as the canonical mapping from a
  shared workflow ID to its React route, Flutter route and public API endpoint
  references. The two clients connect through the workflow contract, never by
  calling one another.
- Preserve workflow parity across clients: the same authorized actions, state
  transitions, role → permission behavior and public API contract must be
  available in both surfaces. Layout and device integration may differ without
  reducing business capability on either client.
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
- Avoid speculative services. A material boundary,
  persistence or orchestration decision requires an ADR and matching tests.

Before changing a boundary, read the relevant `docs/architecture/`,
`docs/adr/`, security and Agentic AI documents and verify the target service
actually exists.

For any UI route or API request change, also read
[`docs/development/ui-integration.md`](../../docs/development/ui-integration.md)
and the endpoint catalog, update the catalog in the same change, then run the
endpoint and UI integration contract validators.

For UI quality and product scope, also read
[`docs/project/ui-experience-principles.md`](../../docs/project/ui-experience-principles.md).
Do not make technical dashboards, raw payloads or unexplained analytics the
default experience when a clearer domain task-oriented interface is suitable.
