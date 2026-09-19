# BLUEVERSE Test Case Implementation Plan

## 1. Purpose and current baseline

This document is the implementation plan for adding traceable automated test
cases across the complete BLUEVERSE repository. It complements
[`strategy.md`](strategy.md) and [`test-matrix.md`](test-matrix.md).

The plan is intentionally staged because the current checkout is a v0
foundation:

- `apps/web` is a Vite React starter with lint/build scripts but no web test
  runner.
- `apps/mobile` is a generated Flutter counter application with one starter
  widget test.
- `services/api` and `services/auth` are reserved architecture locations but
  are not present yet.
- No executable Agentic AI service is present yet; the AI documentation defines
  the target boundary and safety contract only.
- Backend CI currently discovers tests below `services/`, so a centralized
  `test/services/` tree requires a CI discovery update.

This plan must not be interpreted as evidence that the missing services or
their tests already exist.

## 2. Test repository layout

All automated test cases will be stored under the repository-level `test/`
directory. The test location identifies the system under test, while the test
file and test name identify the layer and behavior.

```text
test/
├── README.md
├── app/
│   ├── web/
│   │   ├── unit/
│   │   ├── components/
│   │   ├── workflows/
│   │   ├── contract/
│   │   ├── fixtures/
│   │   └── e2e/
│   └── mobile/
│       ├── unit/
│       ├── widgets/
│       ├── workflows/
│       ├── contract/
│       ├── fixtures/
│       └── integration/
├── services/
│   ├── api/
│   │   ├── unit/
│   │   ├── integration/
│   │   ├── contract/
│   │   ├── security/
│   │   ├── fixtures/
│   │   └── migrations/
│   ├── auth/
│   │   ├── unit/
│   │   ├── integration/
│   │   ├── security/
│   │   └── fixtures/
│   └── <service-name>/
│       ├── unit/
│       ├── integration/
│       ├── contract/
│       ├── security/
│       └── fixtures/
├── ai/
│   ├── orchestrator/
│   ├── planner/
│   ├── marine-climate/
│   ├── marine-biodiversity/
│   ├── safety-sustainability/
│   ├── tools/
│   ├── safety/
│   ├── evaluation/
│   └── fixtures/
├── integration/
│   ├── api-client/
│   ├── cross-platform/
│   ├── agent-workflows/
│   └── docker/
└── shared/
    ├── contracts/
    ├── factories/
    ├── builders/
    └── test-data-policy.md
```

`<service-name>` is replaced by the exact directory name under `services/`.
The AI directory names are placeholders until the assessed AI services are
selected; each implemented AI service gets a matching directory under
`test/ai/`.

The test tree is organizationally centralized, but source-specific test
configuration remains close to the code:

- React runner configuration and test dependencies remain in `apps/web`.
- Flutter test-package configuration remains in `apps/mobile` or a dedicated
  test package, while test files remain in `test/app/mobile`.
- .NET test project files are placed under `test/services/<service-name>` and
  reference only their corresponding source project plus approved test
  infrastructure.
- Python AI test projects are placed under `test/ai/<specific-ai-service>` and
  reference only the AI service contract and implementation under test.

No client test may call an internal Auth or Agentic AI endpoint directly. Client
integration tests call the public API/gateway only.

## 3. Test case identification and evidence

Every test case receives a stable ID and a requirement reference. IDs are
written in the test name and in the test matrix, for example:

```text
WEB-UI-001       React permission-aware navigation
MOB-WF-001       Flutter field-report workflow
API-AUTH-001     API rejects an unauthenticated request
AUTH-PERM-001    Permission allow/deny behavior
SVC-<NAME>-001   Service-specific business rule
AI-SAFE-001      Prompt injection cannot change tool authorization
AI-WF-001        End-to-end agent workflow state transition
INT-XPLAT-001    React and Flutter use the same API contract
OPS-DOCKER-001   Compose gateway and health smoke test
```

Each case must document:

1. preconditions and controlled fixture data;
2. request or user action;
3. expected response/state/audit result;
4. authorization and failure expectations;
5. test level and owning component;
6. requirement, issue, or acceptance criterion covered.

Test design must enumerate the relevant situations before implementation is
considered complete. For each situation, include the nominal passing case and
the applicable rejection/error cases, lower and upper boundaries, values just
inside and outside the boundary, empty/missing and malformed input, extreme or
overflow values, duplicate/idempotency behavior, timeout/retry behavior,
concurrency and dependency failures. Add authorization, state, configuration
and platform variations where the feature supports them. Use data-driven or
property-based tests where they improve coverage without sacrificing
determinism.

