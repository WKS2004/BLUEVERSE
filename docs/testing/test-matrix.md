# Test Matrix

## PR #8 conflict-resolution verification — 2026-10-12

On the integrated conflict-resolution tree, React lint and production build
passed, and the serial web suite passed 384/384. Flutter analysis found no
issues and the serial mobile suite passed 295/295. The full Auth suite passed
85/85, the Coastal Operations suite passed 331 with 6 opt-in PostgreSQL tests
skipped, and the complete API test project passed 57/57. The endpoint catalog
and UI integration validators passed, and `docker compose config --quiet`
accepted the combined services. The first hosted backend run exposed three
remaining 16-permission Auth assertions and valid `/api/...` peer paths being
rejected as absolute file URIs; both were corrected and covered by the complete
local suites. The hosted follow-up run for `37ee55a` passed all 10
PR-triggered GitHub Actions workflows. Docker Stack Health is scoped to pull
requests targeting `main`, so it did not run for this maintenance-base PR; the
combined Compose configuration passed locally. Web tests use one worker on
this Windows host to avoid its parallel Node memory limit.

## Coastal Operations navigation and detailed activity — 2026-10-02

Latest evidence supersedes the historical totals/debounce below. Full web:
**193 passed**; full Flutter: **121 passed**; Coastal Operations: **331 passed,
6 opt-in PostgreSQL skipped, 0 failed**; API suite: **23 passed**. Approved
existing test updates preserve unrelated assertions. New `WEB-OPS-NAV-001`–`005`,
`MOB-OPS-NAV` cases and detailed activity tests cover 500 ms search, all page
sizes and cursor reset, pinned controls, shared cards, Logs detail/edit/Back,
create/edit restoration and invalid/denied/published recovery. Backend
`COASTAL-AUDIT-DETAIL-001`–`003`, actor-context 009 and API signed-identity tests
cover original values, attribution, legacy/system gaps, bounds, spoofing,
tampering and atomic save failure. `COASTAL-POSTGRES-006` requires the guarded
dedicated provider connection; it was skipped, not passed. Migration SQL/model
checks and synthetic browser checks do not establish live provider acceptance.
See [verification details](../v1/coastal-operations-record-navigation-and-audit.md).

## Coastal Operations workspace UX polish — 2026-10-04

Full web suite: **197 passed, 0 failed**. After the final Logs URL-state
simplification, **19 focused web operations tests passed**; changed-source ESLint
and the TypeScript/Vite production build passed. Full Flutter suite:
**123 passed**, `flutter analyze` reported no issues. The shared UI integration
validator and final `git diff --check` passed.

New `WEB-OPS-UX-001`–`004` and `MOB-OPS-UX-001`–`002` cover responsive
header-aligned tabs (including their first-row position before mobile account
navigation), directional navigation, alert fields remaining
read-only until Edit draft, route-restored Alert Logs, mutation-free Logs
details, Logs-only correlation references without a separate actor/“Who” line,
and activity absence in edit forms. The previous `WEB-OPS-NAV-005` expectation
to edit a Logs record was stale against the read-only Logs contract; it now
checks read-only detail and return navigation. The Flutter Logs route test
fixture was updated to resolve the selected-category URL as the app does.
Focused and complete package results are recorded in the
[UX follow-up contract](../v1/coastal-operations-record-navigation-and-audit.md).

### Sticky account rail correction — 2026-10-04

The Coastal pages now use `AccountAreaNavigation` directly as a main-grid item,
with the same sticky position, viewport height, max-height and internal
overflow classes as Profile and Dashboard. Regression checks assert direct
grid placement and the shared sizing classes. The full-suite result above
predates this correction. After it, **11 focused React tests passed**; targeted
ESLint, the production build, UI integration validation and `git diff --check`
passed. The live app served `index-DkmV4bV_`; browser inspection confirmed the
same base rail classes and direct main-grid placement on Assessments, Alerts
and Logs, matching Profile and Dashboard.

## Coastal Operations focused workspace follow-up — 2026-10-01

The [focused workspace contract](../v1/coastal-operations-focused-workspaces.md)
records the earlier hero/search/full-workspace change. User approval for affected
existing-test updates arrived on 2026-10-02. That recheck and the Logs/evidence
regressions passed; the one-second interval described in that historical run is
superseded by the 500 ms behavior in the latest follow-up above.
The earlier 178 web / 107 mobile totals are historical baselines.

## Coastal Operations record experience — 2026-10-01

`COASTAL-RECORD-001`–`006`, `COASTAL-TIMEZONE-001`/`002`,
`COASTAL-OPTIONS-001`, `WEB-OPS-RECORD-001`–`006` and
`MOB-OPS-RECORD-001`–`003` cover titled server-ID drafts, duplicate titles,
legacy replay digests, own-draft discovery, title-only search/Advanced IDs,
immutable title/zone publication context, unavailable/scoped associations,
one DB zone, fractional/DST/invalid local times, option-loading failure/retry,
native pickers and layout/navigation removal. The user approved replacing the
affected existing client expectations for UUID inputs, two offset fields,
objective search and bottom lookup; unrelated assertions were preserved.
Current evidence and provider limitations are in the
[record experience contract](../v1/coastal-operations-record-experience.md).

## Coastal Operations publication update — 2026-10-01

