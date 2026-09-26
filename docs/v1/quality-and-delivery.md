# v1 quality and delivery evidence

This page organizes the acceptance requirements in
[PROJECT_REQUIREMENTS.md](../../PROJECT_REQUIREMENTS.md), sections 32–48 and
55. It is a target checklist, not a claim that the current foundation has
passed the listed checks. Use the
[component relationship map](component-relationships.md) to preserve
producer/consumer contracts and the separate
[member branch workflow](member-branch-workflow.md) for integration and G07.
Each owner uses their linked
[component work plan](README.md#component-and-agent-contract-map). Each member
uses one `features/<component>` branch and a complete PR to `dev`; actual
Agentic AI implementation is gated on all four business components passing
G07.

## Business and persistence

Each of the four components needs meaningful normalized PostgreSQL/EF Core
data, constraints, indexes, appropriate audit fields, migrations and
transactions where necessary. Each provides at least four meaningful public
API endpoints and a non-CRUD operation. The public ASP.NET Core boundary owns
validation, permission enforcement, business execution and structured errors.
Internal Auth, Agentic AI and ML services remain private.

The final domain schema also needs an ER diagram, suitable PostgreSQL types,
documented relationships and seed data where useful. Each owner must be able
to explain their tables, constraints, migration, transaction boundary and
business rule. Each member feature also provides the prepared public workflow
contract and private Agentic AI adapter boundary for its paired role, with
bounded server-side health/dispatch handling and an explicit not-connected or
unavailable outcome when the optional runtime is absent. See the
[integration boundary](agentic-ai-integration-boundary.md). API implementation evidence includes DTOs, application/service
logic, dependency injection, asynchronous I/O, structured logging, secure
configuration, CORS and OpenAPI. The public workflow contract must cover
initiation, status and execution summaries, reviewer decisions and resulting
state where relevant. Record exact routes in the
[endpoint catalog](../api/endpoint-catalog.md) when source exists.

## Client evidence

React and Flutter each implement every authorized v1 role and workflow through
the same public API. Verify form validation, loading, empty, success, denied,
malformed, timeout and dependency-failure states where relevant. Flutter
demonstrates GPS/location and a practical permission-denied alternative.
Verify cross-client date/time semantics for the planner and authorized
assessment image upload/review for operations, including invalid media,
permission denial and storage failure. See the
[device-capability contract](device-capabilities.md).
Reports and analytics use real application data and remain useful to the
person's task. Provide at least one meaningful data-driven report or analytics
view in each client, such as assessment outcomes, condition history, alert
statistics or workflow progress. Both views must reflect real server data;
the exact report and access permissions are implementation decisions.

## Agentic AI evidence

At least one complete golden assessment demonstrates the domain objective,
structured plan, all four distinct agents, delegation, allowlisted tool use,
typed inputs and outputs, durable state, deterministic validation, authorized
approval, audited execution and safe failure. Add deterministic cases for
prompt injection, malformed output, unauthorized or repeated decisions,
unavailable tools, retry exhaustion and dependency failure. An LLM judge may
support evaluation but cannot be its only basis.

Workflow evidence also records or exposes auditable step/tool-call identity,
start and end times or elapsed duration, validation outcomes, errors, retries
and approval decisions as needed. Before an Agentic AI implementation is
called ready, define measurable release gates for the golden case and required
negative/safe-failure cases in the owning evaluation contract. Report actual
fixture, live-model and end-to-end results separately.

## Other integration and delivery evidence

- Open-Meteo weather/marine integration retains source, timestamps, freshness,
  unavailable fields and safe failure behavior.
- Internal biodiversity inference can consume a genuine trained-model
  prediction when available and returns an explicit unavailable state when it
  cannot. Occurrence probability is never presented as guaranteed presence.
- Backend, real PostgreSQL, React, Flutter, cross-platform and Agentic AI
  tests run with traceable expected outcomes; passing compilation alone is
  insufficient.
- Performance evidence records concurrent load, response and database time,
  success/failure rate, third-party latency and Agentic AI latency from actual
  executed tests.
- The backend GitHub Actions workflow must restore, build and run automated
  backend tests on every push and pull request to `main`; the current
  [backend workflow](../../.github/workflows/backend-tests.yml) applies path
  filters, a gap that must be resolved before v1 completion.
- CI, documentation, ADR, ER diagram, individual contribution and AI-usage
  evidence, deployment, health and OpenAPI URLs, Android APK and evaluator
  startup instructions are completed before the v1 completion claim.
- GitHub evidence includes meaningful task branches, issues, pull requests,
  reviews, project-board tracking and attributable contributions throughout
  development. Do not fabricate activity or split commits merely to inflate
  an individual's record.

Deployment planning may use institution-provided or no-cost services. If a
required external service has a confirmed outage near evaluation, preserve
evidence and follow the assignment's evaluator notification procedure.

## Submission and evaluator access

The nominated group leader submits one consolidated PDF through Course Web
by 30 September 2026 at 11:50 PM. It contains the complete Group Report and
a clearly labelled Individual Report section for each member. Each member
section includes contribution, commit/PR/test evidence, challenges, an
individual AI-use log, a signed declaration and an approximately one-page
AI reflection written by that student. The Group Report includes the
consolidated AI-use declaration. The full content list is in
[requirements section 47](../../PROJECT_REQUIREMENTS.md#47-deployment-requirements).

Submit the repository and deployed system links, health and Swagger URLs,
PostgreSQL evidence, Agentic AI setup, required environment-variable names,
startup instructions, a runnable Android APK or approved equivalent, and
an accessible ten-minute demonstration-video link. Keep the repository,
video and deployed services available through at least 21 October 2026.
The group leader checks every submitted link in a private browser session
before submission. Evaluator test-account credentials are shared through
an approved secure channel, never committed to the repository.

Use the existing [test strategy](../testing/strategy.md),
[implementation plan](../testing/implementation-plan.md) and
[test matrix](../testing/test-matrix.md) for test placement and CI discovery.
Do not present a reserved endpoint or documented future service as working
software.
