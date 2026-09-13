# BLUEVERSE

**Coastal Tourism & Marine Resilience**

BLUEVERSE is an integrated coastal ecosystem platform combining coastal tourism, marine intelligence, environmental resilience, safety, sustainability and future coastal-livelihood capabilities.

This repository is the **v0 foundation** for the SE3090 integrated full-stack and Agentic AI project. The generated React, Flutter and ASP.NET application foundations are now present; major domain workflows and the final Agentic AI workflow remain intentionally deferred to later phases.

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
React Web ───────┐
                 ├──> ASP.NET Core Web API ───> PostgreSQL
Flutter Mobile ──┘             │
                               └──> internal Agentic AI services
```

React and Flutter must not call Agentic AI services directly. The ASP.NET Core API remains the authoritative public application layer.

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
│   ├── api/
│   └── auth/
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
- ASP.NET Core API and Auth service foundations
- PostgreSQL foundation
- authentication/authorization foundation
- role → permission authorization model
- health and Swagger/OpenAPI foundations
- automated backend CI
- testing/documentation foundations

No major domain business workflow belongs in v0.

## Expected generated code locations

The generated codebases are located at:

```text
apps/web/          # React
apps/mobile/       # Flutter
services/api/      # ASP.NET Core public API
services/auth/ # ASP.NET Core Auth service
```

Do not move the Dockerfiles into those generated projects. The Dockerfiles remain under `infrastructure/docker/`.

## Local infrastructure

The intended local entry point is:

```text
Browser / Flutter / Postman
          |
          v
    edge-nginx :8080
       /       \
      /         \
 frontend       API ──> internal Auth
                    |
                 PostgreSQL
```

Local PostgreSQL is published on host port `5432` for pgAdmin4 inspection and management. Backend services still communicate over Docker's internal network; do not use this host exposure for production deployments.

Copy `.env.example` to `.env` before starting the stack.

After the generated applications exist:

```bash
docker compose build
docker compose up -d
docker compose ps
```

### Windows PowerShell scripts

Teammates using Windows with Docker Desktop but without a WSL2 Linux
distribution can use the PowerShell equivalents in `scripts/powershell`.
Docker Desktop must be installed and running. Open PowerShell **as
Administrator**, temporarily allow local scripts, run the required script, and
restore the restricted policy afterward:

```powershell
Set-ExecutionPolicy RemoteSigned
pwsh -File scripts\powershell\verify-foundation.ps1
Set-ExecutionPolicy Restricted
```

For web lockfile synchronization, use the same sequence with
`scripts\powershell\sync-web-lockfile.ps1`. The scripts enforce the elevated
Administrator requirement. Bash equivalents remain under `scripts/bash`.

The expected public gateway is:

```text
http://localhost
```

The Flutter application should use the host/LAN address of this gateway when testing from a physical Android device, not `localhost` inside the phone.

All backend requests use the gateway's `/api/...` namespace. Auth and future backend services are internal destinations of the API and are never called directly by clients. The local gateway is published on host port 80, so no port suffix is required.

## CI

GitHub Actions contains backend and repository workflows. Backend CI restores and builds the API/Auth projects and runs any test projects that are added under the services tree.

## Documentation

Start with:

- `PROJECT_REQUIREMENTS.md`
- `AGENTS.md`
- `docs/architecture/overview.md`
- `docs/development/setup.md`
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