New requirement-based coverage: `COASTAL-PUBLICATION-001`–`007`,
`COASTAL-SEARCH-001`–`003`, `COASTAL-AUDIT-001`, `COASTAL-PERMISSION-001`,
`WEB-OPS-PAGES-001`–`004`, `MOB-OPS-PAGES-001`–`005` and
`MOB-OPS-PERMISSION-001`. Coverage includes immutable full publication context,
replay, finite transport retries/timeouts, draft/audit scopes, filtered cursors,
distinct decision grants, separate page requests and grant-aware actions.
The existing Auth seed expectation and PostgreSQL fixture cleanup were changed
only after explicit user approval; original PostgreSQL assertions remain intact.
See the [change contract](../v1/coastal-operations-publication-and-ui.md) for
current results and the live PostgreSQL/peer/Agentic AI acceptance limitations.
## Flutter planner implementation — 2026-10-12

Flutter now includes the same planner introduction, authenticated creation,
caller-owned history and itinerary operations as React, with native hero cards,
permission-aware tabs, reduced-motion route transitions, dedicated imagery and
destination-zone time validation. `MOB-PLANNER-022` checks the guest creation
sign-in return; `MOB-PLANNER-023` checks opposite-direction tab transitions;
`MOB-PLANNER-024` checks reduced-motion behavior. `planner_time_test.dart`
covers destination-local conversion and daylight-saving boundaries.

`flutter analyze --no-pub` reported no issues. The complete Flutter suite passed
all 122 tests with `--no-pub --concurrency=1`. The Android debug build completed
with `BLUEVERSE_API_BASE_URL=http://10.0.2.2:80` and produced
`D:\WKS\FlutterBuildCache\projects\blueverse-adi\build\app\outputs\flutter-apk\app-debug.apk`.
Gradle home, pub cache, temporary files and mobile build output were all on D:;
the default C: Gradle locations were absent. The APK was packaged successfully
after Android SDK XML-version and obsolete Java 8 source/target warnings. Widget
and API tests use deterministic fakes; no connected-device session or live peer
service behavior is inferred from this build.

## Planner tab transitions — 2026-10-12

`WEB-PLANNER-UI-039` exercises actual app routes in both directions with pending
catalogue/history reads. It requires visible destination content, active tabs,
directional motion classes and zero shared-loader activations, then verifies
that departed success/error replies do not affect the current page. Public
request paths, credentials and timeout signals remain asserted. Existing
authorization, failure and loading-operation cases are retained.

All 229 React tests, build/lint, 42 validator cases and both catalogs passed.
The DHI frontend was rebuilt/refreshed. Isolated Edge at 1440/768/390px observed
both animation-start events, delayed reads without inserted loading overlays,
no horizontal overflow and no animation under reduced-motion settings. Browser
API fixtures were synthetic; no real account mutation occurred.

## Planner hero, tabs and history reference layout — 2026-10-12

Creation and history now share permission-aware page tabs and dedicated wide
image heroes, following the supplied Coastal Operations references. Updated
`WEB-PLANNER-UI-035` retains single-image/CTA and permission assertions while
requiring history controls on empty pages under the latest request.
`WEB-PLANNER-UI-036`–`038` cover active tabs and grants, real page-size requests
and paging, current-page filter behavior, conditions refinement, search and reset.
All 228 React tests, build/lint, 42 validator cases and both catalogs passed.
The DHI frontend was rebuilt and refreshed without restarting backend services.

Isolated Edge verified populated and empty history plus creation at
1440/768/390px, actual page-tab navigation, and global planner entry at
1440/1024/768/390px with no horizontal overflow or unexpected requests.
Desktop creation and empty history were visually reviewed. Browser data used
labelled synthetic public-API fixtures; these checks do not establish live
catalogue or peer-service availability. Flutter remains deferred.

## Personal planner menu and empty-history correction — 2026-10-12

The standalone top planner link is omitted across introduction, creation,
history, suggestion and itinerary routes. `WEB-UI-NAV-018` and the approved
planner navigation case cover this while retaining global entry links elsewhere.
`WEB-PLANNER-UI-035` verifies a single empty-history image and creation action,
no duplicate header CTA, no empty list controls, and management-only accounts
without creation access. All 225 React tests, build/lint, 42 validator cases and
both catalogs passed. The DHI frontend was rebuilt/refreshed. Isolated Edge
checked the absence of the personal top link and the single empty-history image
and CTA at 1440/768/390px; desktop empty history was visually reviewed. Signed-in
preview data was synthetic, without real account mutation.

## Public planner layout correction — 2026-10-12

Browser comments require `/planner` to be a full-width public introduction
without account navigation or the redundant top self-link. The global menu
still opens it from other pages; the personal sidebar Coastal planner entry
defaults directly to `/planner/plan`. Updated approved navigation/layout cases
retain sign-in return, grants and workflow assertions; `WEB-UI-NAV-020` covers
the sidebar entry destination. All 224 React cases, lint/build, 42 validator
cases and both catalogs passed. The local frontend was rebuilt/refreshed.
Isolated Edge checked layouts at 1440/768/390px and actual global-menu clicks
at 1440/1024/768/390px, asserting no account sidebar or top self-link on the
introduction and no overflow. Final desktop introduction screenshot was visually
reviewed after the shared loading screen finished; signed-in API data was synthetic.

## Global planner menu and dedicated imagery — 2026-10-12

`WEB-UI-NAV-018` verifies the public planner in both top menus across eight
site areas and signed-out/restoring/unavailable/signed-in account states,
including accounts without planner permissions. `WEB-UI-NAV-019` verifies a
guest's mobile menu opens the public planner and closes without a sign-in detour.
Creation/history remain in the shared account sidebar. All 223 React tests,
build/lint, 42 validator cases and both catalogs passed. Isolated Edge checked
planner layouts at 1440/768/390px and global home/recovery menus at
1440/1024/768/390px without overflow; new introduction images decoded correctly.
Four dedicated illustrative images replace reused section photos. Signed-in
browser data remained explicitly synthetic; this does not establish live peer
compatibility. Final screenshots wait for the shared loading screen to finish.

