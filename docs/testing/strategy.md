# Testing Strategy

The concrete implementation sequence, framework-default test layout, case-ID rules,
service-specific coverage and CI changes are defined in
[`implementation-plan.md`](implementation-plan.md).

Testing will cover the current foundation and the later business workflows:

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

## Current evidence

- React: the Node 24 built-in test runner covers three deterministic Auth
  request-boundary cases (request construction, RFC 7807 error normalization
  and 204 logout handling), while lint and the production build validate the
  implemented cookie-based Auth surface. Component and browser workflow tests
  remain a later increment.
- Flutter: Auth API-boundary/configuration tests, the Auth session UI and the
  static-analysis workflow are present alongside the starter widget test.
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

Agentic AI evaluation must include the complete assessed workflow and should not depend solely on an LLM judge.

## UI integration evidence

The UI integration registry is the first deterministic check for every new,
generated or updated client surface. It links one workflow ID to the React
route, Flutter route and public API endpoint references. The validator rejects
undeclared routes, undeclared API paths, direct internal-service targets and
`/api/v1`-style paths. This structural check is followed by request-boundary,
permission and workflow tests in the owning client locations; it does not
replace live API or gateway integration tests.
