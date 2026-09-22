# Documentation rules

- Keep setup, CI, deployment and operational instructions executable against
  the current checkout; state prerequisites and known foundation gaps.
- Update the narrowest relevant document when behavior, commands, paths,
  boundaries, workflows or ownership change. Avoid duplicating conflicting
  instructions across README, docs and `.agents`.
- Add/update an ADR for a material architecture, service-boundary, framework,
  state-management, data or deployment decision.
- Distinguish implemented behavior from target architecture. Do not document a
  reserved service, future workflow or planned test suite as available.
- Prefer links to detailed source-of-truth docs over copying long matrices into
  agent rules; verify links and commands during validation.
- For database documentation, use `docs/database/` as the detailed source of
  truth and keep `.agents/rules/data-access.md` limited to enforceable routing
  and provider/test decisions.
- UI/API integration behavior is documented in
  [`docs/development/ui-integration.md`](../../docs/development/ui-integration.md)
  and its machine-readable registry. Keep both synchronized with the UI
  workflow subset of actual React, Flutter, gateway and public API routes.
- Cross-platform product scope and user-facing UI quality are documented in
  [`docs/project/ui-experience-principles.md`](../../docs/project/ui-experience-principles.md).
  Keep agent guidance aligned with its default of both React and Flutter for
  participating clients, staff and administrators; platform adaptations must
  not become role ownership rules.
- The complete quick-reference inventory is
  [`docs/api/endpoint-catalog.md`](../../docs/api/endpoint-catalog.md). Update
  its [JSON source](../../docs/api/endpoint-catalog.json) and regenerate the
  Markdown view in the same change whenever any frontend route, client API
  target, backend or internal service route, health route, Swagger/OpenAPI
  mapping, gateway route, test-only fixture route or Agentic AI endpoint is
  added, updated, renamed, moved or removed. Record the exact method/path,
  source, owner, boundary, authorization, purpose and usage. Do not present
  reserved or absent services as implemented. Run the endpoint-catalog
  validator before claiming completion; ordinary route work changes the docs
  catalog, not `.agents` guidance. The universal blocking procedure is in
  [`rules/change-safety.md`](change-safety.md).

For lookup-only endpoint questions, follow
[`rules/endpoint-catalog.md`](endpoint-catalog.md): use the readable catalog
first and inspect implementation source only when the lookup rule's
escalation conditions apply.