## React Planner redesign verification — 2026-10-12

The React-only redesign retains prior API, authorization, validation, error,
optimistic-write, confirmation and condition-history cases. Existing planner
and navigation test updates were explicitly approved. Added `WEB-PLANNER-UI-028`
through `034` for public introduction across auth states, real sign-in/return
routing, signed-in navigation, all-candidate selection/save, per-page history
filters/search, preference-change invalidation and read failure/retry/denial.
`WEB-PLANNER-COPY-001` checks truthful read-failure copy and session/permission
recovery. All 221 web cases, lint and production build passed, along with 42 validator cases and both UI/endpoint catalogs.

Isolated headless Edge checked introduction, creation and history at 1440, 768
and 390px, with no horizontal overflow, plus suggestions and itinerary details.
Signed-in screenshots use labelled synthetic public-API fixtures, including the
normal session-refresh contract; they do not establish live peer compatibility.
A separate public introductory screenshot used the running local stack.
No Flutter source or tests changed for this redesign.

## Coastal Planner completion checks — 2026-10-12

| Check | Evidence and limits |
|---|---|
| Planner | 129 default cases passed. Added `PLANNER-DISPLAY-TIME-*` checks for new and legacy snapshot local clocks across Colombo, New York and Auckland, unchanged UTC instants, stored snapshots and ownership. |
| Auth / API | 80 / 21 cases passed; existing tests preserved. |
| React | All 213 cases passed with two workers; lint and production build passed. Two request fixtures corrected with explicit approval to include contract version 1 and provenance, retaining request and invalid-response assertions. |
| Flutter | All 116 cases passed; analyzer reported no issues. New `MOB-PLANNER-001`–`021` cover full draft edit transport, stable IDs, snapshot append, paging, permission separation, conflict retention, condition comparison/history, malformed/denied replies, loading completion, no write replay, history retry and strict wildlife identity/species/provenance guards. Native device execution remains distinct from widget tests. |
| Real PostgreSQL | `PLANNER-POSTGRES-001` passed on local PostgreSQL 16, using a uniquely named disposable test database. Verified legacy migration upgrade, order/check constraints, stale writes and review cascade. Application data was not reset. |
| Contracts | 42 validator tests, UI registry, endpoint catalog, agent resources and whitespace checks passed. Exact method/path/service/operation evidence replaces suffix guessing. |
| Packaging | API, Auth, Planner and React DHI images built. All six Compose containers became healthy. `/health`, `/api/health`, `/api/auth/health`, `/api/planner/health` passed through the public gateway; planner reports database connected and migrations applied. Missing-peer highlights returned the expected structured 503 without fabricated data. Render Blueprint passed the official JSON schema; no cloud deployment occurred. |
| Integration limits | Member 1–3 service and client implementations are present on this integration branch. Live peer/provider compatibility, all-owner G00 ratification, complete cross-client acceptance and G07 remain unverified; component tests use deterministic fixtures and do not establish live compatibility. |

## Coastal Planner branch evidence — 2026-10-05

| Check | Evidence and limits |
|---|---|
| Planner backend | 106 default cases passed, covering named catalogue projection, schedule intersection, source freshness, experience matching, ownership, authorization, snapshot expiry, stable stop identity, duplicate/overlap validation, bounded dependency failure, failed workflow persistence, review history and the non-executing AI seam. The new catalogue/history HTTP cases assert permissions, schemas, headers and persisted ownership. |
| Auth/API regression | 80 Auth cases and 21 API cases passed. New Auth cases verify idempotent planner seeding and enabled/disabled traveller assignment with issued permission claims and persisted sessions. |
| React | Full 201-case suite passed with two test workers after an unconstrained parallel run exhausted local memory during Docker builds. New coverage includes saved-trip append, stable IDs/current version, conflict draft retention, tab-close protection, review evidence, undo, deletion confirmation, time-zone boundaries and malformed response guards. Lint and production build passed. |
| PostgreSQL | Explicit `PLANNER-POSTGRES-001` uses a unique disposable database for migration upgrade, real unique/check constraints, reorder, stale writes and history cascade. It is enabled separately; no provider evidence is inferred from in-memory tests. Commands are in the planner guide. |
| Gates | Adithya's Member 3 G00 input is recorded, but the Member 3 record remains a proposal until all four owners ratify the shared freeze. Live peer/provider compatibility and accepted IT3091 protocol evidence remain open; G07 and executable AI stay deferred. Local mobile HTTP configuration remains a separate release gate. |

See [planner operations and verification](../development/coastal-planner.md).
The baseline descriptions below preserve earlier scope; the dated evidence
above records this feature branch's additional coverage.

The implementation plan and test-directory convention are documented in
[`implementation-plan.md`](implementation-plan.md). The entries below describe
the minimum evidence expected as each component is introduced.

