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
  auth
  postgres
```

The edge gateway is connected to the edge and internal networks so it can
receive host traffic and proxy to internal services. Auth and PostgreSQL use
the separate database network. The API reaches Auth through
`blueverse_internal` and has no PostgreSQL credential in v0.

## Routing

- `/api/*` → API, which forwards service-specific routes internally
- `/api/auth/*` → API → Auth service
- everything else → frontend

Auth is not connected to the edge network and has no public gateway route. Clients cannot call it directly.

## v1 private service target (not configured in the current stack)

The public API will route member-owned operations to one private .NET service
per component. Component services own domain behavior and data access; clients
never address their hostnames or internal routes. Service IDs, network
membership, transport, actor/permission propagation and data/schema ownership
are agreed at G00. The current Compose networks above describe the implemented
v0 stack only. See [ADR-0020](../adr/ADR-0020-member-component-service-boundaries.md)
and the [member service boundaries](service-boundaries.md).

The selected map API is an outbound dependency of Ushan Srinuka's private service,
not of `services/api`, a host route or a client network target. Its provider
and egress policy are not configured in the current stack; see
[ADR-0017](../adr/ADR-0017-map-provider-integration-boundary.md). React and
Flutter must not call the map provider directly.

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
