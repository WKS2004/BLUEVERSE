# Local Deployment

The local Compose topology is defined. The public API and internal Auth source
are checked in at `services/api` and `services/auth`.

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
and administrator passwords, and confirm `AUTH_SERVICE_URL=http://auth:8080`
in `.env`.

PostgreSQL is available to host tools such as pgAdmin4 at `127.0.0.1:5432`
using the database credentials from `.env`. Auth applies its EF Core migrations
and conditionally seeds the configured administrator account at startup.

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
```

Additional ASP.NET services are checked through the API gateway at:

```text
http://localhost/api/<service-name>/health
```

Swagger UI:

```text
http://localhost/api/swagger
```

The local gateway uses host port `80`. The CI health workflow overrides the
Compose host mapping to `http://127.0.0.1:8080` on the runner. For Android,
use `http://10.0.2.2:80` from an emulator. A physical device requires the
laptop's current LAN address passed to Flutter with
`--dart-define=BLUEVERSE_API_BASE_URL=http://<laptop-lan-ip>:80`; the address
is not hardcoded in the client. If managed Wi-Fi blocks device-to-device
traffic, connect the device by USB and run `adb reverse tcp:80 tcp:80`; the
Flutter client has a final `127.0.0.1:80` fallback for that tunnel.