| Area | Minimum evidence |
|---|---|
| API | `services/api/tests` covers health, non-versioned public routing, API/Auth OpenAPI, CORS, safe gateway errors, JWT validation, signed actor-context forwarding (including Coastal Operations credential stripping), YARP routing and dependency isolation. Marine feature-branch API evidence is recorded separately; combined counts must come from the integrated run. |
| Authentication | `services/auth/tests` cover registration, login, validation, sessions, refresh/revocation, administration, authorization and persistence boundaries. `AUTH-SEED-ADMIN-ALL-001` verifies idempotent Admin System Role grants for all 36 registered permissions, including Operations, Marine, Experience and Planner grants. |
| Authorization | Permission allow/deny, policy-provider behavior, catalog isolation, administrative mutation protection and system-role escalation tests in `services/auth/tests` |
| PostgreSQL | Auth and each component own checked-in EF Core migrations. Opt-in `AUTH-POSTGRES-SESSION-001` and planner `PLANNER-POSTGRES-001` exercise real-provider behavior; Coastal Operations has a recorded local migration/readiness check and five opt-in provider cases that require its dedicated test connection. Marine and Experience migration/provider evidence stays with their service suites. In-memory tests do not replace PostgreSQL evidence. |
| React | `apps/web` uses Node 24 `node:test`; `npm run lint`, `npm run build` and `npm run test:ci` are the package gates. Separate branch snapshots recorded Operations 185, Marine 246 and Planner 221 cases; these are dated component evidence, not counts from the combined tree. Request and state tests target registered public `/api/...` routes, and component UI tests cover navigation and workflows. |
| Flutter | Coverage includes gateway/configuration, Auth, administration, onboarding, navigation, profile/security/session workflows and all component screens. Separate branch snapshots recorded Operations 113, Marine 129 and Planner 131 cases; these are dated component evidence, not counts from the combined tree. No `integration_test` suite is checked in. |
| Backend services | Matching test projects live under each `services/<service-name>/tests` folder and the backend workflow discovers them there. Coastal Planner has package-local peer, recommendation, itinerary, validation, re-evaluation and authorization cases; each other private service keeps its tests at its service boundary. |
| v1 Ushan Srinuka (Member 1) | Experience & Biodiversity service, API integration and React/Flutter workflows are present in this integration branch. Shared G00 agreement, complete cross-client/provider evidence and integration acceptance remain pending; post-G07 agent execution is still gated. The agent route remains a static `not_connected` status. |
| v1 Sanuda Abeysinghe (Member 2) | Marine Safety backend, API, React and Flutter workflows are present. Feature-branch verification recorded Marine, API, React and Flutter test results plus Auth permission-seeding readbacks. Marine tests use EF InMemory and transport doubles; PostgreSQL constraints/concurrency, live provider/device integration and G07 acceptance remain separate gates. |
| v1 Adithya Gunawardana (Member 3) | Coastal Planner backend, React/Flutter workflows, package-local tests and a real PostgreSQL migration/apply check are present. Live peer/IT3091 compatibility, hosted transport and G07 acceptance remain release gates. |
| v1 Wanshaja Sooriyabandara (Member 4) | Branch-local assessment and alert CRUD/publication, reviewer decisions, target state/history, private evidence, scoped activity/search and durable publication delivery are present. The service suite's last recorded result was 322 default cases; five opt-in PostgreSQL cases require the dedicated test connection. React and Flutter expose dedicated Assessment/Alert pages with shared authorization and workflow capabilities. Shared G00 producer acceptance and post-G07 live agents remain pending. |
| UI integration contract | Dependency-free registry/validator checks that every product workflow has both React and Flutter routes and that client API literals resolve to declared public `/api/...` endpoints; role coverage and platform parity are verified in workflow tests |
| Integration | React/Flutter against the same public API, gateway routing and cross-platform workflow; no client-to-client or internal-service calls |
| Agentic AI | Safety Operations proposal-core and Gemini adapter tests are under `services/agents/tests`; `AI-NET-001` covers Gemini-only CONNECT, fixed proxy configuration, DNS/IP filtering and direct-egress denial. The Docker profile builds the private agent and egress proxy; model-call workflow and G07 integration remain separately gated. Other agent-role contracts and complete workflow evaluation remain outstanding. |
| Security | Unauthorized/invalid-input/prompt-injection cases with complete assertions: expected HTTP outcomes across relevant `1xx`–`5xx` statuses plus body/schema, authorization, state and side-effect validation |
| CI | Passing source, client, backend, AI and integration workflows; backend test discovery fails when a service source project has no matching local test project |
| Deployment | Health + smoke-test evidence |

Current Flutter evidence (2026-10-12): `flutter analyze --no-pub` reported no
issues and `flutter test --no-pub` passed all 131 tests. A debug APK was built
and installed on the authorized Android 16 device under `com.example.mobile`
with the USB reverse tunnel. The guest planner action reached Sign in, and the
post-fix onboarding screen was visually checked without the standalone Planner
shortcut. The updated app reopened to onboarding after reinstall, so protected
Create a plan and Plan history were not rechecked at first. After a fresh
sign-in, both routes opened on-device: Plan history showed its empty state and
0 of 0 plans; Create a plan reported destinations unavailable, and the
read-only retry returned to the same state. No plan data was changed.

Every row requires scenario-based coverage, not a single happy-path case:
nominal passing behavior, invalid/rejected behavior, relevant boundaries,
extreme values, malformed or missing input, and applicable concurrency,
dependency, authorization, retry and platform variations. New behavior and its
tests are delivered in the same change. Tests remain in their owning
framework/service default directories; the repository must not grow a
centralized root `test/` tree for these cases.

## Coastal Operations Logs and evidence follow-up — 2026-10-02

The user approved affected existing-test updates for the prior full-workspace
changes and this Logs/search/evidence change. Client regression cases verify
1,000 ms debounce/reset, quiet retained-card feedback, stale responses/retry,
focused forms/details/chrome/Back, all five page sizes and cursor resets,
permission-denied Logs, timeline reads and draft-only confirmed image removal.
Backend evidence/log cases verify stored state/version/audit/bytes plus complete
scope and failure outcomes. PostgreSQL cases 004/005 are opt-in provider tests.
See [current checks and limits](../v1/coastal-operations-logs-and-evidence.md).

