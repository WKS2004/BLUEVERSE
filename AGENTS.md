# BLUEVERSE Agent Instructions

## Scope

These instructions apply to the entire BLUEVERSE repository.

For task routing and focused validation, read
[`.agents/routing.md`](.agents/routing.md), then only the applicable rules or
repository skills. Read [`.agents/README.md`](.agents/README.md) when changing
the agent configuration itself.

## Non-negotiable architecture

- React and Flutter communicate only with the ASP.NET Core public API.
- Every new, generated or updated UI must be registered in
  `docs/contracts/ui-integration.json` with its React route, Flutter route and
  public API endpoint references before it can pass CI.
- The complete route and endpoint quick reference is
  `docs/api/endpoint-catalog.md`. Update its source data in
  `docs/api/endpoint-catalog.json` whenever a frontend route, gateway route,
  public API/Auth route or Agentic AI endpoint changes, then regenerate the
  Markdown view. Ordinary endpoint work does not change `.agents` guidance.
- The two clients connect through shared workflow IDs and the public API; they
  must not call one another or use internal service hostnames.
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

Do not replace repository applications with sample applications.

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

For Agentic AI work, read the applicable requirements and owning agent
contract. Documentation of a workflow is not evidence that its service or
tools are implemented.

## Database

Use PostgreSQL and EF Core migrations.

For EF Core models, migrations, queries, persistence tests or provider-specific
behavior, also read [`.agents/rules/data-access.md`](.agents/rules/data-access.md)
and the owning `blueverse-postgresql-efcore` workflow.

Persist only the data required for the application and Agentic AI workflow. Use constraints, indexes and audit fields where appropriate.

## Clients

React and Flutter must use the same backend contract and authorization model.

For the React Web client, use Tailwind CSS utility classes for page and
component styling. Keep shared design tokens and base styles in the web
Tailwind entry stylesheet; add standalone component CSS only when a behavior
cannot be expressed clearly with Tailwind utilities. Use Flutter's native
widget styling for the mobile client.

React Web and Flutter Mobile must offer the same authorized roles, business
capabilities and workflow actions. Neither frontend has priority, design
emphasis or ownership for management, tourists, field work or any other
stakeholder group. Layout and device integrations may differ while the
business outcome, permission checks and public API contract remain equivalent.
Read the applicable requirements and
[UI integration guidance](docs/development/ui-integration.md) for each workflow.

Do not create client-side business rules that contradict the API.

UI work should be user-friendly, scope-aligned and realistic for the coastal
domain rather than defaulting to a technical or analytical dashboard. Follow
[`docs/project/ui-experience-principles.md`](docs/project/ui-experience-principles.md)
for the detailed experience standard.

For every UI change, re-check frontend route declarations and every request
target with `scripts/validation/validate_ui_integrations.py`. A route or
`/api/...` call that is not registered in the shared contract, a direct Auth,
Agentic AI, PostgreSQL/internal-service target, an unapproved absolute API
host, a dynamic/unverifiable network target, and `/api/v1`-style path are
invalid. Prefer literal relative `/api/...` paths; the check applies even when
a UI is generated from a template.

For any API, gateway, frontend-route or Agentic AI route change, first read
`docs/api/endpoint-catalog.md`, update `docs/api/endpoint-catalog.json` with
the endpoint's purpose and usage, regenerate the Markdown view with
`python .agents/scripts/validate_endpoint_catalog.py --write-markdown`, then
run `python .agents/scripts/validate_endpoint_catalog.py`. The implementation
sources remain authoritative. Change `.agents` rules or skills only when the
user explicitly requests or approves an agent-guidance change; routine
endpoint maintenance belongs in `docs/api/`.

## Testing

Every meaningful change should include or update appropriate tests.

Do not claim an implementation is complete merely because it compiles.

Implement the relevant test cases in the same change as the implementation.
For each behavior, cover the normal passing path plus applicable invalid,
boundary, extreme, empty/missing, malformed, duplicate, timeout/retry,
concurrency, dependency-failure and authorization conditions. Derive expected
behavior from the requirements or contract independently of the implementation
so a wrong implementation and a copied-wrong test can be detected.
Newly authored tests may initially fail against an incorrect implementation;
keep the requirement-based expectation and use the failure as defect evidence
instead of changing the expected result to match current behavior.

Test outcomes must be determined by the complete test logic. For HTTP tests,
an expected status code is only one assertion; the body/schema, headers,
authorization semantics, persistence, audit records, state transitions and
side effects must also match. A matching status alone does not make a test
pass, and an expected non-2xx status is not an error when the complete test
logic intentionally requires it. Do not weaken or rewrite failing tests merely
to make CI pass; fix the implementation or configuration under test.

Agents must ask the user for permission before changing, deleting, skipping or
relaxing an existing test. This includes the case where both implementation
and test were created incorrectly—for example, a requirement says a range is
1–10 but both code and tests use 100–1000. After approval, correct the test and
implementation together, record why the original test was wrong, and add the
boundary cases that prevent the error from returning.

## Git

Prefer focused commits with descriptive messages.

Never commit generated secrets, `.env`, local IDE state, build artifacts or machine-specific configuration.

## Documentation

Architecture changes require an ADR when they materially affect a documented architectural decision.

Keep setup and operational documentation synchronized with the actual repository.
Always update the root `README.md` in the same change when prerequisites,
local setup commands, environment configuration, Docker Compose behavior,
deployment URLs, health checks, or shutdown/data-reset steps change. The README
must remain a complete, reproducible local setup and deployment entry point;
keep it aligned with the detailed setup and deployment documents.

## AI Usage Contributions

Every agent-assisted contribution must be recorded in the acting team member's
`docs/ai-contribution/<GitHub-Username>-ai-usage.md` file. Follow
[`docs/project/ai-usage-log-template.md`](docs/project/ai-usage-log-template.md)
and the account mapping in
[`docs/project/ai-team-members.md`](docs/project/ai-team-members.md).

Before writing a record, identify the acting team member by GitHub username and
actual name. If either value is unknown, does not match the repository mapping,
or is otherwise ambiguous, ask the user to confirm it before changing the log.
Do not guess a team member's identity.

Each new record must include the date/time or time range, GitHub username, actual
team member name, agent name, tool/app, AI model, user-request summary,
agent-action summary, what AI output was accepted, changed or rejected, and
verification/evidence. Never record secrets, tokens, hidden model reasoning or
confidential personal data. Each student's assessed individual AI reflection
must be written by that student; agents may document the requirement but must
not author the reflection.

Do not edit earlier contribution records unless the user explicitly requests
that historical change. A request whose sole purpose is correcting, rewriting
or deleting an earlier AI-usage record must not itself be added as a new usage
record.
