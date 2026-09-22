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

Both clients are product surfaces for clients, staff and administrators. They
are not authoritative for permissions or business rules; the public API and its
role → permission model remain authoritative.

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

## future Agentic AI service

Internal-only service if a separate process is used. It must not become a second public API.
