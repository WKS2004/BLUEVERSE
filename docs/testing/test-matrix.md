# Test Matrix

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
| Gates | G00 accepted per Adithya's explicit confirmation, with provenance recorded. Actual peer services and accepted/live IT3091 wire/model/retention evidence remain unavailable. G07 and executable AI remain deferred. HTTP local mobile configuration retained at user request. |

## Coastal Planner branch evidence — 2026-10-05

| Check | Evidence and limits |
|---|---|
| Planner backend | 106 default cases passed, covering named catalogue projection, schedule intersection, source freshness, experience matching, ownership, authorization, snapshot expiry, stable stop identity, duplicate/overlap validation, bounded dependency failure, failed workflow persistence, review history and the non-executing AI seam. The new catalogue/history HTTP cases assert permissions, schemas, headers and persisted ownership. |
| Auth/API regression | 80 Auth cases and 21 API cases passed. New Auth cases verify idempotent planner seeding and enabled/disabled traveller assignment with issued permission claims and persisted sessions. |
| React | Full 201-case suite passed with two test workers after an unconstrained parallel run exhausted local memory during Docker builds. New coverage includes saved-trip append, stable IDs/current version, conflict draft retention, tab-close protection, review evidence, undo, deletion confirmation, time-zone boundaries and malformed response guards. Lint and production build passed. |
| PostgreSQL | Explicit `PLANNER-POSTGRES-001` uses a unique disposable database for migration upgrade, real unique/check constraints, reorder, stale writes and history cascade. It is enabled separately; no provider evidence is inferred from in-memory tests. Commands are in the planner guide. |
| Integration limits | Missing owner services prevent live catalogue/conditions happy-path checks. Flutter planner screens and post-G07 AI execution remain pending. Component tests use deterministic peer replies; they do not prove live peer compatibility. |

See [planner operations and verification](../development/coastal-planner.md).
The baseline descriptions below preserve earlier scope; the dated evidence
above records this feature branch's additional coverage.

The implementation plan and test-directory convention are documented in
[`implementation-plan.md`](implementation-plan.md). The entries below describe
the minimum evidence expected as each component is introduced.

