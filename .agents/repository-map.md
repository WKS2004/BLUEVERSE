# BLUEVERSE repository map

This is a navigation aid, not a substitute for inspecting the checkout.
Verify paths and implementation state before relying on any entry.

| Path | Responsibility | Contract to preserve |
|---|---|---|
| `apps/web` | React 19 + TypeScript + Vite client | public API only; equal authorized role and workflow coverage with Flutter |
| `apps/mobile` | Flutter/Dart client and platform shells | public API only; equal authorized role and workflow coverage with React |
| `services/api` | public ASP.NET Core API | public routes, existing auth/permission integration, routing to private services; v1 member logic stays in its owning service |
| `services/auth` | internal authentication service | private behind API/gateway; reuse for v1 and preserve its existing flows |
| `services/<component-service>` | future v1 member-owned private .NET services | each owner implements its own domain logic, provider adapters, persistence, health and tests; confirm the service exists before reporting it implemented |
| `apps/web/src`, `apps/web/e2e` | React test cases colocated with the web package | stable IDs and web CI discovery |
| `apps/mobile/test`, `apps/mobile/integration_test` | Flutter unit/widget and integration test cases | stable IDs and mobile CI discovery |
| `services/*/tests` | service-owned backend test projects | stable IDs and backend CI discovery |
| `services/ai/*/tests`, `services/ai-agents/*/tests`, `services/agents/*/tests` | Agentic AI service test/evaluation cases | stable IDs and AI CI discovery |
| `infrastructure/docker` | service Dockerfiles and gateway config | selected DHI images, minimal runtime assumptions |
| `compose.yaml` | local multi-service entry point | gateway routing, networks, health and secret indirection |
| `.github/workflows` | CI/build/test policy | path filters, complete metrics and failure evidence |
| `docs/architecture`, `docs/adr` | architectural source of truth | update for material boundary decisions |
| `PROJECT_REQUIREMENTS.md`, `docs/README.md` | project scope and documentation index | select the applicable release and owning component contract |
| `docs/security`, `docs/agentic-ai` | threat and safety source of truth | deterministic validation and approval controls |
| `docs/contracts/ui-integration.json` | shared React/Flutter workflow, route and public API registry | one workflow ID, both client routes for the participating roles and declared `/api/...` references |
| `scripts/validation` | dependency-free repository and UI integration validators | fail-closed route/API checks and validator tests |
| `docs/api/endpoint-catalog.md` | readable frontend, gateway, public API, test-only and AI endpoint inventory | start here for route lookup |
| `docs/api/endpoint-catalog.json` | machine-checked catalog source | update with route changes and regenerate Markdown |
| `docs/database/README.md`, `docs/database/schema.md` | PostgreSQL/EF Core model and schema reference | distinguish implemented tables from target schema |
| `.agents/skills` | on-demand repository workflows | concise discovery metadata; rules remain authoritative |
| `.agents/rules/data-access.md` | PostgreSQL, EF Core, migration and test-provider policy | use with backend/test workflows for persistence work |
| `.agents/registry` | provenance and compatibility metadata for vendored skills | every imported external skill is source-pinned and licensed |
| `.agents/skill-overlays` | repository-specific bindings for portable skills | overlays extend skills without overriding project rules |
| `.agents/evals` | deterministic routing and resource-quality fixtures | no secrets, hidden reasoning or generated output |
| `.agents/scripts` | deterministic agent-resource checks | standard-library validation usable locally and in CI |

## Persistent conventions

- The SDK is pinned by `global.json`; selected images are listed in
  `AGENTS.md` and `rules/docker.md`.
- Client-facing requests use the public `/api/...` boundary. The React
  and Flutter surfaces have equal authorized role and business-capability
  coverage, with shared workflow IDs and server-owned permissions.
- For v1, G00 shared contracts precede the four parallel member feature
  branches. Actual Agentic AI runtime work is deferred to `agentic-ai/**`
  until the integrated feature components pass G07; pre-G07 member branches
  may implement only their own typed access seam and unavailable behavior.
- The endpoint catalog is the current route inventory; the UI registry is
  its workflow-facing subset. Add implementation routes and client targets
  to both as applicable.
- Verify a service, workflow, migration or test suite in source before
  reporting it as implemented. A reserved path, prose target or ignored
  `bin/`/`obj/` output is not implementation evidence.
- Use real PostgreSQL evidence for provider-sensitive behavior; an isolated
  provider serves only provider-independent tests.

## Source-of-truth lookup

- Project scope and applicable release/component documents:
  `PROJECT_REQUIREMENTS.md`, `docs/README.md`
- v1 ownership, readiness and branch gates: `docs/v1/`; agent enforcement is
  in `.agents/rules/v1-development.md`
- Setup/commands: `docs/development/setup.md`, `docs/development/ci.md`
- UI integration: `docs/development/ui-integration.md`,
  `docs/contracts/ui-integration.json`
- Complete endpoint/routing inventory: `docs/api/endpoint-catalog.md`,
  `docs/api/endpoint-catalog.json`
- Architecture/networking: `docs/architecture/`, `docs/adr/`
- Security/AI safety: `docs/security/`, `docs/agentic-ai/`
- Test matrix/order: `docs/testing/`
- Database foundation/schema: `docs/database/README.md`,
  `docs/database/schema.md`, `.agents/rules/data-access.md`
- Ownership/logging: `docs/project/`
