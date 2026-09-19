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
| `test/` | authoritative centralized test cases | stable IDs and CI discovery |
| `infrastructure/docker` | service Dockerfiles and gateway config | selected DHI images, minimal runtime assumptions |
| `compose.yaml` | local multi-service entry point | gateway routing, networks, health and secret indirection |
| `.github/workflows` | CI/build/test policy | path filters, complete metrics and failure evidence |
| `docs/architecture`, `docs/adr` | architectural source of truth | update for material boundary decisions |
| `docs/security`, `docs/agentic-ai` | threat and safety source of truth | deterministic validation and approval controls |
| `.agents/skills` | on-demand repository workflows | concise discovery metadata; rules remain authoritative |
| `.agents/scripts` | deterministic agent-resource checks | standard-library validation usable locally and in CI |

## Current foundation facts

- SDK is pinned by `global.json`; do not silently change it.
- Selected base images are listed in `AGENTS.md` and `rules/docker.md`.
- The gateway's public convention is `/api/...`; there is no client-facing
  direct `/auth/...` or bare backend `/health` route.
- Backend and Agentic AI implementations may be absent even when Docker,
  Compose, docs or CI refer to them. A reserved path is not an implementation.
- Test workflows may intentionally report zero tests for not-yet-created suites;
  new behavior must add its authoritative tests and workflow discovery together.

## Source-of-truth lookup

- Setup/commands: `docs/development/setup.md`, `docs/development/ci.md`
- Architecture/networking: `docs/architecture/`, `docs/adr/`
- Security/AI safety: `docs/security/`, `docs/agentic-ai/`
- Test matrix/order: `docs/testing/`
- Ownership/logging: `docs/project/`
