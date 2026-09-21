# Testing rules

The detailed matrix and implementation order live in
[`docs/testing/implementation-plan.md`](../../docs/testing/implementation-plan.md)
and [`docs/testing/test-matrix.md`](../../docs/testing/test-matrix.md). This
file is the fast mandatory checklist.

## Placement and traceability

- Keep authoritative tests in the owning package’s default test location:
  React under `apps/web/src` (and optional `apps/web/e2e`), Flutter under
  `apps/mobile/test` and `apps/mobile/integration_test`, backend services under
  `services/<service>/tests`, and AI agents under their package-local
  `tests/` directory, normally `services/ai/<agent>/tests`. Do not create a
  repository-root `test/` tree, scatter tests outside their owner or create
  sample applications.
- Give every case a stable ID and requirement/acceptance reference. A case
  records preconditions, action, expected observable result and failure path.
- Add/update tests in the same change as the behavior. New services bring their
  package-local test runner, minimum suite and CI discovery in the same change.

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
- Every client workflow test must trace to the shared UI integration registry:
  React and Flutter surfaces use the same workflow ID and endpoint references.
  Route/API contract validation runs even while the current starter has zero
  domain endpoints.
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
- Run `scripts/validation/validate_ui_integrations.py` for every UI, API,
  gateway or integration-contract change. It must reject undeclared frontend
  routes, undeclared public `/api/...` calls and direct internal targets.
- For every route or endpoint addition, update, rename, move or removal, update
  `docs/api/endpoint-catalog.json`, regenerate its Markdown view and run
  `.agents/scripts/validate_endpoint_catalog.py` in the same change. It
  verifies source/catalog parity, gateway mappings, client API literals, UI
  references and the separation of test-only routes from production routes.
  A route change is not complete while this validator fails.
