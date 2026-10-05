# BLUEVERSE Foundation Gap Analysis

This review compares the checked-in repository with the implementation plan from the referenced BLUEVERSE analysis and the SE3090 Project Guidelines PDF.

## Current status

**Implementation snapshot — 2026-10-06:** Member 2's marine-safety backend,
React Web surfaces and Flutter marine screens are implemented on
`features/marine-safety`. The current React suite passes 201/201, including
44/44 marine React tests. The backend project builds cleanly and contains 118
authored cases; its latest VSTest execution timed out during testhost startup,
so 117/117 remains the last completed backend run. Flutter runtime analysis,
the other three member components, executable Agentic AI and integrated G07
evidence remain open. The detailed [Member 2 component contract](../v1/components/member-2-marine-conditions-safety-intelligence.md)
and [branch tracker](../v1/member-branch-workflow.md#component-branch-status)
record the current evidence and limits.

| Area | Status | Evidence / next action |
|---|---|---|
| React web project | Auth/account foundation and Member 2 marine workflows present | `apps/web` provides `/signin` and `/signup`, protected-cookie session recovery and saved-account switching, profile/password/session management, account deletion, permission-aware user/role/permission administration, the coastal overview, and marine conditions/suitability, history and safety-profile routes. The full React suite passes 201 cases (44 marine) on 2026-10-06. Other v1 workflows remain unimplemented. |
| Flutter mobile project | Auth/account foundation and Member 2 marine workflows present | `apps/mobile` provides signed-out coastal onboarding, `/signin` and `/signup`, public-gateway Auth with platform secure storage, profile/password/session management, account deletion, permission-aware user/role/permission administration, the coastal overview, and marine conditions/suitability, history and safety-profile screens. Local Flutter analysis/runtime verification remains open because the SDK/dependency setup did not start. Other v1 workflows remain unimplemented. |
| Flutter gateway configuration | Local device path documented; deployment transport unresolved | `ApiGatewayConfig` currently accepts only HTTP on fixed port 80 and does not follow a changed `BLUEVERSE_HTTP_PORT` value. Its checked-in Android fallback differs from the standard emulator address used in the setup guides. Pass `BLUEVERSE_API_BASE_URL` explicitly for local emulator/device runs; design and verify HTTPS-capable release configuration before treating mobile deployment as ready. See `apps/mobile/lib/data/services/api_gateway_config.dart`, `apps/mobile/README.md` and `docs/deployment/local.md`. |
| Cross-platform product scope | Marine workflow is implemented in React and Flutter; other domains are pending | React and Flutter use the same marine public API and permission model for conditions, history, suitability and profile review. Other v1 workflows remain unimplemented. See `docs/v1/cross-platform-and-permissions.md`. |
| ASP.NET Core API | Foundation plus marine public routing | `services/api` provides the .NET 10 public gateway, OpenAPI/Swagger, CORS, JWT validation, health and YARP routing. It forwards `/api/marine/*` to the private `services/marine-safety` service; the service owns marine domain behavior. Other member endpoints remain future work. |
| Auth service | Session lifecycle implemented | `services/auth` provides registration/login, server-issued installations, PBKDF2 password hashing, 15-minute JWTs, rotating hashed refresh tokens, one/30-day absolute sessions, five-account-per-device and five-session-per-account limits, scoped logout, active-session management with ended-session logs, permission policies, role/user administration, health and bootstrap seeding. |
| PostgreSQL | Auth and marine persistence models/migrations present | Development Compose publishes DHI PostgreSQL 16 as `5432:5432` on all host interfaces for pgAdmin4. Use only on a trusted development network; promotion from `dev` to `main` changes the binding to `127.0.0.1:5432:5432`. Auth and `services/marine-safety` own separate EF Core models and migrations in the shared database. Marine production Auth-query behavior still needs PostgreSQL-backed verification. |
| Edge gateway | Present | `edge-nginx` routes frontend and `/api/*` traffic only. The API forwards `/api/auth/*` to Auth and `/api/marine/*` to the private marine-safety service. |
| Permission authorization | Implemented in Auth and marine-safety | Auth remains identity and permission-grant owner. Marine-safety validates active Auth sessions and resolves current role permissions from Auth data; its test host uses deterministic doubles, so production SQL translation remains unverified on PostgreSQL. |
| Agentic AI | Four v1 agents specified, no executable service | Separate contracts for the Planning & Coordination, Marine Conditions Intelligence, Coastal Experience & Biodiversity, and Safety & Operations agents are under `docs/v1/agents/`. No executable agent workflow is present yet. |
| ML biodiversity capability | v1 integration target, not implemented | The separate IT3091 workstream supplies the initial trained model. BLUEVERSE v1 must integrate genuine internal inference when available and handle temporary failure without fabricating predictions. No inference implementation is checked in. |
| Testing | Marine service and React evidence present; mobile/device/production integration evidence remains open | The current React suite passes 201/201, including 44 marine cases; Web build, endpoint catalog and UI integration validation pass. The backend project builds with zero warnings/errors and contains 118 authored cases; its latest VSTest attempt timed out during testhost startup, so 117/117 remains the last completed run. The service tests use an in-memory database and deterministic Auth/provider doubles; they do not validate the production Auth-table SQL. Direct Dart formatting completed for the ten changed marine sources with the expected package-resolution warning; Flutter analysis/runtime evidence remains open because the local package configuration and cached dependencies are absent. No device `integration_test` suite or other member-domain coverage is present. |
| CI/CD | Source, client, Docker and UI-contract checks present; main-branch trigger and marine health-route gaps | `web-ci.yml` and `mobile-ci.yml` run the shared UI integration validator; `web-tests.yml`, `mobile-tests.yml` and `backend-tests.yml` discover and report service/client suites; `ui-integration.yml` rechecks the registry when either client, backend, gateway or contract changes. The Docker stack-health workflow derives each health URL from its service folder name. For `services/marine-safety`, it currently probes `/api/marine-safety/health`, while the registered gateway prefix is `/api/marine`; the workflow needs an explicit public-route mapping before it can verify marine health. The current marine branch has no hosted CI evidence. `backend-tests.yml` also has path filters, so it does not run for every push or pull request to `main` as the assignment specifies. |
| Agent resources | Finalized and validated | `.agents/` contains eight BLUEVERSE-owned workflows and fifteen pinned supplementary skills (23 validated in total), PostgreSQL/EF Core data-access guidance, registry/provenance metadata, a portable .NET overlay, routing evaluations and a dependency-free validator enforced by `repository-ci.yml`. |
| Deployment | Configuration present, evidence pending | Render API/Auth/PostgreSQL and Vercel documentation exist; live URLs, migrations and deployment evidence must be recorded before submission. |
| Submission package | Requirements documented, evidence pending | The group still needs the single consolidated PDF with Group and Individual Report sections, each student's own reflection and signed declaration, a runnable APK, accessible ten-minute demonstration video, evaluator links and secure test-account instructions. Submitted links and services must remain accessible through 21 October 2026. |
| Git/GitHub | Repository present | Git metadata and branch history are present in the reviewed checkout. Continue using focused commits, pull requests and contribution evidence. |

## Final v1 start-readiness decision — 2026-09-26 (historical)

**GO for G00; conditional GO for component coding after G00; not ready to
claim v1 complete.** The repository contains the foundation and detailed
member, agent, relationship, integration and branch contracts needed to begin
the shared contract-freeze work. At that decision date, the source inventory
contained only `services/api` and `services/auth`; the marine-safety service
and React workflows were implemented on the Member 2 branch afterward.

The named owners and their feature/Agentic AI branches are now recorded in the
[canonical owner map](ai-team-members.md). The team must still agree the
shared public and private contracts, cross-component IDs/data ownership, persistence strategy,
service/container/network identities, authorization and failure semantics,
health/readiness behavior, test/CI discovery and shared UI/catalog edits
before starting the four concurrent component implementations. The complete
checklist and post-G00 gates are in the
[v1 requirements coverage and final readiness review](../v1/requirements-coverage-and-readiness.md).
Actual Agentic AI work stays behind G07; the member-service integration seam
and explicit `not connected`/unavailable behavior are part of the earlier
member branches.

The v1 scope and documents are ready to plan and begin, but final acceptance
still has known work: the backend tests workflow is path-filtered despite the
guideline's every-push/every-pull-request-to-`main` requirement; the corrected
Flutter launch test needs a green CI run, duplicate Auth test IDs need a
separately approved baseline correction, and hosted Flutter HTTPS/device
evidence plus deployment and submission artifacts remain outstanding. These
are tracked as later delivery gates, not reasons to serialize the four
component branches.

## Guideline-critical work still required

The SE3090 specification requires a GitHub Actions workflow that restores,
builds and runs backend tests on every push and pull request to `main`.
The current backend workflows apply path filters, which can skip a
documentation-only or unrelated change. Before submission, update the
workflow triggers or obtain written assignment guidance accepting the
path-filtered behavior; a green run on an affected path does not establish
the every-push/every-PR rule.

The Project Guidelines require a final integrated workflow that starts in one client, passes through ASP.NET Core, PostgreSQL and Agentic AI, requires review or approval in the other client, and returns an updated status. This is intentionally not a v0 deliverable, but it must be planned as a first-class cross-platform acceptance test.

The v1 submission needs the four separately documented
[member components](../v1/README.md). Each requires backend, database, React,
Flutter, testing, Git/documentation and a distinct Agentic AI contribution.
Every permitted role and business action must work in both clients. The
current repository has Member 2's marine backend and React workflows in
progress on `features/marine-safety`; the Flutter surface, the other three
member components and integrated Agentic AI remain outstanding. The branch is
not yet a completed or integrated v1 delivery.

## Foundation acceptance checks

After copying `.env.example` to `.env`, setting a unique JWT signing key and
local passwords, and starting Compose, verify:

```text
GET http://localhost/health
GET http://localhost/api/health
GET http://localhost/api/auth/health
GET http://localhost/api/swagger/v1.json
GET http://localhost/api/auth/swagger/v1/swagger.json
```

These checks prove gateway routing and service readiness only; they do not
substitute for authentication, authorization, database, client integration, or
Agentic AI tests.

On 2026-09-23, `docker compose --env-file .env config --quiet` passed and
`docker compose --env-file .env up --detach --no-build --wait` reported healthy
services after the PostgreSQL host binding and API network change. All
five listed gateway requests plus `/api/swagger` returned HTTP 200. The Auth
health response reported the database connected, a read-only PostgreSQL query
listed the Auth tables and `__EFMigrationsHistory`, and Windows
`127.0.0.1:5432` accepted a TCP connection for pgAdmin4. No Android device or
emulator was available in `flutter devices`; that device check remains open.
The authenticated Postman collection still needs a local non-administrator
account for a complete run. These working-tree changes have not been pushed,
so hosted CI evidence for them is not yet available.

## UI integration acceptance check

Before a real screen is considered complete, verify the shared registry and
validator:

```text
python scripts/validation/validate_ui_integrations.py
python -m unittest discover -s scripts/validation/tests -p "test_*.py"
```

The current registry contains the shared home, Auth session and registration,
profile, dashboard, Auth administration and recovery workflows with both
client routes, plus the three Member 2 marine workflows. React implements the
marine routes; corresponding Flutter routes are registered but the screens
are not implemented. Acceptance evidence includes both client surfaces,
backend endpoint tests and the public gateway/API request boundary; a passing
frontend build alone is not sufficient.

## Agent-resource acceptance check

Agent resources are independently considered ready when the following command
passes from the repository root:

```bash
python .agents/scripts/validate_agent_resources.py
```

The current checkout passes this gate with twenty-three repository skills
validated. The optional upstream YAML-based skill validator and the Windows
foundation verifier remain environment-dependent; their unavailable or
elevation-blocked results must be reported separately from the agent-resource
gate.