Final evidence: **322 backend passed / 5 PostgreSQL skipped**, **185 web passed**,
**113 mobile passed**, **35 validator tests passed**. TypeScript, Vite build,
changed-source ESLint, Flutter analysis, EF model-drift checks, route/UI registry,
agent resources and diff whitespace checks passed. Synthetic desktop/phone
browser checks completed; live PostgreSQL, Docker, deployed gateway and real
mobile device acceptance remain unavailable or unexecuted. No test was skipped
or relaxed to fix a failing assertion; PostgreSQL skips use the existing opt-in
connection gate. See the current contract for commands and limitations.

## Coastal Operations Logs hero — 2026-10-04

`WEB-OPS-LOGS-004` checks the Logs-specific coastal image, accessible image
description, and that the hero precedes search and results. The corresponding
asset prompt and design intent are recorded in the
[Logs hero contract](../v1/coastal-operations-record-navigation-and-audit.md).
The focused `OperationsLogs.component.test.js` run passed **8/8**. The React
production build, changed-file ESLint and UI integration validator passed. The
frontend container was rebuilt/restarted, and the browser confirmed the hero
above the live Logs search and list.
### React planner navigation correction — 2026-10-12

`WEB-PLANNER-UI-030` now verifies that the top desktop/mobile menus exclude
creation/history links and that the shared account sidebar opens both routes,
marks the current action and preserves direct authorized navigation. All 221
React tests, build and lint passed; all 42 validator cases and the endpoint/UI
catalogs passed. Isolated Edge rechecked intro, creation and history at
1440/768/390px with labelled synthetic API fixtures and no horizontal overflow.
## Member 2 correction coverage (2026-10-08)

- `M2-ACT-001..011`: read-only canonical activity handoff, retired local writes,
  authenticated access, empty/unavailable/unpublished producer states and full
  canonical profile → independent review → assessment → immutable history flow.
- `M2-CATALOG-001..005`: actual Member 1 response schema, UTC dates, category
  boundary, malformed/duplicate IDs and upstream failures.
- `M2-CONTEXT-001..004`: private service rejects client JWTs; signed context
  authorizes the matching operation; foreign signatures and modified
  path/query/method/body are rejected before provider/domain side effects.
- `M2-AUTH-*` retains current-session, cookie precedence, revocation and current
  permission checks through the production gateway middleware with Auth doubles.
- `API-MARINE-001..011` and `API-ACTOR-001..005`: current Auth actor contract,
  dependency failures, permission combinations, spoofed-header stripping,
  request signing, cookie selection and v0 behavior without Marine configuration.
- `M2-PROVIDER-014..016`: out-of-window forecast timestamps, finite decimal
  overflow, database numeric boundaries and invalid negative weather codes
  become missing evidence.
- Web/mobile catalogue cases cover canonical IDs, malformed/empty responses,
  dependency outage/retry and independent condition-only lookup.

EF InMemory and transport doubles are intentionally identified as such.
PostgreSQL constraints/migration execution and live cross-member deployment
compatibility are not inferred from these results.

- `.github/scripts/tests/test_marine_integration_wiring.py`: shared-contract
  trigger scope, actual public health aliases, ephemeral signing-key setup and
  linked Docker compilation paths; previous CI helper checks are retained.

## Marine branch repair regressions (2026-10-08)

- `M2-REPAIR-CACHE-001..003`: inclusive forecast boundaries, preceding-hour
  rejection, retrieval recency/future rejection, candidate ordering and unchanged
  stored evidence; tests assert IDs, row counts and provider invocation.
- `M2-REPAIR-PRECISION-001..002`: all six limits on create/update, excess precision
  rejection without draft/history changes, exact scale-two values and author audit.
- `M2-REPAIR-TIME-001..003`: current/evaluate ProblemDetails with no provider or
  persistence side effects, one-tick range boundaries and real adapter transport
  behavior at arithmetic-safe date edges.
- `MOB-MARINE-REPAIR-001..009`: edited signed decimal coordinates, malformed
  nonblank filters, reader navigation and mutation controls, all five mutation
  operations after timeout, socket/client failures, read fallback, HTTP rejection,
  four-second completion and actual application-shell profile navigation.
- Docker push and pull-request shared-source path filters are asserted separately
  in the CI helper suite.

The local audit reproductions intentionally asserted the former defects; they
are preserved as historical evidence and are not acceptance regressions.

## Marine dependency isolation regressions (2026-10-08)

- `M2-ISOLATION-001..009`: own readiness and saved evidence/deactivation during
  catalogue outage, all six dependent operation failures with unchanged state,
  valid empty lists and recovery, provider outage, cancellation/timeout, absent
  configuration and invalid optional URLs, safe error reasons and OpenAPI schema.
- `API-MARINE-AVAIL-001..003`: unreachable/timed-out destination metadata, all
  mutation methods with conservative retry hints, unrelated routes and received
  upstream errors preserved, unavailable Auth verification fails closed.
- `WEB-MARINE-AVAIL-001..003` and `MOB-MARINE-ISOLATION-001..002`: typed error
  metadata, malformed optional metadata, valid empty response, saved profiles
  visible during outage, independent deactivation, draft review/edit gating and
  recovery through the actual retry controls.
- CI helper suite: 10/10 pass, including execution of actual OpenAPI selection
  for absent members, Marine alone and both implemented member sources.

Tests use deterministic service/HTTP doubles, EF InMemory, JSDOM and Flutter
widgets. These results do not establish live PostgreSQL, deployed gateway/member
or physical-device behavior. The [availability contract](../api/marine-dependency-availability.md)
records the operation matrix and structured missing-data response.

## Marine React workflow completion (2026-10-08)

