# Architecture Overview

## Target logical architecture

```text
                     Internet / Local Network
                              |
                         edge-nginx
                              |
             +----------------+----------------+
             |                                 |
         React Web                       ASP.NET Core API
         / Vercel                       /        |       \
                                           Auth  Domain  Agentic AI
                                               \       |       /
                                                PostgreSQL
```

Flutter is a client application and communicates with the ASP.NET Core API. It
is not a backend microservice. In the current checkout the ASP.NET service
source is not present yet; the diagram is the intended v0/v1 boundary.

## Local Docker architecture (when backend projects are present)

```text
Client
  |
  v
  edge-nginx :80 (or BLUEVERSE_HTTP_PORT)
  |
  +--> frontend :80
  |
  +--> api :8080 ------+
  |                    |
  +--> auth :8080  |
                       v
                   postgres :5432
```

Docker networks:

- `blueverse_edge`
- `blueverse_internal` — API and Auth only; Docker marks it internal
- `blueverse_database` — API, Auth and PostgreSQL

The edge gateway is the only application entry point. PostgreSQL is explicitly
published on host port `5432` for local pgAdmin4 access; the application
services use the database network rather than that host port.

## Intended public application boundary

ASP.NET Core is authoritative for:

- authentication/authorization integration
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
platform-specific presentation and route syntax.
