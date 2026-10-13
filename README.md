# BLUEVERSE

The [2026-10-02 record navigation and activity update](docs/v1/coastal-operations-record-navigation-and-audit.md) adds 500 ms search, pinned category tabs, shared record cards and 5/10/25/50/100 Assessments/Alerts per page across all collections. Create/edit workspace intent survives refresh; unsaved inputs are not persisted. Logs opens the same full details/actions. New activity captures the verified person/roles and exact field changes; older missing details remain unavailable. G00 and G07 acceptance are recorded from Adithya Gunawardana's 2026-10-13 team-meeting report; meeting minutes and signatures are not in the repository. Live producer and specialist-agent integration remain pending.

**Coastal Tourism & Marine Resilience**

Coastal Operations publication, permissions, record activity and the separate
Assessment/Alert client pages follow the [2026-10-01 contract](docs/v1/coastal-operations-publication-and-ui.md).
Assessment publication uses submit and stores a durable business delivery
envelope; the current Operations publication path does not dispatch an agent.
Service startup applies the additive dispatch migration along with existing
Coastal Operations migrations, including `20261001204558_CoastalDraftEvidenceRemoval`
and `20261002081218_CoastalDetailedAudit`.
Rebuild/restart both API and Coastal Operations with the documented Compose
commands from the repository root (`docker compose up -d --build api coastal-operations`)
to align the extended signed identity envelope and apply the additive audit
columns. Use the same configured actor-context key; no new environment setting
is required. The new migration has not been applied to a live database here.
Existing images and audit rows are retained; old assessments are not retroactively dispatched.
The Logs page is `/operations/logs` in both clients and requires audit plus
record-read access. Unpublished evidence may be removed; published content is
read only. Private-byte cleanup retries through the existing retention worker.
The [record experience update](docs/v1/coastal-operations-record-experience.md)
adds titled drafts, title search, named selectors and one database-backed IANA
time-zone selector in both clients. Catalogue/planner services are not yet
implemented: selectors report `NOT_CONNECTED`, drafts may remain unlinked,
and publication requires a real coastal target. No stakeholder types an ID
into a draft form. IDs remain visible in results and Advanced search.

BLUEVERSE is an integrated coastal ecosystem platform combining coastal tourism, marine intelligence, environmental resilience, safety, sustainability and future coastal-livelihood capabilities.

The checked-in application provides the **v0 foundation** for the SE3090
integrated full-stack and Agentic AI project. The submission target is
**v0 plus v1**. React and Flutter implement the shared Auth registration and session workflows,
backed by the public API gateway, internal Auth service and PostgreSQL. The
The four v1 member components, their private service sources and both client
workflows are present on this integration branch. Live peer/provider
compatibility and shared G00 ratification remain integration targets. The
Member 1 experience specialist now runs as a private stateless Python service;
cross-component orchestration and the other agent roles remain future work.
See the linked component contracts and implementation
evidence for current scope and remaining dependencies.

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
React Web / Flutter Mobile
              │
              ▼
     Public ASP.NET Core API
        ├──> Auth service ──────────────────────────────> PostgreSQL
        ├──> Coastal Operations service (Member 4) ────> PostgreSQL
        ├──> Coastal Planner service (Member 3) ───────> PostgreSQL
        ├──> Marine-safety service (Member 2) ─────────> PostgreSQL
        └──> Experience & Biodiversity service (Member 1) -> PostgreSQL
                      └──> Private Agentic AI runtime (after G07)