| Area | Minimum evidence |
|---|---|
| API | `services/api/tests` covers current foundation behavior with stable cases `API-HEALTH-*`, `API-CONTRACT-*`, `API-CORS-*`, `API-EDGE-*`, `API-ERROR-*`, `API-PROXY-*` and `API-AUTH-*`: unit/HTTP health, non-versioned routing, public/Auth OpenAPI routing, CORS allow/deny/preflight, forwarded headers, safe gateway errors, JWT issuer/audience/lifetime/algorithm/cookie allow/deny and YARP forwarding/failure isolation. The current test source defines 21 cases. |
| Authentication | `services/auth/tests` currently defines 77 default cases for registration/login, validation and malformed-input problem details, server-issued installations and device proof, native/cookie transport and cookie cleanup, rotating refresh tokens and replay/expiry failures, one/30-day expiry, per-session revocation and cross-account isolation, active-session archival and refresh-token relinking, five-session account eviction, five-account device capacity, protected endpoints and safe errors, account/device/everywhere logout, profile-only updates, complete user/role/permission administration, administrative mutation authorization, strict assignment validation, system-role escalation, bootstrap seeding, token revocation, password hashing, JWT/configuration boundaries and persistence-model constraints. |
| Authorization | Permission allow/deny, policy-provider behavior, catalog isolation, administrative mutation protection and system-role escalation tests in `services/auth/tests` |
| PostgreSQL | Checked-in Auth EF Core model/migrations, including device installations and refresh-token rotation; `AUTH-POSTGRES-SESSION-001` exercises real-provider refresh replay and concurrent capacity decisions when explicitly enabled. Coastal Planner owns `services/coastal-planner/Data/Migrations` and has a generated initial migration with schema, uniqueness, ordering and time-range constraints; live PostgreSQL apply/concurrency evidence is still required. Provider-independent tests may use an isolated provider; they do not replace PostgreSQL evidence. |
| React | `apps/web` uses Node 24 `node:test`; `npm run lint`, `npm run build` and `npm run test:ci` are the package gates. The current baseline passes 157 cases (2026-09-25). Request and state tests cover public Auth/Admin API contracts, cookie semantics, session restoration, account capacity, authorization and error mapping (`WEB-AUTH-*`, `WEB-AUTH-REQ-*`, `WEB-AUTH-SESSION-*`, `WEB-ADMIN-API-*`, `WEB-MULTI-ACCOUNT-*`, `AUTH-NAV-*`). JSDOM/React Testing Library cases cover sign-in/registration, home/dashboard, profile/security/session/delete workflows, permission-aware administration, desktop/mobile navigation, protected/recovery routes, app notices, loading feedback, route scrolling and footer behavior (`WEB-UI-*`, `WEB-ADMIN-UI-*`, `WEB-ROUTE-*`, `WEB-ERROR-*`, `WEB-LOADING-*`). Request calls are deterministic stubs against registered public `/api/...` routes. The suite does not replace real-browser or deployed-gateway end-to-end evidence. |
| Flutter | Coverage includes gateway/configuration/build-define, loading/error screens, signed-out launch (`MOB-LAUNCH-*`), `MOB-AUTH-*` Auth API and credential/session boundaries, `MOB-ADMIN-*` public administration requests, and `MOB-AUTH-UI-*`, `MOB-REGISTER-*`, `MOB-HOME-*`, `MOB-DASH-*`, `MOB-PROFILE-*`, `MOB-VM-*`, `MOB-ADMIN-UI-*` onboarding, forms, navigation, profile/security/session actions, permissions and administration. Historical local evidence from 2026-09-25: 86 selected tests passed across 12 then-runnable files and `flutter analyze --no-pub lib` passed; `apps/mobile/test/widget_test.dart` was then excluded because it referenced removed `MyHomePage`. That test has since been corrected as `MOB-LAUNCH-001` to cover the current signed-out launch-to-onboarding flow. The final documentation audit confirmed no remaining `MyHomePage` reference and clean Dart formatting, but could not run Flutter analysis/tests because the sandbox cannot write the SDK cache lockfile under `Program Files`; the revised test still needs green CI evidence. The existing Auth API test file also reuses `MOB-AUTH-011`, `012` and `013`; these labels need a separately approved correction before the ID registry is unique. No `integration_test` suite is checked in; add device tests when platform behavior requires them. |
| Backend services | Matching `services/<service-name>/tests` project for every service under `services/`. Coastal Planner has 20 package-local tests (`PLANNER-PEER-*` for timeout, retry, cancellation, configuration, contract validation and future marine forecasts; `PLANNER-REC-*`; `PLANNER-BIO-*`; `PLANNER-ITINERARY-*`; `PLANNER-VALIDATION-*`; `PLANNER-REEVAL-*`; `PLANNER-AUTH-*`) covering bounded peer failure, safe unavailable results, deterministic eligibility, persistence, owner scoping and permission allow/deny. The backend workflow discovers test projects under `services/**/tests`. |
| v1 Ushan Srinuka (Member 1) | Destination/activity/offering publication and availability, schedules, favourites, location-aware discovery, and React/Flutter rendering of Adithya Gunawardana's sourced biodiversity metadata and unavailable/invalid states through the public contract; backend, PostgreSQL, clients and post-G07 agent evidence. Target, not implemented. |
| v1 Sanuda Abeysinghe (Member 2) | Open-Meteo acquisition and failure, source/time/freshness, safety profiles and deterministic activity suitability; backend, PostgreSQL, React, Flutter and agent evidence. Target, not implemented. |
| v1 Adithya Gunawardana (Member 3) | Coastal Planner backend behavior and package-local tests are implemented on the feature branch: bounded optional peer calls, explicit unavailable states, deterministic eligibility, owner-scoped reads, permission claims, itinerary persistence/re-evaluation, and validated ML caching. A real PostgreSQL apply check, remaining client workflows, full-team G00 record and shared acceptance are still pending. |
| v1 Wanshaja Sooriyabandara (Member 4) | Assessment, proposal, operational state, approval/reject/revision, alerts, audit, concurrency and safe failure; backend, PostgreSQL, React, Flutter and agent evidence. Target, not implemented. |
| UI integration contract | Dependency-free registry/validator checks that every product workflow has both React and Flutter routes and that client API literals resolve to declared public `/api/...` endpoints; role coverage and platform parity are verified in workflow tests |
| Integration | React/Flutter against the same public API, gateway routing and cross-platform workflow; no client-to-client or internal-service calls |
| Agentic AI | Contract, tool, safety and evaluation evidence for all four [v1 agent roles](../v1/README.md), plus the complete assessed workflow under each owning AI service’s local `tests/` directory. No executable agents currently exist. |
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

### React planner navigation correction — 2026-10-12

`WEB-PLANNER-UI-030` now verifies that the top desktop/mobile menus exclude
creation/history links and that the shared account sidebar opens both routes,
marks the current action and preserves direct authorized navigation. All 221
React tests, build and lint passed; all 42 validator cases and the endpoint/UI
catalogs passed. Isolated Edge rechecked intro, creation and history at
1440/768/390px with labelled synthetic API fixtures and no horizontal overflow.
