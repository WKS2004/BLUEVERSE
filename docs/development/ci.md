# CI Workflows

BLUEVERSE uses repository/source validation workflows, dedicated client,
backend and Agentic AI test workflows, and three workflows for the Dockerized
web application and backend services.

## DHI registry credentials

The selected Docker Hardened Images are pulled from `dhi.io`, which requires
registry authentication. Configure these GitHub repository secrets before
expecting Docker workflows to pass:

| Secret | Value |
|---|---|
| `DHI_USERNAME` | Docker ID for a PAT, or the Docker organization name for an OAT. |
| `DHI_TOKEN` | A Docker personal access token or organization access token with read/pull access to the required DHI repositories. |

The web, backend and Compose health workflows log in to `dhi.io` immediately
before any DHI image is pulled. Tokens are supplied only through GitHub Secrets
and are never written to repository files or workflow logs. Pull requests from
forks do not receive repository secrets; their Docker jobs therefore require a
trusted branch or an approved credential strategy before they can pull DHI
images.

## Source and repository workflows

| Workflow | Scope | Current behavior |
|---|---|---|
| `repository-ci.yml` | Repository foundation | Checks required root files/directories, validates `.agents` resources and CI helper scripts, and checks registry metadata, overlays, routing fixtures and links. |
| `ui-integration.yml` | `apps/web/**`, `apps/mobile/**`, `services/**`, API/architecture contract docs, `infrastructure/docker/**`, `compose.yaml`, all workflows and the UI contract | Validates the shared React/Flutter workflow registry, rejects wrong frontend routes and unregistered/direct backend targets, and tests the validator. |
| `backend-ci.yml` | `services/**` | Restores/builds discovered ASP.NET projects when present. Backend tests run in the dedicated backend test workflow. |
| `web-ci.yml` | `apps/web/**` | Runs `npm install`, ESLint and the Vite build. |
| `mobile-ci.yml` | `apps/mobile/**` | Runs Flutter dependency resolution and analysis. Mobile tests run in the dedicated mobile test workflow. |
| `web-tests.yml` | `apps/web/src/**`, optional `apps/web/e2e/**` | Runs all React web test cases and reports overall metrics. |
| `mobile-tests.yml` | `apps/mobile/test/**`, optional `apps/mobile/integration_test/**` | Runs all Flutter mobile test cases and reports test metrics. |
| `backend-tests.yml` | `services/**` | Runs every discovered backend microservice test project from service-local test directories in one workflow, with overall and per-service metrics. |
| `agentic-ai-tests.yml` | `services/ai/**`, `services/ai-agents/**`, `services/agents/**` | Runs only for pushes and pull requests targeting `main`, `dev` or `agentic-ai/**`; runs every discovered Agentic AI test suite from each agent’s local `tests/` directory in one workflow, with overall and per-service metrics. |

### UI integration contract

The source of truth for cross-client UI integration is
`docs/contracts/ui-integration.json`. It maps one workflow ID to its React
route, Flutter route and public `/api/...` endpoint references. The clients do
not call one another or internal service hostnames; the shared workflow ID is
the cross-platform connection and the public API is the only backend boundary.

`ui-integration.yml` runs for client, service, gateway, registry and validator
changes. `web-ci.yml` and `mobile-ci.yml` also run the validator whenever their
client changes. The gate rejects:

- a frontend route used by React or Flutter but absent from the registry;
- a literal `/api/...` call that is not a registered endpoint reference;
- a dynamic or otherwise unverifiable network target;
- a call to Auth, Agentic AI, PostgreSQL, another internal service or a Docker
  hostname or an absolute host not allowlisted by the public API contract; and
- `/api/v1`-style versioned paths.

The current foundation registry contains the generated shared home surface and
the implemented `auth-session-management` workflow. The Auth workflow maps
both client `/login` routes to the public `/api/auth/login`, `/api/auth/refresh`,
`/api/auth/me`, `/api/auth/sessions`, `/api/auth/logout` and
`/api/auth/logout-all-devices` references. Future client workflows must add
their public API contract, both client surfaces and tests in the same change.
The static gate complements the runtime API/Auth gateway and service tests.

Run the same checks locally from the repository root:

```bash
python scripts/validation/validate_ui_integrations.py
python -m unittest discover -s scripts/validation/tests -p "test_*.py"
```

The general source and repository workflows also run for `features/**` and
`agentic-ai/**` branches when their existing path filters match. The Agentic
AI test workflow is restricted to `main`, `dev` and `agentic-ai/**` pushes or
pull requests. Docker image workflows remain limited to `main` and `dev`. The
Agentic AI test workflow reports zero tests while its service
implementations are not yet present. The backend test workflow runs every
discovered service-local test project and fails when a service source project
has no matching test project or when any discovered suite fails.

### Agent-resource validation

`repository-ci.yml` treats `.agents/`, `docs/`, root configuration and other
foundation paths as repository changes. It runs the dependency-free
`.agents/scripts/validate_agent_resources.py` validator, which checks:

- required root and `.agents` files;
- skill names, descriptions, frontmatter and placeholders;
- registry status, local paths, pinned imported revisions and compatibility
  metadata;
- third-party notices, portable-skill overlays and routing evaluation cases;
- relative Markdown links, secret-like assignments and generated/cache paths.

Run the same gate locally from the repository root:

```bash
python .agents/scripts/validate_agent_resources.py
git diff --check
```

The optional upstream skill validator requires a YAML dependency and is not a
CI prerequisite. The foundation verifier is a separate infrastructure check;
its Docker/runtime results remain environment-dependent even though the API
and Auth source projects are now checked in.