```

The diagram combines the implemented v0 foundation and the current v1 service
sources. The API integrates Auth and routes Coastal Operations, Coastal Planner,
Marine Safety, and Experience & Biodiversity operations to their private
services, which own their PostgreSQL-backed behavior, provider adapters and
component business rules. The API remains the only client-facing boundary and
receives only the authentication, permission and routing integration needed to
expose those services. G07 acceptance was recorded on 2026-10-13. Partial
Planning & Coordination implementation runs only when explicitly enabled;
specialist agents and AI-00 decisions remain outstanding.
React and Flutter never call member services or Agentic AI directly. See
[ADR-0020](docs/adr/ADR-0020-member-component-service-boundaries.md), the
[v1 documentation index](docs/v1/README.md) and the
[Agentic AI integration boundary](docs/v1/agentic-ai-integration-boundary.md).

React Web and Flutter Mobile must provide equal business capability at release.
Auth and planner workflows are present in both clients. Planner includes
persisted suggestions, full draft editing, lifecycle actions and condition
review history. Live owner-service compatibility remains an integration gate. Both use the same workflow intent, role → permission
model, data and public API contract.
Both clients follow the shared visual system in [`DESIGN.md`](DESIGN.md): React
maps it to Tailwind styles in `apps/web`, and Flutter maps it to native theme
and widgets in `apps/mobile`.

The marine conditions & safety component (Member 2, branch
`features/marine-safety`) implements equivalent React Web and Flutter Mobile surfaces:
`/marine/conditions`, `/marine/history` and `/marine/safety-profiles` query the
public marine API for condition evidence, the deterministic suitability
assessment and permission-gated safety profiles. The paired Flutter routes are
registered under the same shared workflow IDs and implemented with native
forms, coastal heroes and pinned navigation. Open **Marine & safety** from the
mobile Dashboard or account menu with `marine.profile.read`; profile writes
also require `marine.profile.manage`. Mobile calls reuse the selected account's
secure credentials through the public gateway.

The [v0 foundation guide](docs/v0/README.md) maps the implemented technical
components and their integration path. The
[v1 documentation index](docs/v1/README.md) links the separate member
component and agent documents, shared workflows and acceptance requirements.
The [v1 owner map](docs/project/ai-team-members.md) records each component
owner's full name, exact GitHub account and feature/Agentic AI branch. The
full baseline is [PROJECT_REQUIREMENTS.md](PROJECT_REQUIREMENTS.md).

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
│       ├── auth/
│       ├── marine-safety/
│       └── experience-biodiversity/
├── services/                # ASP.NET Core services
│   ├── api/                  # checked-in public API foundation
│   ├── auth/                 # internal Auth service
│   ├── coastal-operations/   # private Member 4 operations service
│   ├── coastal-planner/      # private planning service
│   ├── marine-safety/        # internal marine conditions & safety service (v1)
│   └── experience-biodiversity/ # internal Experience & Biodiversity service
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

`services/api/`, `services/auth/`, and the Coastal Operations, Coastal Planner,
Marine Safety, and Experience & Biodiversity projects contain the checked-in
backend services. Compose, Dockerfiles and CI include these v1 component
services; hosted deployment is not yet evidenced. They provide operations
assessment/alert workflows, planning, marine conditions and safety, catalogue
management, schedules, publication lifecycle, nearby discovery and map support.
Coastal Operations currently reads Member 1's canonical availability endpoint.
Its Marine Safety and Coastal Planner dependencies return `NOT_CONNECTED`
without sending requests until their route, payload and authorized service
identity are agreed. Planner, Marine Safety and Experience adapters use their
implemented public API routes and caller permissions. Live provider behavior,
G00 and G07 acceptance are recorded. Live provider compatibility and
specialist-agent integration remain pending; AI-00 decisions remain open.
Configure Docker-internal peer addresses in `.env`. The edge gateway allows
6 MiB request bodies on assessment-item routes to carry a 5 MiB PNG with
multipart framing; the service enforces the per-image limit. Auth and all
component services remain internal and reachable by clients only through the
public API gateway. Keep Dockerfiles under `infrastructure/docker/`.

## Local setup and deployment

The supported local deployment runs the checked-in React client, public API,
internal Auth, Coastal Operations, Coastal Planner, Marine Safety and
Experience & Biodiversity services, and PostgreSQL with Docker Compose. The
component services provide assessment/alert workflows, planning, marine
conditions and safety, catalogue discovery and management, map support, and
nearby discovery through the public API gateway. Run Compose commands from the
repository root (the directory containing `compose.yaml`). The public entry
point is `edge-nginx`; clients use the gateway's `/api/...` routes and do not
connect directly to internal services.

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
  host. The repository selects the `10.0.300` SDK feature band in `global.json`
  and allows forward feature-band roll-forward. This also matches the SDK
  available in the selected Docker Hardened .NET image. Install the SDK from the [official .NET 10 download
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

Edit `.env` and replace the example `JWT_SIGNING_KEY`, independent
`MARINE_ACTOR_SIGNING_KEY`, PostgreSQL password and administrator password with
unique local values. The JWT signing key must be
at least 32 UTF-8 bytes. Use a password manager or secure random generator.
The API gateway, Auth service and Experience & Biodiversity service use this
same key so both public and private service boundaries can validate Auth-issued
tokens.
The API and Marine Safety also share `MARINE_ACTOR_SIGNING_KEY` for signed
request-bound actor context; Marine Safety does not read Auth tables or accept
client JWTs directly.
Auth and Experience & Biodiversity use the same `POSTGRES_DB`, `POSTGRES_USER`
and `POSTGRES_PASSWORD` settings for the single PostgreSQL database. Never
commit `.env`. Keep
`AUTH_SERVICE_URL=http://auth:8080` and
`EXPERIENCE_BIODIVERSITY_SERVICE_URL=http://experience-biodiversity:8080` as
the Docker-internal API-to-service addresses. `ADMIN_EMAIL` and
`ADMIN_PASSWORD` are used for the local administrator account.

