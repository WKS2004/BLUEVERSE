# Task routing and validation matrix

Use this file as the first lookup after the root `AGENTS.md`. Read the
universal `rules/change-safety.md`, then only the rows that match changed paths.
Keep the initial context limited to the root instructions, this routing matrix,
the universal safety/validation rules and one relevant workflow skill. Load
supporting references only when the selected skill requires them.
Add `rules/documentation.md` when behavior, commands or architecture are
documented, `rules/testing.md` for behavior changes, and `rules/ai-usage.md`
for every AI-assisted contribution.

When a task matches a repository skill below, use that skill after reading the
universal rule. Skills provide the workflow; rules remain authoritative.

| Changed paths or task | Rules to read | Source of truth / focused checks |
|---|---|---|
| `apps/web/**` | architecture, security, testing, validation | `apps/web/README.md`; web lint/build/test workflow |
| `apps/mobile/**` | architecture, security, testing, validation | `apps/mobile/README.md`; Flutter analyze/test workflow |
| `docs/contracts/**`, `scripts/validation/**`, UI route/API integration | architecture, security, testing, documentation, validation | `docs/development/ui-integration.md`; shared UI/API contract validator and client workflows |
| `services/api/**`, `services/auth/**`, `services/*/**` | architecture, security, testing, validation, docker if containerized | `docs/api/`, architecture/security docs; matching backend workflow |
| `services/**` | architecture, security, testing, validation, docker if containerized | `docs/testing/`; backend workflow discovery |
| `services/ai/**`, `services/ai-agents/**`, `services/agents/**`, AI orchestration/tools/workflows | architecture, security, testing, validation | `docs/agentic-ai/`; deterministic safety/evaluation workflow |
| `compose.yaml`, `infrastructure/docker/**` | architecture, security, docker, validation | `docs/deployment/`, `infrastructure/docker/README.md`; config/health checks |
| `.github/workflows/**`, `.github/scripts/**` | testing, documentation, validation, git | `docs/development/ci.md`; YAML/script and discovery review |
| `docs/**`, `README.md`, `PROJECT_REQUIREMENTS.md` | documentation, architecture/security/testing as applicable, validation | affected source/workflows; link and command review |
| `.agents/**`, `AGENTS.md` | change-safety, documentation, validation | this directory, root rules, `git diff --check` |
| `.agents/skills/**` | change-safety, documentation, validation | selected skill, registry entry and linked supporting files |

## Skill selection

| Task | Skill |
|---|---|
| Whole-repository audit or readiness review | `$blueverse-foundation-audit` |
| ASP.NET API, Auth, persistence or backend service | `$blueverse-backend-service` |
| Shared React/Flutter API contract or permission UI | `$blueverse-client-contract` |
| Test design, stable IDs, matrices, fixtures or runners | `$blueverse-test-design` |
| Agentic AI orchestration, tools, approvals or evaluation | `$blueverse-agentic-ai-workflow` |
| Docker, Compose, edge-nginx, networks or health | `$blueverse-docker-gateway` |
| GitHub Actions, path filters, metrics or artifacts | `$blueverse-ci-validation` |

Do not load a skill merely because its component appears in the repository;
load it when the requested work matches the workflow in its description.

## Portable framework skill routing

Portable external skills are supplementary guidance. Load the matching
BLUEVERSE-owned skill and rules first; project rules win if guidance conflicts.

| Task | Supplementary skill | Required BLUEVERSE context |
|---|---|---|
| React/Vite performance, rendering or bundle work | `vercel-react-best-practices` | `blueverse-client-contract`, architecture, security, validation |
| Flutter architecture, HTTP, JSON, routing or widget/integration tests | matching `flutter-*` skill | `blueverse-client-contract` or `blueverse-test-design`, security, validation |
| ASP.NET endpoint/OpenAPI work | `dotnet-webapi` | `blueverse-backend-service`, architecture, security, validation |
| EF Core query performance | `optimizing-ef-core-queries` | `blueverse-backend-service`, architecture, security, validation |
| .NET test execution or test-quality analysis | matching `run-tests`, `assertion-quality`, `test-*` or `grade-tests` skill | `blueverse-test-design`, testing, validation; read the matching overlay when present |

The current React project is React 19 + Vite, not Next.js. Ignore Next.js,
React Server Components, server-action and server-route advice unless the
repository explicitly adopts those technologies. Flutter and React must still
use only the public `/api/...` boundary.

## Cross-layer triggers

- Public endpoint, DTO, auth or database change: architecture + security +
  testing; include client/contract and persistence effects.
- New service or boundary: architecture + docker + documentation + testing;
  add its source, test discovery, health/readiness and ADR as applicable.
- Permission or identity change: security + testing; cover allow/deny,
  unauthenticated and role-to-permission semantics.
- Model/tool/approval change: security + testing; validate schemas,
  authorization, unsafe output, approval and audit/state transitions.
- Docker/gateway/health change: docker + architecture + documentation +
  validation; check internal isolation and public `/api/...` routing.

## Efficient inspection order

1. `git status --short` and the user-requested scope.
2. Changed file plus direct imports/references and its local README/config;
   exclude generated and cache paths by default.
3. Routed rule files and only the linked detailed docs needed for a decision.
4. Existing tests and the matching CI discovery path.
5. Narrow checks first, broader checks only when the change crosses a boundary.

Do not read every document or run every workflow by default. Do not skip a
routed rule merely because the target implementation is currently absent; use
the absence as evidence and report it accurately.
