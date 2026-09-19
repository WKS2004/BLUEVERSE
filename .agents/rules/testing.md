# Testing rules

The detailed matrix and implementation order live in
[`docs/testing/implementation-plan.md`](../../docs/testing/implementation-plan.md)
and [`docs/testing/test-matrix.md`](../../docs/testing/test-matrix.md). This
file is the fast mandatory checklist.

## Placement and traceability

- Keep authoritative tests under `test/`: `app/web`, `app/mobile`,
  `services/api`, `services/auth`, `services/<service>`, `ai/<ai-service>` and
  `integration` as applicable. Do not scatter service tests or create sample
  applications.
- Give every case a stable ID and requirement/acceptance reference. A case
  records preconditions, action, expected observable result and failure path.
- Add/update tests in the same change as the behavior. New services bring their
  test runner, minimum suite and CI discovery in the same change.

## Required quality

- Cover nominal, invalid, empty/missing, malformed, lower/upper boundary and
  extreme values where relevant; add duplicate/idempotency, timeout/retry,
  cancellation, concurrency, dependency-failure, configuration and platform
  cases when the behavior supports them.
- Derive expectations from requirements/contracts, not the implementation.
  Preserve tests that expose defects; do not change, delete, skip or weaken an
  existing test without explicit user permission. If both code and test are
  wrong, obtain approval, correct both and restore the missing boundary cases.
- API tests assert exact status, response schema/body, headers, authorization,
  persistence, audit/events and permitted side effects—not status alone.
- Test permission names and role-to-permission behavior, migrations,
  constraints, indexes and transactions for data-owning services.
- Clients test only the public API/gateway. React covers component states,
  request boundaries, permissions, accessibility and workflows; Flutter covers
  analysis, unit/widget, API-boundary and mobile workflow behavior.
- Agentic AI tests use deterministic fixtures for schemas, tool authorization,
  output validation, approvals, prompt injection, recovery and safe failure;
  never assert or persist hidden reasoning and never rely only on an LLM judge.

## CI and determinism

- Use synthetic data, stable clocks/IDs and test doubles for ordinary AI and
  dependency tests. Keep live-model/evaluation checks explicit.
- Ensure the matching workflow discovers every new test and reports complete
  assertion outcomes plus a `FAILED TEST CASES` list. Backend and Agentic AI
  workflows must retain aggregate and per-service evidence.
- A compile/lint pass or happy-path smoke test is not sufficient evidence.
