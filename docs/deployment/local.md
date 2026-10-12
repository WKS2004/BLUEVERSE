# Local Deployment

For the [detailed Coastal activity update](../v1/coastal-operations-record-navigation-and-audit.md),
rebuild/restart API and Coastal Operations together from the repository root:
`docker compose up -d --build api coastal-operations`. Both must understand the
extended signed display-identity envelope. Use the existing actor-context key
and environment configuration; no new setting is needed. Owning-service startup
applies `20261002081218_CoastalDetailedAudit` with safe empty audit arrays for
existing rows. Live migration execution was unavailable during this update.

The local Compose topology includes the public API, internal Auth and private
Coastal Operations service sources at `services/api`, `services/auth` and
`services/coastal-operations`. Coastal Operations provides private assessment,
alert, decision, target-history, private image-evidence and PostgreSQL-backed
workflow routes. Producer-owned target-state handoff and cross-component
contract acceptance remain pending shared G00. Assessment creation makes bounded, read-only requests
to the provisional Member 1 experience-availability, Member 2 suitability and
optional Member 3 workflow endpoints. Missing peers never block Coastal
Operations startup or readiness; their latest outcomes appear in assessment
responses and the health response. The same Compose topology includes Coastal
Planner, Marine Safety, Experience & Biodiversity, the public API and Auth.
Coastal Planner provides trip planning and review workflows; Marine Safety
provides conditions and safety workflows and calls Open-Meteo server-side.
Experience & Biodiversity provides catalogue management, nearby discovery, map
vector tile support and pre-G07 Agentic AI dispatch seams. Clients reach each
private service through the public API.

## Prerequisites

- Docker Desktop with WSL2 backend
- Git
- .NET SDK compatible with `global.json` for host-side development when required
- Node.js/npm for frontend development when required
- Flutter/Dart and Android tooling for mobile development

## First setup

```bash
cp .env.example .env
```

Set a unique `JWT_SIGNING_KEY` with at least 32 UTF-8 bytes, local PostgreSQL
and administrator passwords, and set `COASTAL_OPERATIONS_CONTEXT_KEY` to
standard padded Base64 for 32 random bytes. Keep any trailing `=`; do not use
Base64URL or remove its padding. In WSL/Linux, generate it with
`openssl rand -base64 32`. Share that key only with the API and Coastal
Operations containers. Confirm the Docker-internal destinations
`AUTH_SERVICE_URL=http://auth:8080` and
`COASTAL_OPERATIONS_SERVICE_URL=http://coastal-operations:8080` in `.env`.
The optional peer base addresses default to
`EXPERIENCE_SERVICE_URL=http://experience-biodiversity:8080`,
`MARINE_SAFETY_SERVICE_URL=http://marine-safety:8080` and
`COASTAL_PLANNER_SERVICE_URL=http://coastal-planner:8080`. Override these
variables in `.env` when a peer uses another internal address. Current peer
paths and payloads are provisional G00 assumptions. Each request has a
2-second timeout and up to two retries for timeouts, network failures and
retryable HTTP responses; failures are reported as dependency outcomes and do
not change the service's database-only readiness. Set independent
`MARINE_ACTOR_SIGNING_KEY` and `COASTAL_OPERATIONS_CONTEXT_KEY` values with at
least 32 UTF-8 bytes for those integrations, in addition to `JWT_SIGNING_KEY`.
Auth and the private services use the configured PostgreSQL credentials and
database. Confirm `AUTH_SERVICE_URL=http://auth:8080` and the private-service
URLs in `.env`; Compose defaults them to their service names.

On the development branch, PostgreSQL is available to host tools such as
pgAdmin4 through `5432:5432`, which binds all host interfaces. Use that
development setting only on a trusted network with the database credentials
from `.env` and a host firewall. When promoting the Compose configuration
from `dev` to `main`, change the mapping to `127.0.0.1:5432:5432` for
loopback-only access. Both mappings use host port `5432`; if another local
PostgreSQL process already owns that port, stop or reconfigure that process
so the development Compose mapping can keep the required host port. Auth
applies its EF Core migrations and conditionally seeds the configured
administrator account at startup. Coastal Operations applies its migrations
at startup and remains unready while PostgreSQL or its schema is unavailable.
Each private service applies its own EF Core migrations; no per-service
database creation or SQL initialization step is required, including when
reusing an existing PostgreSQL volume.

## Build

```bash
bash scripts/Unix/bash/sync-web-lockfile.sh
docker compose --profile dev build
```

The four future Agentic AI image shells are excluded from the local runtime
stack. To build the DHI Python images without starting them:

```bash
docker compose --profile agentic-ai build \
  agent-experience-biodiversity \
  agent-marine-conditions \
  agent-planning-coordination \
  agent-safety-operations
```

