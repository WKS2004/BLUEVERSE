# BLUEVERSE Test Case Rules

This directory contains the repository’s authoritative automated test cases.
Use the matching subdirectory from
[`docs/testing/implementation-plan.md`](../docs/testing/implementation-plan.md)
for each application, backend service, Agentic AI service or integration.

## Authoring checklist

For every relevant situation, add more than one condition as applicable:

- nominal passing behavior;
- invalid and rejected behavior;
- lower/upper boundaries and just-inside/just-outside values;
- empty, missing, null and malformed input;
- extreme, overflow, size and rate conditions;
- duplicate/idempotency, timeout, retry and cancellation behavior;
- concurrency/race behavior;
- dependency outage or partial failure;
- identity, permission and authorization variations;
- state-transition, configuration, platform and device variations.

Expected behavior comes from the requirement, acceptance criterion or contract,
not from copying the current implementation. A correct status code alone is
not enough; assert the complete behavior required by the case.

A newly authored test may fail against a wrong implementation. Keep the
requirement-based expectation and use the failure to locate the defect instead
of changing the test to match the current behavior.

Implement tests in the same change as the implementation they protect.

Existing tests are protected specifications. Ask the user for explicit
permission before changing, deleting, skipping or relaxing an existing test.
If both implementation and test are wrong, obtain approval, correct both, and
add regression/boundary cases that prevent the mistake from returning.
