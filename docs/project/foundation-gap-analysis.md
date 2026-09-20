# BLUEVERSE Foundation Gap Analysis

This review compares the checked-in repository with the implementation plan from the referenced BLUEVERSE analysis and the SE3090 Project Guidelines PDF.

## Current status

| Area | Status | Evidence / next action |
|---|---|---|
| React web project | Foundation present | `apps/web` is a Vite/React starter. Replace the starter screen with the administrative/staff surface during v1 and register every workflow route/API reference in `docs/contracts/ui-integration.json`. |
| Flutter mobile project | Foundation present | `apps/mobile` is a generated Flutter starter. Add mobile workflows, routing, validation and API integration during v1 using the same workflow registry and public API contract as React. |
| ASP.NET Core API | Expected, not checked in | `infrastructure/docker/api/Dockerfile`, Compose and the API documentation define the intended .NET 10 public service, but `services/api` is absent. No API route or OpenAPI document is currently runnable. |
| Auth service | Expected, not checked in | `infrastructure/docker/auth/Dockerfile`, Compose and the API documentation define the intended internal service, but `services/auth` is absent. Authentication, password hashing, JWT issuance and user/role persistence remain unimplemented. |
| PostgreSQL | Local infrastructure present | Compose provides DHI PostgreSQL 16 and publishes host port `5432` for pgAdmin4. EF Core provider, migrations, schema constraints and audit fields are not yet present. |
| Edge gateway | Present | `edge-nginx` routes frontend and `/api/*` traffic only. The API forwards `/api/auth/*` to the internal Auth service. |
| Permission authorization | Design documented only | The role -> permission model is documented in requirements and ADRs, but no policy/permission implementation exists yet. |
| Agentic AI | Architecture documented only | Safety, tools, workflow state and evaluation guidance exist under `docs/agentic-ai`; no executable agent workflow is present yet. |
| ML biodiversity capability | Planned | The requirements describe the OBIS/Bio-ORACLE direction, but correctly defer model implementation until the data-dependent workstream is ready. |
| Testing | Foundation only | Flutter has the generated counter widget test. React has lint/build scripts but no test project; backend tests cannot run because services are absent. Add unit, integration and end-to-end coverage with each component. |
| CI/CD | Source, client, Docker and UI-contract foundations present | `web-ci.yml` and `mobile-ci.yml` run the shared UI integration validator; `ui-integration.yml` rechecks the registry when either client, backend, gateway or contract changes; Docker workflows remain as documented. Runtime API/gateway and cross-platform acceptance checks still require tracked backend services. |
| Agent resources | Finalized and validated | `.agents/` contains seven BLUEVERSE-owned workflows, fourteen pinned supplementary skills, registry/provenance metadata, a portable .NET overlay, routing evaluations and a dependency-free validator enforced by `repository-ci.yml`. |
| Deployment | Configuration present, evidence pending | Render API/Auth/PostgreSQL and Vercel documentation exist; live URLs, migrations and deployment evidence must be recorded before submission. |
| Git/GitHub | Repository present | Git metadata and branch history are present in the reviewed checkout. Continue using focused commits, pull requests and contribution evidence. |

## Guideline-critical work still required

The Project Guidelines require a final integrated workflow that starts in one client, passes through ASP.NET Core, PostgreSQL and Agentic AI, requires review or approval in the other client, and returns an updated status. This is intentionally not a v0 deliverable, but it must be planned as a first-class cross-platform acceptance test.

The final submission also needs four distinct business components for a standard four-person group. Each component must have backend, database, React, Flutter, testing, Git/documentation and a distinct Agentic AI contribution. The current foundation does not claim those components are implemented.

## Foundation acceptance checks

After the missing ASP.NET projects are added, copying `.env.example` to `.env`,
setting local passwords and starting Compose, verify:

```text
GET http://localhost/health
GET http://localhost/api/health
GET http://localhost/api/auth/health
GET http://localhost/api/swagger/v1.json
GET http://localhost/api/auth/swagger/v1/swagger.json
```

These checks prove gateway routing and service liveness only; they do not substitute for authentication, authorization, database, client integration, or Agentic AI tests.

## UI integration acceptance check

Before a real screen is considered complete, verify the shared registry and
validator:

```text
python scripts/validation/validate_ui_integrations.py
python -m unittest discover -s scripts/validation/tests -p "test_*.py"
```

The current foundation check intentionally reports one starter workflow and
zero API endpoint references. Once a backend endpoint exists, the acceptance
evidence must include both client workflow tests and the public gateway/API
request boundary; a passing frontend build alone is not sufficient.

## Agent-resource acceptance check

Agent resources are independently considered ready when the following command
passes from the repository root:

```bash
python .agents/scripts/validate_agent_resources.py
```

The current checkout passes this gate with twenty-one repository skills
validated. The optional upstream YAML-based skill validator and the Windows
foundation verifier remain environment-dependent; their unavailable or
elevation-blocked results must be reported separately from the agent-resource
gate.
