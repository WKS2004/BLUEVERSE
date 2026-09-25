# Shared contracts, tests and CI

## Contract sources

The [UI integration registry](../../contracts/ui-integration.json) maps a
stable workflow ID to both client routes and its public API references.
It currently includes the shared home entry and the implemented
`auth-session-management` workflow. The
[endpoint catalog JSON](../../api/endpoint-catalog.json) is the
machine-checked route inventory; its
[Markdown view](../../api/endpoint-catalog.md) is generated for readers.
Implementation source remains authoritative for actual behavior.

Any frontend route, client API call, backend endpoint, gateway mapping,
health/OpenAPI route or AI endpoint change updates the catalog source and
regenerates its Markdown view. A changed UI workflow also updates the
shared registry. Both clients must point to the same public boundary and
support every authorized role/action. The validators reject unregistered
routes, direct internal targets, unverifiable dynamic hosts and
`/api/v1`-style paths.

## Test ownership

React tests live with `apps/web`, Flutter tests with
`apps/mobile`, and service tests with the owning package under
`services/<service>/tests`. Cases need stable IDs and requirement-based
assertions. The API and Auth suites cover their public contracts and
failure boundaries; client tests cover request construction and behavior
through the gateway. PostgreSQL-specific behavior needs real-provider
evidence. See the [test plan](../../testing/implementation-plan.md) and
[test matrix](../../testing/test-matrix.md).

## CI and local checks

GitHub Actions separates repository, UI contract, web, mobile, backend,
Agentic AI and Docker checks. Discovery, path filters, complete result
metrics, failed-case lists and artifacts are documented in the
[CI guide](../../development/ci.md). For a documentation or guidance change,
use link review and `git diff --check`. For source changes, run the
focused client/service suites and the affected contract validators. The
[UI integration guide](../../development/ui-integration.md) gives the
sequence and commands. A passing build or HTTP status alone does not prove
the workflow contract.