`WEB-MARINE-WORKFLOW-001..009` adds 14 cases covering accessible navigation,
all four unchanged server classifications, explicit missing condition evidence,
grouped cited drafts at contract upper bounds, excess precision, equal/greater
caution rejection without mutation, independently successful history datasets,
last-loaded data disclosure, retry recovery, read-only profile inspection and
server rejection without duplicate writes.
The complete React suite passes 224/224, with Web lint/build and the shared
endpoint/UI contract validators passing. UI tests use deterministic synthetic
criteria, JSDOM and public API doubles; live browser/deployment/provider evidence
is separate. The existing caution test now requires client rejection with no mutation; a
separate case retains server-error, single-submit and draft-preservation
assertions. This correction uses the existing user permission for affected tests.

## Marine entry and Admin header correction (2026-10-08)

- `WEB-MARINE-ENTRY-001..005`: ten cases cover all four Admin paths, removal
  of desktop/mobile home links and the empty mobile toggle, preserved account
  actions, desktop/mobile Marine navigation and menu closure, all three sidebar
  routes, read grants without role names and denial for role-name/manage-only
  accounts. Home retains its section links.
- `AUTH-SEED-DOMAIN-001..002`: three provider-independent cases cover one/both
  preferred IDs occupied by other member permissions, existing Marine codes
  with different IDs, preservation of descriptions/audit/grants and repeated
  bootstrap without duplicates. Existing tests were retained.
- Latest complete suites: React 234/234 and Auth 80/80, zero failures/skips;
  Web lint/build and endpoint/UI validators pass. All 35 UI validator tests pass.
- Actual local deployment: frontend serves `index-B8KXY9xG.js`; API, Auth and
  Marine health are 200 (Auth/Marine report database connected). PostgreSQL
  readbacks before/after normal Auth startup show 14 → 16 permissions/Admin
  grants, with both Marine codes added and all previous IDs/grants plus account/
  session counts preserved. A second Auth startup retains identical IDs/counts.
  This narrowly verifies real-provider permission repair, not the optional
  Auth PostgreSQL suite or cross-member workflows. Browser automation could not
  initialize; header behavior is verified by JSDOM and deployed public assets.

Evidence: `local/marine-verification-2026-10-08/web-entry-full.log`,
`auth-entry.trx`, `permission-seed-{before,after,repeat,comparison}.json` and
`runtime-entry-health.json`. Normal Docker frontend build passed; Auth Docker
restore stalled and was cancelled. Auth was published with the installed SDK
cache and packaged locally using the selected DHI ASP.NET runtime. The standard
checked-in Dockerfiles remain unchanged by this navigation correction.


## Consistent Marine navigation labels (2026-10-08)

The sidebar and page tabs now share **Coastal conditions**, **Coastal evidence**
and **Activity safety limits** from a single display-label module. Existing
`WEB-MARINE-ENTRY-004` and `WEB-MARINE-WORKFLOW-001` assertions were updated
under the earlier test-edit approval to require the user-selected naming change;
route, permission and current-page assertions remain. The workflow case also
asserts matching desktop/mobile sidebar labels and destinations independently
of the label module. Affected suites pass 24/24; lint/build, catalog/UI checks
and all 35 UI validator tests pass. The full 234-case suite above is earlier
whole-suite evidence, not rerun for this label-only change. The local frontend
was rebuilt with its existing DHI Dockerfile and serves `index-BvjgAZvJ.js` with
the shared labels. Evidence: `web-labels-tests.log`, `web-labels-docker.log` and
`web-labels-runtime.json` under the existing local verification directory.


## Marine editorial heroes (2026-10-08–09)

`WEB-MARINE-HERO-001..002` adds six cases across conditions, history and profiles:
correct distinct image and eyebrow; one associated H1 and readable guidance;
decorative alt/ARIA treatment; intrinsic dimensions and priority loading;
copy retained when the image emits an error; correct page-to-scene binding,
current-route navigation and content during dependency outage. These are
JSDOM checks, not a browser screenshot or breakpoint rendering proof.

Focused tests pass 30/30; the complete React suite passes 240/240 with one worker,
zero failures/cancellations/skips. Web lint/build, endpoint/UI checks and all
35 UI validator tests pass. Initial parallel tests and Docker CLI exhausted
host memory; tests were rerun with `--test-concurrency=1`, then Docker rebuilt
successfully with `GOMAXPROCS=2`. No assertions were weakened or tests skipped.
The local frontend serves `index-DVKULqR5.js`; all three Marine routes and each
new WebP image return 200. Served image bytes match the validated build.
Browser automation could not initialize, so live interaction is not claimed.

The three new built-in-generated images total 930,622 bytes, each 1536 × 1024.
Only the current page's decorative image is rendered, with solid-panel text
and a stacked mobile layout. Prompts/provenance are recorded in the
[asset inventory](../../apps/web/src/assets/coastal/marine/README.md).
Evidence remains under `local/marine-verification-2026-10-08/web-hero-*`.


## Marine heroes stacked at every width (2026-10-09)

Updated the existing hero checks under the user's continuing affected-test
approval: the image panel must be the first DOM child, heading/copy below it,
with one grid column and no breakpoint column or order overrides. The image-first
structure now applies to conditions, history and safety profiles at every width,
matching the user's screenshot. Existing image/semantic/dependency assertions
remain. Focused suites pass 30/30; lint/build, catalog/UI validation and all
35 validator tests pass. The full 240-case result above is prior whole-suite
verification, not rerun for this layout change. These structural JSDOM checks
do not claim pixel rendering at every physical screen resolution.

Local frontend rebuild/refresh passed. All three routes return 200 and serve
`index-WLREUoDt.js` plus `index-6ynYzJSB.css`, byte-identical to the validated
build. Evidence is under `local/marine-verification-2026-10-08/web-hero-stack-*`.

