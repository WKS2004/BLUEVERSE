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
sources are checked in at `services/api` and `services/auth`. Member 2's
marine-safety service is implemented at `services/marine-safety` and included
in local Compose. The other three member-owned services, Agentic AI runtime
and ML-inference implementation are not checked in. The [v1 guide](../v1/README.md)
defines the full target service/component and four-agent boundaries; the
[branch tracker](../v1/member-branch-workflow.md#component-branch-status)
records current implementation status.
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
  +--> api :8080 ------┬--> auth :8080 --------┐
                       └--> marine-safety :8080 ├--> postgres :5432
                                |
                                └--> Open-Meteo APIs (outbound)
```

Docker networks:

- `blueverse_edge`
- `blueverse_internal` — API, Auth and marine-safety; Docker marks it internal
- `blueverse_database` — Auth, marine-safety and PostgreSQL

The edge gateway is the only application entry point. Development Compose
publishes PostgreSQL as `5432:5432` on all host interfaces; Auth and
marine-safety use the private database network rather than the host-published
port. The API has no database credential or database-network attachment.
Promotion from `dev` to `main` changes the host mapping to
`127.0.0.1:5432:5432`; both mappings still require host port `5432`.

### Current and planned v1 private services

```text
React / Flutter
      |
      v
edge-nginx -> public services/api ──┬-> internal Auth ───────────────┐
                                   ├-> marine-safety (implemented) ─┼-> PostgreSQL
                                   ├-> Member 1 service (planned) ──┤
                                   ├-> Member 3 service (planned) ──┤
                                   └-> Member 4 service (planned) ──┘
                                                    marine-safety ─────> Open-Meteo
```

The API remains the only client entry point. The local stack implements the
marine-safety route, service, database connection and Open-Meteo adapter; the
other three private member services remain target architecture. Each service
owns its component's domain behavior and data. Member 2's identifiers and
route are recorded in its [G00 decision record](../v1/g00/member-2-marine-safety-decisions.md);
the shared choices for remaining services are still part of team G00. See
[ADR-0020](../adr/ADR-0020-member-component-service-boundaries.md).

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

After G07, the private Agentic AI runtime is reached through the member
component service that owns the business workflow. The clients still enter
through the public API; the API does not own member workflow persistence,
agent dispatch or protected domain execution:

```text
React / Flutter
       |
       v
ASP.NET Core API
       |
       v
Owning private member service
       ├──► private Agentic AI runtime
       ├──► approved private tools / member-service data
       └──► PostgreSQL through the owning service
```

The member service validates and persists business state, handles the
pre-G07 not-connected/unavailable behavior, and revalidates any protected
operation after an authorized human decision. The runtime and tools never
connect directly to PostgreSQL or Auth. The separate IT3091 inference adapter
is owned by Adithya Gunawardana's service. Exact private tool transport is a G00/Agentic
AI design decision. See the [Agentic AI architecture](../agentic-ai/architecture.md)
and [ADR-0020](../adr/ADR-0020-member-component-service-boundaries.md).

## Cross-client UI integration

React and Flutter do not connect to one another. A shared workflow ID in
`docs/contracts/ui-integration.json` links the relevant frontend routes and
the public `/api/...` endpoint references used by both clients. The registry
and its CI validator must be updated whenever a UI is created, generated or
changed. This preserves one public API/permission contract while allowing
platform-adapted presentation and route syntax. Every authorized role and
business action requires both client surfaces.
