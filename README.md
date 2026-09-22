# BLUEVERSE

**Coastal Tourism & Marine Resilience**

BLUEVERSE is an integrated coastal ecosystem platform combining coastal tourism, marine intelligence, environmental resilience, safety, sustainability and future coastal-livelihood capabilities.

This repository is the **v0 foundation** for the SE3090 integrated full-stack and Agentic AI project. The React and Flutter starter projects include the shared Auth session workflow, while the public API gateway and internal Auth service are checked in. Major domain workflows and the final Agentic AI workflow remain deferred to later phases.

## Project identity

- **Project:** BLUEVERSE
- **Domain:** Coastal Tourism & Marine Resilience
- **Current release phase:** v0 — Foundation
- **Version format:** `x.y.z`
  - `x=0`: Foundation
  - `x=1`: Coastal Tourism & Operations
  - `x=2`: Environmental Resilience
  - `x=3`: Fisheries & Coastal Livelihoods
  - `y`: integration/development release
  - `z`: bug-fix release

## SE3090 integration target

The system is intentionally designed as one integrated system:

```text
React Web Client ───────┐
                         ├──> ASP.NET Core Web API ───> PostgreSQL
Flutter Mobile Client ──┘             │
                                     └──> internal Agentic AI services
```

React and Flutter must not call Agentic AI services directly. The ASP.NET Core API remains the authoritative public application layer.

React Web and Flutter Mobile are peer product surfaces for clients, staff and
administrators. The platform choice is based on the user's context and
preference, not a fixed role assignment: both clients use the same workflow
intent, role → permission model and public API contract. React can optimize
wide browser workspaces and Flutter can optimize mobile and field interaction,
but neither platform is reserved for one stakeholder group.

Every new, generated or updated UI is a cross-layer change. Register its
shared workflow ID, React route, Flutter route and public `/api/...` endpoint
references in [`docs/contracts/ui-integration.json`](docs/contracts/ui-integration.json)
and pass the UI integration contract gate before merging. The clients do not
call one another or internal backend service hostnames.

## Repository layout

```text
BLUEVERSE/
├── .agents/
├── .github/
├── apps/                    # IDE-generated application code
│   ├── web/                 # React
│   └── mobile/              # Flutter
├── docs/
├── infrastructure/
│   └── docker/
│       ├── api/
│       ├── edge-nginx/
│       ├── frontend/
│       └── auth/
├── services/                # ASP.NET Core services
│   ├── api/                  # checked-in public API foundation
│   └── auth/                 # internal Auth service
├── AGENTS.md
├── compose.yaml
├── global.json
├── LICENSE.md
├── PROJECT_REQUIREMENTS.md
├── render.yaml
└── ...
```

## v0 scope

v0 establishes:

- repository and engineering conventions
- Docker/microservice infrastructure
- DHI-based container strategy
- edge Nginx gateway
- React and Flutter application foundations
- ASP.NET Core public API gateway and internal Auth service foundations
- PostgreSQL foundation
- authentication/authorization foundation
- server-issued multi-account Auth sessions with rotating refresh tokens,
  protected browser cookies and mobile secure-storage integration
- active-session state separated from ended-session lifecycle logs, with
  one-day default and 30-day Remember Me session lifetimes
- role → permission authorization model
- health and Swagger/OpenAPI foundations
- automated web/backend Docker builds and Compose health validation
- testing/documentation foundations

No major domain business workflow belongs in v0.

## Application and service status

The checked-in client codebases are located at:

```text
apps/web/          # React
apps/mobile/       # Flutter
```

`services/api/` and `services/auth/` are the service locations referenced by
Compose, the Dockerfiles, Render and CI. Both backend projects are checked in;
Auth remains internal and is reachable by clients only through the public API
gateway. Do not move the Dockerfiles into generated projects; keep them under
`infrastructure/docker/`.

## Local infrastructure

The intended local entry point is:

```text
Browser / Flutter / Postman
          |
          v
    edge-nginx :80 (BLUEVERSE_HTTP_PORT)
       /       \
      /         \
 frontend       API ──> internal Auth
                    |
                 PostgreSQL
```

Local PostgreSQL is published on host port `5432` for pgAdmin4 inspection and management. Backend services still communicate over Docker's internal network; do not use this host exposure for production deployments.

Copy `.env.example` to `.env` before starting the stack.

```bash
bash scripts/Unix/bash/sync-web-lockfile.sh
docker compose build
docker compose up -d
docker compose ps
```

On Windows without WSL2, use the PowerShell lockfile script described in
`docs/development/setup.md`.

The current checkout can run the API or Auth service directly with `dotnet run`
when the required environment variables and PostgreSQL instance are available.
The complete Compose stack requires Docker Desktop, DHI registry access and the
values documented in `.env.example`.

### Windows PowerShell scripts

Teammates using Windows with Docker Desktop but without a WSL2 Linux
distribution can use the PowerShell equivalents in `scripts/Windows/powershell`.
Docker Desktop must be installed and running. Open PowerShell **as
Administrator**, temporarily allow local scripts, run the required script, and
restore the restricted policy afterward:

```powershell
Set-ExecutionPolicy RemoteSigned
pwsh -File scripts\Windows\powershell\verify-foundation.ps1
Set-ExecutionPolicy Restricted
```

