# ADR-0020: One Internal Service per v1 Member Component

**Status:** Accepted for the v1 service-ownership boundary; service identifiers,
transport contracts and deployment details are finalized at G00.

**Date:** 2026-09-26

## Context

The existing `services/api` and `services/auth` applications are shared v0
foundations. The four v1 members need to build distinct business components in
parallel, and the team requires that members not implement their component
logic by changing the logical flow of those shared services. The components
still need ASP.NET Core, persistence, meaningful API operations, third-party
integrations where assigned, and equal React/Flutter capabilities. Clients
must continue to use one public API boundary.

## Decision

1. **Create one new .NET microservice per v1 member component.** Each service
   lives in its own component-specific subfolder under `services/`, has its
   own .NET project and owns the business rules, validation, provider
   adapters, persistence/migrations, business workflow state, tests and
   private Agentic AI access seam specified by its component contract.
2. **Keep component services internal.** They are reachable by `services/api`
   over the private service network. React and Flutter never address their
   hostnames, ports or internal routes. All user-facing operations continue
   through public `/api/...` routes.
3. **Limit `services/api` changes to integration code.** A member may add the
   smallest public route/proxy or typed-client mapping, existing
   authentication/permission integration, dependency-injection/options
   registration, configuration and required availability reporting needed to
   connect its service. Member business handlers, rules, provider logic and
   persistence belong in the member service. Preserve existing API endpoints,
   middleware order, JWT/cookie and CORS behavior, forwarded headers, Auth
   forwarding, shared errors/health semantics and permission-resolution flow.
4. **Reuse Auth.** Component services consume authenticated actor and
   permission context through the agreed API-to-service boundary. They do not
   call Auth directly. The normal implementation adds no changes to
   `services/auth`; any essential Auth change is integration-only and must
   preserve existing identity/session/role/permission behavior.
5. **Keep component and Agentic AI delivery separate.** A member service may
   implement the ordinary business workflow and its typed private AI
   integration seam before G07. Executable agents, orchestration, tools and
   AI-owned execution state remain deferred to `agentic-ai/**` until all four
   member services and clients pass G07.
6. **Develop one complete member service/component per branch.** The owner
   includes the service project, required service Dockerfile/Compose wiring,
   React and Flutter feature modules, minimal API integration, tests and docs
   in that member's single `features/<component>` branch. There is no
   implementation sequence among the four member branches.

At G00, agree unique service subfolder/project/container identifiers, internal
route and transport contracts, actor/permission propagation, PostgreSQL and
schema ownership, configuration/secrets, health/readiness behavior and test
and CI discovery. These technical details are not inferred by this ADR.

## Rationale

- Separate ownership lets the four members implement business behavior
  without editing the shared API/Auth logical flows.
- The public API remains a stable, single client boundary and handles the
  narrow integration needed to route authenticated operations to their owner.
- Independent service folders and project files reduce same-file conflicts
  between parallel branches. Shared Compose, gateway, client routing and
  route/API registries remain explicit integration points.
- The decision follows the repository's accepted service-oriented
  architecture and does not change v0 routes, Auth lifecycle, role-to-
  permission semantics or client capabilities.

## Consequences

- Each owner must add and maintain an internal service project, Dockerfile,
  Compose integration, configuration, service health behavior, tests and
  relevant endpoint-catalog entries.
- API integration and service-to-service identity propagation need compatible
  contracts at G00. The API's existing `/api/health` remains API liveness and
  must not silently become component-service readiness.
- Each service owns its domain persistence. The team must settle the shared
  PostgreSQL database/schema and migration ownership model before
  implementation; no member service may bypass its owning service to access
  another component's data.
- CI must discover, restore, build and test every new service, and Compose
  validation must cover required configuration and private-network isolation.
- The existing route catalog and UI registry remain authoritative; every
  implemented public route and client workflow is registered as required.

## Alternatives considered

### Implement component business logic inside `services/api`

Rejected for v1 because it would require each member branch to add domain
logic and persistence to the shared API service, contrary to the confirmed
component-isolation requirement and increasing conflicts in shared API files.

### Let clients call each member service directly

Rejected because it would create multiple client-facing boundaries, duplicate
authentication/routing behavior and expose internal service addresses.

### Put all four components in one shared v1 domain service

Rejected because it would centralize implementation across member branches
and weaken component-level ownership and parallel work isolation.

## Acceptance evidence

Each member PR demonstrates its independent service project, domain behavior,
owned persistence and tests; a private service route reachable through the
public `/api/...` contract; no direct client-to-service/Auth path; and only
integration changes in `services/api` or `services/auth`. After sequential PR
merges, the full service graph, both clients, shared contracts and existing
v0 API/Auth behavior are verified together on `dev` before G07.
