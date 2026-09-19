---
name: blueverse-test-design
description: Design, add, or review BLUEVERSE automated tests, stable test IDs, fixtures, test matrices, runners, metrics, or CI discovery across web, mobile, backend, integration, and Agentic AI suites.
---

# BLUEVERSE test design

Read the root `AGENTS.md`, `.agents/rules/testing.md`,
`docs/testing/implementation-plan.md`, and `docs/testing/test-matrix.md`.

1. Derive expected behavior from the requirement or contract independently of
   the implementation.
2. Place the case under the authoritative `test/` path and assign a stable ID
   with requirement traceability.
3. Cover the normal path plus applicable invalid, missing, malformed, boundary,
   extreme, duplicate, authorization, timeout/retry, concurrency, cancellation,
   dependency-failure, persistence, and platform variations.
4. Assert the complete observable result. For HTTP behavior, status alone is
   insufficient; include schema/body, headers, state, audit/events, and allowed
   side effects.
5. Use deterministic synthetic fixtures and ensure the matching workflow
   discovers the test and reports complete failures and metrics.

Treat existing tests as protected specifications. Obtain explicit user
permission before changing, deleting, skipping, or weakening one. Preserve a
new requirement-based failing test as defect evidence and fix the implementation.
