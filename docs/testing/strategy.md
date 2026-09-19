# Testing Strategy

The concrete implementation sequence, centralized test layout, case-ID rules,
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
- Agentic AI workflow evaluation
- deployment smoke tests

## Current evidence

- React: lint and production build scripts are configured; no component test
  runner is checked in yet.
- Flutter: the generated counter widget test and static-analysis workflow are
  present.
- ASP.NET/API: `services/api` is checked in and builds, but no API test project
  is checked in yet. The Auth service and its tests remain pending.
- Docker: web/backend image and Compose health workflows are configured. The
  API image can be built, while the complete backend/Compose workflow remains
  blocked by the missing Auth service.

Critical business rules should have deterministic tests.

Test implementation is part of the same change as the behavior implementation.
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
