# Architecture Overview

## Local gateway with target v1 services

```text
React Web ──┐
            ├──► edge-nginx ──► public /api/... ──► ASP.NET Core API
Flutter ────┘                                      │
                       ┌──────────────────────────┼──────────────────────┐
                       ▼                          ▼                      ▼
               Auth / domain services     private Agentic AI     private ML inference
                       │
                       ▼
                   PostgreSQL
```

This shows the local gateway convention. Hosted clients still use the
approved public API boundary, but need not share the local `edge-nginx`
deployment.

React and Flutter are peer client applications and communicate with the
ASP.NET Core API. Neither is a backend microservice. The public API and Auth
sources are checked in at `services/api` and `services/auth`; the four v1
member-owned component services, Agentic AI and ML-inference implementations
are not yet checked in. The [v1 guide](../v1/README.md) defines the target
service/component and four-agent boundaries without claiming they are live.
The [v0 guide](../v0/README.md) maps the implemented client, API, Auth,
PostgreSQL and infrastructure foundation.

Both clients must provide every permitted business workflow to tourists,
coastal operators, operations reviewers and platform administrators. The
same rule applies to later stakeholder roles. Presentation or device input
may vary, but platform choice cannot limit a role, action or result.

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

### v1 member-service target (not implemented in the current Compose stack)

```text
React / Flutter
      |
      v
edge-nginx -> public services/api ──┬-> internal Auth ─────────────> PostgreSQL
                                   ├-> private Member 1 service ─┐
                                   ├-> private Member 2 service ─┤
                                   ├-> private Member 3 service ─┼-> PostgreSQL
                                   └-> private Member 4 service ─┘
```

The API remains the only client entry point and changes only to integrate the
four private services. Each service owns its component's domain behavior and
data. Actual service IDs, route mapping, network/service credentials and data
schema ownership must be agreed at G00; this target does not describe deployed
containers. See [ADR-0020](../adr/ADR-0020-member-component-service-boundaries.md).

## Intended public application boundary

The existing `services/api` remains the only public application boundary. For
v1 member work it authenticates/authorizes requests under the existing model,
routes or forwards each component operation to its private owning service,
and preserves the shared gateway/error behavior. Its shared logical flow is
not rewritten to implement component behavior.

The owning member component service is responsible for:

- component-specific request validation and business rules;
- component-owned PostgreSQL persistence, migrations and audit history;
- provider integration and safe dependency failures;
- business workflow state and Agentic AI access seams; and
- deterministic approval checks and protected state changes for its domain.

Auth session lifecycle, refresh-token rotation, identity and role-to-permission
resolution remain owned by the existing Auth/API foundation. Client
applications never connect directly to Auth or a member service.

## AI boundary

If internal Python Agentic AI or ML inference services are introduced:

```text
React / Flutter
       |
       v
ASP.NET Core API
       |
       v
Internal Agentic AI or ML inference service
       |
       +--> tools / data sources
```

The clients never call AI or ML inference services directly. The assessed
operational workflow uses four distinct agents, deterministic validation and
authorized human approval before a high-impact business action. See
[Agentic AI architecture](../agentic-ai/architecture.md).

## Cross-client UI integration

React and Flutter do not connect to one another. A shared workflow ID in
`docs/contracts/ui-integration.json` links the relevant frontend routes and
the public `/api/...` endpoint references used by both clients. The registry
and its CI validator must be updated whenever a UI is created, generated or
changed. This preserves one public API/permission contract while allowing
platform-adapted presentation and route syntax. Every authorized role and
business action requires both client surfaces.
