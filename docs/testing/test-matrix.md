# Test Matrix

The implementation plan and test-directory convention are documented in
[`implementation-plan.md`](implementation-plan.md). The entries below describe
the minimum evidence expected as each component is introduced.

| Area | Minimum evidence |
|---|---|
| API | `services/api/tests` covers current foundation behavior with stable cases `API-HEALTH-*`, `API-CONTRACT-*`, `API-CORS-*`, `API-EDGE-*`, `API-ERROR-*`, `API-PROXY-*` and `API-AUTH-*`: unit/HTTP health, non-versioned routing, public/Auth OpenAPI routing, CORS allow/deny/preflight, forwarded headers, safe gateway errors, JWT issuer/audience/lifetime/algorithm/cookie allow/deny and YARP forwarding/failure isolation. These foundation groups define 21 cases; the full API suite, including Marine integration, passes 54/54. |
| Authentication | `services/auth/tests` currently defines 80 default cases for registration/login, validation and malformed-input problem details, server-issued installations and device proof, native/cookie transport and cookie cleanup, rotating refresh tokens and replay/expiry failures, one/30-day expiry, per-session revocation and cross-account isolation, active-session archival and refresh-token relinking, five-session account eviction, five-account device capacity, protected endpoints and safe errors, account/device/everywhere logout, profile-only updates, complete user/role/permission administration, administrative mutation authorization, strict assignment validation, system-role escalation, bootstrap seeding, token revocation, password hashing, JWT/configuration boundaries and persistence-model constraints. |
| Authorization | Permission allow/deny, policy-provider behavior, catalog isolation, administrative mutation protection and system-role escalation tests in `services/auth/tests` |
| PostgreSQL | Checked-in Auth EF Core model/migrations, including device installations and refresh-token rotation; `AUTH-POSTGRES-SESSION-001` exercises real-provider refresh replay and concurrent capacity decisions when explicitly enabled, while local live migration and gateway smoke checks provide deployment evidence. Provider-independent tests may use the isolated test provider; they do not replace PostgreSQL evidence. |
| React | `apps/web` uses Node 24 `node:test`; `npm run lint`, `npm run build` and `npm run test:ci` are the package gates. The full suite passes 246 cases (2026-10-09), using one worker after host memory exhaustion with parallel processes. Request and state tests cover public Auth/Admin API contracts, cookie semantics, session restoration, account capacity, authorization and error mapping (`WEB-AUTH-*`, `WEB-AUTH-REQ-*`, `WEB-AUTH-SESSION-*`, `WEB-ADMIN-API-*`, `WEB-MULTI-ACCOUNT-*`, `AUTH-NAV-*`). JSDOM/React Testing Library cases cover sign-in/registration, home/dashboard, profile/security/session/delete workflows, permission-aware administration, desktop/mobile navigation, protected/recovery routes, app notices, loading feedback, route scrolling and footer behavior (`WEB-UI-*`, `WEB-ADMIN-UI-*`, `WEB-ROUTE-*`, `WEB-ERROR-*`, `WEB-LOADING-*`). Marine-safety coverage contributes 89 cases: 6 workspace/tab cases (`WEB-MARINE-TABS-*`), 6 hero cases (`WEB-MARINE-HERO-*`), 10 entry/navigation cases (`WEB-MARINE-ENTRY-*`), 24 public API request/error cases (`WEB-MARINE-API-*`, `WEB-MARINE-022..023`) and 43 page/workflow cases (`WEB-MARINE-UI-*`) covering permission denial, cookie credentials, expired-session recovery, provider failure, suitability outcomes, history filters, legacy evidence gaps, profile validation and manager actions. Calls use deterministic stubs against registered public `/api/...` routes. The suite does not replace real-browser or deployed-gateway end-to-end evidence. |
| Flutter | Coverage includes gateway/configuration/build-define, loading/error screens, signed-out launch (`MOB-LAUNCH-*`), `MOB-AUTH-*` Auth API and credential/session boundaries, `MOB-ADMIN-*` public administration requests, and `MOB-AUTH-UI-*`, `MOB-REGISTER-*`, `MOB-HOME-*`, `MOB-DASH-*`, `MOB-PROFILE-*`, `MOB-VM-*`, `MOB-ADMIN-UI-*` onboarding, forms, navigation, profile/security/session actions, permissions and administration. Historical local evidence from 2026-09-25: 86 selected tests passed across 12 then-runnable files and `flutter analyze --no-pub lib` passed; `apps/mobile/test/widget_test.dart` was then excluded because it referenced removed `MyHomePage`. That test has since been corrected as `MOB-LAUNCH-001` to cover the current signed-out launch-to-onboarding flow. The 2026-10-08 dependency-isolation verification ran the complete current suite: 103/103 pass, including the corrected launch test and Marine regressions; Flutter analysis reports no issues. Hosted CI/device behavior remains separate evidence. The existing Auth API test file also reuses `MOB-AUTH-011`, `012` and `013`; these labels need a separately approved correction before the ID registry is unique. No `integration_test` suite is checked in; add device tests when platform behavior requires them. |
| Backend services | Matching `services/<service-name>/tests` project for every service under `services/` |
| v1 Ushan Srinuka (Member 1) | Destination/activity/offering publication and availability, schedules, favourites, location-aware discovery, and React/Flutter rendering of Adithya Gunawardana's sourced biodiversity metadata and unavailable/invalid states through the public contract; backend, PostgreSQL, clients and post-G07 agent evidence. Target, not implemented. |
| v1 Sanuda Abeysinghe (Member 2) | Marine backend, React and Flutter implementation is present. The latest scoped verification passes Marine 183/183, API 54/54, React 246/246 and Flutter 129/129; Web lint/build and Flutter analysis pass. Marine uses EF InMemory and Auth/catalogue/provider transports, not live PostgreSQL. The Auth default provider-independent suite passes 80/80; optional PostgreSQL tests are not enabled. Seven branch-review findings are repaired. Local Auth/frontend containers were refreshed, public API/Auth/Marine health checks pass, and real PostgreSQL readbacks verify permission seeding preservation and repeat-startup idempotency. Broader PostgreSQL constraints/concurrency and deployed member/device integration remain separate evidence. G07 Agentic AI execution remains gated. |
| v1 Adithya Gunawardana (Member 3) | Recommendation constraints, validated planning/delegation, genuine backend-mediated IT3091 inference and public prediction contract, provenance/uncertainty/unavailable/invalid behavior, itinerary CRUD and re-evaluation, unsuitable exclusion and uncertainty; backend, PostgreSQL, React, Flutter and post-G07 agent evidence. Target, not implemented. |
| v1 Wanshaja Sooriyabandara (Member 4) | Assessment, proposal, operational state, approval/reject/revision, alerts, audit, concurrency and safe failure; backend, PostgreSQL, React, Flutter and agent evidence. Target, not implemented. |
| UI integration contract | Dependency-free registry/validator checks that every product workflow has both React and Flutter routes and that client API literals resolve to declared public `/api/...` endpoints; role coverage and platform parity are verified in workflow tests |
| Integration | React/Flutter against the same public API, gateway routing and cross-platform workflow; no client-to-client or internal-service calls |
| Agentic AI | Contract, tool, safety and evaluation evidence for all four [v1 agent roles](../v1/README.md), plus the complete assessed workflow under each owning AI service’s local `tests/` directory. No executable agents currently exist. |
| Security | Unauthorized/invalid-input/prompt-injection cases with complete assertions: expected HTTP outcomes across relevant `1xx`–`5xx` statuses plus body/schema, authorization, state and side-effect validation |
| CI | Passing source, client, backend, AI and integration workflows; backend test discovery fails when a service source project has no matching local test project |
| Deployment | Health + smoke-test evidence |

Every row requires scenario-based coverage, not a single happy-path case:
nominal passing behavior, invalid/rejected behavior, relevant boundaries,
extreme values, malformed or missing input, and applicable concurrency,
dependency, authorization, retry and platform variations. New behavior and its
tests are delivered in the same change. Tests remain in their owning
framework/service default directories; the repository must not grow a
centralized root `test/` tree for these cases.

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