Compose defaults `AUTH_SELF_SERVICE_PLANNER_ACCESS=true`: new registrations
receive the non-administrative **Coastal traveller** role with five planner
permissions. Set it to `false` to require administrator assignment. Existing
accounts retain their roles; an administrator can assign Coastal traveller
through the existing role administration screen. The configured Admin role
also receives planner grants at startup. Sign in again or refresh the session
after grants change to obtain updated permission claims.

The optional `EXPERIENCE_CATALOGUE_URL`, `MARINE_CONDITIONS_URL`,
`COASTAL_OPERATIONS_URL` and `BIODIVERSITY_ML_URL` settings identify private
peer services. Other peer services are not supplied by this branch. The
synthetic IT3091 fake runs only with the `dev` Compose profile; it does not
represent the real provider. The local `.env.example` points the planner to
that fake for development. Real ML URL, key, enabled flag, and expected model
version remain environment-configured. Without destination data,
`/planner` shows a recoverable unavailable state. Recommendations
require verifiable offering schedules, marine suitability and operations
evidence. Connect compatible owner services to enable genuine suggestions;
see [the peer contract](docs/development/coastal-planner.md).

Also set `COASTAL_OPERATIONS_CONTEXT_KEY` to Base64 for 32 random bytes. For
PowerShell, generate a value with:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

In WSL/Linux, use `openssl rand -base64 32`. Keep any trailing `=` in the
output; the API and Coastal Operations expect standard padded Base64, not
Base64URL or an unpadded value.

Store the value only in `.env`; Compose supplies it to the API gateway and
Coastal Operations so the gateway can sign each authenticated actor/permission
context for the private service. The API validates Auth-issued JWTs and strips
the bearer token and browser cookies before forwarding the signed context.

The gateway is published on host port `80` by default. On the development
branch, PostgreSQL is published as `5432:5432`, which binds the host port on
all network interfaces for the team's current development configuration.
Use this only on a trusted development network and protect the database with
the local secret and host firewall. Make sure host ports `80` and `5432` are
available. If port 80 is already in use, set `BLUEVERSE_HTTP_PORT=8080` in
`.env` and use `http://localhost:8080` below. Both the development mapping and
the loopback-only mapping planned for `main` use host port `5432`; changing the
bind address does not resolve a host-port collision with a PostgreSQL process
already listening on `5432`.

The v1 marine conditions & safety service (`marine-safety`) joins the same
stack: the public API forwards `/api/marine/...` to it, and it owns
its marine-safety tables in the shared PostgreSQL database and applies its
migrations at startup. Its two permission grants
(`marine.profile.read`, `marine.profile.manage`) are seeded for the Admin role
by the Auth seeder. Optional environment
variables configure its Open-Meteo integration and freshness policy
(`OPEN_METEO_WEATHER_URL`,
`OPEN_METEO_MARINE_URL`, `OPEN_METEO_FRESHNESS_MINUTES`, defaults documented in
`services/marine-safety/appsettings.json`); no provider key is required.

The API and Marine Safety require the same independent
`MARINE_ACTOR_SIGNING_KEY` (at least 32 UTF-8 bytes). API/Auth continue using
`JWT_SIGNING_KEY`; Marine Safety receives only a signed request-bound actor
context after the API verifies the caller through Auth `/api/auth/me`.
Marine Safety does not access Auth tables or accept client JWTs directly.

Activity CRUD belongs to the `experience-biodiversity` service. The API uses
`EXPERIENCE_CATALOGUE_URL` for its private base URL; the default is
`http://experience-biodiversity:8080`. Compose starts both component services
on the shared private network. When activity data is unavailable,
activity lists, new profile drafts/edits/reviews and evaluations return a
structured 503 with `availability`, `dependency`, `code`, `reason`,
`missingFields`, `retryable` and null `data`. Marine starts and checks its own
database without requiring other member services. Empty or invalid catalogue
configuration is reported as `UNCONFIGURED` when data is requested; the
catalogue request timeout is five seconds. Conditions-only queries, saved
profiles/snapshots/assessments and profile deactivation remain available.
Both clients retain saved records, indicate "Activity data unavailable" and
offer retry; only actions requiring published activity data are disabled.
A successful empty catalogue remains 200 `[]`. See the complete
[availability contract](docs/api/marine-dependency-availability.md).
Both clients load published canonical activity IDs through
`GET /api/marine/activities`, with loading, empty and retry states. The former
marine activity POST/PUT/DELETE endpoints are removed.

The React Marine pages provide shared workflow navigation, plain-language
assessment explanations, factor-grouped cited profile forms, and independent
history/profile recovery. See the [Web workflow guide](apps/web/README.md#marine-safety-web-workflows)
for routes, permissions and available actions.

Open **Account → Marine & safety**, the account sidebar or Dashboard's marine
quick link with `marine.profile.read`. The Admin header contains account actions
without the home-section links. An Admin role name alone does not grant Marine
access: current role assignments must include the read permission (and manage
for profile writes).

When an older local image or another member branch supplied Auth, rebuild the
Auth and Web images together so this branch's existing bootstrap grants apply:

```powershell
docker compose build auth frontend
docker compose up -d --no-deps auth frontend
```

Use the existing `.env` and PostgreSQL volume. Reload the browser to retrieve
current grants; signing out is unnecessary. Permission seeding preserves codes,
existing rows and assignments when another member has used a preferred seed ID.

Old placeholder IDs are retained for historic references; they are never
mapped by activity name. Create cited profiles for Member 1's canonical IDs
and have a different manager approve them. The new
`CanonicalActivityProjection` migration removes the projection's unique-name
constraint and expands category storage to Member 1's 100-character limit.
The forward migration preserves all rows. A downgrade needs manual duplicate
name/category reconciliation before restoring the previous constraints.
See [ADR-0021](docs/adr/ADR-0021-marine-catalogue-and-gateway-actor-context.md).

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
  docker compose --profile dev build
  docker compose --profile dev up -d
docker compose ps
```

The first build pulls the DHI base images and may take several minutes. Auth
and component Dockerfiles reuse NuGet packages across builds with BuildKit
cache mounts. To build and run the full development stack, including the
optional planner fake provider, use `docker compose --profile dev up --build`.
Auth and each component service apply their own EF Core migrations at startup;
Coastal Operations reports ready only when PostgreSQL and its schema are
available. Coastal Planner uses an exec-form healthcheck compatible with the
minimal DHI runtime, and the API waits for planner readiness. Missing optional
peer services do not block Coastal Operations startup or readiness. No second
database or PostgreSQL initialization script is required; see the
[database setup guide](docs/database/README.md). The operations migrations add
assessment titles and IANA time-zone/location choices while retaining existing
UTC instants; the planner migration supplies `Asia/Colombo` to existing
itineraries without changing their IDs or schedules.

Four optional Agentic AI images use the DHI Python 3.13 Alpine image and are
excluded from the normal stack. Private runtimes are present for Experience &
Biodiversity, Marine Conditions, and Planning & Coordination; Safety
Operations remains a runtime shell. The full four-agent workflow and live
provider and peer integration remain incomplete. Build the images without
starting them with:

```bash
docker compose --profile agentic-ai build \
  agent-experience-biodiversity \
  agent-marine-conditions \
  agent-planning-coordination \
  agent-safety-operations
```

The private runtimes have no host-published ports and join only the internal
Docker network. Planner dispatch is disabled by default. Set
`AGENTIC_SERVICE_KEY`, `AGENTIC_MODEL_NAME`, and `AGENTIC_MODEL_API_KEY` in
local `.env` to configure the Planner runtime. Gemini is the configured
default; `AGENTIC_MODEL_PROVIDER=openai-compatible` and
`AGENTIC_MODEL_BASE_URL` select an OpenAI-compatible provider. Gemini
free-tier inputs and outputs may be used to improve Google products; do not
send personal or sensitive data through a free-tier key. Missing models or
specialist services produce unavailable/blocked states and cannot change
deterministic eligibility or safety outcomes.

The API signs the authenticated actor and `operations.*` permission claims with
`COASTAL_OPERATIONS_CONTEXT_KEY`; bearer tokens and cookies are removed before
the private-service request is sent.

### Verify the local deployment

Open the web app at `http://localhost` (or the port configured by
`BLUEVERSE_HTTP_PORT`). The unified Swagger UI is at
`http://localhost/api/swagger` and includes the Marine Safety and Experience &
Biodiversity documents at `/api/marine/swagger/v1/swagger.json` and
`/api/experiences/swagger/v1/swagger.json`. Check the frontend, public API,
Auth and all three component health routes; each should return HTTP 200:

```powershell
curl.exe -f http://localhost/health
curl.exe -f http://localhost/api/health
curl.exe -f http://localhost/api/auth/health
curl.exe -f http://localhost/api/marine/health
curl.exe -f http://localhost/api/experiences/health
curl.exe -f http://localhost/api/planner/health
curl.exe -f http://localhost/api/operations/health/ready
```

The marine-safety document is also listed directly in the unified Swagger UI
at `http://localhost/api/swagger` (select it from the top-right definition
picker), and a standalone marine Swagger UI is available at
`http://localhost/api/marine/swagger` for backend contract
inspection; its operations require an authenticated account with the marine
permission grants (`marine.profile.read`, `marine.profile.manage`), which the
bootstrap administrator receives automatically.

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
curl --fail --silent --show-error http://localhost/api/marine/health
curl --fail --silent --show-error http://localhost/api/experiences/health
curl --fail --silent --show-error http://localhost/api/planner/health
```

In `docker compose ps`, PostgreSQL should report `healthy` and the frontend,
API, Auth, Coastal Operations, Coastal Planner, Marine Safety, Experience &
Biodiversity and gateway containers should be running. The development Compose
stack exposes PostgreSQL on host port `5432` across all interfaces; backend
services connect over the Docker network. When promoting the Compose
configuration from `dev` to `main`, change the host mapping to
`127.0.0.1:5432:5432` so host access is loopback-only. Production databases
remain privately managed and are not configured through this local binding.

If a service does not start or a health URL fails, inspect its recent logs:

```text
docker compose logs --tail=100 edge-nginx frontend api auth coastal-operations coastal-planner marine-safety experience-biodiversity postgres
```

Common first-run causes are Docker Desktop not running, missing DHI registry
access, an unedited required value in `.env`, or host ports `80`/`5432` already
being occupied.

### Stop the stack and preserve data

Stop containers and networks with:

```text
docker compose down
```

The named PostgreSQL and Coastal Operations evidence volumes remain so local
data survives a restart. Only use `docker compose down --volumes` when you
intentionally want to permanently delete the local database and assessment
images and start from a clean state.

For Flutter on an Android emulator, explicitly pass the laptop gateway as
`http://10.0.2.2:80` with
`--dart-define=BLUEVERSE_API_BASE_URL=http://10.0.2.2:80`. A physical Android
device can use the laptop's current Wi-Fi/LAN address or a USB reverse tunnel
with the compile-time setting. Without an explicit value, Android tries the
standard emulator host `10.0.2.2:80`, then the USB reverse tunnel at
`127.0.0.1:80`; an explicit value overrides those defaults. The current mobile
resolver accepts local HTTP on port `80` only,
so a deployment HTTPS configuration remains future work. The Flutter
resolver also has a fixed port-80 limit; changing `BLUEVERSE_HTTP_PORT` does
not update the mobile client's accepted port. Keep the gateway on port 80 for
Flutter or update the resolver before changing that setting. If managed Wi-Fi
isolates the phone from the laptop, use the USB workflow below. Enable USB
debugging, accept the phone's authorization prompt and put Android platform-tools
(`adb`) on PATH. Replace the device ID with the one reported by `adb devices`.
On Windows the wrapper keeps Gradle, pub, temporary and app build caches under
`D:\WKS\FlutterBuildCache`:

