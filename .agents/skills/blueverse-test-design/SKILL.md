---
name: blueverse-test-design
description: Design, add, or review BLUEVERSE automated tests, stable test IDs, fixtures, test matrices, runners, metrics, or CI discovery across web, mobile, backend, integration, and Agentic AI suites.
---

# BLUEVERSE test design

Read the root `AGENTS.md`, `.agents/rules/testing.md`,
`docs/testing/implementation-plan.md` and `docs/testing/test-matrix.md`.

1. Derive expected behavior from the requirement or contract independently of
   the implementation.
2. Place the case under the authoritative `test/` path and assign a stable ID
   with requirement traceability.
3. Cover normal, invalid, missing, malformed, boundary, extreme, duplicate,
   authorization, timeout/retry, concurrency, cancellation, dependency-failure,
   persistence and platform variations where applicable.
4. Assert complete observable behavior. For HTTP behavior, status alone is
   insufficient: assert schema/body, headers, authorization, persistence,
   audit/state transitions and allowed side effects.
5. Use deterministic synthetic fixtures and ensure the matching workflow
   discovers tests and reports complete failures and metrics.
6. For .NET test execution, read `.agents/skill-overlays/dotnet-test/run-tests.md`
   before using the portable `run-tests` skill. Use the other imported test
   quality skills only for their narrow stated purpose.

Treat existing tests as protected specifications. Obtain explicit user
permission before changing, deleting, skipping or weakening one. Preserve a
new requirement-based failing test as defect evidence and fix the implementation.
