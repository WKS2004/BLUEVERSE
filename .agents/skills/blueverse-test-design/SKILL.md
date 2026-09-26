---
name: blueverse-test-design
description: Design, add, or review BLUEVERSE automated tests, stable test IDs, fixtures, test matrices, runners, metrics, or CI discovery across web, mobile, backend, integration, and Agentic AI suites.
---

# BLUEVERSE test design

Read the root `AGENTS.md`, `.agents/rules/testing.md` and
`.agents/rules/data-access.md` when a test touches persistence,
`docs/testing/implementation-plan.md` and `docs/testing/test-matrix.md`.

For route and endpoint tests, also read `docs/api/endpoint-catalog.md`.
**MUST** update `docs/api/endpoint-catalog.json` and regenerate its Markdown
view in the same change for production, internal, test-host-only or AI route
additions, updates, renames or removals. Keep production endpoints and
test-host-only fixture endpoints separated in the catalog, and run
`.agents/scripts/validate_endpoint_catalog.py` before completion whenever
route declarations or client API literals change.

1. Derive expected behavior from the requirement or contract independently of
   the implementation.
   Use the applicable owning component, agent, workflow and acceptance
   documents under `docs/`.
   For cross-client UI work, use
   `docs/contracts/ui-integration.json` as the route/API traceability
   contract, read `docs/project/ui-experience-principles.md`, and run
   `scripts/validation/validate_ui_integrations.py`.
2. Place the case under the owning package or service's framework-default test
   path and assign a stable ID with requirement traceability. Use
   `apps/web/src` (and optional `apps/web/e2e`), `apps/mobile/test` or
   `apps/mobile/integration_test`, `services/<service-name>/tests`, or the
   relevant AI service's local `tests/` directory.
3. Cover normal, invalid, missing, malformed, boundary, extreme, duplicate,
   authorization, timeout/retry, concurrency, cancellation, dependency-failure,
   persistence and platform variations where applicable.
4. Assert complete observable behavior. For HTTP behavior, status alone is
   insufficient: assert schema/body, headers, authorization, persistence,
   audit/state transitions and allowed side effects.
5. Use deterministic synthetic fixtures and ensure the matching workflow
   discovers tests and reports complete failures and metrics.
   UI request-boundary tests must call only the registered public gateway
   endpoint and route/workflow tests must cover both clients and every
   authorized role and business action. Differences between clients may
   reflect layout or device integrations while preserving business outcomes.
6. For .NET test execution, read `.agents/skill-overlays/dotnet-test/run-tests.md`
   before using the portable `run-tests` skill. Use the other imported test
   quality skills only for their narrow stated purpose.
7. For PostgreSQL/EF Core behavior, use `blueverse-postgresql-efcore` and
   distinguish provider-independent tests from real-provider migration,
   translation, constraint, transaction and concurrency tests. Do not add a
   Testcontainers dependency or replace existing fixtures without an approved
   implementation need.

Treat existing tests as protected specifications. Obtain explicit user
permission before changing, deleting, skipping or weakening one. Preserve a
new requirement-based failing test as defect evidence and fix the implementation.
