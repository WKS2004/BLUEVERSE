# BLUEVERSE

**Coastal Tourism & Marine Resilience**

BLUEVERSE is an integrated coastal ecosystem platform combining coastal tourism, marine intelligence, environmental resilience, safety, sustainability and future coastal-livelihood capabilities.

The checked-in application provides the **v0 foundation** for the SE3090
integrated full-stack and Agentic AI project. The submission target is
**v0 plus v1**. React and Flutter implement the shared Auth registration and session workflows,
backed by the public API gateway, internal Auth service and PostgreSQL. The
four v1 business components, four agents and biodiversity integration are
documented targets, not yet implemented behavior.

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

React Web and Flutter Mobile are equally complete product surfaces. Every
participating role can perform every permitted business workflow in either
client. Both use the same workflow intent, role → permission model, data and
public API contract. Screen layout or device input may differ without
restricting a role, action or result.
Both clients follow the shared visual system in [`DESIGN.md`](DESIGN.md): React
maps it to Tailwind styles in `apps/web`, and Flutter maps it to native theme
and widgets in `apps/mobile`.

The [v0 foundation guide](docs/v0/README.md) maps the implemented technical
components and their integration path. The
[v1 documentation index](docs/v1/README.md) links the separate member
component and agent documents, shared workflows and acceptance requirements.
The full baseline is [PROJECT_REQUIREMENTS.md](PROJECT_REQUIREMENTS.md).

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

## Local setup and deployment

The supported local deployment runs the checked-in React client, public API,
internal Auth service and PostgreSQL with Docker Compose. Run Compose commands
from the repository root (the directory containing `compose.yaml`). The public
entry point is `edge-nginx`; clients use the gateway's `/api/...` routes and do
not connect directly to internal services.

See [Local Deployment](docs/deployment/local.md) for deployment topology and
additional platform notes.

### Install the prerequisites

