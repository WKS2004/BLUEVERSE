# Local Deployment

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

Set a local PostgreSQL password and confirm `AUTH_SERVICE_URL=http://auth:8080` in `.env`.

PostgreSQL is available to host tools such as pgAdmin4 at `127.0.0.1:5432` using the database credentials from `.env`.

## Build

After the IDE-generated projects are present:

```bash
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
http://localhost/api/health
http://localhost/api/auth/health

Swagger UI:

```text
http://localhost/api/swagger
```
```