```powershell
adb devices
$mobileDevice = 'REPLACE_WITH_DEVICE_ID'
adb -s $mobileDevice reverse tcp:80 tcp:80
.\scripts\Windows\Invoke-FlutterCached.ps1 -FlutterArgs @('run','-d',$mobileDevice,'--dart-define=BLUEVERSE_API_BASE_URL=http://127.0.0.1:80')
```

Keep USB connected for gateway access. Remove the tunnel with
`adb -s $mobileDevice reverse --remove tcp:80` when it is no longer needed.
See [Mobile setup](apps/mobile/README.md) for the full device workflow and
current limitations. Kotlin incremental compilation is disabled in the mobile
Gradle configuration to avoid cache errors across Windows drive roots; keep
space available for Gradle, Pub and temporary compiler output.

For Windows builds using a relocated Flutter cache, initialize each PowerShell
terminal from the repository root before running Flutter:

```powershell
. .\scripts\Windows\powershell\initialize-flutter-cache.ps1 -CacheRoot D:\WKS\FlutterBuildCache
Set-Location apps\mobile
flutter pub get
```

This reuses the existing D: Gradle and Pub caches and redirects temporary
compiler files to D: for that terminal. Already-open processes may not inherit
saved user environment settings. See [Android build caches](apps/mobile/README.md#android-build-caches)
for verification commands and the USB-device launch command.

On Android, select **Precise** when using the coastal map location control for
the most accurate device fix. Existing installs can enable **Use precise
location** in the app's Location permission settings. See the
[mobile permission guide](apps/mobile/README.md#android-location-permission).

Before a Flutter Android run, check `flutter doctor -v` and ensure the Gradle
cache drive and workspace drive have free space for engine extraction and APK
packaging. SDK/device detection and passing Flutter tests do not establish a
successful Android build when the build-cache drive is full.
On Windows, use `GRADLE_USER_HOME` and `PUB_CACHE` to place Gradle and Dart
dependency caches on a drive with space; use a process-local temporary directory
for the build. The [Windows cache setup](apps/mobile/README.md#windows-build-cache-location)
records the configuration and the ARM64 phone build command.

On the Windows development workstation, Flutter's Gradle and Pub caches use
`D:\WKS\FlutterBuildCache\gradle` and `D:\WKS\FlutterBuildCache\pub`.
The [mobile cache setup](apps/mobile/README.md#windows-caches-on-a-second-drive)
documents persistent user environment settings, build-only temporary files
and reuse of cached dependencies. For USB testing, pass
`--dart-define=BLUEVERSE_API_BASE_URL=http://127.0.0.1:80` after setting up
`adb reverse tcp:80 tcp:80`.

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
- `docker-stack-health.yml` waits for both build workflows, creates temporary Compose signing keys, starts the root stack and checks frontend/API/discovered-service health endpoints plus the API/Auth and each present member OpenAPI document.

The web and backend build workflows run on supported branch families when their relevant application, service, Docker infrastructure, lockfile synchronization, Compose, workflow or build-context paths change. The stack-health workflow waits for the required image builds for the same commit and runs only for pull requests targeting `main`; it synchronizes the web lockfile again on its separate runner before running the Compose health checks. Backend build failures are collected across all services so later services are still checked before the workflow fails.

The Docker backend workflow builds `services/api` first and then discovers and
builds the other ASP.NET service directories, including Auth and all four
component services, followed by the four optional Agentic AI images. Private
workloads are present for Experience & Biodiversity, Marine Conditions, and
Planning & Coordination; Safety Operations remains a shell.
The stack-health workflow maps each service folder to its
public health route (including the `/api/operations/health` route), supplies
masked ephemeral signing keys for the isolated CI stack, prints diagnostics
on failure and tears down the stack. A hosted run is still required for
deployment evidence. The backend test workflow fails if a service source
project has no discovered service-local test project.

`github-config-sync.yml` uses GitHub Actions to copy `.github/**` changes
through focused pull requests and queues automatic squash merges when allowed
by repository settings. This repository automation is distinct from agent
source-control behavior: an agent does not create or push commits unless the
user explicitly asks. `branch-policy.yml` enforces lowercase approved names;
`dev-backup.yml` preserves mistaken backup history on a timestamped rescue
branch before force-resetting `dev-backup` to the exact `dev` commit.

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

## Planner verification and hosted packaging update — 2026-10-12

The Flutter planner supports saved recommendation URLs, permitted workflow
progress, paged saved trips, adding suggestions to a draft, native trip/stop
clock editing, stable-ID reorder/removal, confirmation, and before/after
condition review history. Its requests use the shared public API, scoped
credentials, a 55-second budget for the 40-second planner search, and no
connectivity replay for writes. The app uses the shared loading screen.
Android emulators use `10.0.2.2`; physical devices still require the explicit
HTTP `BLUEVERSE_API_BASE_URL` override described above.

All three backend Dockerfiles use `dhi.io/aspnetcore:10-alpine` with exec-form
startup. The Render Blueprint now includes the private planner and public API
forwarding. Its Auth and Planner compute plans are billable. Supply each
backend's `ConnectionStrings__DefaultConnection` as a secret Npgsql key/value
string using the managed private database values; Render's PostgreSQL URL is
not directly accepted. Auth owns the shared JWT key referenced by the other
services. See [Render setup](docs/deployment/render.md) for grants, readiness,
peer/provider configuration and remaining hosted-client gates. No cloud
resources have been deployed by this change.

From `apps/mobile`, run `flutter analyze` and `flutter test`. From the root,
run the planner/API/Auth test projects and contract checks in
[the planner guide](docs/development/coastal-planner.md). The optional real
PostgreSQL test creates and drops only its own uniquely named test database;
use local test credentials. Preserve the application volume when stopping.


### React planner experience — 2026-10-12

Open `/planner` for the public introduction, `/planner/plan` to create a plan,
and `/planner/saved` for personal plan history. Guests who choose creation or
history sign in first and return to the requested page; authorized users enter
directly. The shared top menu opens The planner as a full-width public page
without an account sidebar or redundant self-link. Creation and personal
history use the shared account sidebar; its Coastal planner entry opens the
creation page directly. Personal create/history pages use route tabs and wide
coastal hero cards. History supports Draft, Confirmed and Past views, search and
condition-check refinement on the displayed page, reset and selectable page size.
The redesign
retains existing public API contracts and permission checks. Flutter design
changes are deferred until requested. See [the web guide](apps/web/README.md).
## Marine Safety input and recovery behavior

React and Flutter expose all three Marine pages with `marine.profile.read`;
profile mutation controls additionally require `marine.profile.manage`. Enter
latitude/longitude as signed decimal WGS84 coordinates. Nonblank invalid filters
are rejected before querying. Maximum and optional caution limits accept at most
two decimal places, preserving the exact reviewed values.

Condition reuse requires matching rounded coordinates, an applicable forecast
from requested time minus 30 minutes through plus one hour, and retrieval within
the preceding 15 minutes. Future retrieval timestamps cannot be reused.
Acquisition/evaluation rejects UTC request times outside
`0001-01-01T01:00:00Z`–`9999-12-31T22:59:59.9999999Z` with 400 before provider
access; provider forecast availability still has its own limits.

Flutter allows 45 seconds per request. An ambiguous timeout or connection failure
after dispatching a write reports that its outcome is unconfirmed and makes no
automatic second attempt. Check assessment history or the profile before retrying;
a timeout does not cancel server work. Read requests may try another configured
gateway candidate. Server-enforced mutation idempotency is not implemented.
