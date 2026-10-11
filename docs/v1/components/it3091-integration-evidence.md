# Coastal Planner IT3091 Integration Evidence

## Drafted provider contract

See [the unconfirmed IT3091 contract proposal](it3091-biodiversity-contract-proposal.md).
It proposes a private `POST /v1/predictions`, validated canonical location
identifiers, supplier provenance/version/time/limitations, a 3-second timeout,
one retry maximum and bounded failure handling. It records the existing
six-hour cache policy as unapproved and lists the IT3091 owner questions.
The synthetic fake reports provenance as `synthetic-it3091-fake` and cannot
represent a real model result.

The client-facing result is documented in
[the public biodiversity contract](biodiversity-public-contract.md). IT3091
details and G00 ratification remain pending. The opt-in .NET contract smoke
test can target a real service later through `IT3091_TEST_BASE_URL`,
`IT3091_TEST_API_KEY`, and optionally `IT3091_TEST_MODEL_VERSION`; those values
must remain in the caller's environment and must not be committed.

## Fake-service verification

- Coastal Planner .NET test project: **125 passed, 0 failed**. Includes the
  HTTP adapter against fake valid, timeout, 5xx, malformed JSON, wrong identity,
  out-of-range value, stale timestamp and unsupported-location behaviors;
  bearer credential isolation; retry count; circuit-breaker short-circuit;
  cache freshness/model-version checks; and the safety/eligibility invariant.
- React focused planner + biodiversity status tests: **37 passed, 0 failed**.
- Flutter biodiversity status model test: **8 passed, 0 failed**.
- Endpoint catalog, UI integration, and `git diff --check`: pass.

These results verify the adapter and clients against synthetic fixtures only;
they do not establish that the provider contract is accepted or that IT3091 is
reachable.

## Real IT3091 call result — fill after owner approval

- Approval / contract revision:
- Call date/time (UTC):
- Environment/endpoint identifier (no secret URL or credential):
- Outcome/status:
- Returned provider and model version:
- Timestamp/freshness result:
- Schema/provenance validation:
- Redacted correlation ID:
- Notes / follow-up:
