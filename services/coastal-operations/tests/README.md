# Coastal Operations tests

Run the service suite from the repository root:

```powershell
dotnet test services/coastal-operations/tests/Blueverse.CoastalOperations.Tests/Blueverse.CoastalOperations.Tests.csproj
```

The suite uses stable `COASTAL-*` test IDs and covers assessment input,
draft create/update/cancel/submit, idempotency, queue/detail/tombstone
visibility, the pre-G07 disconnected AI state, reviewer decisions,
target-state changes/history, alert draft withdrawal/lifecycle and authorization,
signed API actor-context validation/replay, and dynamic controller permission
policy coverage. Optional component calls use synthetic HTTP handlers to verify
request contracts, timeouts, retryable and non-retryable HTTP statuses,
malformed/stale/oversized responses, cancellation, and dependency-health
snapshots without live peer services. Configuration is bounded by the
provisional G00 limits of a two-second attempt timeout, two retries and a
32 KiB response body. Assessment draft creation is local and does not probe
peer or AI services. Submission records bounded peer outcomes; peer failures
remain explicit and do not make the service unavailable or produce a proposal
before G07.

`PublicationAndSearchTests` adds `COASTAL-PUBLICATION-001` through `007`,
`COASTAL-SEARCH-001` through `003`, `COASTAL-AUDIT-001` and
`COASTAL-PERMISSION-001`. It verifies publication replay/full payloads,
disconnected/accepted/invalid delivery, finite retries, uncooperative-call
timeouts, lease recovery/cancellation, draft ownership, filtered pagination,
public alert visibility/history, audit cursors and distinct publish/resolve
grants. These are controlled service/transport checks, not production agents
or live PostgreSQL concurrency evidence. The existing PostgreSQL submission
fixture now removes its own dispatch row before deleting its assessment.

`RecordExperienceTests` adds `COASTAL-RECORD-001` through `006`,
`COASTAL-TIMEZONE-001`/`002` and `COASTAL-OPTIONS-001`: titles/duplicates,
legacy digest compatibility, immutable titled publication context, own-draft discovery and title-only filters,
unlinked publication rejection, 419 seeded IANA choices, scoped association
availability, fractional offsets and daylight-saving/invalid local periods.
These deterministic/model tests do not prove live migration or PostgreSQL
query translation. Existing client expectation changes were explicitly approved.

Contract tests also verify the DTO data-annotation boundaries used by MVC model
validation, and reflection-based route checks ensure every protected controller
action has a registered permission policy.

Evidence coverage includes PNG structure and decompression boundaries, exact
and over-limit uploads, metadata removal, assessment ownership/version/audit,
storage cleanup, private reads, digest integrity, retention retries, filesystem
permissions, and hosted alert expiration. The tests do not require Docker,
credentials, or live peer services.

Most service and workflow tests use EF Core InMemory. The persistence tests
inspect the PostgreSQL relational model and migration discovery without opening
a database, and readiness is tested with an unavailable local database port.
These checks do not prove PostgreSQL migrations, transaction behavior,
constraints, raw SQL bootstrap, or provider-specific concurrency. The local
Compose PostgreSQL migration/readiness smoke path has separate runtime evidence;
run it again when changing migrations or provider-specific persistence behavior.
Synthetic dependency tests do not prove the other member services' final G00
contracts or live endpoint availability.

Six PostgreSQL workflow/concurrency cases are opt-in. Provision a dedicated
database whose name starts with `blueverse_co_test`, set
`BLUEVERSE_CO_POSTGRES_TEST_CONNECTION` to its connection string, and run:

```powershell
dotnet test services/coastal-operations/tests/Blueverse.CoastalOperations.Tests/Blueverse.CoastalOperations.Tests.csproj --filter "FullyQualifiedName~PostgresWorkflowIntegrationTests"
```

The opt-in cases apply pending migrations and create/clean up only their own
synthetic records. They are skipped when the variable is absent, and refuse a
database whose name does not have the required test prefix. Do not point them at
a development or production database.

## Logs and draft evidence regressions — 2026-10-02

`OperationsLogsTests` covers complete retained-state scopes, title/type/ID
filtering, cursor paging, malformed queries, read/audit policies, bodies and
private response headers. `COASTAL-EVIDENCE-026`–`030` assert removals, duplicate
and stale requests, published-state denials, attachment replacement, retained
audit linkage, no removed content reads and cleanup/commit failure behavior.
Publication tests also exclude removed evidence from the immutable envelope.
The approved older upload fixtures now start in DRAFT: the previous SUBMITTED
fixture contradicted publication immutability. Existing unrelated assertions
are retained. `COASTAL-POSTGRES-004`/`005` add real-provider migration/removal,
query translation and publication/removal race checks. They use the same guarded
dedicated `blueverse_co_test*` database as the existing opt-in fixture; doubles
cannot establish PostgreSQL constraints or transaction behavior.

## Detailed activity and signed identity — 2026-10-02

`COASTAL-AUDIT-DETAIL-001`–`003` cover exact before/after values, identity match,
immutable previous events, system attribution, no sensitive provider fields,
atomic save failure and signed snapshot tamper/format/bounds rejection.
`COASTAL-AUTH-CONTEXT-009` checks signed display claims without changing permissions.
`COASTAL-POSTGRES-006` adds migration/JSONB round-trip and array-constraint
checks using the existing dedicated database guard and fixture cleanup.
The current suite has 331 passed, 6 opt-in PostgreSQL skipped and 0 failed.
Model consistency and generated idempotent SQL pass; no live database or Docker
execution was available for this update. See the [change contract](../../../docs/v1/coastal-operations-record-navigation-and-audit.md).
