# BLUEVERSE Test Case Implementation Plan

## 1. Purpose and current baseline

This document is the implementation plan for adding traceable automated test
cases across the complete BLUEVERSE repository. It complements
[`strategy.md`](strategy.md) and [`test-matrix.md`](test-matrix.md).

The plan is intentionally staged because the current checkout is a v0
foundation:

- `apps/web` is a Vite React client with an implemented cookie-based Auth
  session workflow, lint/build scripts and a dependency-free Node 24
  request-boundary test runner; no web component test runner exists yet.
- `apps/mobile` is a Flutter client with the Auth session workflow, mock
  public-API and in-memory credential-store boundary tests, plus the original
  starter widget test. A live device workflow test remains future work.
- `services/api` and `services/auth` are checked-in ASP.NET services with
  package-local test projects under their owning service directories.
- No executable Agentic AI service is present yet; the AI documentation defines
  the target boundary and safety contract only.
- Test workflows use each framework’s normal package/service test locations;
  no repository-root `test/` directory is used for authoritative cases.

The remaining phases describe future client/domain/Agentic AI coverage; the
current API/Auth foundation evidence is recorded in `test-matrix.md`. Future
domain workflow coverage is cross-platform by default: the same participating
roles must be exercised through React Web and Flutter Mobile, with test
differences limited to interaction context and device capabilities.

## 2. Default framework test locations

Authoritative test cases stay with the application or service that owns them.
The test location identifies the system under test, while the test file and
test name identify the layer and behavior. Do not create a repository-root
`test/` directory for these cases.

```text
apps/web/
├── src/
│   ├── **/*.test.ts(x)       unit/component/request-boundary tests
│   └── **/__tests__/         colocated React test suites
└── e2e/                      browser workflows when adopted

apps/mobile/
├── test/                     unit, widget and package-level Flutter tests
├── integration_test/         device/application integration workflows
└── test/fixtures/             mobile-only fixtures and builders

services/<service-name>/
└── tests/                    .NET test project and cases for that service
    ├── <service>.Tests.csproj
    ├── unit/
    ├── integration/
    ├── contract/
    ├── security/
    ├── migrations/
    └── fixtures/

services/ai/<agent-service>/
├── tests/                    pytest/unit/contract/safety/evaluation tests
│   ├── unit/
│   ├── integration/
│   ├── safety/
│   ├── evaluation/
│   └── fixtures/
└── pyproject.toml or requirements*.txt
```

A backend service may keep its `*.Tests.csproj` directly beside the service
source when that is its package convention, but it must remain inside the
owning `services/<service-name>` directory.

`services/ai-agents/<agent-service>/tests` and
`services/agents/<agent-service>/tests` are also supported if the selected AI
architecture uses either of those service roots. The test workflow discovers
all three approved AI roots. Backend integration tests belong in the owning
service’s `tests/integration` project; cross-platform client workflows belong
in the relevant client’s integration suite or the API service’s integration
project. Shared fixtures should be kept in the owning package and reused only
through explicit test helpers, not through a new root test tree.

Runner configuration and dependencies remain beside the code:

- React runner configuration and test dependencies remain in `apps/web`.
- Flutter uses `apps/mobile/test` and `apps/mobile/integration_test` with the
  existing app `pubspec.yaml`.
- .NET test projects reference only their owning service and approved test
  infrastructure under `services/<service-name>/tests`.
- Python AI tests reference the actual AI service package from its local
  `tests/` directory.

No client test may call an internal Auth or Agentic AI endpoint directly. Client
integration tests call the public API/gateway only.

Every new, generated or updated UI also updates
`docs/contracts/ui-integration.json`. The registry must connect the relevant
React and Flutter routes through one workflow ID and list each public API
endpoint used by that workflow. Run
`scripts/validation/validate_ui_integrations.py` before client tests; an
undeclared route or request target is a contract failure, not a test omission.

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

### 4.1 React web (`apps/web/src` and optional `apps/web/e2e`)

The current dependency-free Auth request-boundary suite uses Node 24's built-in
test runner and mocks `fetch`; it does not depend on a live backend. When UI
component and browser workflow coverage is introduced, use Vitest and React
Testing Library (with MSW or an equivalent request boundary) for deterministic
tests.

The current `/login` surface is the first product workflow. Implement cases
for it and later workflows covering:

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

The existing Auth surface is the first web product-test target and currently
has request-boundary cases for login, RFC 7807 error mapping and
everywhere-logout. Extend it with component and browser workflow cases before
using the generated home surface as quality evidence for future domain
workflows.

### 4.2 Flutter mobile (`apps/mobile/test` and `apps/mobile/integration_test`)

Use `flutter_test` for widget tests and Dart unit tests. Add `integration_test`
only for workflows that need the real application shell, navigation, platform
permissions or device behavior.

The current Auth surface already exercises the public gateway and platform
secure-storage boundary. Continue implementing cases for:

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

Use Flutter’s package-default `apps/mobile/test` location for unit/widget tests
and `apps/mobile/integration_test` for application/device workflows. Run both
locations when they contain tests; do not create a second test-only package or
an authoritative repository-root test tree.

### 4.3 Public API (`services/api/tests`)

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

### 4.4 Auth service (`services/auth/tests`)

The current default suite passes 67 deterministic cases and implements these
behaviors:

- valid and invalid registration/login;
- server-issued device installations, proof-key validation and cookie/native
  transport behavior;
- five-account-per-device capacity and account/device/everywhere logout scopes;
- five-session-per-account eviction with the oldest active session removed;
- profile-only updates with password changes isolated to their dedicated route;
- password hashing and verification without exposing passwords;
- account/credential failure handling and non-enumerating errors;
- JWT session claims, expiry, issuer/audience and signing configuration;
- rotating hashed refresh tokens, replacement links and consumed-token replay
  revocation;
