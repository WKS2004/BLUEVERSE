# Coastal Planner implementation and operations

## Implemented on the Member 3 feature branch

The private ASP.NET Core service owns recommendation snapshots, workflows,
itineraries, condition review history and validated biodiversity caching.
React and Flutter use only the public API through edge-nginx. Destination and activity
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

These are consumer requirements implemented on this branch. Shared G00
acceptance was confirmed by Adithya on 2026-10-12, but that confirmation is
not live compatibility evidence or approval of the private IT3091 wire contract. Owners must confirm
paths, payloads and service-to-service authorization before integration.
The peer adapters forward the authenticated caller's bearer context and a
correlation ID. They never expose private hostnames in client requests.

| Owner source | Proposed private call | Required facts |
|---|---|---|
| Member 1 catalogue | `GET /api/experiences/destinations` | Array of canonical `destinationId`, `name`, `region`, IANA `timeZone`, and `activities: [{activityId,name}]`; maximum 500 destinations and 100 activities per destination; no duplicate IDs. |
| Member 1 offerings | `GET /api/experiences/catalogue?destinationId=...&activityIds=...` | Published available offerings with destination/activity/offering IDs, title, UTC `availableFrom`/`availableUntil`, UTC `checkedAt`, `timeZone`, and supported `experienceLevels`. Availability must have been checked within 15 minutes. |
| Member 2 marine | `GET /api/marine/suitability?destinationId=...&activityId=...&start=...&end=...` | Matching IDs, deterministic status, UTC `conditionTimestamp`, safety profile ID for suitable/caution, and explicit `isFresh`. Member 2 owns profile freshness. |
| Member 4 operations | `GET /api/operations/status?destinationId=...` | Matching destination and verified current operating status. Suspended, cancelled and completed operations exclude recommendations. |
| Member 3 external ML (proposal, unconfirmed) | `POST /v1/predictions` | Minimal canonical IDs; validated version, matching identity, provenance, UTC inference time and finite 0–1 values. Optional context never overrides safety or availability. See the IT3091 proposal and public v1 contract. |

The selected experience fits the intersection of the user's window and the
offering's interval. Its duration must fit entirely in that interval and its
experience level must be supported. Missing schedules, stale availability,
unverified marine evidence or missing operations cannot become suggestions.
Outcomes distinguish `MATCHES_FOUND`, `NO_MATCHES` and
`DEPENDENCIES_UNAVAILABLE`. The overall search budget is 40 seconds; individual
peer calls have bounded retries and timeouts. Biodiversity failures produce
explicit unavailable context and no fabricated species. Valid cache entries
expire six hours after inference; corrupt entries are discarded.

The other peer URL variables are documented in `.env.example`; those owner
services are absent from this branch's Compose stack. The synthetic IT3091 fake
is available only with the Compose `dev` profile and is not evidence of a real
provider. Configure the real provider URL, credential, enabled flag, and
expected model version via local environment values. The planner remains healthy and
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

Flutter provides planner home, planning, saved recommendation, paged saved
trips and itinerary details through the same public contract. Full draft
editing preserves stable item IDs, concurrency versions and snapshot
references; native date/time pickers submit wall clocks with the owning IANA
zone. Confirmed trips remain read-only. Condition reviews show prior/current
availability, marine and operating evidence with suggested actions and history.
Permissions gate each capability independently. Saved suggestion clocks are
computed by the service, including for older snapshots, without rewriting
stored instants. Shared loading completes on both success and failure.

The UI registry records implemented client workflows. Component release still
requires genuine peer/provider compatibility and G07 integration evidence.
The G00 confirmation is recorded in the Member 3 checklist and shared trackers.
This branch does not implement the other owners' services or executable AI.

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

## Scope fixes and evidence — 2026-10-12

React's biodiversity normalizer now returns a validated typed result. The two
older request-contract fixtures were corrected with explicit user approval to
include contract version 1 and required provenance/model fields; request
assertions and malformed-response tests were preserved. The UI validator binds
method, full route, service and operation together and has regression cases
for wrong prefixes/methods, misleading comments, test-only routes and OpenAPI.

Mobile pagination sends real query parameters, not an encoded `?` in the path.
The emulator default is `10.0.2.2`, not a laptop LAN address. The request budget
is 55 seconds; ambiguous writes are not replayed through another gateway.
After an interrupted mutation, refresh saved trips before resubmitting.
HTTP-only local configuration is retained at the user's request.

