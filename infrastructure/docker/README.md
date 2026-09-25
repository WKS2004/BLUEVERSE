# Docker Infrastructure

Custom Dockerfiles remain here so application codebases can stay close to their normal IDE-generated structures.

## Services

- `api/` — ASP.NET Core public API
- `auth/` — ASP.NET Core Auth service
- `frontend/` — React build + static Nginx runtime
- `edge-nginx/` — local reverse proxy configuration

PostgreSQL is consumed directly from its selected DHI image in `compose.yaml`; a custom PostgreSQL Dockerfile is intentionally not created.

The selected DHI images require authentication to `dhi.io`. Local developers
should run `docker login dhi.io` with a Docker PAT or organization access token.
CI uses the `DHI_USERNAME` and `DHI_TOKEN` GitHub Secrets documented in
`docs/development/ci.md`.

Auth is an internal backend service. It is not attached to the edge network and must be reached through the API's `/api/auth/...` reverse-proxy route.

Every ASP.NET backend service should keep its Dockerfile in a matching directory under `infrastructure/docker/<service-name>/Dockerfile`. The Docker backend workflow discovers first-level ASP.NET services under `services`, builds the public `api` service first, and then builds the remaining services one by one.

The frontend Nginx file is a `server`-context configuration because the Dockerfile installs it under `/etc/nginx/conf.d/default.conf`. Global directives such as `worker_processes`, `events` and `http` belong only in the edge gateway's `/etc/nginx/nginx.conf`.

Both Nginx layers map extensionless internal error URIs to the branded static
`404.html` and `500.html` recovery assets. The frontend image receives them
from `apps/web/public`; Compose mounts the same assets read-only into the edge
gateway so it can show a recovery page if the frontend upstream is unavailable.
Direct requests for the physical asset names are redirected to the extensionless
React `/404` and `/500` routes.
When the frontend is available, unknown extensionless browser paths load the
React app and its router shows the 404 inside the shared header/main/footer
layout without changing the requested URL.
Error interception is enabled only for browser traffic. The `/api/` proxy
preserves API status codes and structured error bodies.

## Important

The React/mobile application directories and both ASP.NET applications are
checked in. Their Docker build contexts are:

```text
apps/web
apps/mobile
services/api
services/auth
```

The Docker web and backend build workflows use branch and path filters on
`main`, `dev` and the supported work branches. GitHub starts each workflow
when a listed application, service, Docker, Compose, lockfile, SDK or workflow
path changes; unrelated changes do not start the image build. The workflow
then checks its target branch and build scope before building. Before the web
image build, it runs the shared DHI Node 24 lockfile synchronization script so
`npm ci` can work when `apps/web/package-lock.json` is missing or stale. Missing
backend service directories/Dockerfiles and failed backend builds are reported
while later backend services continue building.