For web lockfile synchronization, use the same sequence with
`scripts\Windows\powershell\sync-web-lockfile.ps1`. The scripts enforce the elevated
Administrator requirement. Bash equivalents remain under `scripts/Unix/bash`.

The expected public gateway is:

```text
http://localhost
```

The local Compose file publishes the gateway on host port `80` by default. The Docker Stack Health workflow overrides this to port `8080` on the GitHub Actions runner.

The Flutter application uses port `80` for the local gateway. Android
emulators reach the laptop through `http://10.0.2.2:80`; physical Android
devices must use the laptop's current Wi-Fi/LAN address, not `localhost` inside
the phone. Pass that address to both `flutter run` and `flutter build` with
`--dart-define=BLUEVERSE_API_BASE_URL=http://<laptop-lan-ip>:80`; no
laptop-specific IP is compiled into the client. If managed Wi-Fi isolates the
phone from the laptop, use `adb reverse tcp:80 tcp:80` over USB; the mobile
client includes that as a final local-development fallback.

All backend requests use the gateway's `/api/...` namespace. Auth and future backend services are internal destinations of the API and are never called directly by clients. The local gateway is published on host port 80, so no port suffix is required.

## CI

GitHub Actions contains separate source, test, contract, repository-policy
and Docker workflows so each check remains visible on a commit or pull
request:

- `repository-ci.yml` checks required repository structure.
- `ui-integration.yml` validates the shared React/Flutter route and public
  API contract whenever clients, services, gateway routing or the registry
  change.
- `backend-ci.yml` restores/builds discovered ASP.NET projects when present.
- `web-ci.yml` lints/builds the React project when relevant.
- `mobile-ci.yml` analyzes the Flutter project when relevant.
- `web-tests.yml` runs all React Web test cases and reports test metrics.
- `mobile-tests.yml` runs all Flutter Mobile test cases and reports test metrics.
- `backend-tests.yml` runs all backend microservice test cases in one workflow,
  with overall and per-service metrics.
- `agentic-ai-tests.yml` runs all Agentic AI test cases in one workflow, with
  overall and per-service metrics.

The source, test and Docker image workflows combine the active work branch
families (`main`, `dev`, `features/**`, `agentic-ai/**`, `claude/**`,
`codex/**`, `antigravity/**`, `gemini/**`, `maintenance/**` and `bug-fixes/**`)
with workflow-specific path filters. A workflow starts only when its relevant
paths change; manual dispatch remains available for an explicit run.

Test cases stay in their owning package's default locations: React under
`apps/web/src` (and optional `apps/web/e2e`), Flutter under `apps/mobile/test`
and `apps/mobile/integration_test`, backend services under
`services/<service>/tests`, and each Agentic AI service under its local
`tests/` directory. The repository does not use a centralized root `test/`
tree for authoritative cases.

- `docker-web-build.yml` synchronizes the React lockfile with the DHI Node 24 image, then builds the React web image.
- `docker-backend-build.yml` builds the public API and discovered ASP.NET services one by one.
- `docker-stack-health.yml` waits for both build workflows, starts the root Compose file and checks frontend/API/service health endpoints.

The web and backend build workflows run on supported branch families when their relevant application, service, Docker infrastructure, lockfile synchronization, Compose, workflow or build-context paths change. The stack-health workflow waits for the required image builds for the same commit and runs only for pull requests targeting `main`; it synchronizes the web lockfile again on its separate runner before running the Compose health checks. Backend build failures are collected across all services so later services are still checked before the workflow fails.

The Docker backend workflow builds `services/api` first and then discovers and
builds the other ASP.NET service directories, including the internal Auth
service. The backend test workflow fails if a service source project has no
discovered service-local test project.

`github-config-sync.yml` copies only `.github/**` into focused pull requests
for every branch except `main` and `dev-backup`, then queues automatic squash
merges when repository settings allow GitHub Actions to do so. `branch-policy.yml`
enforces lowercase approved names, while `dev-backup.yml` preserves mistaken
backup commits before synchronizing the backup ref to the exact `dev` commit.

See [Docker CI Workflows](docs/development/ci.md) for the complete workflow behavior and service conventions.

## Documentation

Start with:

- `PROJECT_REQUIREMENTS.md`
- `AGENTS.md`
- `docs/development/agent-resources.md`
- `docs/architecture/overview.md`
- `docs/development/setup.md`
- `docs/development/ci.md`
- `docs/development/ui-integration.md`
- `docs/project/ui-experience-principles.md`
- `docs/api/README.md`
- `docs/database/schema.md`
- `docs/project/foundation-gap-analysis.md`
- `docs/security/security.md`
- `docs/testing/strategy.md`
- `docs/deployment/local.md`
- `docs/contracts/ui-integration.json`
- `docs/adr/README.md`
- `docs/agentic-ai/architecture.md`

## Security

Never commit:

- passwords
- JWT signing secrets
- API keys
- database connection strings containing credentials
- production environment files
- private user data

Use `.env.example` for variable names and safe example values only.

## Assignment basis

This repository structure follows the SE3090 requirement for an integrated React + Flutter + ASP.NET Core + PostgreSQL + Agentic AI system, Git/GitHub, CI, testing, documentation, ADRs and deployment evidence.
