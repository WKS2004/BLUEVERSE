# Local Deployment

The local Compose topology runs the public API, internal Auth, Marine Safety
and Experience & Biodiversity services with PostgreSQL. Marine Safety provides
marine conditions and safety workflows and calls Open-Meteo server-side.
Experience & Biodiversity provides catalogue management, nearby discovery,
map vector tile support and pre-G07 Agentic AI dispatch seams. Clients reach
both private services through the public API.

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

Set independent `JWT_SIGNING_KEY` and `MARINE_ACTOR_SIGNING_KEY` values with
at least 32 UTF-8 bytes each, local PostgreSQL and administrator passwords.
Auth and Experience & Biodiversity use the same `POSTGRES_DB`,
`POSTGRES_USER` and `POSTGRES_PASSWORD` values to connect to the single
database configured for the stack. Confirm `AUTH_SERVICE_URL=http://auth:8080`
and `EXPERIENCE_BIODIVERSITY_SERVICE_URL=http://experience-biodiversity:8080`
in `.env`.

On the development branch, PostgreSQL is available to host tools such as
pgAdmin4 through `5432:5432`, which binds all host interfaces. Use that
development setting only on a trusted network with the database credentials
from `.env` and a host firewall. When promoting the Compose configuration
from `dev` to `main`, change the mapping to `127.0.0.1:5432:5432` for
loopback-only access. Both mappings use host port `5432`; if another local
PostgreSQL process already owns that port, stop or reconfigure that process
so the development Compose mapping can keep the required host port. Auth
applies its EF Core migrations and conditionally seeds the configured
administrator account at startup. Marine Safety and Experience & Biodiversity
use the same database server with service-owned EF Core migration histories;
each applies its migrations at startup. No per-service database creation or
SQL initialization step is required, including when reusing an existing
PostgreSQL volume.

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
http://localhost/api/marine/health
http://localhost/api/experiences/health
```

Other ASP.NET services are checked through the API gateway at:

```text
http://localhost/api/<service-name>/health
```

Marine-safety has a shorter public prefix than its service folder name; use
`http://localhost/api/marine/health` for that service.

Swagger UI:

```text
http://localhost/api/swagger
```

The Marine Safety and Experience & Biodiversity OpenAPI documents are available
through the same gateway at `/api/marine/swagger/v1/swagger.json` and
`/api/experiences/swagger/v1/swagger.json`; both appear in the unified Swagger
UI.

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
service. Auth continues using `JWT_SIGNING_KEY`. Configure Member 1's private
catalogue base URL with `EXPERIENCE_CATALOGUE_URL`. That service is supplied by
the Member 1 branch and is not copied into this Compose file. Its absence
disables only activity-dependent Marine operations with 503/Retry-After.
Condition-only reads and history remain available. See the root README for
canonical profile setup and the forward-only migration caveat.

For the Marine Web entry, open **Account → Marine & safety** after the current
account has `marine.profile.read`. Rebuild `auth` and `frontend` from this checkout
when existing images came from another member branch; then restart just these
services and reload the browser as shown in the root README. The existing Auth
bootstrap seeder adds missing Marine codes, preserves existing permissions and
assignments, and allocates a free ID if a preferred seed ID is occupied. This
requires the configured bootstrap credentials; it does not change user passwords
or clear sessions/data. Do not remove the PostgreSQL volume to repair navigation.
