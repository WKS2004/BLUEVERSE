# Local Deployment

The local Compose topology includes the public API, internal Auth and private
Coastal Operations service sources at `services/api`, `services/auth` and
`services/coastal-operations`. Coastal Operations provides private assessment,
alert, decision, target-history, private image-evidence and PostgreSQL-backed
workflow routes. Producer-owned target-state handoff and cross-component
contract acceptance remain pending shared G00. Assessment creation makes bounded, read-only requests
to the provisional Member 1 experience-availability, Member 2 suitability and
optional Member 3 workflow endpoints. Missing peers never block Coastal
Operations startup or readiness; their latest outcomes appear in assessment
responses and the health response.

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
Base64 for 32 random bytes. Share that key only with the API and Coastal
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
not change the service's database-only readiness.

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

## Build

```bash
bash scripts/Unix/bash/sync-web-lockfile.sh
docker compose build
```

## Start

```bash
docker compose up -d
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
```

Additional ASP.NET services are checked through the API gateway at:

```text
http://localhost/api/operations/health
```

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

The local gateway uses host port `80`. The CI health workflow overrides the
Compose host mapping to `http://127.0.0.1:8080` on the runner. For Android,
explicitly pass `http://10.0.2.2:80` for a standard emulator. A physical device
requires the laptop's current LAN address passed to Flutter with
`--dart-define=BLUEVERSE_API_BASE_URL=http://<laptop-lan-ip>:80`. This explicit
value overrides the checked-in environment-specific Android fallback; do not
rely on that fallback for a device run. If managed Wi-Fi blocks
device-to-device traffic, connect the device by USB and run
`adb reverse tcp:80 tcp:80`; the Flutter client has a final `127.0.0.1:80`
fallback for that tunnel.

The current Flutter resolver accepts only plain HTTP on port `80` and does
not follow a changed `BLUEVERSE_HTTP_PORT` value. Keep the host gateway on
port 80 for Flutter until the resolver is updated. The local address is for
development and is not a deployment URL; HTTPS support for a production
mobile build remains unresolved.