### Test metrics and result artifacts

Each dedicated test workflow prints a metrics block in the job log and appends
a table to the GitHub Actions step summary. The table includes total,
completed, passed, failed, skipped, errors, not-run and duration values. The
backend and Agentic AI workflows additionally print and summarize one row per
service while retaining an overall aggregate row. JUnit, TRX, Flutter machine
logs and console output are uploaded as workflow artifacts for failed or
successful runs. Backend VSTest output is written as `.trx`; the metrics helper
parses both `.trx` and JUnit `.xml` files. When backend test projects are
discovered, missing or empty parseable result files fail the workflow instead
of silently reporting zero tests. A test is counted as passed or failed
according to the test runner’s complete assertion result; the metrics do not
infer correctness from an HTTP status code alone. Test completeness remains a
test-authoring and review responsibility: new behavior must bring its scenario,
boundary and extreme-condition tests in the same change.

The repository foundation workflow also runs the dependency-free unit tests for
`.github/scripts/report_test_metrics.py` so result-file discovery and the
fail-closed metrics guard remain covered when CI helper code changes.

## Docker workflows

| Workflow | Trigger/dependency | Purpose |
|---|---|---|
| `.github/workflows/docker-web-build.yml` | Push or pull request for `main`/`dev` | Synchronizes the React lockfile and builds the web image. |
| `.github/workflows/docker-backend-build.yml` | Push or pull request for `main`/`dev` | Builds the public API and discovered ASP.NET backend services sequentially. |
| `.github/workflows/docker-stack-health.yml` | After both build workflows succeed for the same commit | Starts the complete Compose stack and checks the public health endpoints. |

## Branch and path behavior

The web and backend Docker workflows run for pushes to `main` and `dev`, and
for pull requests targeting either branch.

On `main`, both image workflows always build, regardless of which files changed.

On `dev`, the build step runs only when relevant paths change:

- Web: `apps/web/**`, `infrastructure/docker/frontend/**`, `scripts/Unix/bash/sync-web-lockfile.sh`, `scripts/Windows/powershell/sync-web-lockfile.ps1`, `.dockerignore`, or the web workflow itself.
- Backend: `services/**`, `infrastructure/docker/api/**`, any first-level backend directory under `infrastructure/docker/**` other than `frontend` and `edge-nginx`, `.dockerignore`, `global.json`, or the backend workflow itself.

Backend Docker infrastructure changes trigger validation even when the matching service directory is missing. This allows the workflow to report an invalid Dockerfile/service layout instead of silently skipping it. The scope checks use the changed-file range for a push or pull request; an initial `dev` push is treated as requiring a build.

## Web image workflow

The web workflow first runs `scripts/Unix/bash/sync-web-lockfile.sh`. This uses the DHI Node 24 image to create or update `apps/web/package-lock.json` from `package.json`, so `npm ci` has a synchronized lockfile during the Docker build. The workflow then validates `apps/web`, the frontend Dockerfile and the frontend Nginx configuration, and builds from the repository root as `blueverse-frontend:ci`.

The synchronization script is called directly rather than duplicating its commands in the workflow. If the script is moved or missing, the workflow fails with the expected path so the workflow and local lockfile process cannot silently diverge.

The image is built locally on the GitHub runner. It is not pushed to a container registry.

## Backend image workflow

The backend workflow requires `services/api` and builds it first. It then
examines each first-level directory under `services`, excluding `api`:

1. Find `Program.cs`.
2. Check for ASP.NET Core indicators in the program or project files.
3. Find the service's direct `.csproj` file.
4. Use `infrastructure/docker/<service-name>/Dockerfile`.
5. Build the image as `blueverse-<service-name>:ci`.

Non-ASP.NET directories are skipped. The workflow also checks for backend Dockerfiles that have no matching service directory. A missing service directory, missing Dockerfile, missing project file or failed Docker build is recorded as an error, but does not stop the remaining services from building. The workflow prints all collected errors at the end and then fails.

The backend Dockerfile convention is therefore:

```text
services/<service-name>/              # ASP.NET service source and .csproj
infrastructure/docker/<service-name>/Dockerfile
```

The backend Docker workflow builds the checked-in `services/api` first and then
the discovered `services/auth` service. Missing directories, Dockerfiles,
project files or failed builds are still collected and reported as errors.

## Compose stack health workflow

The stack workflow is not directly triggered by a push or pull request. It listens for completed runs of both image workflows, waits for both matching workflow runs for the same commit to complete successfully, and then checks out that exact commit. Failed or cancelled image workflows prevent the health checks from starting.

It searches the repository root for `compose.yaml` or `docker-compose.yml`. If neither file exists, the workflow fails with an explicit error. Because this workflow uses a separate runner from the web image workflow, it runs the same web lockfile synchronization script again before Compose builds the frontend. It then validates and starts the stack with a CI host port of `8080` and waits for the services to start.

The following gateway endpoints are checked with retries:

```text
http://127.0.0.1:8080/health
http://127.0.0.1:8080/api/health
http://127.0.0.1:8080/api/<service-name>/health
```

The first endpoint checks the frontend. The second checks the public API. Additional endpoints are discovered from ASP.NET services under `services`, excluding `api`.

When a health check fails, the workflow prints Compose status and recent service logs. Compose is always torn down with volumes and orphan containers removed, including after a failed build or health check.

Each GitHub Actions workflow uses its own runner, so the stack workflow reruns lockfile synchronization and rebuilds its Compose images rather than reusing the local image tags from the two build workflows. None of the workflows pushes images to a registry.
