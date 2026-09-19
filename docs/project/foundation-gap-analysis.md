# BLUEVERSE Foundation Gap Analysis

This review compares the checked-in repository with the implementation plan from the referenced BLUEVERSE analysis and the SE3090 Project Guidelines PDF.

## Current status

| Area | Status | Evidence / next action |
|---|---|---|
| React web project | Foundation present | `apps/web` is a Vite/React starter. Replace the starter screen with the administrative/staff surface during v1. |
| Flutter mobile project | Foundation present | `apps/mobile` is a generated Flutter starter. Add mobile workflows, routing, validation and API integration during v1. |
| ASP.NET Core API | Foundation implementation present | `services/api` contains the .NET 10 public API, `/api/health`, YARP Auth forwarding, CORS, gateway error handling and Swagger/OpenAPI configuration. Domain endpoints, persistence, authorization policies and API tests remain to be implemented. |
| Auth service | Expected, not checked in | `infrastructure/docker/auth/Dockerfile`, Compose and the API documentation define the intended internal service, but `services/auth` is absent. Authentication, password hashing, JWT issuance and user/role persistence remain unimplemented. |
| PostgreSQL | Local infrastructure present | Compose provides DHI PostgreSQL 16 and publishes host port `5432` for pgAdmin4. EF Core provider, migrations, schema constraints and audit fields are not yet present. |
| Edge gateway | Present | `edge-nginx` routes frontend and `/api/*` traffic only. The API forwards `/api/auth/*` to the internal Auth service. |
| Permission authorization | Design documented only | The role -> permission model is documented in requirements and ADRs, but no policy/permission implementation exists yet. |
| Agentic AI | Architecture documented only | Safety, tools, workflow state and evaluation guidance exist under `docs/agentic-ai`; no executable agent workflow is present yet. |
| ML biodiversity capability | Planned | The requirements describe the OBIS/Bio-ORACLE direction, but correctly defer model implementation until the data-dependent workstream is ready. |
| Testing | Foundation only | Flutter has the generated counter widget test. React has lint/build scripts but no test project. The API builds but has no test project yet; Auth and broader backend tests remain pending. Add unit, integration and end-to-end coverage with each component. |
| CI/CD | Docker CI foundation present | `docker-web-build.yml` synchronizes the React lockfile and builds the React image, `docker-backend-build.yml` discovers and builds ASP.NET services sequentially while aggregating errors, and `docker-stack-health.yml` synchronizes the lockfile again and validates the Compose stack after both builds. Flutter, security, broader integration and deployment checks remain to be expanded. |
| Deployment | Configuration present, evidence pending | Render API/Auth/PostgreSQL and Vercel documentation exist; live URLs, migrations and deployment evidence must be recorded before submission. |
| Git/GitHub | Repository present | Git metadata and branch history are present in the reviewed checkout. Continue using focused commits, pull requests and contribution evidence. |

## Guideline-critical work still required

The Project Guidelines require a final integrated workflow that starts in one client, passes through ASP.NET Core, PostgreSQL and Agentic AI, requires review or approval in the other client, and returns an updated status. This is intentionally not a v0 deliverable, but it must be planned as a first-class cross-platform acceptance test.

The final submission also needs four distinct business components for a standard four-person group. Each component must have backend, database, React, Flutter, testing, Git/documentation and a distinct Agentic AI contribution. The current foundation does not claim those components are implemented.

## Foundation acceptance checks

After the Auth project is added, copying `.env.example` to `.env`, setting local
passwords and starting Compose, verify:

```text
GET http://localhost/health
GET http://localhost/api/health
GET http://localhost/api/auth/health
GET http://localhost/api/swagger/v1.json
GET http://localhost/api/swagger/v1/swagger.json
GET http://localhost/api/auth/swagger/v1/swagger.json
```

These checks prove gateway routing and service liveness only; they do not substitute for authentication, authorization, database, client integration, or Agentic AI tests.
