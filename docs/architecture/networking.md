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

The gateway is published on host port `80` for local development by the
Compose default. Set `BLUEVERSE_HTTP_PORT` when a different host port is
required; the client contract still targets the gateway that is configured for
the environment:

```text
http://localhost
```

For a physical mobile device, pass the development machine's current
Wi-Fi/LAN IPv4 address to Flutter at compile time:
`--dart-define=BLUEVERSE_API_BASE_URL=http://<laptop-lan-ip>:80`. The address
is intentionally not hardcoded because it is environment-specific and may
change through DHCP. Android emulators use `10.0.2.2:80` to reach the host.
An attached Android device can use `adb reverse tcp:80 tcp:80` as a USB
fallback when a managed Wi-Fi network isolates clients. Port `50872` or
another changing client-side port is not the gateway port; the mobile client
must target port `80`.