## Marine shared workspace and tab transitions (2026-10-09)

The shared heading and existing pill navigation persist across the three Marine
routes, before each stacked hero and content panel. Navigation uses sticky
positioning below the 76 px site header. A 260 ms panel entrance respects reduced
motion. Tab bootstrap reads use inline feedback; initial entry, explicit retry,
manual requests, mutations and authentication retain normal loading feedback.

Six new `WEB-MARINE-TABS-*` cases exercise the actual route layout, rapid switches
with unresolved reads, back/forward navigation, deep/near-top scrolling,
keyboard focus, permission denial without requests, read errors, recovery and
leaving/re-entering the workspace. Existing hero and workflow checks now compose
the shared layout and retain their business/image/error assertions. All three
existing scroll-manager tests pass. Full React evidence is **246/246**, with zero
failures/skips, using one worker. Web lint/build, endpoint catalog/UI validation
and all **35** validator tests pass. Validator tests initially encountered sandbox
temporary-directory access errors; rerunning with a writable workspace temporary
directory passed without test changes.

JSDOM checks structure, state and sticky/motion declarations; it does not provide
visual screenshots at multiple widths. Logs are under
`local/marine-verification-2026-10-08/web-tabs-*`.
The frontend DHI build and refresh pass; all three public Marine URLs return 200
and their JavaScript, CSS and three hero images match validated build bytes.
Runtime hashes and paths are in `web-tabs-runtime.json` in that evidence folder.

## Marine native mobile experience (2026-10-09)

Flutter now uses a single Marine shell with pinned shared workflow labels,
stacked bundled heroes and reduced-motion-aware content changes. Dashboard and
account-menu entries require `marine.profile.read`; manager writes additionally
require `marine.profile.manage`. The standard Dashboard landing and signed-out
root onboarding route are preserved/restored. Marine reuses the selected Auth
account's secure header on each public request, closes its owned HTTP transport
and rebuilds/removes protected content after account/grant changes.

Fifteen new `MOB-MARINE-APP-*` cases cover credential rotation/account selection,
missing credentials without transport, denial without write replay, malformed
saved rows, UTC calendar/offset validation, 320 px enlarged-text layout, pinned
navigation, reduced motion, inline slow reads, live permission removal, partial
history/assessment failures, labelled retained records and retry, UNKNOWN without
conditions, refresh without assessment writes, profile read failure, entry-point
grants and real-app signed-out launch. The existing reader-navigation test was
updated under the user's continuing affected-test authorization to use the
Dashboard entry and new shared pills; its read-only, request and denied-action
assertions are retained. A new test initially exposed a narrow heading overflow,
which was fixed rather than relaxing layout expectations. An incomplete
synthetic assessment fixture was corrected to include the contract's createdAt.

Full Flutter evidence is **118/118**, zero failures/skips, with clean analysis
and successful `flutter pub get`. Endpoint/UI validation and all **35** validator
tests pass. Logs are under `local/marine-verification-2026-10-08/mobile-workspace-*`.
The Android debug build attempt emitted no output and was stopped; no newly
built APK or Android packaging success is claimed for that earlier run.
Widget/synthetic API checks do not establish physical-device, live provider or
deployed-gateway behavior.

### Marine Flutter recheck — 2026-10-10

The complete default Flutter suite passes **129/129**, with zero failures/skips
and clean analysis. Endpoint/UI validators and all **35** validator tests pass.
Evidence is under `local/marine-verification-2026-10-10/`.

- `M2-MOB-NET-001` checks the portable emulator/USB gateway defaults. The
  hardcoded laptop IP was corrected to the standard emulator host `10.0.2.2`.
- `M2-MOB-ROUTE-*` and `M2-MOB-FORM-*` cover registered and unknown paths,
  form controls, malformed assessment evidence and public activity identifiers.
  The old `marine_ui_tests.dart` file was outside default/CI discovery and had
  six failures when run explicitly. With user approval, it was renamed to
  `marine_ui_test.dart`; incorrect detail-route, idle-prompt and rendered-menu
  assumptions were corrected against the registry and actual widget behavior.
  Unknown routes now return an unmatched result. No detail route is exposed.
- `M2-MOB-LIFETIME-*` reproduced a disposed-widget confirmation crash and an
  older response replacing refreshed profiles. Guards now stop dialog actions
  after page removal and accept only the latest profile-read generation.
- `M2-MOB-WORKFLOW-001` exercises invalid input without dispatch, cited draft
  creation, immutable version creation, blocked self-review, review by another
  manager, cancelled deactivation without DELETE and confirmed deactivation.
  It asserts exact public methods/paths, criteria, version and active states.

The installed Android toolchain and attached Android 16 phone pass the refreshed
`flutter doctor -v` check, including SDK licences and USB authorization. Local
gateway/API/Marine health returns 200; unauthenticated profile access returns
401. Authenticated live provider and cross-component flows remain separate from
synthetic workflow evidence.

Android debug packaging reached native compilation and engine/JNI extraction,
then failed with `java.io.IOException: There is not enough space on the disk`.
At failure, C: had zero free bytes and D: approximately 2.2 GiB free. No APK or
phone-launch success is claimed. Packaging and authenticated phone workflows
require free build-cache space and a subsequent successful build/run. The exact
failure is captured in `android-build.log`; this is distinct from passing
Dart analysis, widget/API-double tests and toolchain detection.

