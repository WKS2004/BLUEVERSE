# Networking

## Local

```text
blueverse_edge
  edge-nginx
  frontend

blueverse_internal (internal=true)
  api
  auth

blueverse_database
  api
  auth
  postgres
```

The edge gateway is connected to the edge and internal networks so it can
receive host traffic and proxy to internal services. API/Auth/PostgreSQL use
the separate database network.

## Routing

- `/api/*` → API, which forwards service-specific routes internally
- `/api/auth/*` → API → Auth service
- everything else → frontend

Auth is not connected to the edge network and has no public gateway route. Clients cannot call it directly.

No `/api/v1` style path versioning is planned.

Client route/API integration is recorded in
[`../contracts/ui-integration.json`](../contracts/ui-integration.json). React
and Flutter use shared workflow IDs and public `/api/...` paths; they never
call each other, Auth, Agentic AI, PostgreSQL or an internal Docker hostname.

## Host access

The gateway is published on host port `80` by default (`BLUEVERSE_HTTP_PORT`):

```text
http://localhost
```

For a physical mobile device, replace `localhost` with the development machine's LAN address. The gateway listens on port 80 by default.