- **Git** to clone and update the repository. Install it from the [official Git
  downloads](https://git-scm.com/downloads).
- **Docker Compose v2.** On Windows, install [Docker Desktop for Windows](https://docs.docker.com/desktop/setup/install/windows-install/)
  and use the WSL 2 backend with Linux containers. Start Docker Desktop before
  running commands. On macOS, install [Docker Desktop](https://docs.docker.com/desktop/);
  on Linux, install Docker Engine and the [Compose plugin](https://docs.docker.com/compose/install/).
- **Docker Hardened Images (DHI) registry access.** The Compose images are
  pulled from `dhi.io`; you need a Docker PAT or organization access token with
  access to those images. See the [Docker infrastructure guide](infrastructure/docker/README.md).

For host-side development, also install:

- **Python 3** for the repository's documentation, route-contract and agent
  resource validators. These checks use the standard library and need no
  third-party Python packages. Python is not required for the containerized
  local deployment.
- **Node.js 24 LTS and npm** for running the React app and its tools directly
  on the host. Install Node 24 from the [official Node.js download page](https://nodejs.org/en/download/).
  The containerized web build already uses the DHI Node 24 image, so a host
  Node installation is not required just to deploy with Compose.
- **.NET 10 SDK** for running or building the ASP.NET projects directly on the
  host. The repository selects SDK `10.0.400` in `global.json` and allows the
  latest feature band. Install the SDK from the [official .NET 10 download
  page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0); on Windows,
  `winget install Microsoft.DotNet.SDK.10` is also available. The Compose build
  uses the DHI .NET 10 SDK image, so a host SDK is not required for the
  containerized deployment.
- **Flutter and Android tooling** only if you will run the mobile client; see
  [Mobile setup](apps/mobile/README.md).

Open a new terminal after installing tools and check them:

```text
git --version
docker --version
docker compose version
```

For host-side web development, check `node --version` (major version 24) and
`npm --version`. For host-side .NET work, run `dotnet --version` from the
repository root; it should select the SDK version allowed by `global.json`.
For repository validation scripts, check `python --version` and confirm it
reports Python 3.

### Get the source

Clone the repository using its GitHub clone URL, then open a terminal in the
`BLUEVERSE` directory. If the repository is already cloned, open the terminal
at its root; the root contains `compose.yaml`.
Replace `<repository-clone-url>` below with the clone URL from the repository
page.

```bash
git clone <repository-clone-url>
cd BLUEVERSE
```

### Configure local settings

Authenticate Docker to the DHI registry. Docker prompts for credentials; use a
PAT or organization token, and never save it in `.env` or a repository file:

```text
docker login dhi.io
```

Create the local environment file from the safe template:

```powershell
Copy-Item .env.example .env
```

On macOS, Linux or WSL2, use:

```bash
cp .env.example .env
```

Edit `.env` and replace the example `JWT_SIGNING_KEY`, database password and
administrator password with unique local values. The JWT signing key must be
at least 32 UTF-8 bytes. Use a password manager or secure random generator;
never commit `.env`. Keep `AUTH_SERVICE_URL=http://auth:8080` because it is the
Docker-internal API-to-Auth address. `ADMIN_EMAIL` and `ADMIN_PASSWORD` are
used for the local administrator account.

The gateway is published on host port `80` by default, and PostgreSQL is
available to host tools such as pgAdmin at `127.0.0.1:5432`. Make sure these
ports are available. If port 80 is already in use, set `BLUEVERSE_HTTP_PORT=8080`
in `.env` and use `http://localhost:8080` below. PostgreSQL's host port is
currently fixed at `5432` in `compose.yaml`.

### Build and start the stack

The checked-in web lockfile is used by the container build. If it is missing
or out of sync with `apps/web/package.json`, synchronize it first; the script
runs DHI Node 24 in Docker, so it does not need Node installed on the host:

```bash
bash scripts/Unix/bash/sync-web-lockfile.sh
```

On native Windows PowerShell, use
`scripts\Windows\powershell\sync-web-lockfile.ps1`; see the exact
PowerShell setup instructions in [Developer Setup](docs/development/setup.md).

Validate the environment and build/start the Compose deployment:

```bash
docker compose --env-file .env config --quiet
docker compose build
docker compose up -d
docker compose ps
```

The first build pulls the DHI base images and may take several minutes. To
build and run in the foreground while watching logs, use
`docker compose up --build`. Auth waits for PostgreSQL to become healthy, applies its
migrations and seeds the configured administrator account.

### Verify the local deployment

Open the web app at `http://localhost` (or the port configured by
`BLUEVERSE_HTTP_PORT`). The unified Swagger UI is at
`http://localhost/api/swagger`. Check the frontend, public API and Auth health
routes; each should return HTTP 200:

```powershell
curl.exe -f http://localhost/health
curl.exe -f http://localhost/api/health
curl.exe -f http://localhost/api/auth/health
```

The React and Flutter clients show branded 404 and 500 recovery screens for
unknown pages and unexpected rendering failures. Both Nginx layers also serve
matching static error pages when the frontend cannot respond. API errors keep
their structured response bodies and are not replaced by browser error pages.

If you changed `BLUEVERSE_HTTP_PORT`, add `:8080` (or your selected port) to
`localhost` in the web, Swagger and health URLs above.

On macOS, Linux or WSL2, run:

```bash
curl --fail --silent --show-error http://localhost/health
curl --fail --silent --show-error http://localhost/api/health
curl --fail --silent --show-error http://localhost/api/auth/health
```

In `docker compose ps`, PostgreSQL should report `healthy`
and the frontend, API, Auth and gateway containers should be running. The
Docker stack also exposes PostgreSQL to host database tools only on
`127.0.0.1:5432`; Auth itself connects to it over the Docker network.

If a service does not start or a health URL fails, inspect its recent logs:

```text
docker compose logs --tail=100 edge-nginx frontend api auth postgres
```

Common first-run causes are Docker Desktop not running, missing DHI registry
access, an unedited required value in `.env`, or host ports `80`/`5432` already
being occupied.

### Stop the stack and preserve data

Stop containers and networks with:

```text
docker compose down
```

The named PostgreSQL volume remains so local data survives a restart. Only use
`docker compose down --volumes` when you intentionally want to permanently
delete the local database and start from a clean state.

For Flutter on an Android emulator, explicitly pass the laptop gateway as
`http://10.0.2.2:80` with
`--dart-define=BLUEVERSE_API_BASE_URL=http://10.0.2.2:80`. A physical Android
device must use the laptop's current Wi-Fi/LAN address with the same
compile-time setting. The explicit value overrides the checked-in,
environment-specific Android fallback; do not rely on that fallback for a
device run. The current mobile resolver accepts local HTTP on port `80` only,
so a deployment HTTPS configuration remains future work. The Flutter
resolver also has a fixed port-80 limit; changing `BLUEVERSE_HTTP_PORT` does
not update the mobile client's accepted port. Keep the gateway on port 80 for
Flutter or update the resolver before changing that setting. If managed Wi-Fi
isolates the phone from the laptop, use `adb reverse tcp:80 tcp:80` over USB.
See [Mobile setup](apps/mobile/README.md) for the full device workflow and
current limitations.

## CI

GitHub Actions contains separate source, test, contract, repository-policy
and Docker workflows so each check remains visible on a commit or pull
request:

- `repository-ci.yml` checks required repository structure.
- `ui-integration.yml` validates the shared React/Flutter route and public
  API contract whenever clients, services, gateway routing or the registry
  change.
- `backend-ci.yml` restores/builds the checked-in API and Auth projects and
  discovers additional ASP.NET projects as they are added.
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
- `docker-stack-health.yml` waits for both build workflows, starts the root Compose file and checks frontend/API/service health endpoints plus the public API/Auth Swagger routes.

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
- `DESIGN.md`
- `AGENTS.md`
- `docs/v0/README.md` and `docs/v1/README.md`
- `docs/development/agent-resources.md`
- `docs/architecture/overview.md`
- `docs/development/setup.md`
- `docs/development/ci.md`
- `docs/development/ui-integration.md`
- `docs/project/ui-experience-principles.md`
- `docs/api/README.md`
- `docs/api/endpoint-catalog.md`
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
