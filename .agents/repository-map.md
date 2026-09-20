# BLUEVERSE repository map

This is a navigation aid, not a substitute for inspecting the checkout.
Verify paths and implementation state before relying on any entry.

| Path | Responsibility | Contract to preserve |
|---|---|---|
| `apps/web` | React 19 + TypeScript + Vite client | public API only; current code is starter UI |
| `apps/mobile` | Flutter/Dart client and platform shells | public API only; current code is starter app |
| `services/api` | public ASP.NET Core API | REST, DTO/application layers, validation, auth proxy, OpenAPI |
| `services/auth` | internal authentication service | private behind API/gateway |
| `services/<name>` | future/internal backend services | own data/service contract and matching tests |
| `apps/web/src`, `apps/web/e2e` | React test cases colocated with the web package | stable IDs and web CI discovery |
| `apps/mobile/test`, `apps/mobile/integration_test` | Flutter unit/widget and integration test cases | stable IDs and mobile CI discovery |
| `services/*/tests` | service-owned backend test projects | stable IDs and backend CI discovery |
| `services/ai/*/tests`, `services/ai-agents/*/tests`, `services/agents/*/tests` | Agentic AI service test/evaluation cases | stable IDs and AI CI discovery |
| `infrastructure/docker` | service Dockerfiles and gateway config | selected DHI images, minimal runtime assumptions |
| `compose.yaml` | local multi-service entry point | gateway routing, networks, health and secret indirection |
| `.github/workflows` | CI/build/test policy | path filters, complete metrics and failure evidence |
| `docs/architecture`, `docs/adr` | architectural source of truth | update for material boundary decisions |
| `docs/security`, `docs/agentic-ai` | threat and safety source of truth | deterministic validation and approval controls |
| `docs/contracts/ui-integration.json` | shared React/Flutter workflow, route and public API registry | one workflow ID, both relevant client routes and declared `/api/...` references |
| `scripts/validation` | dependency-free repository and UI integration validators | fail-closed route/API checks and validator tests |
| `.agents/skills` | on-demand repository workflows | concise discovery metadata; rules remain authoritative |
| `.agents/registry` | provenance and compatibility metadata for vendored skills | every imported external skill is source-pinned and licensed |
| `.agents/skill-overlays` | repository-specific bindings for portable skills | overlays extend skills without overriding project rules |
| `.agents/evals` | deterministic routing and resource-quality fixtures | no secrets, hidden reasoning or generated output |
| `.agents/scripts` | deterministic agent-resource checks | standard-library validation usable locally and in CI |

## Current foundation facts

- SDK is pinned by `global.json`; do not silently change it.
- Selected base images are listed in `AGENTS.md` and `rules/docker.md`.
- The gateway's public convention is `/api/...`; there is no client-facing
  direct `/auth/...` or bare backend `/health` route.
- The current UI registry contains only the generated shared home surface and
  no domain API endpoints; the starter clients must not add unregistered calls.
- Backend and Agentic AI implementations may be absent even when Docker,
  Compose, docs or CI refer to them. A reserved path, empty source folder or
  directory containing only ignored `bin/`/`obj/` output is not an
  implementation.
- Test workflows may intentionally report zero tests for not-yet-created suites;
  new behavior must add its authoritative tests and workflow discovery together.

## Source-of-truth lookup

- Setup/commands: `docs/development/setup.md`, `docs/development/ci.md`
- UI integration: `docs/development/ui-integration.md`,
  `docs/contracts/ui-integration.json`
- Architecture/networking: `docs/architecture/`, `docs/adr/`
- Security/AI safety: `docs/security/`, `docs/agentic-ai/`
- Test matrix/order: `docs/testing/`
- Ownership/logging: `docs/project/`
