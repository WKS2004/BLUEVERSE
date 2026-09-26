# Docker and local network

## Responsibility and source

`compose.yaml` defines the local BLUEVERSE stack. Dockerfiles and Nginx
configuration live under `infrastructure/docker/`. The selected Docker
Hardened Images are Node 24 Debian 13 development, Nginx 1.30 Alpine,
.NET 10 SDK Alpine, ASP.NET Core 10 Alpine and PostgreSQL 16 Alpine.
Preserve those project selections unless an explicit architecture change
replaces them.

## Topology

```text
host ─► edge-nginx ─┬─► frontend (React static files)
                   └─► api ─► auth ─► postgres
```

`blueverse_edge` connects edge-nginx and frontend.
`blueverse_internal` connects edge-nginx, API and Auth and is marked
internal. `blueverse_database` connects Auth and PostgreSQL. The
gateway publishes host port 80 by default. Development Compose publishes
PostgreSQL with `5432:5432`, bound on all host interfaces; Auth uses the
private database network. Use this development mapping only on a trusted
network with the local database secret and host firewall. When promoting
Compose from `dev` to `main`, use `127.0.0.1:5432:5432` for loopback-only host
access. Both mappings use host port `5432`, so the bind-address choice does
not avoid a collision with a PostgreSQL process already using that port. API
and Auth expose container ports to their networks, not direct host-facing
application ports.

The edge routes `/api/*` to API and frontend paths to the React container.
API/YARP forwards `/api/auth/*` to Auth. The stack uses environment
variables for PostgreSQL credentials, bootstrap administrator and JWT key.
Do not commit `.env` values or put secrets in images.

## Operational path

Use the [local deployment guide](../../deployment/local.md) and
[Docker infrastructure guide](../../../infrastructure/docker/README.md)
for prerequisites, lockfile synchronization, Compose commands and health
checks. Verify `/health`, `/api/health` and
`/api/auth/health` at the gateway, plus public API/Auth OpenAPI routes.
The ASP.NET runtime image is minimal; startup and health assumptions must
not depend on an unavailable shell. A new service needs source, tests,
Dockerfile, network/health wiring, route documentation and CI discovery
in one implementation change.
