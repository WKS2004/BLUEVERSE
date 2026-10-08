# Coastal Planner implementation and operations

## Implemented on the Member 3 feature branch

The private ASP.NET Core service owns recommendation snapshots, workflows,
itineraries, condition review history and validated biodiversity caching.
React uses only the public API through edge-nginx. Destination and activity
selection uses canonical names from the catalogue projection; users do not
enter UUIDs. Suggestions have persistent URLs, evidence, cautions and optional
wildlife context. Users can save a named trip, add a suggestion to an existing
trip, browse saved trips, edit notes and schedules, reorder or remove stops,
review current conditions and inspect history, and confirm deletion.

The React `/planner` route is the public recommendation home with current
highlight cards and a `Plan a trip` action. `/planner/plan` is the protected
planning form. Draft trips can be edited or cancelled; confirmed trips cannot
be edited or cancelled.

Optimistic updates use `concurrencyVersion`. Stable item IDs survive reorder
and note edits. Changing a stop's schedule resets its previous condition
evidence to `UNKNOWN`. Reordering uses temporary unused order values inside a
PostgreSQL transaction to preserve the unique order index. Stops must not
overlap or repeat; a trip supports at most 50 stops. Windows must be future
UTC times within 30 days. Clients interpret wall clocks in the destination's
IANA zone, rejecting ambiguous or skipped daylight saving times.

`recommendationId` on create/update validates newly added suggestions against
the caller's exact snapshot and its 15-minute lifetime. Unchecked API drafts
remain possible with `UNKNOWN` evidence. Saving a trip does not book an
offering or guarantee future conditions. `HasChanges` compares prior and new
evidence; `RequiresReview` separately reports cautions and missing evidence.
Reviews persist atomically with the itinerary version; history returns the
latest 20 reviews and is removed when its itinerary is deleted.

The history migration preserves existing trip rows. Its `Down` migration
removes the added review table and display-zone/outcome columns, losing those
new records; retain backups and prefer a reviewed forward recovery after
deployment rather than downgrading a database containing review history.

## Permissions and setup

Startup seeds five `planner.*` permissions idempotently, assigns them to the
non-system **Coastal traveller** role and adds them to the existing Admin role.
Compose enables `AUTH_SELF_SERVICE_PLANNER_ACCESS=true` by default. Newly
registered users receive only that traveller role. Set the variable to `false`
to preserve registration without automatic planner grants. Existing users
require administrator role assignment. Refresh their session or sign in
again after changing grants. The service checks permission claims, and all
saved results and mutations are scoped to the authenticated owner.

Run `docker compose --env-file .env config --quiet`, `docker compose build`
and `docker compose up -d` from the root. Coastal Planner applies its own
migrations. Its exec-form `dotnet Blueverse.CoastalPlanner.dll --healthcheck`
checks database and migration readiness without a shell; the API waits for
this healthcheck. Verify `/health`, `/api/health`, `/api/auth/health` and
`/api/planner/health` at the gateway. Preserve the PostgreSQL volume when
stopping; `down --volumes` intentionally deletes local data.

## Required owner contracts — acceptance pending

These are consumer requirements implemented on this branch, **not accepted
G00 contracts or evidence that the other services exist**. Owners must confirm
paths, payloads and service-to-service authorization before integration.
The peer adapters forward the authenticated caller's bearer context and a
correlation ID. They never expose private hostnames in client requests.

| Owner source | Proposed private call | Required facts |
|---|---|---|
| Member 1 catalogue | `GET /api/experiences/destinations` | Array of canonical `destinationId`, `name`, `region`, IANA `timeZone`, and `activities: [{activityId,name}]`; maximum 500 destinations and 100 activities per destination; no duplicate IDs. |
| Member 1 offerings | `GET /api/experiences/catalogue?destinationId=...&activityIds=...` | Published available offerings with destination/activity/offering IDs, title, UTC `availableFrom`/`availableUntil`, UTC `checkedAt`, `timeZone`, and supported `experienceLevels`. Availability must have been checked within 15 minutes. |
| Member 2 marine | `GET /api/marine/suitability?destinationId=...&activityId=...&start=...&end=...` | Matching IDs, deterministic status, UTC `conditionTimestamp`, safety profile ID for suitable/caution, and explicit `isFresh`. Member 2 owns profile freshness. |
| Member 4 operations | `GET /api/operations/status?destinationId=...` | Matching destination and verified current operating status. Suspended, cancelled and completed operations exclude recommendations. |
| Member 3 external ML | `GET /predict?destinationId=...&activityId=...` | Validated model version, UTC timestamp, matching IDs and species fields with finite suitability in 0–1. This optional context never overrides safety or availability. |