- separation of `ActiveSessions` from `UserSessionLogs`, including refresh-token
  relinking when a session is archived;
- protected endpoint behavior;
- malformed JSON and field-level validation problem details;
- administrative mutation authorization and creation-payload validation;
- role and permission assignment/revocation;
- admin bootstrap validation using environment-provided configuration;
- rate limiting/lockout if selected for the final design;
- health, database failure and migration failure behavior.

The API foundation suite currently passes 21 cases. It covers health, public and
Auth OpenAPI routing, CORS allow/deny/preflight, forwarded headers, safe gateway
errors, YARP forwarding/failure isolation and JWT issuer/audience/lifetime,
algorithm and protected-cookie boundaries.

The PostgreSQL-backed capacity/refresh/archive smoke test is opt-in. The
remaining health/database-failure and deployment-specific migration cases are
environment-dependent follow-up coverage.

### 4.5 Every additional backend service (`services/<service-name>/tests`)

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

### 4.6 Agentic AI services (`services/ai/<agent-service>/tests`)

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
services/ai/orchestrator/tests/
services/ai/planner/tests/
services/ai/marine-climate/tests/
services/ai/marine-biodiversity/tests/
services/ai/safety-sustainability/tests/
```

If an AI capability is implemented inside the API rather than as a separate
service, its tests remain in `services/api/tests` for API orchestration behavior
and in the capability’s package-local `tests/` directory for AI-specific
evaluation behavior.

### 4.7 Shared integration and operational tests

Implement integration tests in the owning package/service’s default test
location for behavior that cannot be proven by one component alone:

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

- package-local READMEs with commands, naming rules and ownership rules;
- the first version of this plan’s matrix with case IDs;
- approved test-data policy and reusable factories/builders;
- runner/configuration decisions for React, Flutter, .NET and AI;
- CI failure behavior that fails when a discovered test suite is missing or
  cannot run, while still allowing future service or Agentic AI gaps to be
  reported honestly until those implementations are created.

Exit criteria: a contributor can locate a test by product area, service and
case ID, and can run each available suite locally.

### Phase 1 — React test harness and foundation cases

The first product-level Auth request-boundary cases now run through Node 24's
built-in test runner with JUnit output and complete assertion checks. Extend
this foundation with Vitest, React Testing Library, request mocking and
coverage reporting when component/workflow tests are introduced; do not rely
on the generated home surface as quality evidence.

Exit criteria: `lint`, build and web tests run in CI; failures produce readable
case IDs and JUnit artifacts. Coverage reporting remains part of the future
component/workflow harness.

### Phase 2 — Flutter test harness and foundation cases

Use the default Flutter test locations. Extend the existing Auth API-boundary
and widget fixtures with startup, persistence, device and workflow cases. Keep
`flutter analyze` mandatory.

Exit criteria: analyzer, unit/widget tests and any selected integration tests
run in CI on a supported Flutter channel.

### Phase 3 — API and Auth services

The API and Auth test projects are now present. Maintain HTTP, authorization,
validation, persistence, migration, security and health cases as the service
contracts grow.

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
  runs the React test cases from `apps/web/src` and optional `apps/web/e2e` and
  publishes metrics; include those paths when path filters are introduced.
- `mobile-ci.yml`: keep Flutter dependency resolution and analysis separate
  from `mobile-tests.yml`, which runs `apps/mobile/test` and
  `apps/mobile/integration_test` and reports metrics.
- `ui-integration.yml`: run the shared route/API contract validator and its
  deterministic validator tests for React, Flutter, service, gateway and
  contract changes.
- `web-ci.yml` and `mobile-ci.yml`: run the same shared validator whenever
  their client source changes, so a screen cannot pass its local build while
  using an undeclared route or backend endpoint.
- `backend-ci.yml`: keep source restore/build validation separate from
  `backend-tests.yml`, which discovers service-local test projects under
  `services/**/tests` and supported `*.Tests.csproj` layouts, maps them to
  services, runs all projects and reports aggregate/per-service metrics.
- `agentic-ai-tests.yml`: run all discovered Agentic AI suites in one workflow
  for `main`, `dev` and `agentic-ai/**` pushes or pull requests, with fast
  deterministic tests on pull requests and the full evaluation suite on the
  protected branch as the AI implementation matures.
- Extend `docker-stack-health.yml` or add an integration workflow to run the
  owning service/API integration smoke tests after Compose starts, including gateway routing,
  health, internal-network isolation and teardown.
- Update workflow path filters for package-local test directories, shared test
  configs and relevant documentation.
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
2. it is stored in the owning package/service’s default test directory;
3. it has deterministic fixtures and no secrets;
4. it asserts the externally observable result and relevant side effects;
5. it runs in the local command documented for its component;
6. it runs in the correct CI workflow;
7. failure output identifies the case and owning component;
8. the matrix and acceptance evidence are updated.

## 9. Initial implementation order

The recommended order for the repository is:

1. create the test registry, IDs, fixture policy and runner documentation;
2. maintain the React runner and extend the Auth session-workflow cases;
3. extend the Flutter test runner with Auth persistence/device workflow cases
   and retire reliance on the generated counter-only evidence;
4. expand API/Auth tests alongside new service behavior and domain workflows;
5. apply the same test template to every additional backend service;
6. implement AI service tests after actual AI boundaries and schemas are
   committed;
7. add shared cross-platform and Docker acceptance tests;
8. enforce coverage, safety and missing-suite gates in CI;
9. publish test and evaluation evidence with each release/assessment milestone.
