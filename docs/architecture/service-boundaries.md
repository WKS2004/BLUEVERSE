# Service Boundaries

The following are the intended boundaries. The edge/frontend configuration,
client starter projects and public API foundation are checked in. The Auth and
future domain services remain separate implementation work.

## edge-nginx

Responsibilities:

- local HTTP entry point
- reverse proxying
- basic security response headers
- routing

Not responsible for business rules.

## frontend

Responsibilities:

- React static application
- client-side routing
- presentation and user interaction

Not authoritative for permissions or business rules.

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
publication, CORS, forwarded headers, RFC 7807-style gateway errors and YARP
forwarding for `/api/auth/*`. The domain responsibilities above are the target
boundary for subsequent API work.

## auth (pending at `services/auth`)

Responsibilities:

- auth/authentication foundation
- credentials and auth-related operations

Its exact boundary must be kept explicit as implementation evolves.

## postgres

Persistent relational data store.

## future Agentic AI service

Internal-only service if a separate process is used. It must not become a second public API.
