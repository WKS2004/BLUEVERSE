# BLUEVERSE Foundation Gap Analysis

This review compares the checked-in repository with the implementation plan from the referenced BLUEVERSE analysis and the SE3090 Project Guidelines PDF.

## Current status

| Area | Status | Evidence / next action |
|---|---|---|
| React web project | Auth workflow present | `apps/web` provides a public `/login` cookie-based Auth surface, refresh recovery, current-device/everywhere logout and active-session display. Future domain workflows remain v1 work. |
| Flutter mobile project | Auth workflow present | `apps/mobile` provides a `/login` workflow using the public gateway, platform secure storage for installation/session credentials, refresh recovery and session/logout controls. Future domain workflows remain v1 work. |
| ASP.NET Core API | Foundation present | `services/api` provides the .NET 10 public gateway, OpenAPI/Swagger, CORS, JWT validation, health and YARP routing. Domain workflow endpoints remain future work. |
| Auth service | Session lifecycle implemented | `services/auth` provides registration/login, server-issued installations, PBKDF2 password hashing, 15-minute JWTs, rotating hashed refresh tokens, one/30-day absolute sessions, five-account-per-device and five-session-per-account limits, scoped logout, active-session management with ended-session logs, permission policies, role/user administration, health and bootstrap seeding. |
| PostgreSQL | Local infrastructure and Auth persistence present | Compose provides DHI PostgreSQL 16 and publishes host port `5432` for pgAdmin4. Auth uses EF Core constraints and checked-in migrations; domain schema remains future work. |
| Edge gateway | Present | `edge-nginx` routes frontend and `/api/*` traffic only. The API forwards `/api/auth/*` to the internal Auth service. |
| Permission authorization | Implemented for Auth | Permission policies use role-derived claims, unknown assignments are rejected, and system roles are protected by a dedicated permission. |
| Agentic AI | Architecture documented only | Safety, tools, workflow state and evaluation guidance exist under `docs/agentic-ai`; no executable agent workflow is present yet. |
| ML biodiversity capability | Planned | The requirements describe the OBIS/Bio-ORACLE direction, but correctly defer model implementation until the data-dependent workstream is ready. |
| Testing | API/Auth/client foundation coverage present | API tests cover the gateway foundation, the default Auth suite covers 23 deterministic session/authorization cases plus an opt-in PostgreSQL session/concurrency smoke test, and React build/lint plus Flutter analyze/widget/API-boundary tests cover the shared Auth client surfaces. Domain workflow coverage remains future work. |
| CI/CD | Source, client, Docker and UI-contract foundations present | `web-ci.yml` and `mobile-ci.yml` run the shared UI integration validator; `ui-integration.yml` rechecks the registry when either client, backend, gateway or contract changes; Docker workflows remain as documented. Runtime API/gateway and cross-platform acceptance checks still require tracked backend services. |
| Agent resources | Finalized and validated | `.agents/` contains seven BLUEVERSE-owned workflows, fourteen pinned supplementary skills, registry/provenance metadata, a portable .NET overlay, routing evaluations and a dependency-free validator enforced by `repository-ci.yml`. |
| Deployment | Configuration present, evidence pending | Render API/Auth/PostgreSQL and Vercel documentation exist; live URLs, migrations and deployment evidence must be recorded before submission. |
| Git/GitHub | Repository present | Git metadata and branch history are present in the reviewed checkout. Continue using focused commits, pull requests and contribution evidence. |

## Guideline-critical work still required

The Project Guidelines require a final integrated workflow that starts in one client, passes through ASP.NET Core, PostgreSQL and Agentic AI, requires review or approval in the other client, and returns an updated status. This is intentionally not a v0 deliverable, but it must be planned as a first-class cross-platform acceptance test.

The final submission also needs four distinct business components for a standard four-person group. Each component must have backend, database, React, Flutter, testing, Git/documentation and a distinct Agentic AI contribution. The current foundation does not claim those components are implemented.

## Foundation acceptance checks

After copying `.env.example` to `.env`, setting a unique JWT signing key and
local passwords, and starting Compose, verify:

```text
GET http://localhost/health
GET http://localhost/api/health
GET http://localhost/api/auth/health
GET http://localhost/api/swagger/v1.json
GET http://localhost/api/auth/swagger/v1/swagger.json
```

These checks prove gateway routing and service readiness only; they do not
substitute for authentication, authorization, database, client integration, or
Agentic AI tests.

## UI integration acceptance check

Before a real screen is considered complete, verify the shared registry and
validator:

```text
python scripts/validation/validate_ui_integrations.py
python -m unittest discover -s scripts/validation/tests -p "test_*.py"
```

The current registry contains the shared Auth session-management workflow and
its public endpoint references. Acceptance evidence includes both client
surfaces, backend endpoint tests and the public gateway/API request boundary; a
passing frontend build alone is not sufficient.

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
