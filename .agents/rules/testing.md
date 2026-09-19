# Testing Rules

Follow [`docs/testing/implementation-plan.md`](../../docs/testing/implementation-plan.md)
for the complete test-case matrix, implementation phases and CI requirements.
These rules are the minimum agent checklist and apply to every implementation
change in this repository.

## Test location and traceability

- Store automated test cases in the repository-level `test/` tree:
  - React: `test/app/web`.
  - Flutter: `test/app/mobile`.
  - Public API: `test/services/api`.
  - Auth: `test/services/auth`.
  - Every additional backend service: `test/services/<exact-service-name>`.
  - Agentic AI: `test/ai/<specific-ai-service-name>`.
  - Cross-component and operational tests: `test/integration`.
- Do not create replacement sample applications or scatter service test cases
  in unrelated directories.
- Give every case a stable ID in the test name and matrix, such as
  `WEB-UI-001`, `MOB-WF-001`, `API-AUTH-001`, `SVC-<NAME>-001`, `AI-SAFE-001`
  or `INT-XPLAT-001`.
- Each case must identify its preconditions, action, expected result, failure
  behavior and requirement/acceptance reference.
- When adding a service under `services/`, add its matching test directory,
  test project/runner and minimum test set in the same change.
- Implement or update the relevant test cases in the same change as the
  behavior they verify. Do not postpone test creation until a later task.

## Required coverage

- Add tests for every meaningful behavior change; do not claim completion from
  compilation, linting or a happy-path test alone.
- For every relevant situation, design multiple conditions: nominal passing
  behavior, invalid/rejected behavior, lower and upper boundaries, just-inside
  and just-outside values, empty/missing values, malformed values, extreme or
  overflow values, duplicates/idempotency, timeout/retry, concurrency and
  dependency-failure behavior where applicable.
- Include authorization/identity variations, state-transition variations,
  configuration variations and platform/device variations when the behavior
  supports them. Use property-based, fuzz or data-driven tests where they
  reduce blind spots without making the suite nondeterministic.
- Derive expected values and rules from the requirement, acceptance criterion
  or API contract—not from the current implementation. A test must be able to
  expose an implementation that is consistently wrong in the same direction.
- A newly authored test is allowed to fail against an incorrect implementation;
  do not change its expected value to match the current behavior. Fix the
  implementation after confirming the requirement and use the failure as the
  defect evidence.
- During test authoring, actively look for missing scenarios and likely defects
  even when the implementation appears to work. Test completeness is part of
  implementation completeness.
- Treat an existing failing test as evidence of an implementation, runtime,
  validation or environment defect. Fix the service, client, workflow or
  configuration under test; do not weaken, delete, skip or rewrite the test
  merely to make the workflow pass.
- Ask the user for explicit permission before changing, deleting, skipping or
  relaxing an existing test. This permission is required when a reviewed
  requirement/contract changes and when the test itself is proven incorrect.
  Record the approval and reason, preserve the original risk coverage, and
  add replacement cases before removing the old protection.
- If both implementation and test are wrong, correct both only after user
  approval. For example, if the requirement is `1–10` but code and tests both
  use `100–1000`, change the implementation and tests to `1–10`, then add
  boundary cases for `0`, `1`, `2`, `9`, `10` and `11` as applicable.
- For every endpoint, cover valid input, invalid input, authentication and
  authorization. Document exceptions for intentionally public endpoints.
- Judge HTTP tests by the asserted expected result, not by whether the status
  code looks positive or negative. Any relevant HTTP status from the `1xx`,
  `2xx`, `3xx`, `4xx` or `5xx` classes is a valid passing outcome when the
  test explicitly expects it. A test fails when the observed status differs
  from the intended status—for example, receiving a success status for a
  request that should be rejected, or receiving an error status for a request
  that should succeed. `401`, `403` and `404` are examples, not an exhaustive
  list.
- API tests must assert the exact expected status code and relevant response
  body/schema. An exact status match alone is not a passing result: also assert
  every behavior required by the test, including headers, authorization
  semantics, persisted state, audit records, events and permitted side
  effects. The workflow failure list should identify the test when any
  assertion reports a mismatch.
- For every test type, define pass/fail from the complete test logic. A test
  must fail when any required assertion fails, even if another assertion such
  as the HTTP status is correct. Metrics must come from the test runner’s full
  assertion result, not from status-code counting or a superficial smoke
  check.
- Test permission names and role-to-permission behavior; never replace the
  permission model with hard-coded role checks.
- Cover validation boundaries, structured errors, dependency failures,
  cancellation, retry/idempotency behavior and health/readiness behavior where
  applicable.
- Test PostgreSQL migrations, constraints, indexes, persistence and
  transaction behavior for services that own data.
- React tests must include component states, API request boundaries,
  permission-aware UI, accessibility-critical behavior and representative
  workflows. Use deterministic request mocks for unit/component tests.
- Flutter tests must include analysis, unit/widget behavior, API boundary
  behavior and relevant mobile workflows. Keep authoritative cases in
  `test/app/mobile` and document the package runner used by CI.
- Client tests may call only the public ASP.NET Core API/gateway. They must not
  call internal Auth or Agentic AI services directly.

## Agentic AI safety and evaluation

- Treat every model output, tool result, prompt and external document as
  untrusted input.
- Test schemas, planning/delegation, authorized tool selection, tool input and
  output validation, state transitions, retries, recovery and safe failure.
- Include prompt-injection, indirect-instruction, approval-bypass and
  privilege-escalation cases.
- High-impact actions must prove deterministic validation and human approval
  before execution.
- Assert observable structured outputs, state and audit records; never assert or
  persist hidden model reasoning.
- AI evaluation must include deterministic fixtures and must not rely only on
  an LLM judge.

## Determinism, data and CI

- Prefer isolated, deterministic tests for critical business, authorization,
  approval and safety rules. Use stubs/test doubles for AI in ordinary service
  tests; reserve live-model checks for the explicit evaluation suite.
- Use synthetic fixtures only. Never commit passwords, tokens, JWT signing
  keys, API keys, production data or private user data.
- Update the relevant test matrix and documentation when introducing a new
  workflow or test category.
- Ensure the correct CI workflow discovers and runs the new tests. Update path
  filters and test discovery when adding files under `test/`.
- Keep test execution separated by main scenario: `web-tests.yml` for React,
  `mobile-tests.yml` for Flutter, `backend-tests.yml` for all backend services
  together, and `agentic-ai-tests.yml` for all Agentic AI suites together.
- Backend and Agentic AI workflows must run every discovered service, continue
  after an individual service failure, and report both aggregate and
  per-service metrics in the job log and GitHub step summary.
- Every dedicated test workflow must finish its test output with a `FAILED
  TEST CASES` list. Include the test identifier and, for backend/Agentic AI
  cases, the owning service so runtime and validation failures can be located
  without searching the entire log.
- Preserve test results, coverage and AI evaluation evidence without uploading
  secrets or private fixture data.
- Do not silently quarantine flaky tests. Assign an owner and expiry date, and
  document the reason and follow-up issue.
- Before reporting completion, run the applicable local checks and report what
  was run, what passed, and what remains blocked by missing services or
  environment dependencies.
