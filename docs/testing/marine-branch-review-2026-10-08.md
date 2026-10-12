# Marine Safety branch review — 2026-10-08

## Scope and outcome

Reviewed `features/marine-safety` at HEAD `0f115d2`, including the current
uncommitted implementation, existing Marine business services, API/Auth boundary,
React/Flutter workflows, persistence/migrations, Docker/CI wiring and contracts.
The initial review found seven functional defects despite passing local suites.
The subsequent user-authorized repair on 2026-10-08 fixes all seven, with
requirement-based regressions. The finding descriptions below preserve the
original audit evidence; the resolution section records the current state.
Live PostgreSQL and cross-member deployment readiness remain unverified.

## Initial review findings (historical evidence)

### 1. P1 — Flutter changes the entered coastal coordinates

Evidence: `apps/mobile/lib/ui/marine/suitability_screen.dart:524` and
`apps/mobile/lib/ui/marine/history_screen.dart:546` apply `digitsOnly` to
latitude/longitude fields. Both widget reproductions show `-6` becomes `6` and
`6.025` becomes `6025`. Removing the minus sign can query the wrong hemisphere;
removing the decimal point can reject a valid coastal location or query another
location. The initial controller values bypass editing formatters, explaining
why default-location tests pass.

Correction: allow signed decimal input, validate finite WGS84 ranges at submit,
and add coordinate-editing widget cases on both forms.

### 2. P1 — Mobile timeouts can repeat writes that are still executing

Evidence: `apps/mobile/lib/data/services/marine_api_client.dart:309` times out
every operation after three seconds, then tries another gateway candidate at
line 321. `Future.timeout` does not cancel `_send`. The local reproduction
observed two DELETE sends and completion of the first request after fallback.
The same loop handles POST evaluate/profile create and PUT profile updates;
those operations can create duplicate assessments/drafts. Even a single configured
gateway can report failure while the original request succeeds. Auth/catalogue
calls have five-second budgets and provider calls have ten seconds per attempt,
so the mobile timeout is shorter than supported backend work.

Correction: align client/backend operation budgets; do not automatically retry
a dispatched mutation after an ambiguous timeout. Use server-enforced idempotency
before adding mutation retries. Keep safe discovery/fallback behavior distinct
from retrying a potentially completed write.

### 3. P1 — Cached conditions can belong to the preceding requested hour

Evidence: `services/marine-safety/Services/ConditionService.cs:94` and `:111`
select any recent snapshot up to requested time plus 15 minutes, then accept
an absolute forecast-time difference of up to 60 minutes. The isolated service
reproduction requested 12:00 and 13:00 at the same coordinates: the second
request reused the 12:00 snapshot, with only one provider call. This is outside
the provider adapter's requested-minus-30-minutes/plus-one-hour selection window
and contradicts the service comment that reuse represents the same requested hour.

Correction: use one documented requested-hour/window rule for acquisition and
cache reuse; query that window explicitly and cover adjacent-hour boundaries.

### 4. P2 — Profile validation ignores its stored decimal precision

Evidence: `services/marine-safety/Services/SafetyProfileService.cs:278` validates
unrounded limits; `services/marine-safety/Data/MarineSafetyDbContext.cs:43`
stores them at scale two. Domain creation accepted maximum wave height `1.004`
and caution height `1.003`, although both become `1.00` at the database scale.
That conflicts with the strict caution-below-maximum database constraint.
Other accepted limits can change after storage instead of matching the reviewed
input. The acceptance/precision mismatch was reproduced with EF InMemory;
the resulting PostgreSQL error path has not been executed against a live server.

Correction: normalize or reject excess precision before validating/storing the
limits, keep reviewed and returned values identical, and exercise the constraint
with real PostgreSQL boundary tests.

### 5. P2 — Flutter denies authorized safety-profile reads

Evidence: `apps/mobile/lib/ui/marine/marine_auth_navigation.dart:24` omits the
profile route for a caller with only `marine.profile.read`; `apps/mobile/lib/main.dart:186`
uses that list to deny the page. React registers the page with the read grant,
and the API allows profile GET with read alone. The local unit reproduction
confirmed the exclusion. This breaks equal authorized capability across clients.

Correction: allow the profile page with the read grant and gate only mutation
controls with manage; cover actual shell navigation as well as the child screen.

### 6. P2 — Shared actor-contract changes do not trigger Docker builds

Evidence: `.github/workflows/docker-backend-build.yml:6` and `:17` have push/PR
path filters for `services/**` and Docker files, but omit
`infrastructure/contracts/**`. The in-job branch check cannot compensate for a
workflow event that is never triggered. Both Dockerfiles compile that shared
source, so contract-only changes can bypass image validation. The current CI
helper tests check three other workflows and miss this trigger gap.

Correction: add the shared source path to both event filters and extend the
trigger-scope regression test. Stack-health scope already recognizes the path.

### 7. P2 — Extreme parsed timestamps cause an internal exception

Evidence: `services/marine-safety/Services/ConditionService.cs:94` adds 15 minutes
without guarding the date range, and `Providers/OpenMeteoClient.cs:43` adds/subtracts
one hour. The isolated service reproduction of `DateTime.MaxValue` threw
`ArgumentOutOfRangeException` before provider access. Public current/evaluate
inputs accept DateTime without a compatible boundary check; their handlers do
not translate this exception to an input error, leaving the global 500 handler.

Correction: validate a documented supported time range before arithmetic/provider
access, and cover earliest/latest representable dates with structured rejection.

## Additional integration risks and planned gaps

- Profile review uses a transaction and uniqueness checks, but lacks a concurrency
  token/row lock for the same draft. Two simultaneous reviewers could overwrite
  the reviewer attribution; review/deactivation can also race. This is a
  source-based concurrency risk requiring real PostgreSQL reproduction, not a
  demonstrated live database failure.
- This branch contains API, Auth and Marine service sources. Member 1's producer
  and the other two member services are not integrated here. The documented
  Member 1 consumer-route/schema mismatch still needs owner integration.
- Render's Blueprint remains the API/Auth foundation; Marine deployment is a
  documented future integration step. The Docker stack smoke workflow also
  assumes the Experiences OpenAPI route, absent from this standalone branch.
  This smoke-workflow assumption is resolved by the later dependency-isolation
  update below; the Render integration remains outstanding.
- Docker's daemon is not running. PostgreSQL upgrade/constraint/concurrency,
  live Member 1/provider deployment, physical-device tests and G07 acceptance
  remain unverified. No executable agents exist in this branch.

## Initial review checks

| Check | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Marine Safety suite | 145 | 0 | 0 |
| Public API suite | 47 | 0 | 0 |
| Auth suite (default provider-independent configuration) | 77 | 0 | 0 |
| React suite | 207 | 0 | 0 |
| Flutter suite | 86 | 0 | 0 |
| CI helper tests | 8 | 0 | 0 |
| UI validator tests | 35 | 0 | 0 |

Web lint/production build, Flutter analysis, endpoint catalog (51 public endpoints,
28 frontend routes), UI integration, foundation structure, agent resources
(23 skills), Compose configuration, migration/model consistency and diff checks
pass. Optional Auth PostgreSQL tests are excluded by their existing explicit
project configuration and were not enabled; the default-suite zero skipped count
does not imply PostgreSQL coverage.

The local-only reproduction harnesses confirm the observed defects; their
passing checks assert that those defects are present, and are not product
regression passes. Logs, harness sources and TRX evidence are retained under
`local/marine-verification-2026-10-08/`. Protected application tests were not
changed, disabled or relaxed during this review.

## Repair resolutions — 2026-10-08

| Finding | Implemented correction | Regression evidence |
|---|---|---|
| 1. Edited coordinates | Signed decimal keyboard/input, finite WGS84 parsing, correct negative fractional values, and explicit rejection of malformed nonblank history filters. | `MOB-MARINE-REPAIR-001..002`; submitted coordinate/query values and blocked invalid requests. |
| 2. Ambiguous write retries | 45-second mobile budget; POST/PUT/DELETE make no automatic second attempt after timeout/socket/client failure. UI reports an unconfirmed outcome and directs the user to verify stored state. GET fallback remains available. | `MOB-MARINE-REPAIR-005..008`; all five mutation methods, late original completion, transport errors, read fallback and four-second success. |
| 3. Cache time mismatch | Acquisition/cache share requested-minus-30-minutes through plus-one-hour inclusive. Cache requires recent nonfuture retrieval and prefers the earliest applicable forecast. | `M2-REPAIR-CACHE-001..003`; window edges, preceding hour, stale/future retrieval, ordering, provider count and immutable stored rows. |
| 4. Decimal precision | Reject any maximum/caution value not representable at two decimal places before dependency/persistence access; preserve exact input values. | `M2-REPAIR-PRECISION-001..002`; all six fields on create/update, unchanged approved history, exact accepted values and author audit. PostgreSQL runtime remains separate. |
| 5. Reader profile access | All Marine pages are available with the read grant; profile mutation controls still require manage. | `MOB-MARINE-REPAIR-003..004,009`; navigation grants, read-only page and actual application-shell navigation. |
| 6. Docker event coverage | Both Docker push and pull-request filters include `infrastructure/contracts/**`. | CI helper explicitly checks both event blocks; all prior wiring assertions are retained. |
| 7. Extreme time failures | Validate acquisition/evaluation UTC input before dependency access; both endpoints return structured 400. One hour of DateTime headroom keeps provider arithmetic safe. | `M2-REPAIR-TIME-001..003`; ProblemDetails, no provider/persistence side effects, one-tick boundaries and direct adapter transport. |

