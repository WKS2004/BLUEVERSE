# Service Boundaries

The following boundaries are implemented for the current foundation. The edge,
client starter projects with the shared Auth workflow, public API gateway and
internal Auth service are checked in; future domain services remain separate
implementation work.

## edge-nginx

Responsibilities:

- local HTTP entry point
- reverse proxying
- basic security response headers
- routing

Not responsible for business rules.

The frontend's client routes and public API references are linked through the
shared UI integration registry. Frontend route changes must pass its validator
alongside the normal web/mobile checks.

## client applications (`apps/web` and `apps/mobile`)

Responsibilities:

- React Web and Flutter Mobile applications
- client-side routing and navigation
- role-aware presentation and user interaction
- input capture, local state and user-facing validation
- loading, empty, success, denied and recoverable failure states
- platform-appropriate layouts and interaction patterns for the same workflow

Both clients provide every permitted business workflow to tourists, coastal
operators, operations reviewers and platform administrators, and to future
roles when introduced. Neither client owns or prioritizes a stakeholder
group. They are not authoritative for permissions or business rules; the
public API and its role → permission model remain authoritative. The
[v1 component map](../v1/README.md) defines target business ownership.

## api (`services/api`)

Responsibilities:

- public REST API
- common public authentication and role-to-permission integration
- public route/contract documentation and integration with private services
- common request/response and gateway-level error handling

Current foundation behavior includes `GET /api/health`, OpenAPI/Swagger
publication, CORS, forwarded headers, JWT validation, RFC 7807-style gateway
errors and YARP forwarding for `/api/auth/*`. Domain workflow responsibilities
are implemented by the owning v1 component service and exposed through this
public boundary.

### Stability for v1 member component branches

Each v1 member implements new domain behavior in its own internal ASP.NET Core
service. Changes to `services/api` are limited to the smallest routing,
authentication/permission and typed-service integration needed to expose the
component operation under the existing public `/api/...` boundary. Existing
endpoint semantics, middleware and authorization pipeline, CORS,
forwarded-header behavior, JWT/cookie handling, error translation, Auth
forwarding and API health meaning remain stable. A member does not implement
its domain rules or persistence inside the existing API service.

## auth (`services/auth`)

Responsibilities:

- credential registration, login, password changes and multi-account device
  session/logout management
- active-session state and ended-session lifecycle logging
- JWT issuance and token-version revocation
- user, role and permission administration
- role-derived permission policies and system-role protections
- PostgreSQL persistence, migrations and bootstrap administrator seeding

Auth is internal-only. Clients use the public API gateway and never call the
Auth container, database or internal hostname directly.

The v1 member branches reuse this Auth service. They do not change its
registration, sign-in, refresh, logout, password, device/session, token,
role-to-permission or system-role behavior. An essential Auth integration
change must be additive, minimal and justified by the component contract; the
default is no Auth service change.

## v1 member component services

These four separately owned internal services are v1 target architecture and
are **not implemented in the current repository**. Each member implements one
service on the same `features/<component>` branch as its React and Flutter
work. The service owns its component's domain operations, domain validation,
application/data-access code, EF Core migrations and PostgreSQL records,
third-party adapter, tests, health/readiness contract and prepared private
Agentic AI client seam as required by its component contract.

| Owner | Service-owned responsibilities |
|---|---|
| Member 1 | Coastal catalogue/discovery, availability, favourites and the backend map-provider adapter |
| Member 2 | Open-Meteo acquisition, normalized marine conditions, safety profiles and deterministic suitability |
| Member 3 | Planning, recommendations, itineraries and the IT3091 biodiversity inference adapter/public result contract |
| Member 4 | BLUEVERSE-managed operational assessments, decisions, restrictions, alerts, audit and controlled execution |

The services remain private on the approved Docker network. React and Flutter
never call them directly; the public API authenticates/authorizes and routes
or forwards client operations. Component services never call Auth directly.
G00 must settle service identifiers, the API-to-service transport and route
map, actor/permission propagation, PostgreSQL/schema ownership, and
health/readiness semantics before parallel implementation. Keep each service
isolated so its owner does not need to edit another member's service.

## postgres

Persistent relational data store.

## v1 Agentic AI target

Four distinct agents are specified in the [v1 agent documents](../v1/README.md):
Planning & Coordination, Marine Conditions Intelligence, Coastal Experience
& Biodiversity, and Safety & Operations. No executable agent service is
currently checked in. If a separate process is used, it remains internal
and cannot become a second client-facing API. Component services perform
deterministic validation; only the owning, authorized component service
executes approved protected changes.

## v1 ML inference target

The separate IT3091 workstream supplies the initial biodiversity model and
inference service. Member 3 owns BLUEVERSE's validated public result contract
and private-service adapter; its component service calls the inference
boundary.
Member 1 owns experience-facing presentation and consumes the Member 3
contract. A missing service yields an explicit unavailable result. The
adapter, model and inference service are not implemented in this repository
today. The BLUEVERSE adapter is ordinary member-feature ML/API integration,
separate from post-G07 Agentic AI work; see
[ADR-0019](../adr/ADR-0019-biodiversity-inference-integration-ownership.md).

## v1 map-provider target

Member 1 owns the BLUEVERSE adapter for the selected map API as part of
location-aware destination and activity discovery. React and Flutter reach
map capabilities only through the public API and Member 1's private component
service; provider credentials and outbound requests remain server-side.
Provider-returned place
or map data is untrusted discovery/display context and does not own canonical
destination records. The provider, exact map features and compatible
rendering approach remain open under
[ADR-0017](../adr/ADR-0017-map-provider-integration-boundary.md). This
integration is not implemented in the current repository.

The one-service-per-member target and the public API integration-only rule are
recorded in [ADR-0020](../adr/ADR-0020-member-component-service-boundaries.md).
