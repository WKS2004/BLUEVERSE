# CI Workflows

BLUEVERSE uses repository/source validation workflows plus three workflows for
the Dockerized web application and backend services.

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
| `repository-ci.yml` | Repository foundation | Checks required root files and top-level directories. |
| `backend-ci.yml` | `services/**` | Restores/builds/tests discovered ASP.NET projects when present. |
| `web-ci.yml` | `apps/web/**` | Runs `npm install`, ESLint and the Vite build. |
| `mobile-ci.yml` | `apps/mobile/**` | Runs Flutter dependency resolution, analysis and tests. |

These workflows also run for `features/**` branches when their existing path
filters match. The backend workflow builds the checked-in API project and
discovers additional ASP.NET projects when they are added. It also runs any
matching test projects; no API test project is checked in yet.

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

The checked-in `services/api` directory is required and is built first. The
workflow also validates every Dockerfile under `infrastructure/docker` against
its matching service directory. Since the Auth Dockerfile exists while
`services/auth` is still pending, the full backend image workflow continues to
report that missing Auth service until it is added.

## Compose stack health workflow

The stack workflow is not directly triggered by a push or pull request. It listens for completed runs of both image workflows, waits for both matching workflow runs for the same commit to complete successfully, and then checks out that exact commit. Failed or cancelled image workflows prevent the health checks from starting.

It searches the repository root for `compose.yaml` or `docker-compose.yml`. If neither file exists, the workflow fails with an explicit error. Because this workflow uses a separate runner from the web image workflow, it runs the same web lockfile synchronization script again before Compose builds the frontend. It then validates and starts the stack with a CI host port of `8080` and waits for the services to start.

The following gateway endpoints are checked with retries:

```text
http://127.0.0.1:8080/health
http://127.0.0.1:8080/api/health
http://127.0.0.1:8080/api/<service-name>/health
```

The first endpoint checks the frontend. The second checks the public API.
Additional endpoints are discovered from ASP.NET services under `services`,
excluding `api`.

When a health check fails, the workflow prints Compose status and recent service logs. Compose is always torn down with volumes and orphan containers removed, including after a failed build or health check.

Each GitHub Actions workflow uses its own runner, so the stack workflow reruns lockfile synchronization and rebuilds its Compose images rather than reusing the local image tags from the two build workflows. None of the workflows pushes images to a registry.