The selected experience fits the intersection of the user's window and the
offering's interval. Its duration must fit entirely in that interval and its
experience level must be supported. Missing schedules, stale availability,
unverified marine evidence or missing operations cannot become suggestions.
Outcomes distinguish `MATCHES_FOUND`, `NO_MATCHES` and
`DEPENDENCIES_UNAVAILABLE`. The overall search budget is 40 seconds; individual
peer calls have bounded retries and timeouts. Biodiversity failures produce
explicit unavailable context and no fabricated species. Valid cache entries
expire six hours after inference; corrupt entries are discarded.

The four peer URL variables are documented in `.env.example`. Those services
are absent from this branch's Compose stack. The planner remains healthy and
saved-trip operations remain available without them; the destination form
shows a recoverable unavailable state. A live happy-path integration with
the owner services still needs their implementations and contract agreement.

## Agentic AI and client readiness

The typed private planning coordination seam checks bounded availability when
configured through `AgenticAi__BaseUrl`. Before accepted G07 it performs no
dispatch, model call, tools or agent execution. Business workflow completion
is separate from `aiDependencyStatus` and `aiExecutionStatus`; the latter
remains `NOT_STARTED`. The future health path is a proposed private contract,
not an implemented AI endpoint.

Flutter planner routes are reserved in the UI registry and remain planned.
The whole workflow is marked `in_progress`; equal-client capability and G00/G07
acceptance must be completed before claiming release readiness. This branch
does not implement the other owners' services or mark their gates accepted.

## Verification commands

```text
dotnet test services/coastal-planner/tests/Blueverse.CoastalPlanner.Tests/Blueverse.CoastalPlanner.Tests.csproj
dotnet test services/auth/tests/Blueverse.Auth.Tests/Blueverse.Auth.Tests.csproj
dotnet test services/api/tests/Blueverse.Api.Tests/Blueverse.Api.Tests.csproj
cd apps/web
npm ci
npm run lint
npm run build
npm run test:ci
```

When concurrent Docker builds exhaust local test-worker memory, run the same
complete React suite with `node --experimental-strip-types --test --test-concurrency=2`
from `apps/web`. This limits workers without excluding test files or cases.

From the root, validate the route and UI registries:

```text
python .agents/scripts/validate_endpoint_catalog.py --write-markdown
python .agents/scripts/validate_endpoint_catalog.py
python scripts/validation/validate_ui_integrations.py
```

For explicit provider checks, set `BLUEVERSE_PLANNER_POSTGRES_PASSWORD` from
your local secret environment and optionally `BLUEVERSE_PLANNER_POSTGRES_HOST`
and `BLUEVERSE_PLANNER_POSTGRES_USER`, then run:

```text
dotnet test services/coastal-planner/tests/Blueverse.CoastalPlanner.Tests/Blueverse.CoastalPlanner.Tests.csproj -p:EnablePostgresPlannerTests=true --filter FullyQualifiedName~PostgresPlannerTests
```

The provider test creates a uniquely named test database, upgrades a legacy
trip, checks migration readiness, reordering, stale writes, database constraints
and review cascade deletion, and drops only its own test database. It requires
local test credentials authorized to create databases. Do not point tests at
production or reset an existing application database.

Existing planner tests were corrected with the user's explicit approval:
UUID-entry expectations became named selection; missing schedules no longer
qualify as offers; duplicates fail validation; repeated caution and actual
evidence changes are distinct. Authorization and failure coverage are retained.