Expected behavior must come from the requirement or contract independently of
the implementation. This prevents both the implementation and its tests from
repeating the same mistake. If a requirement says a valid range is `1–10` but
the implementation and test both use `100–1000`, the test and implementation
are both wrong and must be corrected together.

A test passes only when all assertions required by its logic pass. For HTTP
cases, matching the expected status is necessary when status is part of the
contract, but it is not sufficient: the response body/schema, headers,
authorization semantics, persistence, audit record, event, state transition
and allowed side effects must also match the case. The same principle applies
to UI, mobile, database and Agentic AI tests. CI metrics must use the test
runner’s complete assertion result rather than inferring success from one
field.

Test cases are implemented in the same change as the behavior they cover. An
agent must ask the user for explicit permission before changing, deleting,
skipping or relaxing an existing test, including when the existing test is
proven incorrect. Approved changes must record the reason and preserve or
replace the original risk coverage.

Test data must be synthetic and must not contain passwords, tokens, JWT
signing keys, production data, or hidden model reasoning. AI fixtures may store
structured prompts and outputs needed for evaluation, but not private chain of
thought.

## 4. Coverage by product area

### 4.1 React web (`test/app/web`)

Use Vitest and React Testing Library for deterministic unit, component and
workflow tests. Use MSW or an equivalent request boundary for API responses;
do not make unit tests depend on a live backend.

Implement cases for:

- rendering, loading, empty, success and error states;
- form validation, server validation errors and retry behavior;
- permission-based navigation and action visibility;
- authentication state, expiry, logout and protected-route behavior;
- API request construction, response mapping and error normalization;
- dashboards, reporting and approval workflows as they are introduced;
- accessibility-critical behavior: labels, keyboard operation, focus and
  visible status/error messages;
- prevention of direct calls to Auth or AI service URLs;
- browser-level smoke flows for login, one representative business workflow,
  approval and logout after the public API exists.

The initial starter counter test should be replaced with product behavior once
the first BLUEVERSE screen is implemented. Do not preserve generated demo tests
as the project’s quality evidence.

### 4.2 Flutter mobile (`test/app/mobile`)

Use `flutter_test` for widget tests and Dart unit tests. Add `integration_test`
only for workflows that need the real application shell, navigation, platform
permissions or device behavior.

Implement cases for:

- startup, routing, loading, offline, empty, success and error states;
- shared API contract behavior with the React client;
- authentication persistence, expiry and logout;
- permission-aware mobile actions;
- field workflows, draft/save/retry behavior and safe duplicate submission;
- GPS/location permission denial, unavailable location and valid location;
- camera/evidence capture permission denial, cancellation and successful
  attachment;
- notification handling where notifications are part of the delivery scope;
- responsive layouts and accessibility semantics for supported device sizes;
- prevention of direct calls to Auth or AI service URLs.

Because Flutter’s standard runner expects tests inside a Dart package, the
implementation phase must choose one of these explicit approaches before the
first mobile test is added:

- make `test/app/mobile` a small test-only package with a path dependency on
  `apps/mobile`; or
- keep a thin package-local runner under `apps/mobile/test` that imports the
  centralized cases.

The chosen approach must keep the authoritative cases in `test/app/mobile`,
be runnable in CI, and be documented in `test/README.md`.

### 4.3 Public API (`test/services/api`)

Use xUnit (or the repository-approved .NET test framework), WebApplicationFactory
for HTTP integration tests, and a controlled PostgreSQL integration fixture.
Keep unit tests independent of the database and network.

Implement cases for:

- route availability, HTTP methods, DTO serialization and Swagger contract;
- server-side validation, boundary values, malformed payloads and structured
  error responses;
- exact HTTP outcome assertions across every relevant status class: an
  intentional `1xx`, `2xx`, `3xx`, `4xx` or `5xx` response is passing when it
  matches the test expectation, but only when all other assertions also pass;
  any unexpected status or other contract/behavior mismatch fails the case;
  `401`, `403` and `404` are only illustrative examples;
- JWT authentication, expiry, malformed tokens and missing credentials;
- permission-based authorization for every protected endpoint, including both
  allow and deny cases;
- role-to-permission changes, including the rule that new roles begin with zero
  permissions;
- CORS, forwarded headers and gateway path behavior;
- service-to-service authentication and failure isolation;
- transaction boundaries, idempotency and concurrency-sensitive operations;
- audit fields, correlation IDs and structured logging behavior;
- health/readiness endpoints and safe behavior when dependencies are down;
- EF Core migrations, constraints, indexes, relationships and rollback-safe
  startup behavior;
- API calls to Agentic AI services only through the internal service boundary,
  never from the client.

### 4.4 Auth service (`test/services/auth`)

Implement cases for:

- valid and invalid login;
- password hashing and verification without exposing passwords;
- account/credential failure handling and non-enumerating errors;
- JWT claims, expiry, issuer/audience and signing configuration;
- refresh/revocation behavior if refresh tokens are implemented;
- protected endpoint behavior;
- role and permission assignment/revocation;
- admin bootstrap validation using environment-provided configuration;
- rate limiting/lockout if selected for the final design;
- health, database failure and migration failure behavior.

### 4.5 Every additional backend service (`test/services/<service-name>`)

When a service is added under `services/`, create its test project and matching
directory in the same change. The service test set must include:

- pure domain/business-rule unit tests;
- application/service-layer tests with mocked dependencies;
- HTTP integration tests for every endpoint;
- validation and structured-error tests;
- permission allow/deny tests using permission names, never hard-coded
  stakeholder role checks;
- PostgreSQL persistence and migration tests where the service owns data;
- outbound API/agent contract tests with deterministic stubs;
- timeout, retry, cancellation, duplicate request and dependency-failure
  cases;
- health/readiness and observability tests;
- security tests for unauthorized access, input abuse and data isolation.

A service is not complete when only its happy path is tested. Every endpoint
must have at least one valid, invalid, unauthenticated and unauthorized case,
unless the endpoint is explicitly public and the exception is documented.

### 4.6 Agentic AI services (`test/ai`)

AI tests must validate observable behavior, structured outputs and safety
properties. They must not assert hidden model reasoning or rely only on an
LLM-as-judge.

For the orchestrator and every specialized AI service, implement:

- input schema and output schema validation;
- deterministic planning/delegation decisions for controlled fixtures;
- authorized tool selection and rejection of unavailable tools;
- tool input validation, output validation, timeout and failure behavior;
- persisted workflow-state transitions and restart/resume behavior;
- deterministic business-rule validation after model output;
- approval required before high-impact execution;
- rejection of approval bypass, privilege escalation and unauthorized tool use;
- prompt-injection, indirect-instruction and untrusted-document cases;
- safe handling of malformed, incomplete, contradictory and low-confidence
  model outputs;
- retries, partial failure, cancellation, idempotency and recovery;
- audit record completeness without hidden reasoning;
- regression evaluation against a versioned fixture set.

The initial AI service test directories should follow the actual selected
service boundaries. The planned names are examples only:

```text
test/ai/orchestrator/
test/ai/planner/
test/ai/marine-climate/
test/ai/marine-biodiversity/
test/ai/safety-sustainability/
```

If an AI capability is implemented inside the API rather than as a separate
service, its tests remain in `test/services/api` for API orchestration behavior
and `test/ai/<capability>` for AI-specific evaluation behavior.

### 4.7 Shared integration and operational tests

Implement tests in `test/integration` for behavior that cannot be proven by one
component alone:

- React and Flutter call the same public API contract;
- gateway routing exposes `/api/...` without introducing `/api/v1`;
- clients cannot reach internal Auth or AI services;
- login → authorized request → business workflow → approval → updated status;
- API → PostgreSQL persistence and retrieval;
- API → AI recommendation → deterministic validation → human approval →
  execution/audit;
- failure recovery across service boundaries;
- Compose startup, health endpoints, gateway headers and dependency health;
- deployment smoke checks for the selected environment.

Use service stubs or deterministic test doubles for AI in ordinary API tests.
Reserve live-model evaluation for the explicit AI evaluation suite so CI remains
repeatable and safe.

## 5. Implementation phases

### Phase 0 — Test foundation and traceability

Deliver:

- `test/README.md` with commands, naming rules and ownership rules;
- the first version of this plan’s matrix with case IDs;
- approved test-data policy and reusable factories/builders;
- runner/configuration decisions for React, Flutter, .NET and AI;
- CI failure behavior that fails when a discovered test suite is missing or
  cannot run, while still allowing the current v0 service gap to be reported
  honestly until services are created.

Exit criteria: a contributor can locate a test by product area, service and
case ID, and can run each available suite locally.

### Phase 1 — React test harness and foundation cases

Add Vitest, React Testing Library, request mocking, coverage reporting and
scripts for unit/component/workflow tests. Add the first product-level cases
and remove reliance on the generated counter as quality evidence.

Exit criteria: `lint`, build and web tests run in CI; failures produce readable
case IDs and coverage artifacts.

### Phase 2 — Flutter test harness and foundation cases

Choose and document the centralized test-package approach. Add unit/widget
fixtures, API boundary mocks and the first startup/auth/workflow cases. Keep
`flutter analyze` mandatory.

Exit criteria: analyzer, unit/widget tests and any selected integration tests
run in CI on a supported Flutter channel.

### Phase 3 — API and Auth services

