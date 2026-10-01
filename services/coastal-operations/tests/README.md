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

Three PostgreSQL workflow/concurrency cases are opt-in. Provision a dedicated
database whose name starts with `blueverse_co_test`, set
`BLUEVERSE_CO_POSTGRES_TEST_CONNECTION` to its connection string, and run:

```powershell
dotnet test services/coastal-operations/tests/Blueverse.CoastalOperations.Tests/Blueverse.CoastalOperations.Tests.csproj --filter "FullyQualifiedName~PostgresWorkflowIntegrationTests"
```

The opt-in cases apply pending migrations and create/clean up only their own
synthetic records. They are skipped when the variable is absent, and refuse a
database whose name does not have the required test prefix. Do not point them at
a development or production database.
