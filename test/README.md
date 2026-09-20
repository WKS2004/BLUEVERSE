# BLUEVERSE Test Case Rules

This directory contains the repository’s authoritative automated test cases.
Use the matching subdirectory from
[`docs/testing/implementation-plan.md`](../docs/testing/implementation-plan.md)
for each application, backend service, Agentic AI service or integration.

## Current checkout

The centralized application and service test packages are not implemented yet.
The checked-in Flutter counter test remains under `apps/mobile/test` as
generated package-local starter content; it is not the project’s final quality
evidence. React currently has lint/build scripts but no test runner, and the
backend and Agentic AI test trees remain reserved until their implementations
are added. New authoritative cases belong under the paths in the testing
implementation plan and must be discovered by the matching workflow.

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
