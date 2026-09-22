# Task routing and validation matrix

Use this file as the first lookup after the root `AGENTS.md`. Read the
universal `rules/change-safety.md`, then only the rows that match changed paths.
Keep the initial context limited to the root instructions, this routing matrix,
the universal safety/validation rules and one relevant workflow skill. Load an
additional cross-layer skill when the routing row explicitly selects it, and
load supporting references only when the selected skill requires them.
Add `rules/documentation.md` when behavior, commands or architecture are
documented, `rules/testing.md` for behavior changes, and `rules/ai-usage.md`
for every AI-assisted contribution.

When a task matches a repository skill below, use that skill after reading the
universal rule. Skills provide the workflow; rules remain authoritative.

| Changed paths or task | Rules to read | Source of truth / focused checks |
|---|---|---|
| `apps/web/**` | architecture, security, testing, validation | `apps/web/README.md`; web lint/build/test workflow |
| `apps/mobile/**` | architecture, security, testing, validation | `apps/mobile/README.md`; Flutter analyze/test workflow |
| API/route/endpoint lookup or inventory question without a code change | endpoint-catalog | **Fast path:** read `.agents/rules/endpoint-catalog.md` and `docs/api/endpoint-catalog.md`; inspect source only for doubt, missing data, validator drift or an explicit verification request |
| `docs/contracts/**`, `scripts/validation/**`, UI route/API integration | architecture, security, testing, documentation, validation, endpoint-catalog | `docs/development/ui-integration.md`, `docs/api/endpoint-catalog.md`; shared UI/API and endpoint-catalog validators |
| Any frontend route, client API target, backend/internal/test/AI endpoint, health/OpenAPI route or gateway mapping addition, update, rename, move or removal; `docs/api/endpoint-catalog.*` | architecture, security, documentation, testing, validation, endpoint-catalog | **Mandatory:** read `docs/api/endpoint-catalog.md`, update its JSON source in the same change, regenerate Markdown, run the source/catalog validator, and run UI validation when a client workflow is affected |
| `services/api/**`, `services/auth/**`, `services/*/**` | architecture, security, testing, documentation, validation, endpoint-catalog, docker if containerized | `docs/api/endpoint-catalog.md`, `docs/api/`, architecture/security docs; matching backend workflow |
| PostgreSQL, EF Core, migrations, persistence models, constraints, indexes or provider-specific queries | architecture, security, data-access, testing, documentation, validation | `docs/database/README.md`, `docs/database/schema.md`, owning service migrations/tests; `blueverse-postgresql-efcore` workflow |
| `services/**` | architecture, security, testing, validation, docker if containerized | `docs/testing/`; backend workflow discovery |
| `services/ai/**`, `services/ai-agents/**`, `services/agents/**`, AI orchestration/tools/workflows | architecture, security, testing, validation, endpoint-catalog | `docs/agentic-ai/`; deterministic safety/evaluation workflow |
| `compose.yaml`, `infrastructure/docker/**` | architecture, security, docker, documentation, validation, endpoint-catalog | `docs/api/endpoint-catalog.md`, `docs/deployment/`, `infrastructure/docker/README.md`; config/health checks |
| `.github/workflows/**`, `.github/scripts/**` | testing, documentation, validation, git | `docs/development/ci.md`; YAML/script and discovery review |
| `docs/database/**` | data-access, architecture, testing, documentation, validation | owning EF model/migrations/tests plus `.agents/rules/data-access.md` |
| `docs/**`, `README.md`, `PROJECT_REQUIREMENTS.md` | documentation, architecture/security/testing as applicable, validation | affected source/workflows; link and command review |
| `.agents/**`, `AGENTS.md` | change-safety, documentation, validation | this directory, root rules, `git diff --check`; edit guidance only on explicit user request or approval |
| `.agents/skills/**` | change-safety, documentation, validation | selected skill, registry entry and linked supporting files |

## Skill selection

| Task | Skill |
|---|---|
| Whole-repository audit or readiness review | `$blueverse-foundation-audit` |
| ASP.NET API, Auth, persistence or backend service | `$blueverse-backend-service` |
| PostgreSQL, EF Core persistence, migrations or provider-specific data behavior | `$blueverse-postgresql-efcore` |
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

For any React/Flutter workflow, treat both clients as first-class surfaces for
clients, staff and administrators. Plan both routes and shared role/permission
behavior by default; use platform strengths to adapt interaction rather than
assigning a stakeholder group to one frontend. Read
`docs/project/ui-experience-principles.md` for the user-friendly,
scope-aligned and realistic UI standard.

## Cross-layer triggers

- Any endpoint or route addition, update, rename, move or removal—including
  frontend, client request, public/internal/test/AI, health, OpenAPI and
  gateway routes: architecture + security + testing + documentation;
  update `docs/api/endpoint-catalog.json` and its Markdown view in the same
  change, then run the endpoint validator before completion. Include
  client/contract and persistence effects where applicable.
- New service or boundary: architecture + docker + documentation + testing;
  add its source, test discovery, health/readiness and ADR as applicable.
- Permission or identity change: security + testing; cover allow/deny,
  unauthenticated and role-to-permission semantics.
- Model/tool/approval change: security + testing; validate schemas,
  authorization, unsafe output, approval and audit/state transitions.
- Docker/gateway/health change: docker + architecture + documentation +
  validation; update the gateway section of the docs catalog in the same
  change and check internal isolation and public `/api/...` routing.
- Persistence model, migration, constraint, index, transaction or provider
  behavior change: read `data-access.md`, use the PostgreSQL/EF Core workflow,
  update the owning database and test documentation, and use real PostgreSQL
  evidence for provider-sensitive behavior. Do not replace protected tests or
  add a database runtime dependency solely to satisfy agent guidance.

## Efficient inspection order

1. `git status --short` and the user-requested scope.
2. For route/API work, read `docs/api/endpoint-catalog.md` before
   opening the complete source tree.
3. Changed file plus direct imports/references and its local README/config;
   exclude generated and cache paths by default.
4. For persistence work, read `docs/database/README.md` and
   `docs/database/schema.md` before scanning migrations or the complete service.
5. Routed rule files and only the linked detailed docs needed for a decision.
6. Existing tests and the matching CI discovery path.
7. Narrow checks first, broader checks only when the change crosses a boundary.

Do not read every document or run every workflow by default. Do not skip a
routed rule merely because the target implementation is currently absent; use
the absence as evidence and report it accurately.
