# Testing Strategy

The concrete implementation sequence, framework-default test layout, case-ID rules,
service-specific coverage and CI changes are defined in
[`implementation-plan.md`](implementation-plan.md).

Testing covers the current foundation and the four defined
[v1 business components and agents](../v1/README.md) as they are implemented:

- ASP.NET Core unit tests
- API/integration tests
- authorization tests
- validation/business-rule tests
- React component/integration tests
- Flutter tests and static analysis
- database/migration verification
- cross-platform API integration
- cross-client route/API contract validation through
  `docs/contracts/ui-integration.json`
- Agentic AI workflow evaluation in each AI service’s local `tests/` directory
- deployment smoke tests

The React and Flutter suites must cover every permitted role and business
action in the same v1 workflows. Test differences may reflect input or
screen context, never reduced capability coverage. UI behavior should also
be reviewed
against [`../project/ui-experience-principles.md`](../project/ui-experience-principles.md)
so passing technical request tests does not hide an unrealistic or confusing
user experience.

Database-provider decisions are defined by
[`../../.agents/rules/data-access.md`](../../.agents/rules/data-access.md):
provider-independent tests may use the existing isolated test provider, while
PostgreSQL translation, migrations, constraints, indexes, transactions,
locking, concurrency and persistence behavior require real PostgreSQL
evidence.

## Current evidence

- React: Node 24 `node:test` covers public Auth/Admin request contracts, saved
  account/session workflows, form boundaries, profile and security actions,
  role/user administration, protected and recovery routes, navigation,
  loading feedback, route scrolling, app notices and the shared footer. DOM
  tests use JSDOM and React Testing Library; request tests stub the registered
  public API. The current baseline passes 157 cases (2026-09-25). Lint,
  production build and shared UI integration validation are separate gates.
  Real-browser and deployed-gateway workflows remain separate integration
  evidence.
- Flutter: 86 selected package tests pass across 12 runnable files, covering the
  API boundary, credential/session state, onboarding and Auth forms,
  dashboard/profile/security/session UI,
  permission-aware role/user administration, loading/error screens and gateway
  configuration. `flutter analyze --no-pub lib` passes. The run excluded the
  pre-existing modified `apps/mobile/test/widget_test.dart`, which still
  references the removed `MyHomePage`; migration awaits the user approval
  required for existing tests. `apps/mobile/test/auth_api_service_test.dart`
  also contains duplicate labels for `MOB-AUTH-011`, `MOB-AUTH-012` and
  `MOB-AUTH-013`; existing test names remain unchanged pending approval. No
  `integration_test` directory is checked in.
- ASP.NET/API: `services/api` and its foundation test project are checked in;
  the API suite currently passes 21 cases covering health, OpenAPI/Swagger,
  CORS, forwarded headers, safe gateway errors, YARP forwarding/failure
  isolation and JWT issuer/audience/lifetime/algorithm/cookie boundaries.
- Auth: `services/auth` has a package-local test project with 67 passing
  default test cases covering endpoint behavior, Auth OpenAPI/health and safe
  exception contracts, malformed-input validation, administration and
  administrative mutation authorization, escalation, server-issued
  installations and proof failures, five-account device capacity, five-session
  account eviction, account/device/everywhere logout including cookie cleanup,
  active-session archival and refresh-token relinking, rotating refresh-token
  replay/expiry protection, logout/password token revocation, profile-only
  updates, session metadata isolation, bootstrap seeding, password hashing,
  JWT claims/configuration boundaries, opaque-secret properties and
  persistence-model constraints. The PostgreSQL session/concurrency smoke test
  is opt-in.
- Docker: web/backend image and Compose health workflows are configured. Full
  runtime evidence still depends on Docker Desktop/DHI access and PostgreSQL.

Critical business rules should have deterministic tests.

Test implementation is part of the same change as the behavior implementation,
and tests stay in the default test directory of the owning package/service.
Each relevant situation should be exercised with nominal passing, invalid,
boundary, just-inside/just-outside, empty/missing, malformed and extreme
conditions, plus duplicate, concurrency, timeout/retry and dependency-failure
conditions where applicable. Expected outcomes must be derived independently
from requirements or contracts so a wrong implementation cannot make its own
tests pass by copying the same wrong rule.

Test results are determined by the complete test logic. For API tests, the
expected HTTP status is only one assertion; matching it does not pass the case
if the response body/schema, headers, authorization behavior, persistence,
audit data, state transition or side effects are incorrect. This same rule
applies to every client, service and Agentic AI test.

Existing tests are protected specifications. Agents must ask the user for
explicit permission before changing, deleting, skipping or relaxing one. If
both the implementation and test are wrong, correct them together only after
approval and add the missing boundary/regression cases.

Agentic AI evaluation must include the complete assessed operational
workflow, all four distinct agents, deterministic validation, authorized
approval and safe failure. It must not depend solely on an LLM judge. The
tourist recommendation path also needs deterministic constraint evidence.

## UI integration evidence

The UI integration registry is the first deterministic check for every new,
generated or updated client surface. It links one workflow ID to the React
route, Flutter route and public API endpoint references. The validator rejects
undeclared routes, undeclared API paths, direct internal-service targets and
`/api/v1`-style paths. This structural check is followed by request-boundary,
permission and workflow tests in the owning client locations; it does not
replace live API or gateway integration tests.
