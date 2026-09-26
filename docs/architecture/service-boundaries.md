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
- DTOs
- validation
- application services
- business rules
- persistence
- workflow initiation
- approval enforcement

Current foundation behavior includes `GET /api/health`, OpenAPI/Swagger
publication, CORS, forwarded headers, JWT validation, RFC 7807-style gateway
errors and YARP forwarding for `/api/auth/*`. Domain workflow responsibilities
above remain the target boundary for subsequent API work.

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

## postgres

Persistent relational data store.

## v1 Agentic AI target

Four distinct agents are specified in the [v1 agent documents](../v1/README.md):
Planning & Coordination, Marine Conditions Intelligence, Coastal Experience
& Biodiversity, and Safety & Operations. No executable agent service is
currently checked in. If a separate process is used, it remains internal
and cannot become a second client-facing API. Application code performs
deterministic validation; only authorized ASP.NET Core business logic
executes approved protected changes.

## v1 ML inference target

The separate IT3091 workstream supplies the initial biodiversity model and
inference service. Member 3 owns BLUEVERSE's validated public result contract
and server-side adapter; ASP.NET Core calls the private inference boundary.
Member 1 owns experience-facing presentation and consumes the Member 3
contract. A missing service yields an explicit unavailable result. The
adapter, model and inference service are not implemented in this repository
today. The BLUEVERSE adapter is ordinary member-feature ML/API integration,
separate from post-G07 Agentic AI work; see
[ADR-0019](../adr/ADR-0019-biodiversity-inference-integration-ownership.md).

## v1 map-provider target

Member 1 owns the BLUEVERSE adapter for the selected map API as part of
location-aware destination and activity discovery. React and Flutter reach
map capabilities only through the public ASP.NET Core API; provider
credentials and outbound requests remain server-side. Provider-returned place
or map data is untrusted discovery/display context and does not own canonical
destination records. The provider, exact map features and compatible
rendering approach remain open under
[ADR-0017](../adr/ADR-0017-map-provider-integration-boundary.md). This
integration is not implemented in the current repository.
