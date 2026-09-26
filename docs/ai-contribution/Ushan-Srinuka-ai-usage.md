# Ushan Srinuka — AI Usage Contributions

## 2026-09-27 — Database status in component health check

- Date/time or time range: 2026-09-27 03:16 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Include PostgreSQL connection status in the
  Experience & Biodiversity health response, matching Auth.
- Summary of what the AI Agent did: Added a bounded, anonymous EF Core
  connectivity check to the health controller. It returns the Auth-style
  service/status/database fields and 503 when PostgreSQL is unavailable.
  Updated the G00 proposal, API endpoint catalog and API documentation.
- AI output accepted/changed/rejected: Mirrored Auth's response behavior and
  timeout; kept the endpoint anonymous. No other routes or client workflows
  were changed.
- Verification/evidence: The service `dotnet build --no-restore` passed with
  0 warnings and 0 errors; endpoint catalog generation/validation passed
  (35 public endpoints, 22 frontend routes); `git diff --check` passed. No
  tests were added or run.

## 2026-09-27 — Align component database setup with Auth

- Date/time or time range: 2026-09-27 02:51 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Replace the Experience & Biodiversity SQL
  initialization approach with the Auth service's EF Core setup and keep all
  services on one PostgreSQL database.
- Summary of what the AI Agent did: Removed the PostgreSQL init SQL mount and
  service-specific password setting. Configured the service to use the same
  database, login and password as Auth, the PostgreSQL default schema, and an
  independent EF migration-history table. Added bounded connection checking
  and startup migration handling. Updated deployment, database, architecture,
  readiness, CI and owner proposal documentation.
- AI output accepted/changed/rejected: Replaced the earlier dedicated login
  and schema bootstrap as requested. Kept G00 pending and added no domain
  entities, tables or migrations.
- Verification/evidence: The service `dotnet build --no-restore` passed with
  0 warnings and 0 errors; `appsettings.json` parsed and `git diff --check`
  passed. Docker is unavailable, so Compose/live database behavior was not
  verified. No tests were added or run.

## 2026-09-27 — Experience & Biodiversity PostgreSQL connection

- Date/time or time range: 2026-09-27 02:23 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Implement the Experience & Biodiversity
  service's database connection and update the relevant documentation.
- Summary of what the AI Agent did: Added EF Core/Npgsql configuration, a
  schema-scoped DbContext and design-time factory, and a bounded startup
  connection check. Added PostgreSQL role/schema provisioning for the service,
  Compose credentials/network/dependency wiring and the Docker stack-health
  workflow environment value. Updated setup, deployment, database, architecture
  and readiness documentation.
- AI output accepted/changed/rejected: Kept the work to connection and schema
  infrastructure; added no domain entities, business tables, migrations or
  workflows. G00 remains pending.
- Verification/evidence: `dotnet build` for the service project with
  `--no-restore` passed with 0 warnings and 0 errors; `appsettings.json` parsed
  successfully; `git diff --check` passed. Docker and `psql` are unavailable in
  this environment, so Compose and live PostgreSQL behavior were not verified.
  Python is not on `PATH`, so the repository catalog/route validators could not
  be rerun. No tests were added or run.

## 2026-09-27 — Swagger and gateway JWT security

- Date/time or time range: 2026-09-27 01:29 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Remove the Swagger Servers field and protect
  the Experience & Biodiversity service using Auth JWT security while matching
  the API/Auth structure.
- Summary of what the AI Agent did: Replaced built-in OpenAPI generation with
  Swashbuckle and the API/Auth Bearer configuration. Protected the component
  YARP route with the API default JWT policy and kept only health and the
  Swagger document anonymous. Updated the public Swagger URL, endpoint
  catalog, security/deployment documentation and CI health check.
- AI output accepted/changed/rejected: Applied the existing API gateway JWT
  validation boundary; did not add duplicate JWT secrets or direct Auth calls
  to the private service. Kept G00 pending and added no business workflows.
- Verification/evidence: Service and API `dotnet build --no-restore` passed;
  endpoint catalog validation passed (35 public endpoints, 22 frontend routes),
  UI integration validation passed, and `git diff --check` passed. No tests were
  added or run.

## 2026-09-27 — Same-origin health check and concise route

- Date/time or time range: 2026-09-27 00:55 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Fix Swagger Try it out using the Docker-only
  service host and use the shorter `/api/experiences` public route for the
  Experience & Biodiversity health/OpenAPI endpoints.
- Summary of what the AI Agent did: Configured a relative `/` server URL in the
  component OpenAPI document, renamed its public gateway paths to
  `/api/experiences`, updated Swagger UI and the stack health workflow to use
  the public alias, and synchronized the endpoint catalog and documentation.
  Kept the internal Compose/YARP destination name `experience-biodiversity`.
- AI output accepted/changed/rejected: Applied the requested browser-visible
  route and same-origin behavior; left the service private and G00 pending.
  No domain workflow, persistence or Agentic AI execution was added.
- Verification/evidence: API and service `dotnet build --no-restore` passed;
  endpoint catalog validation passed (35 public endpoints, 22 frontend routes)
  and UI integration validation passed; `git diff --check` passed. Docker is
  unavailable in this environment, so Compose runtime behavior was not checked.
  No tests were added or run.

## 2026-09-27 — Experience & Biodiversity service host bootstrap

- Date/time or time range: 2026-09-27 00:26 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Initialize the Experience & Biodiversity
  service host, health controller and Docker setup on its assigned feature
  branch, following the existing API/Auth service structure.
- Summary of what the AI Agent did: Added controller/authorization/OpenAPI
  host setup, anonymous process liveness, structured unhandled-error responses,
  a DHI-based Dockerfile, private Compose networking, API YARP routing and a
  Swagger UI document entry. Updated endpoint catalog, service/readiness and
  deployment documentation to identify the scaffold as host-only.
- AI output accepted/changed/rejected: Used the service identity, route prefix
  and port proposed in Ushan's G00 input for this requested bootstrap while
  retaining G00 as pending; no shared agreement, domain workflow, persistence,
  provider integration or Agentic AI execution was claimed or implemented.
- Verification/evidence: Endpoint catalog generation and validation passed
  (35 public endpoints, 22 frontend routes; no AI endpoints). The service
  built successfully with `dotnet build ... --no-restore`. `git diff --check`
  passed. A restoring build was blocked by denied access to the user NuGet
  configuration; Docker/Compose validation was unavailable because Docker
  CLI is not installed. No tests were added or run.

## 2026-09-26 — Experience & Biodiversity G00 contract input

- Date/time or time range: 2026-09-26 22:55 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Prepare only G00 decisions for the
  experience-biodiversity component on its assigned feature branch and update
  the AI usage log as Ushan.
- Summary of what the AI Agent did: Reviewed the repository's G00 readiness,
  component contract, branch workflow, architecture and existing branch state.
  Added a Member 1 G00 proposal covering service/data identity, canonical IDs,
  candidate public operations and permissions, publication/availability and
  time semantics, UI workflows, and cross-component handoffs. Linked it from
  the component contract. Kept the shared gate pending and preserved existing
  untracked service starter files and the unrelated API health-controller
  deletion.
- AI output accepted/changed/rejected: Retained the component-specific
  contract proposals. Marked shared-owner items as pending review and the
  document as a proposal because G00 is still pending in the repository.
  No team agreement or implementation was represented as complete.
- Verification/evidence: `git diff --check` passed; relative Markdown links in
  the changed component documentation resolved. No tests or application
  validation were run for this documentation-only G00 proposal.