Create the service test projects when the corresponding source projects are
introduced. Add HTTP, authorization, validation, persistence, migration,
security and health cases before exposing the services to clients.

Exit criteria: API and Auth tests run against the same contracts used by React
and Flutter, and every protected route has allow/deny evidence.

### Phase 4 — Repeatable backend-service template

For each new backend service, copy the test structure only as a starting
template, then replace it with service-specific business cases. Add the service
to the registry, matrix, CI discovery and Compose/integration checks in the
same pull request.

Exit criteria: no service source directory can be merged without its matching
test directory, test project and minimum case set.

### Phase 5 — AI service tests and evaluation

Add pytest-based unit/contract/safety/evaluation suites for the actual AI
services. Version deterministic fixtures and define score thresholds for the
assessed workflow. Add explicit prompt-injection and approval-bypass cases.

Exit criteria: the complete workflow demonstrates planning, delegation, tools,
structured output, state, deterministic validation, approval, execution,
recovery and safe failure.

### Phase 6 — Cross-platform acceptance and operational tests

Add the end-to-end workflow through the gateway, database and internal AI
boundary. Add Compose health/routing/security smoke tests and deployment
smoke evidence.

Exit criteria: one representative workflow can be started from each client,
uses the same API contract, reaches the approval gate, and produces verifiable
final state and audit evidence.

## 6. CI and tooling changes required

The existing workflows must be extended as follows:

- `web-ci.yml`: keep lint/build validation separate from `web-tests.yml`, which
  runs the React test cases and publishes metrics; include `test/app/web/**` in
  the relevant test workflow scope when path filters are introduced.
- `mobile-ci.yml`: keep Flutter dependency resolution and analysis separate
  from `mobile-tests.yml`, which runs the centralized mobile test command and
  reports metrics.
- `backend-ci.yml`: keep source restore/build validation separate from
  `backend-tests.yml`, which discovers `test/services/**/*.csproj` and any
  service-local test projects, maps them to services, runs all projects and
  reports aggregate/per-service metrics.
- `agentic-ai-tests.yml`: run all discovered Agentic AI suites in one workflow,
  with fast deterministic tests on pull requests and the full evaluation suite
  on the protected branch/release workflow as the AI implementation matures.
- Extend `docker-stack-health.yml` or add an integration workflow to run
  `test/integration/docker` after Compose starts, including gateway routing,
  health, internal-network isolation and teardown.
- Update workflow path filters for `test/**`, shared fixtures, test configs and
  relevant documentation.
- Retain artifacts: test results, coverage, evaluation summaries and smoke-test
  logs. Do not upload secrets or private fixture data.

Use lockfiles and reproducible dependency installation. Test containers and
base images must follow the repository’s Docker Hardened Image and secret
handling rules; any test-only image choice must be documented rather than
silently replacing the selected runtime images.

## 7. Quality gates

Coverage is a signal, not a substitute for meaningful cases. The gates are:

- every changed behavior has a test case or a documented reason that a test is
  not applicable;
- every new behavior includes its test cases in the same change;
- every relevant scenario includes nominal, invalid, boundary and extreme
  conditions, with additional concurrency, dependency and platform cases when
  applicable;
- every protected endpoint has authentication and permission allow/deny cases;
- critical authorization, approval, deterministic validation and safety rules
  have complete decision-path coverage;
- unit and contract suites are deterministic and run on every pull request;
- integration and Compose suites run before merge for affected components;
- AI evaluation thresholds and safety regressions block release;
- coverage may not fall below the protected-branch baseline; target at least
  80% line coverage for stable domain/application code and higher coverage for
  security and safety-critical code;
- flaky tests are quarantined with an owner and expiry date, never silently
  ignored.

## 8. Definition of done for a test case

A case is complete only when:

1. the case has a stable ID and requirement reference;
2. it is stored in the correct `test/` subdirectory;
3. it has deterministic fixtures and no secrets;
4. it asserts the externally observable result and relevant side effects;
5. it runs in the local command documented for its component;
6. it runs in the correct CI workflow;
7. failure output identifies the case and owning component;
8. the matrix and acceptance evidence are updated.

## 9. Initial implementation order

The recommended order for the repository is:

1. create the test registry, IDs, fixture policy and runner documentation;
2. add the React runner and starter application cases;
3. formalize the Flutter centralized test runner and replace the generated
   counter-only evidence;
4. implement API/Auth tests alongside the missing service projects;
5. apply the same test template to every additional backend service;
6. implement AI service tests after actual AI boundaries and schemas are
   committed;
7. add shared cross-platform and Docker acceptance tests;
8. enforce coverage, safety and missing-suite gates in CI;
9. publish test and evaluation evidence with each release/assessment milestone.