Planning Coordination runs a private HTTP service; the other role images
remain shells. The profile is optional and keeps all AI services on the
internal network. Set `AGENTIC_SERVICE_KEY` and an approved no-cost or
institution-provided model configuration in local `.env`; then set
`AGENTIC_AI_ENABLED=true` to enable Planner dispatch. Without specialist
services, execution returns unavailable/blocked status. The AI service is
disabled by default and has no database credentials.

## Start

```bash
docker compose --profile dev up -d
```

## Inspect

```bash
docker compose ps
docker compose logs -f edge-nginx
```

## Stop

```bash
docker compose down
```

Persistent PostgreSQL data remains in the named Docker volume unless the volume is explicitly removed.

## Gateway

```text
http://localhost
```

Health:

```text
http://localhost/health
http://localhost/api/health
http://localhost/api/auth/health
http://localhost/api/operations/health/live
http://localhost/api/operations/health/ready
http://localhost/api/marine/health
http://localhost/api/experiences/health
http://localhost/api/planner/health
```

Other ASP.NET services are checked through the API gateway at:

```text
http://localhost/api/operations/health
```

Use `planner` for Coastal Planner, `marine` for Marine Safety (the shorter
public prefix), and `experiences` for Experience & Biodiversity.

Swagger UI:

```text
http://localhost/api/swagger
```

Select **BLUEVERSE Coastal Operations API** in the Swagger document selector to
inspect its current service contract. Swagger's Authorize control accepts the
same bearer JWT format as the other BLUEVERSE API documents.

When `ADMIN_EMAIL` and `ADMIN_PASSWORD` are configured, Auth startup assigns
every permission registered in its database to the `Admin` system role,
including the current `operations.*` permissions. These are explicit
role-permission grants; Coastal Operations continues to enforce its named
permission policies. After deploying a service that adds permissions or
restarting Auth to run its seeders, sign in again (or refresh the session) so
the JWT contains the updated permission claims. Other roles still need only
the grants required for their work.
The Coastal Planner, Marine Safety and Experience & Biodiversity OpenAPI
documents are available at `/api/planner/swagger/v1/swagger.json`,
`/api/marine/swagger/v1/swagger.json` and
`/api/experiences/swagger/v1/swagger.json`; all appear in the unified Swagger UI.

The local gateway uses host port `80`. The CI health workflow overrides the
Compose host mapping to `http://127.0.0.1:8080` on the runner. For Android,
explicitly pass `http://10.0.2.2:80` for a standard emulator. A physical device
requires the laptop's current LAN address passed to Flutter with
`--dart-define=BLUEVERSE_API_BASE_URL=http://<laptop-lan-ip>:80`. This explicit
value overrides the standard `10.0.2.2:80` emulator and `127.0.0.1:80` USB
defaults. No laptop LAN address is hardcoded. If managed Wi-Fi blocks
device-to-device traffic, connect the device by USB and run
`adb reverse tcp:80 tcp:80`; the Flutter client has a final `127.0.0.1:80`
fallback for that tunnel.

The current Flutter resolver accepts only plain HTTP on port `80` and does
not follow a changed `BLUEVERSE_HTTP_PORT` value. Keep the host gateway on
port 80 for Flutter until the resolver is updated. The local address is for
development and is not a deployment URL; HTTPS support for a production
mobile build remains unresolved.

## Marine activity integration

`MARINE_ACTOR_SIGNING_KEY` is shared by the API and private Marine Safety
service. Auth continues using `JWT_SIGNING_KEY`. Compose configures the private
catalogue base URL with `EXPERIENCE_CATALOGUE_URL`; if the catalogue is
unavailable, activity-dependent Marine operations return 503/Retry-After while
condition-only reads and history remain available. See the root README for
canonical profile setup and the forward-only migration caveat.

For the Marine Web entry, open **Account → Marine & safety** after the current
account has `marine.profile.read`. Rebuild `auth` and `frontend` from this checkout
when existing images came from another member branch; then restart just these
services and reload the browser as shown in the root README. The existing Auth
bootstrap seeder adds missing Marine codes, preserves existing permissions and
assignments, and allocates a free ID if a preferred seed ID is occupied. This
requires the configured bootstrap credentials; it does not change user passwords
or clear sessions/data. Do not remove the PostgreSQL volume to repair navigation.

Coastal Planner uses an exec-form .NET healthcheck and applies its own EF Core
migrations. The API waits for its database readiness. Compose seeds planner
permissions and enables traveller access for new registrations by default;
configure `AUTH_SELF_SERVICE_PLANNER_ACCESS=false` for administrator assignment.
Peer URLs use the private environment settings in `.env.example`. Missing
catalogue, marine, operations or ML peers do not prevent startup, but can limit
suggestions. See [planner deployment details](../development/coastal-planner.md).
