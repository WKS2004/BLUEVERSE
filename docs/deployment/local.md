# Local Deployment

The local Compose topology is defined. The public API source is checked in at
`services/api`; the complete Compose build remains blocked until the internal
Auth source is added.

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

Set local PostgreSQL and administrator passwords and confirm
`AUTH_SERVICE_URL=http://auth:8080` in `.env`.

PostgreSQL is available to host tools such as pgAdmin4 at `127.0.0.1:5432` using the database credentials from `.env`. The administrator seed behavior depends on the Auth implementation, which is not present yet.

## Build

After `services/auth` is present:

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

The local gateway uses host port `80` by default. The CI health workflow uses `http://127.0.0.1:8080` to avoid relying on the default host port.
