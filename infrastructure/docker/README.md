# Docker Infrastructure

Custom Dockerfiles remain here so application codebases can stay close to their normal IDE-generated structures.

## Services

- `api/` — ASP.NET Core public API
- `auth/` — ASP.NET Core Auth service
- `coastal-operations/` — ASP.NET Core Coastal Operations service
- `coastal-planner/` — ASP.NET Core Coastal Planner service
- `experience-biodiversity/` — ASP.NET Core Experience & Biodiversity service
- `marine-safety/` — ASP.NET Core Marine Safety service
- `agentic-ai/` — four private Python runtime image shells for the paired v1 agent roles
- `frontend/` — React build + static Nginx runtime
- `edge-nginx/` — local reverse proxy configuration

PostgreSQL is consumed directly from its selected DHI image in `compose.yaml`; a custom PostgreSQL Dockerfile is intentionally not created.
Auth and all four component services use the configured PostgreSQL database,
login and password in the current Compose setup. Each service applies its own
EF Core migrations and history table; Experience & Biodiversity stores its
domain tables in the `experience_biodiversity` schema. The shared database
credential and schema ownership policy remain subject to all-owner G00
ratification. No service SQL initialization file or second database is
required by the current stack.

The selected DHI images require authentication to `dhi.io`. Local developers
should run `docker login dhi.io` with a Docker PAT or organization access token.
CI uses the `DHI_USERNAME` and `DHI_TOKEN` GitHub Secrets documented in
`docs/development/ci.md`.

Auth and all component services are internal backend services. They are not
attached to the edge network; the API reaches them through `/api/auth/...`,
`/api/operations/...`, `/api/planner/...`, `/api/marine/...` and
`/api/experiences/...`. Coastal Planner and Experience & Biodiversity call
their configured peers through the API where a public permission contract
exists. Marine Safety reads the Experience activity catalogue over the private
network. Keep client traffic on the public API gateway.

Coastal Operations is also private, has no host-published port, and is reachable
through the API's `/api/operations/...` reverse-proxy route. The shared Swagger
UI at `/api/swagger` loads its OpenAPI document through that gateway route.
The API validates Auth JWTs, strips bearer tokens and cookies from forwarded
Coastal Operations requests, and signs the actor ID plus `operations.*`
permission claims with `COASTAL_OPERATIONS_CONTEXT_KEY`. The private service
accepts only that short-lived, single-use signed context on authorized routes.

The edge gateway permits request bodies up to 6 MiB on Coastal Operations
assessment-item subroutes so evidence uploads can carry a 5 MiB PNG plus
multipart framing; the service enforces the file-size and image-dimension
limits.
Coastal Operations stores accepted image bytes in the named
`blueverse_coastal_operations_evidence` volume. It persists across
`docker compose down` and is removed by `docker compose down --volumes`.

Compose uses Docker Hardened Images for Node 24, Nginx 1.30, .NET 10 SDK and
ASP.NET Core 10 runtime, and PostgreSQL 16. The ASP.NET Dockerfiles use
multi-stage builds and exec-form runtime entry points; their health probes call
the service's built-in `--healthcheck` mode.

The four Agentic AI images and the Gemini egress proxy use
`dhi.io/python:3.13-alpine` under the opt-in `agentic-ai` Compose profile. The
Safety Operations image copies its dependency-free Python proposal package;
its default command still only prints the Python version and exits. The other
three agent images remain runtime-only shells. The egress proxy is a separate
bounded standard-library service; it accepts CONNECT only to
`generativelanguage.googleapis.com:443` and never receives the API key. Build
the profile images with:

```sh
docker compose --profile agentic-ai build \
  agent-experience-biodiversity \
  agent-marine-conditions \
  agent-planning-coordination \
  agent-safety-operations \
  gemini-egress-proxy
```

The Safety Operations container joins the private service network and a
dedicated internal Gemini network. The proxy alone also joins a separate
outbound bridge; neither container publishes a host port. The agent transport
cannot bypass the proxy through `NO_PROXY` or use an alternate proxy address.
This egress plumbing does not enable the disconnected pre-G07 proposal
workflow. Keep runtime dispatch gated until G07 is accepted and its
`agentic-ai/**` implementation adds the reviewed workflow behavior.

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

The React/mobile application directories and ASP.NET service hosts are checked
in. The checked-in API/Auth applications and four component services have these
Docker build contexts:

```text
apps/web
apps/mobile
services/api
services/auth
services/coastal-operations
services/coastal-planner
services/experience-biodiversity
services/marine-safety
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
