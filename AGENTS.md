# BLUEVERSE Agent Instructions

## Scope

These instructions apply to the entire BLUEVERSE repository.

## Non-negotiable architecture

- React and Flutter communicate only with the ASP.NET Core public API.
- Do not expose internal Agentic AI services directly to clients.
- PostgreSQL is accessed through the backend/service layer.
- `edge-nginx` is the local Docker entry point.
- Do not introduce API version path segments such as `/api/v1`.
- Keep the role → permission model; do not replace it with hard-coded role checks.

## Codebase locations

- React: `apps/web`
- Flutter: `apps/mobile`
- Public API: `services/api`
- Auth: `services/auth`
- Docker infrastructure: `infrastructure/docker`
- Architecture/docs: `docs`

Do not generate replacement sample applications into this foundation package.

## Docker

Use Docker Hardened Images where the project has selected DHI images.

Current base images:

- `dhi.io/node:24-debian13-dev`
- `dhi.io/nginx:1.30-alpine`
- `dhi.io/dotnet:10-sdk-alpine`
- `dhi.io/aspnetcore:10-alpine`
- `dhi.io/postgres:16-alpine`

Do not silently switch to unrelated base images.

The ASP.NET runtime image is intentionally treated as a minimal runtime image. Prefer exec-form `ENTRYPOINT`/`CMD`; do not assume `/bin/sh` exists.

## API

- ASP.NET Core 10
- SDK pinned by `global.json`
- RESTful routes
- DTOs and application/service layers
- asynchronous I/O
- validation
- structured error handling
- Swagger/OpenAPI
- tests

## Security

Never commit secrets.

Do not store:

- passwords
- access tokens
- JWT signing keys
- API keys
- hidden Agentic AI reasoning

Use environment variables or approved secret management.

## Agentic AI

AI output is untrusted input.

Validate model/tool output deterministically before it can influence business operations. High-impact actions require authorization and human approval where defined by the business workflow.

Protect agent tools and workflows against prompt injection and unauthorized tool execution.

## Database

Use PostgreSQL and EF Core migrations.

Persist only the data required for the application and Agentic AI workflow. Use constraints, indexes and audit fields where appropriate.

## Clients

React and Flutter must use the same backend contract and authorization model.

Do not create client-side business rules that contradict the API.

## Testing

Every meaningful change should include or update appropriate tests.

Do not claim an implementation is complete merely because it compiles.

## Git

Prefer focused commits with descriptive messages.

Never commit generated secrets, `.env`, local IDE state, build artifacts or machine-specific configuration.

## Documentation

Architecture changes require an ADR when they materially affect a documented architectural decision.

Keep setup and operational documentation synchronized with the actual repository.
