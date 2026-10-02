# Test Matrix

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

The implementation plan and test-directory convention are documented in
[`implementation-plan.md`](implementation-plan.md). The entries below describe
the minimum evidence expected as each component is introduced.

| Area | Minimum evidence |
|---|---|
| API | `services/api/tests` covers current foundation behavior with stable cases `API-HEALTH-*`, `API-CONTRACT-*`, `API-CORS-*`, `API-EDGE-*`, `API-ERROR-*`, `API-PROXY-*` and `API-AUTH-*`: unit/HTTP health, non-versioned routing, public/Auth OpenAPI routing, CORS allow/deny/preflight, forwarded headers, safe gateway errors, JWT issuer/audience/lifetime/algorithm/cookie allow/deny, signed Coastal Operations actor-context forwarding with client-credential stripping, and YARP forwarding/failure isolation. The current test source defines 22 cases. |
| Authentication | `services/auth/tests` currently defines 79 default cases for registration/login, validation and malformed-input problem details, server-issued installations and device proof, native/cookie transport and cookie cleanup, bearer-over-stale-cookie precedence (`AUTH-JWT-BEARER-COOKIE-001`), rotating refresh tokens and replay/expiry failures, one/30-day expiry, per-session revocation and cross-account isolation, active-session archival and refresh-token relinking, five-session account eviction, five-account device capacity, protected endpoints and safe errors, account/device/everywhere logout, profile-only updates, complete user/role/permission administration, administrative mutation authorization, strict assignment validation, system-role escalation, bootstrap seeding, idempotent Admin System Role grants for every registered permission (`AUTH-SEED-ADMIN-ALL-001`), token revocation, password hashing, JWT/configuration boundaries and persistence-model constraints. |
| Authorization | Permission allow/deny, policy-provider behavior, catalog isolation, administrative mutation protection and system-role escalation tests in `services/auth/tests` |
| PostgreSQL | Checked-in Auth EF Core model/migrations, including device installations and refresh-token rotation; `AUTH-POSTGRES-SESSION-001` exercises real-provider refresh replay and concurrent capacity decisions when explicitly enabled, while local live migration and gateway smoke checks provide deployment evidence. Provider-independent tests may use the isolated test provider; they do not replace PostgreSQL evidence. |
| React | `apps/web` uses Node 24 `node:test`; `npm run lint`, `npm run build` and `npm run test:ci` are the package gates. The complete current suite passed 185 cases (2026-10-02), including the approved focused-workspace and Logs/evidence follow-ups, using serial execution on this Windows host. Request and state tests cover public Auth/Admin API contracts, cookie semantics, session restoration, account capacity, authorization and error mapping (`WEB-AUTH-*`, `WEB-AUTH-REQ-*`, `WEB-AUTH-SESSION-*`, `WEB-ADMIN-API-*`, `WEB-MULTI-ACCOUNT-*`, `AUTH-NAV-*`). JSDOM/React Testing Library cases cover sign-in/registration, home/dashboard, profile/security/session/delete workflows, permission-aware administration, desktop/mobile navigation, protected/recovery routes, app notices, loading feedback, route scrolling and footer behavior (`WEB-UI-*`, `WEB-ADMIN-UI-*`, `WEB-ROUTE-*`, `WEB-ERROR-*`, `WEB-LOADING-*`). Request calls are deterministic stubs against registered public `/api/...` routes. The suite does not replace real-browser or deployed-gateway end-to-end evidence. |
| Flutter | Coverage includes gateway/configuration/build-define, loading/error screens, signed-out launch (`MOB-LAUNCH-*`), `MOB-AUTH-*` Auth API and credential/session boundaries, `MOB-ADMIN-*` public administration requests, and `MOB-AUTH-UI-*`, `MOB-REGISTER-*`, `MOB-HOME-*`, `MOB-DASH-*`, `MOB-PROFILE-*`, `MOB-VM-*`, `MOB-ADMIN-UI-*` onboarding, forms, navigation, profile/security/session actions, permissions and administration. The complete current suite passed 113 cases and Flutter analysis reported no issues (2026-10-02), using the installed SDK and locked dependencies. Signed-out launch is covered by MOB-LAUNCH-001. The existing Auth API test file also reuses `MOB-AUTH-011`, `012` and `013`; these labels need a separately approved correction before the ID registry is unique. No `integration_test` suite is checked in; add device tests when platform behavior requires them. |
| Backend services | Matching `services/<service-name>/tests` project for every service under `services/` |
| v1 Ushan Srinuka (Member 1) | Destination/activity/offering publication and availability, schedules, favourites, location-aware discovery, and React/Flutter rendering of Adithya Gunawardana's sourced biodiversity metadata and unavailable/invalid states through the public contract; backend, PostgreSQL, clients and post-G07 agent evidence. Target, not implemented. |
| v1 Sanuda Abeysinghe (Member 2) | Open-Meteo acquisition and failure, source/time/freshness, safety profiles and deterministic activity suitability; backend, PostgreSQL, React, Flutter and agent evidence. Target, not implemented. |
| v1 Adithya Gunawardana (Member 3) | Recommendation constraints, validated planning/delegation, genuine backend-mediated IT3091 inference and public prediction contract, provenance/uncertainty/unavailable/invalid behavior, itinerary CRUD and re-evaluation, unsuitable exclusion and uncertainty; backend, PostgreSQL, React, Flutter and post-G07 agent evidence. Target, not implemented. |
| v1 Wanshaja Sooriyabandara (Member 4) | Branch-local assessment and alert draft CRUD/publication, reviewer decisions, target state/history, private evidence, scoped activity/search and durable publication delivery are implemented. The service suite passes 322 default cases; five opt-in PostgreSQL cases remain skipped without the dedicated test connection. React and Flutter now expose dedicated Assessment/Alert pages and share draft, publication, authorization and activity capabilities. The prior record-experience client baseline was 178 web and 107 mobile cases; the approved follow-up is recorded below. Controlled transports verify retries/timeouts and safe outcomes; EF model/migration checks do not prove live PostgreSQL transactions or concurrency. Shared G00 producer acceptance and post-G07 live agents remain pending. See the publication change contract above for current evidence and limits. |
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
