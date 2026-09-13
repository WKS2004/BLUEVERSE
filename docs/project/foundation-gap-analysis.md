# BLUEVERSE Foundation Gap Analysis

This review compares the checked-in repository with the implementation plan from the referenced BLUEVERSE analysis and the SE3090 Project Guidelines PDF.

## Current status

| Area | Status | Evidence / next action |
|---|---|---|
| React web project | Foundation present | `apps/web` is a Vite/React starter. Replace the starter screen with the administrative/staff surface during v1. |
| Flutter mobile project | Foundation present | `apps/mobile` is a generated Flutter starter. Add mobile workflows, routing, validation and API integration during v1. |
| ASP.NET Core API | Foundation present | `services/api` targets .NET 10 and exposes `/api/health` plus OpenAPI. EF Core, DTOs, application services, authentication and domain endpoints remain to be implemented. |
| Auth service | Foundation present | `services/auth` exposes `/api/auth/health` and `/api/auth/swagger/v1/swagger.json` only through the API boundary. Authentication, password hashing, JWT issuance and user/role persistence remain to be implemented. |
| PostgreSQL | Local infrastructure present | Compose provides DHI PostgreSQL 16 and publishes host port `5432` for pgAdmin4. EF Core provider, migrations, schema constraints and audit fields are not yet present. |
| Edge gateway | Present | `edge-nginx` routes frontend and `/api/*` traffic only. The API forwards `/api/auth/*` to the internal Auth service. |
| Permission authorization | Design documented only | The role -> permission model is documented in requirements and ADRs, but no policy/permission implementation exists yet. |
| Agentic AI | Architecture documented only | Safety, tools, workflow state and evaluation guidance exist under `docs/agentic-ai`; no executable agent workflow is present yet. |
| ML biodiversity capability | Planned | The requirements describe the OBIS/Bio-ORACLE direction, but correctly defer model implementation until the data-dependent workstream is ready. |
| Testing | Foundation only | Flutter has the generated widget test; backend and React test projects are not present. Add unit, integration and end-to-end coverage with each component. |
| CI/CD | CI foundation present | Backend/repository workflows exist. Add frontend, Flutter, security, integration and deployment checks as implementations mature. |
| Deployment | Configuration present, evidence pending | Render API/Auth/PostgreSQL and Vercel documentation exist; live URLs, migrations and deployment evidence must be recorded before submission. |
| Git/GitHub | Local checkout issue | The reviewed directory currently has no `.git` metadata, so commit history and individual contribution evidence cannot be verified here. Initialize/restore the repository before collaborative development. |

## Guideline-critical work still required

The Project Guidelines require a final integrated workflow that starts in one client, passes through ASP.NET Core, PostgreSQL and Agentic AI, requires review or approval in the other client, and returns an updated status. This is intentionally not a v0 deliverable, but it must be planned as a first-class cross-platform acceptance test.

The final submission also needs four distinct business components for a standard four-person group. Each component must have backend, database, React, Flutter, testing, Git/documentation and a distinct Agentic AI contribution. The current foundation does not claim those components are implemented.

## Foundation acceptance checks

After copying `.env.example` to `.env` and starting Compose, verify:

```text
GET http://localhost/api/health
GET http://localhost/api/auth/health
GET http://localhost/api/swagger/v1.json
GET http://localhost/api/auth/swagger/v1/swagger.json
```

These checks prove gateway routing and service liveness only; they do not substitute for authentication, authorization, database, client integration, or Agentic AI tests.
