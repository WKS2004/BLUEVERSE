# Service Boundaries

The following are intended boundaries. Only the edge/frontend Docker
configuration and client starter projects are currently checked in; the API
and Auth source directories remain to be generated.

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

## api (expected at `services/api`)

Responsibilities:

- public REST API
- DTOs
- validation
- application services
- business rules
- persistence
- workflow initiation
- approval enforcement

## auth (expected at `services/auth`)

Responsibilities:

- auth/authentication foundation
- credentials and auth-related operations

Its exact boundary must be kept explicit as implementation evolves.

## postgres

Persistent relational data store.

## future Agentic AI service

Internal-only service if a separate process is used. It must not become a second public API.
