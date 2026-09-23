# Architecture Overview

## Target logical architecture

```text
                     Internet / Local Network
                              |
                         edge-nginx
                              |
             +----------------+------------------+
             |                                   |
      React Web Client                 Flutter Mobile Client
             |                                   |
             +----------------+------------------+
                              |
                       ASP.NET Core API
                         /       |       \
                     Auth      Domain  Agentic AI
                               \       |       /
                                PostgreSQL
```

React and Flutter are peer client applications and communicate with the
ASP.NET Core API. Neither is a backend microservice. The public API and Auth
sources are checked in at `services/api` and `services/auth`; domain and
Agentic AI services will be added incrementally.

Both clients are planned for clients, staff and administrators. React may
optimize broad browser workspaces and Flutter may optimize mobile, location,
camera and notification interactions, but platform strengths do not define
which roles or business capabilities are allowed to use a client.

## Local Docker architecture

```text
Client
  |
  v
  edge-nginx :80 (or BLUEVERSE_HTTP_PORT)
  |
  +--> frontend :80
  |
  +--> api :8080 ------> auth :8080
                            |
                            v
                        postgres :5432
```

Docker networks:

- `blueverse_edge`
- `blueverse_internal` — API and Auth only; Docker marks it internal
- `blueverse_database` — Auth and PostgreSQL

The edge gateway is the only application entry point. PostgreSQL is explicitly
published on `127.0.0.1:5432` for local pgAdmin4 access; Auth uses the
database network rather than that host port. The API has no database
credential or database-network attachment in the v0 stack.

## Intended public application boundary

ASP.NET Core is authoritative for:

- authentication/authorization integration
- Auth session lifecycle, refresh-token rotation and device/account logout
- request validation
- business rules
- persistence
- Agentic AI workflow initiation
- approval enforcement
- audit/execution history

## AI boundary

If an internal Python Agentic AI service is introduced:

```text
React / Flutter
       |
       v
ASP.NET Core API
       |
       v
Internal Agentic AI service
       |
       +--> tools / data sources
```

The clients never call the AI service directly.

## Cross-client UI integration

React and Flutter do not connect to one another. A shared workflow ID in
`docs/contracts/ui-integration.json` links the relevant frontend routes and
the public `/api/...` endpoint references used by both clients. The registry
and its CI validator must be updated whenever a UI is created, generated or
changed. This preserves one public API/permission contract while allowing
platform-adapted presentation and route syntax. Cross-platform capability is
the default; a missing client surface is not an implicit product decision.