Subsequent cache relocation resolved the packaging failure. User-level
`GRADLE_USER_HOME` and `PUB_CACHE` now select the existing
`D:\WKS\FlutterBuildCache\gradle` and `pub` folders; build-process `TEMP`/`TMP`
select its `temp` folder. The authorized C: Gradle cache was removed, freeing
approximately 3.7 GiB. Existing Gradle 9.3.1, Android plugin 9.1.0, Kotlin 2.4.0
and exact Flutter engine artifacts were compatible; `flutter pub get` resolved
the pinned dependencies without changing `pubspec.lock`.

With those caches, analysis remains clean and all **129/129** tests pass.
`flutter build apk --debug --no-pub --target-platform=android-arm64
--dart-define=BLUEVERSE_API_BASE_URL=http://127.0.0.1:80` succeeds. The ARM64 debug
APK was installed with `adb install -r` on the attached Android 16 phone and
launched with a USB reverse tunnel for port 80. Its signed-out coastal welcome
screen renders, the app process remains running and the scoped runtime check
reports no captured Flutter/AndroidRuntime errors. This establishes native
packaging and initial launch; authenticated Marine, live-provider and
cross-component phone workflows remain unverified. The SDK XML-version warning
was nonfatal. Follow-up evidence: `cache-pub-get.log`, `android-d-cache-build.log`,
`d-cache-analysis.log`, `d-cache-tests.log`, `android-install.log`,
`android-launch.log`, `android-runtime-errors.log` and `android-launch.png` under
the same local evidence directory.

## Agentic AI restoration and private Gemini egress — 2026-10-13

The four private agent services now live under `services/agents`; the prior
`services/ai` paths are removed. Each agent has a distinct Gemini API-key and
model configuration. Environment-driven model generation remains disabled,
and no generation request was made because shared AI-00 provider/data-handling
approval is still pending. Role credentials stay in the ignored local `.env`;
credential values are not recorded here.

- Python role suites passed in read-only, network-disabled DHI Python
  containers: Experience & Biodiversity 22, Marine Conditions 54 (10
  subtests), Planning & Coordination 24, Safety & Operations 61 (31
  subtests).
- Backend suites passed: Experience & Biodiversity 103, Coastal Planner 132;
  Public API 58, including `API-PROXY-004`, which verifies that the private
  Marine agent has no public API route. `PLANNER-AI-SEAM-004` verifies the
  planner sends its actual planning context to its private agent.
- All four agent DHI images and the Gemini-only egress proxy image built.
  Compose configuration passed; all five containers reached healthy status.
  Runtime network inspection confirmed each agent is only on
  `blueverse_internal`; only the proxy also joins `blueverse_agent_egress`.
  The smoke-test containers were removed after validation.
- No-generation credential checks retrieved model metadata for each agent: all four returned HTTP 200, matched the configured model and advertised `generateContent` support through the internal proxy. This does not verify model generation.
- Endpoint catalog regeneration and both validators passed: 136 public
  endpoints, 54 frontend routes and 24 documented Agentic AI endpoints. All
  20 endpoint-catalog regression tests passed.

- Marine Safety passed 183 tests; Coastal Operations passed 332 tests, with six opt-in PostgreSQL cases skipped.

At the time of this restoration check, live backend dispatch for Marine and
Safety remained deferred: Member 2's G00 scope had no AI seam, and the
Operations service still used its disconnected proposal port. These local
role suites and health checks did not establish live Gemini or cross-agent
compatibility.

## Full local suites and live workflow attempt — 2026-10-13

Ten student-facing Bash and PowerShell runners now cover the complete React,
Flutter, four backend and four agent test suites. The PowerShell runners were
executed on this workstation; all ten PowerShell files and ten Bash files pass
syntax parsing. The full results are:

| Component | Passed | Failed | Skipped | Notes |
| --- | ---: | ---: | ---: | --- |
| React Web | 397 | 0 | 0 | Full `test:ci` suite |
| Flutter Mobile | 295 | 0 | 0 | No `integration_test` directory is checked in |
| Experience & Biodiversity backend | 106 | 0 | 0 | |
| Marine Safety backend | 183 | 0 | 0 | |
| Coastal Planner backend | 134 | 0 | 0 | Includes dispatch body framing coverage |
| Coastal Operations backend | 339 | 0 | 6 | Optional PostgreSQL cases require a dedicated test connection |
| Experience & Biodiversity agent | 24 | 0 | 0 | |
| Marine Conditions agent | 54 | 0 | 0 | Plus 10 unittest subtests |
| Planning & Coordination agent | 31 | 0 | 0 | Includes execution-state ownership coverage |
| Safety & Operations agent | 61 | 0 | 0 | Plus 31 unittest subtests |

Compose configuration, endpoint catalog validation and UI integration
validation pass. The endpoint catalog contains 136 public endpoints, 54
frontend routes and 24 documented Agentic AI endpoints. The local synthetic
experience catalogue already held 24 records; the seeder confirmed there were
no missing demo records to add. Coastal Planner and Planning Coordination were
rebuilt and restarted before the final dispatch attempts.

Three tourist recommendation workflows and three coastal-operator assessments
were then submitted with synthetic records. The tourist requests returned 201
and persisted `UNAVAILABLE` agent execution status. The operations dispatches
also returned `UNAVAILABLE`; no proposal or reviewer decision was created.
Filtered local logs showed Gemini `generateContent` requests returning HTTP
503 at the Planning Coordination model call, before specialist delegation.
The earlier per-key model-metadata checks returned HTTP 200, but they do not
prove successful generation. Accordingly, these six attempts do not establish
successful Gemini generation or four-agent coordination. All operation
assessments remained unapproved and no operational state change was executed.

`git diff --check` reports no whitespace errors; Git only prints its existing
Windows line-ending notices. No credentials or model response bodies are
recorded here.