Backend regressions are in
[`BranchCorrectionTests.cs`](../../services/marine-safety/tests/Blueverse.MarineSafety.Tests/Integration/BranchCorrectionTests.cs)
and [`ProviderTimeBoundaryTests.cs`](../../services/marine-safety/tests/Blueverse.MarineSafety.Tests/Unit/ProviderTimeBoundaryTests.cs).
Mobile regressions are in
[`marine_correction_regression_test.dart`](../../apps/mobile/test/marine_correction_regression_test.dart).
The actual application accepts optional repository/service injection for the
shell regression; production construction still uses the existing adapters.

### Completed repair checks

| Check | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Marine Safety suite | 167 | 0 | 0 |
| Public API suite | 47 | 0 | 0 |
| Auth default provider-independent suite | 77 | 0 | 0 |
| React suite | 207 | 0 | 0 |
| Flutter suite | 100 | 0 | 0 |
| CI helper tests | 9 | 0 | 0 |
| UI/catalog validator tests | 35 | 0 | 0 |

Web lint/build, Flutter analysis, endpoint catalog (51 public endpoints,
28 frontend routes), UI registry validation, agent resources (23 skills), Compose
configuration, migration/model consistency, foundation structure and syntax
checks for five workflow YAML files / 21 Bash run blocks pass. Final logs/TRX are under
`local/marine-verification-2026-10-08/` with `*-repairs` names.
Initial sandbox attempts could not initialize Git Bash or write test temporary
files; the same unchanged checks passed outside that sandbox.

No database model change is required by these seven repairs. The original audit
reproductions are preserved as historical evidence and were not used as passing
acceptance tests. No existing assertions were removed or relaxed. Auth's optional
PostgreSQL source tests remain excluded by their existing project setting.
Docker's daemon is stopped, so live migrations, constraints, same-draft review
concurrency and member/device integration are still open. The additional
integration risks above are not represented as resolved by these local checks.
No executable agents, commit or push were created.

## Dependency isolation update — 2026-10-08

The subsequent user request requires Marine to withstand absent member services.
Readiness and stored records/deactivation stay independent; only operations
needing published activity data fail with structured unavailable metadata.
Provider failures use the same missing-data contract. No placeholder IDs, empty
success fallback, implicit suitability or automatic mutation replay is introduced.
Both clients retain records and recover dependent controls on catalogue retry.
Stack-health selects member OpenAPI documents by actual source presence,
resolving the absent-Experiences smoke assumption identified above.

Regression coverage includes full 503 bodies/headers, unchanged persistence and
audit state, cancellation/timeout, malformed configuration/data, own readiness,
valid empty response, outage recovery, gateway route isolation and both clients.
The current complete suites pass Marine **183/183**, API **54/54**, Auth default
**77/77**, React **210/210**, Flutter **103/103**, CI helpers **10/10** and
UI/catalog validator tests **35/35**, with zero failures/skipped cases. Web
lint/build, Flutter analysis, contract validators, migration/model consistency,
Compose configuration and workflow syntax pass. Logs/TRX are under
`local/marine-verification-2026-10-08/` with isolation names.

The original review/repair counts above remain historical evidence. Live
PostgreSQL constraints/migrations/concurrency, deployed member integration,
physical devices and G07 remain unverified; Docker's daemon is stopped.
See [the availability contract](../api/marine-dependency-availability.md).