Render packaging includes the private planner and public API forwarding, with
shared JWT secret wiring and the same self-service traveller toggle as Compose.
Its database settings require Npgsql key/value secrets. See
[Render setup](../deployment/render.md); no cloud deployment is claimed.


Final local checks on 2026-10-12: 129 planner, 80 Auth, 21 API, 213 React,
116 Flutter and 42 validator cases passed; the explicit real PostgreSQL test
passed. React lint/build, Flutter analysis, catalogs and agent resources passed.
DHI builds and all six Compose health checks passed, including public gateway
readiness and the expected missing-peer 503. Render schema validation passed.
Use `python -B -m unittest discover -s scripts/validation/tests -p 'test_*.py'`
for validator tests so imports do not write bytecode into `.agents` resources.


## React experience redesign — 2026-10-12

`/planner` is an introductory page for both visitors and signed-in accounts,
including accounts without planner grants. It explains the planner, its three
steps, practical benefits and optional live inspiration. Live data failures
leave the introduction and creation actions available.

The shared top desktop/mobile menu opens **The planner** from other React pages
for all account states. `/planner` is a full-width public introduction without
the account sidebar or a redundant top self-link. All personal planner routes
also omit the standalone top planner link. Personal planning pages use
the shared Profile/Dashboard/Administration
navigation appears as a left sidebar on desktop and an in-page navigation panel
at narrow widths. Its **Coastal planner** entry opens `/planner/plan` directly;
the group exposes **Create a plan**
(`/planner/plan`) and **Plan history** (`/planner/saved`). Guests who choose
creation or history sign in first and return to the requested page. Authorized
users enter directly. The creation form separates destination, local date/time
and pace, shows all verified suggestions, and saves the user's selected
experience as a snapshot-linked draft or appends it to an existing draft.
Confirmation remains an explicit action in plan details.

History uses caller-owned API results. Draft, Confirmed and Past filters and
name/notes search apply only to the displayed page. Create and history use
permission-aware route tabs and wide image hero cards matching the supplied
Coastal Operations references. Both destinations load in one deferred module;
tab navigation has no intermediate full-page loader. Catalogue and history
reads use inline status, retaining visible navigation and hero content during
slow or failed reads. The page content enters with a 320ms directional fade and
slide, disabled by reduced-motion preferences. Other planner operations retain
the shared loading screen.

The history panel follows the supplied Coastal Operations structure with status
filters, search/reset, optional condition-check refinement and 10/20/25/50
page-size selection. Counts describe the current returned page, not a global
total. Empty results keep the controls, a text state and one authorized creation
action; the hero is the only image. Paging uses the raw returned page length so
local filtering does not hide Next; empty later pages retain Previous.
The plan detail timeline retains existing edit, reorder, removal, lifecycle and
condition-review behavior. Initial route-module loading uses the shared loading
screen. No API/Auth business flow was changed for this redesign. The Flutter
follow-up is recorded below.

The introduction, benefits, creation guide and history use four dedicated
planner images, generated for this component rather than reused from other
site sections. They are illustrative scenes, never destination or offering
evidence. Asset provenance and usage are recorded in
`apps/web/src/assets/planner/README.md`.

## Flutter native planner update — 2026-10-12

The Flutter client now follows the same public introduction, protected create
page, plan history and trip-management workflows. Its native menus provide a
global route to the public planner and permission-aware routes to creation and
history. Guests retain the requested creation or history destination through
sign-in. The public introduction has no redundant self-link or account rail.

Creation and history share native hero cards and route tabs with directional
transitions; reduced-motion settings suppress the transition. Catalogue and
history requests stay inline beneath the hero. History exposes caller-owned
status filters, per-page search, condition review, reset, page sizing and
pagination. Dedicated planner images are bundled and identified as
illustrative. The date/time picker keeps destination-local wall-clock fields
independently of the device zone; an IANA time-zone conversion rejects skipped,
repeated or malformed times before posting to the public API.

Windows Flutter commands use `scripts/Windows/Invoke-FlutterCached.ps1`, which
places the Gradle home, pub cache, temporary files and mobile build output in
`D:\WKS\FlutterBuildCache`. It preserves an existing cache and will not merge
two conflicting app-build directories. C: had no default Gradle home when
checked for this update, so there was no C: Gradle cache to remove. Flutter
analysis reported no issues, all 122 Flutter tests passed, and the Android
debug APK built successfully. The command, output path, warnings and verification
limits are recorded in the test matrix.
