# G00 shared contract decision record — Marine Conditions & Safety Intelligence

**Gate:** G00 (shared contract freeze) as required by
[requirements-coverage-and-readiness.md](../requirements-coverage-and-readiness.md#g00-exit-criteria)
and the [member branch workflow](../member-branch-workflow.md).

**Status:** Accepted for Member 2's component scope on 2026-09-27.

**Owner:** Sanuda Abeysinghe (`@sanudaabey`) — frozen trace label Member 2.
Feature branch: `features/marine-safety`; paired Agentic AI branch
`agentic-ai/marine-conditions` remains post-G07.

This record freezes the shared decisions for Member 2's component before and
during its implementation. It is scoped to the marine-safety component; the
team-wide G00 record covering all four members is the maintainers'
responsibility, and cross-member contracts here describe only the
marine-safety side of each handoff. Decisions follow the eight G00 exit
criteria.

## Latest implementation verification — 2026-10-09

The implementation now consumes Member 1's published activity contract and
uses the API-issued request-bound actor context from ADR-0021. Activity CRUD
belongs exclusively to Member 1. Current checks: Marine backend **183/183**,
API gateway **54/54**, React **246/246**, Flutter **118/118**, Web build/lint and
both route/contract validators pass. Flutter analysis reports no issues.
CI helper tests pass **10/10** and UI validator tests pass **35/35**.
Five changed workflow YAML files and 21 Bash/default Ubuntu run blocks pass syntax checks. Compose
configuration and idempotent migration SQL generation pass; physical-device
runs remain separate evidence. Backend tests use EF InMemory and transport doubles;
PostgreSQL migration execution and a live Member 1 deployment are not verified.
Latest scoped Auth checks pass **80/80**. The Docker daemon is running; refreshed
Auth/frontend containers and public API/Auth/Marine health checks pass. Real
PostgreSQL readbacks verify missing Marine grant repair with existing permission
IDs, grants and account/session counts preserved across repeated startup. These
checks do not establish broader member integration or the optional Auth
PostgreSQL suite. Browser automation could not initialize; JSDOM and served
assets verify the header/entry behavior. No executable Agentic
AI, PR/merge or G07 acceptance is recorded. Historical revision notes retain
their original evidence.

## Branch review and repairs — 2026-10-08

The repeated branch review identified seven functional gaps. The subsequent
user-authorized repair fixes signed decimal coordinate editing, ambiguous mobile
write retries, condition-cache time matching, profile precision, read-only mobile
access, Docker trigger coverage and extreme-time validation. New requirement-based
regressions include actual Flutter shell navigation and provider time boundaries.
See the [branch review](../../testing/marine-branch-review-2026-10-08.md) for the
original evidence, resolution mapping and remaining integration limits. These
checks do not establish PostgreSQL runtime readiness or accepted G07.

## Independent dependency availability — 2026-10-08

The [availability contract](../../api/marine-dependency-availability.md) freezes
operation-level dependencies and additive 503 fields: `availability`,
`dependency`, `code`, `reason`, `missingFields`, `retryable` and null `data`.
Marine startup/own readiness, saved records and profile deactivation require no
other member service. Condition lookup requires only provider evidence. Profile
create/edit/review and evaluation require published activity data; unavailable
data blocks those operations without fabricated records or assessments.
Catalogue recovery restores dependent actions without restarting Marine.
Auth verification remains mandatory for protected public API operations.
Both clients preserve independent actions and use "unavailable" wording.
See [ADR-0022](../../adr/ADR-0022-member-service-data-availability-and-fault-isolation.md).

## 1. Owner and branch assignment (exit criterion 1)

| Item | Decision |
|---|---|
| Full name / GitHub account | Sanuda Abeysinghe / `@sanudaabey` |
| Feature branch | `features/marine-safety` (one branch, one component PR to `dev`) |
| Agentic AI branch | `agentic-ai/marine-conditions`, started only after G07 |
| Trace label | Member 2 (frozen for requirement traceability) |

## 2. Shared business identities and data authority (exit criterion 2)

| Decision | Record |
|---|---|
| Activity identity | Member 1 owns Activity CRUD and canonical opaque GUIDs. Marine consumes its implemented published `GET /api/experiences/activities` contract through `IExperienceCatalogueClient`; both clients fetch `/api/marine/activities`. Local rows are non-authoritative projections/legacy FK references, never a catalogue fallback. Existing placeholder IDs are not mapped by name; managers create and independently approve profiles for canonical IDs. |
| Activity reference to suitability | Profiles and assessments reference `ActivityId` (GUID). Consumer handoffs (Member 3 planner, Member 4 operations) receive `activityId`, the activity name, and the assessment result/evidence; they never re-derive suitability. |
| Condition/suitability data authority | Member 2 is the sole source of condition snapshots, freshness classification and deterministic suitability results. Consumers preserve `UNSUITABLE`/`UNKNOWN`/missing/stale meanings; no consumer recalculates or softens them. |
| Workflow identity | Marine suitability assessments are point-in-time evaluations, not long-running workflows; they carry an `assessmentId` (GUID) and `snapshotId` for correlation. No shared long-running workflow ID is required for this component pre-G07. |
| Open-Meteo ownership | Member 2 exclusively owns the Open-Meteo Weather and Marine adapter; no other component or client calls the provider. |

## 3. Public and private contracts (exit criterion 3)

| Decision | Record |
|---|---|
| Service identity | Folder `services/marine-safety`, project `Blueverse.MarineSafety`, container `blueverse-marine-safety`, internal base route `/api/marine` (short prefix chosen deliberately; the component keeps the `marine-safety` name for folder, project and container identity). |
| Public operation set | `GET /api/marine/health`, `GET /api/marine/current`, `GET /api/marine/snapshots/{id}`, `GET /api/marine/history`, `POST /api/marine/evaluate`, `GET /api/marine/assessments`, `GET /api/marine/assessments/{id}`, `GET /api/marine/activities`, `GET /api/marine/activities/{id}`, `GET|POST /api/marine/safety-profiles`, `GET|PUT|DELETE /api/marine/safety-profiles/{id}`, `POST /api/marine/safety-profiles/{id}/review`, `GET /api/marine/safety-profiles/by-activity/{activityId}`. Revision (12) removes Marine activity writes; the endpoint catalog is authoritative. |
| Route/transport | `services/api` forwards `/api/marine/{**catch-all}` over YARP to `http://marine-safety:8080` on the private network; clients never address the component service. No `/api/v1`-style segments. |
| Permission codes | `marine.profile.read` for condition/catalogue/history/evaluation reads and actions; profile creation/update/review/deactivation additionally needs `marine.profile.manage`. Auth owns current role assignments. The API verifies them through Auth `/api/auth/me`; Marine enforces the signed operation-context permissions, never caller JWT role claims or role names. |
| Authentication and session validity | The public API selects an explicit bearer token before Auth account cookies, fails malformed selections closed, and calls existing Auth `/api/auth/me` for current account/session/permission checks. It strips caller credentials and signs actor/permissions under the independent `MARINE_ACTOR_SIGNING_KEY`, binding method, path/query and body hash with 30-second expiry. Marine validates this context only and never reads Auth tables. Revoked/invalid sessions receive 401; missing current permission receives 403; unavailable Auth receives 503/Retry-After. See ADR-0021. |
| Swagger | The component serves its OpenAPI document under `/api/marine/swagger` (anonymous, gateway-forwarded). The public API Swagger UI at `/api/swagger` lists it beside the public API and Auth documents; the gateway forwards document requests over the existing `/api/marine` route. The component service is not modified for aggregation. |
| Error/status behavior | RFC 7807 ProblemDetails: 400 invalid input; 401 missing/invalid/revoked identity or internal context; 403 missing required permission; 404 absent/unpublished activity or unknown stored resource; 409 missing reviewed active profile or review conflict; 503 with structured availability metadata on provider, Auth, catalogue or Marine destination failure. Provider uses Retry-After 60; Auth/catalogue and gateway reads use 5. Ambiguous forwarded writes have retryable false and no retry hint. Profile delete deactivates. Marine activity writes no longer exist (405). |
| DTO ownership | Request/response DTOs live in the component service (`Blueverse.MarineSafety.Dtos`); EF entities are never exposed. Response bodies always carry source, forecast/retrieval times, freshness and missing fields where applicable. |
| Provider seam | `IOpenMeteoClient` is the only provider boundary. Result `MarineConditionsResult` carries nullable values plus `MissingFields`; failure is the typed `OpenMeteoUnavailableException`. Bounded retry (1, transient-only) and 10 s per-attempt timeout. |

## 4. Persistence and time (exit criterion 4)

| Decision | Record |
|---|---|
| Database sharing | One shared PostgreSQL 16 `blueverse` database. Each member service owns its own tables and EF Core migrations in the public schema; no separate database and no cross-service table writes. |
| Migration ownership | `services/marine-safety/Migrations`: InitialMarineSafetySchema, SafetyProfileReviewAndAssessmentEvidence, CanonicalActivityProjection. The latter preserves rows, removes name uniqueness and expands categories to 100 characters. Auth retains identity migrations. Startup applies migrations with a bounded timeout. |
| Table ownership | `MarineActivities`, `SafetyProfiles`, `ConditionSnapshots`, `SuitabilityAssessments` are owned by this service only. Other services must not read or write them; cross-component data moves through public API contracts. |
| Keys/constraints | GUID PKs; FK profile→activity (cascade); FK assessment→activity (restrict); CHECK constraints for positive limits and caution-below-max; partial unique index enforcing one active profile per activity; unique `(ActivityId, Version)`; numeric precision `numeric(8,5)`/`(9,5)` coordinates, `(6,2)`/`(5,2)` environmental values; `MissingFields text[]`. |
| Time semantics | All persisted and exchanged times are UTC (`timestamptz`). Open-Meteo is queried with `timezone=UTC`. The requested period is an ordinary query input (`dateTime`, optional; defaults to now). Acquisition/evaluation accepts UTC times from 0001-01-01T01:00:00Z through 9999-12-31T22:59:59.9999999Z to allow provider arithmetic; outside values receive 400 before dependency/persistence access. This arithmetic-safe range does not guarantee provider forecast availability. Historical read filters need no such arithmetic restriction. Freshness compares retrieval recency and forecast validity against the configured window — retrieval time and forecast time are distinct facts and both are stored. |
| Freshness policy | `FRESH` when `now − RetrievedAt ≤ FreshnessMaxAge` and `now − ForecastTime ≤ FreshnessMaxAge`; `STALE` otherwise; `UNAVAILABLE` only where no snapshot exists. Default window 60 minutes via `OpenMeteo:FreshnessMaxAgeMinutes` (documented implementation decision; the component contract deliberately sets no numeric limit). |
| Missing data | A provider field that is absent, malformed, negative, non-finite or unrepresentable in its database numeric column is reported in `MissingFields` and stored as NULL. Missing values are never coerced to zero or another safe value, and a missing required factor forces `UNKNOWN`. |
| Snapshot retention/reuse | A stored snapshot may be reused for matching rounded coordinates when its forecast lies within requested time minus 30 minutes through plus one hour (inclusive), matching provider acquisition. Retrieval must be within the preceding 15 minutes and cannot be in the future. The earliest applicable forecast is preferred; otherwise fresh acquisition is persisted. History endpoints classify freshness at read time. |
| Suitability rule vocabulary | `SUITABLE`, `CAUTION`, `UNSUITABLE`, `UNKNOWN`. `CAUTION` is produced only when the applied profile configures explicit caution bands; strict profiles yield three-state results. No implicit percentage rule. |
| Safety-profile evidence and review | Maximum and optional caution limits must be representable at scale two (at most two decimal places); excess precision receives 400 before persistence and values are never silently rounded. Numeric limits remain owner-supplied; no default live values are approved by this record. Every wind, wave and swell criterion requires its own source reference and rationale. New or changed limits are immutable inactive drafts. A different authorized manager must review and approve the draft before evaluation; evaluation returns 409 while no reviewed, source-complete active profile exists. The author, reviewer, review time and effective interval are recorded. Test-only example citations are not evidence for production limits. |

## 5. Service and delivery identity (exit criterion 5)

| Decision | Record |
|---|---|
| Compose service | `marine-safety`, image built from `infrastructure/docker/marine-safety/Dockerfile` (DHI `dhi.io/dotnet:10-sdk-alpine` / `aspnetcore:10-alpine`, exec-form entrypoint). |
| Networks | `blueverse_internal` (reachable by the API) + `blueverse_database` (PostgreSQL access). Not on the edge network; no published host port. |
| Configuration/secrets | `MARINE_ACTOR_SIGNING_KEY` shared only by API/Marine; `JWT_SIGNING_KEY` remains API/Auth-owned; `ConnectionStrings__DefaultConnection`; private `EXPERIENCE_CATALOGUE_URL`. Open-Meteo base URLs/freshness options retain documented defaults. No secrets in source/logs. |
| Health/readiness | `GET /api/marine/health` (anonymous, mirroring the Auth health contract) reports service liveness plus its own PostgreSQL connectivity with a bounded 2-second probe; database failure yields a structured 503. No other member service is required by startup/readiness. Marine operations' availability is additionally evidenced by their responses; dependency failures stay structured 503s rather than redefining API liveness. `GET /api/health` remains public-API liveness and is not redefined. The service Swagger UI under `/api/marine/swagger` is a development/contract-inspection surface registered in the catalog and listed in the public API Swagger UI. |
| Tests/CI | Test project `services/marine-safety/tests/Blueverse.MarineSafety.Tests` with requirement-linked case IDs (`M2-*`); discovered by the existing backend test workflows, which require every service directory to have a test project. |
| Local ports | Container-internal `8080`; no host port. Host-run verification may bind any free host port (e.g. 5085) and is not part of the deployment topology. |

## 6. Shared client and registry edits (exit criterion 6)

| Decision | Record |
|---|---|
| Registry edits | Endpoint catalog gains only the marine-safety entries; `backendSources` gains `services/marine-safety`. React Web registration (revision 6) adds the three marine workflow IDs, six React/Flutter route rows and seven workflow-linked endpoints to the same shared registries; no Auth or public API operation changed. |
| Client routes (before revision 6; superseded) | React and Flutter routes were to be registered under shared workflow IDs when client work was implemented. Revision 6 records the React implementation; the Flutter routes remain registered for the paired mobile implementation, which is not implemented yet. |
| Client routes (React/Flutter, revision 12) | React owns `/marine/conditions`, `/marine/history` and `/marine/safety-profiles` under the shared workflow IDs `marine-conditions`, `marine-condition-history` and `marine-safety-profile-management`, permission-gated by `marine.profile.read` (+ `marine.profile.manage` for profile writes) resolved from current role assignments. Flutter implements the same workflow routes, canonical activity lookup and authorized profile actions. The browser submits `latitude`/`longitude`/`time` and evaluate `dateTime` in UTC and renders — never recomputes — the server classification, source, timestamps, freshness and missing fields. |
| Shared files | Edits limited to: endpoint catalog JSON+MD, `services/api/appsettings.json` (YARP route), `services/api/Program.cs` (Swagger listing and narrow Marine gateway actor integration), API/Marine linked actor contract and Docker build context, `compose.yaml`, `.env` documentation, Auth permission codes/seeder and seeder test counts (approved additive change), README and database docs. No reformatting of unrelated shared content. |

## 7. Provider and evidence seams (exit criterion 7)

| Decision | Record |
|---|---|
| Open-Meteo request scope | Weather API: `wind_speed_10m`, `precipitation`, `weather_code` (km/h). Marine API: `wave_height`, `swell_wave_height`. No other variables are requested or stored. |
| Validation | Provider responses are validated for shape (hourly time series), timestamp parsing (UTC), value ranges (non-negative, finite) and field presence; failures degrade to `MissingFields` or the typed unavailable exception. Provider content never mutates configuration or authorization. |
| Safe fallback | Weather and Marine are fetched independently; a marine outage degrades only marine fields (they become missing → `UNKNOWN` suitability) rather than discarding weather evidence. Exhausted recovery yields an explicit unavailable outcome with `Retry-After`. |
| Live evidence | Component acceptance includes one genuine Open-Meteo acquisition and controlled fixtures for every error path; test doubles never count as live integration. |
| Snapshot persistence | Snapshots are persisted (not cache-only) to support history and audit; raw provider payloads are not stored, only the normalized validated fields plus provenance. |

## 8. Pre-G07 AI dependency behavior (exit criterion 8)

| Decision | Record |
|---|---|
| Scope | No agent, model call, tool or AI-owned state is implemented on this branch. The typed adapter seam for the future Marine Conditions Intelligence Agent is deferred with the AI work; its public contract will expose the validated condition/suitability evidence defined here read-only. |
| Status meanings | The repository-wide `NOT_CONNECTED` / `UNAVAILABLE` / `AVAILABLE` semantics from the integration-boundary contract apply when the seam is implemented post-G07. API liveness and database readiness stay separate from AI availability. |
| Prohibition | No AI output may alter thresholds, snapshots, profiles or a deterministic result. |

## Compliance checks

- Endpoint catalog and UI-integration validators pass after these decisions
  were implemented (53 public endpoints, 28 frontend routes; AI endpoints:
  none implemented).
- Revision 2026-09-27 (2): base route shortened to `/api/marine`; marine
  OpenAPI document listed in the public API Swagger UI. Marine permission
  codes are seeded by the Auth seeder (owner decision restored after the
  provisioner alternative was withdrawn); Auth keeps the grant seed data.
- Revision 2026-09-27 (3): full-service re-evaluation fixed three defects
  without changing any endpoint contract: (a) request timestamps without an
  explicit offset are now parsed as UTC (`MarineTime.ToUtc`) in every entry
  point instead of the host's local time zone, keeping the time-semantics row
  below true on any deployment host; (b) profile creation computes the next
  `Version` from all of the activity's profile rows, so recreating a profile
  after full deactivation cannot reuse version 1 and make past
  `ProfileVersion` references ambiguous; (c) PUT on a safety profile with
  `isActive=true` now explicitly supersedes any other active profile for the
  activity, upholding the one-active-profile invariant (and the partial unique
  index) instead of relying on never exercising that path. Regression tests
  M2-PROF-010/011/012 and M2-SUIT-011 pin all three behaviors; dead DTOs
  (`ActivityDto`, `ConditionsQueryDto`, `AssessmentHistoryDto`) were removed.
  Endpoint set, status codes and response shapes are unchanged.
- Revision 2026-09-27 (4): full test-suite expansion (38 → 87 tests) added
  direct provider-adapter tests, condition/suitability/profile edge cases,
  token-level security tests, an OpenAPI contract test and a CaseId
  uniqueness meta-test. Two defects surfaced and were fixed: (a) an absent
  `weather_code` variable was not reported in `MissingFields`, contradicting
  the missing-data row below — the adapter now reports it; (b) creating a
  safety profile for an inactive activity returned 400 while a nonexistent
  activity returned 409; both are activity-state conflicts and now map to
  409 consistently with the error/status row below. No endpoint, route or
  request/response-shape changes.
- Revision 2026-09-27 (5): finalization review hardened both supersede paths
  (profile create, and PUT reactivation) to commit the deactivations before
  the activation in separate saves, so every intermediate database state
  satisfies the partial unique (ActivityId, IsActive) index under real
  PostgreSQL — statement ordering inside a single SaveChanges is an EF
  implementation detail, not a contract. Final state: 87/87 marine tests,
  api/auth/marine builds clean, endpoint-catalog and UI validators OK.
- `dotnet build` and the full test suites pass; migration and Open-Meteo
  behavior were verified against real PostgreSQL and the live provider.
- Changes to shared files are limited to the entries listed in section 6.
- Revision 2026-09-27 (6): React Web client implemented for this component on
  the same branch. `apps/web` gained a member-specific `features/marine/`
  module (typed API adapter over the frozen public operations plus the frozen
  activity reference identities) and `pages/marine/MarinePages.tsx`
  (conditions + suitability, history, safety-profile pages in the established
  coastal design system), permission-gated routes, an account-navigation
  group and a dashboard quick-access link gated by `marine.profile.read`.
  Shared-file edits are limited to the registry/catalog updates above, the
  route table, the account navigation groups and README status lines. Both
  contract validators pass (46 public endpoints, 28 frontend routes); the web
  suite passes 187/187 cases including 30 new marine request-contract and
  component tests (normal, invalid, empty, malformed, denied, outage and
  transport-failure paths). The backend contract, DTOs, permissions and
  status semantics were not changed.
- Revision 2026-09-28 (7): the public operation set was expanded to full CRUD
  for the marine-safety component and the backend service was patched to
  match, with no change to any pre-existing operation, DTO shape, permission
  code or status semantic. Two gaps were closed. (a) The locally-owned
  `MarineActivities` reference table had no API surface at all; it now
  exposes manager-maintained CRUD — `GET|POST /api/marine/activities` and
  `GET|PUT|DELETE /api/marine/activities/{id}` — where DELETE deactivates
  (never hard-deletes: assessments hold a restrict FK reference and profiles
  a cascade reference, and the section 2 activity-identity row above was
  amended accordingly). Create/update require a unique name (duplicate name
  is a 409 state conflict) and map to the same read/manage permission pair
  as the safety-profile surface. (b) Suitability assessments were persisted
  by every evaluate call but were not queryable; read-only
  `GET /api/marine/assessments` (filters: `activityId`, `result`, `from`,
  `to`; newest first, bounded to 200 rows like condition history) and
  `GET /api/marine/assessments/{id}` now expose the stored evidence with
  profile-version and snapshot references. The DTO names
  `AssessmentHistoryDto` and an activity DTO family return as live contract
  types (revision (3) removed them as dead code; they are now bound to real
  operations). New tests M2-ACT-001..010 and M2-ASMT-001..006 pin the CRUD
  semantics, deactivation interplay and history queries; the OpenAPI
  contract test enumerates the expanded operation set. Final state: marine
  suite 103/103, api/auth/marine builds clean, endpoint-catalog and UI
  validators OK (53 public endpoints).
- Revision 2026-10-03 (8): marine authentication was aligned with the Auth
  session contract without changing Auth or gateway flows. The service now
  accepts bearer tokens and Auth-selected account cookies, validates current
  active-user/token-version/session-version state, and resolves permissions
  from live role assignments rather than JWT permission claims. Permission
  checks now honor request cancellation. No public route, permission code or
  response contract changed.
- Revision 2026-10-03 (9): recheck fixed bearer-header precedence over browser
  cookies and made malformed account selections fail closed. Seven focused
  cookie-selection cases were added; the marine suite passes 110/110 and the
  endpoint/UI validators pass. The in-memory suite still does not execute the
  production Auth-table SQL; PostgreSQL-backed query verification remains
  outstanding because this environment has no Docker CLI, local PostgreSQL
  listener, or configured Auth PostgreSQL test connection.
- Revision 2026-10-03 (10): expanded and reconciled the marine backend and
  React test suites. The backend now passes 117/117 tests; the complete React
  suite passes 198/198, including 41 marine-safety cases. React lint and the
  endpoint/UI contract validators pass. The Auth identity/permission tests
  still substitute deterministic resolvers and do not prove the production
  Auth-table SQL against PostgreSQL. Documentation was reconciled with the
  frozen G00 decisions and current implementation; Flutter, executable AI,
  cross-component integration, PR/merge and G07 remain open.
- Revision 2026-10-06 (11): completed the profile-review and assessment-evidence
  implementation across the backend, React Web and Flutter marine surfaces.
  Every new wind, wave and swell criterion requires a source and rationale;
  drafts are immutable, a different authorized manager must review them, and
  evaluation blocks until a reviewed active profile exists. Assessment history
  stores condition snapshots, exact criteria evidence and an explicit legacy
  completeness state. The React suite passes 201/201 (44 marine cases), the
  Web build and both contract validators pass, and the backend project builds
  cleanly. The source contains 118 backend test cases, but VSTest could not
  complete on this host because testhost startup timed out; PostgreSQL/Auth SQL
  and Flutter runtime verification remain open.

- Revision 2026-10-08 (12): user-requested Member 2 reimplementation adopts the
  published Member 1 activity contract verified in the local remote branch,
  removes duplicated Marine catalogue writes and fixed frontend IDs, and
  supplies a safe unavailable result until the producer is deployed. API
  integration now verifies existing Auth sessions/current permissions and
  passes the signed actor/operation context prescribed by ADR-0020; direct
  Marine Auth SQL is removed. Historical IDs/assessments remain readable.
  Both clients load activities with loading/empty/failure/retry states and
  offer independent conditions-only queries. Provider times outside the
  requested bounded window, numeric conversion overflow and unrepresentable
  database values become missing
  evidence. Existing tests were updated with explicit user permission; no
  passing assertions were relaxed. M2-SUIT-019 uses an unprofiled activity so
  its review gate is tested independently of the seeded approved profile.
  Full-current validation details are in the latest evidence section above.

- Revision 2026-10-08 (13): repaired all seven findings from the repeated branch
  review with explicit user authorization. Flutter preserves signed decimal
  coordinates, validates filters, offers all profile reads with read permission,
  and uses a 45-second request budget without automatically replaying ambiguous
  writes. Condition cache and provider acquisition share the bounded forecast
  window; excess profile precision and unsafe acquisition times return 400
  before side effects. Docker push/PR filters include the shared actor source.
  New regression cases preserve approved-profile history, assert persistence and
  provider side effects, navigate the actual mobile shell and verify uncancelled
  writes are not repeated. Current suite results and limitations are recorded
  above and in the branch review; no database model change or executable AI was
  introduced by these repairs.

- Revision 2026-10-08 (14): implemented dependency isolation for the standalone
  Marine branch. Optional catalogue misconfiguration no longer prevents startup;
  unavailable dependency errors carry structured missing-data metadata, preserve
  valid empty responses and produce no fabricated success. Both clients retain
  stored records/deactivation and disable only dependent actions until recovery.
  The public API preserves received service errors, reports unavailable Auth
  verification safely, and handles unreachable/timed-out Marine destinations
  without automatically retrying writes. Stack CI checks member OpenAPI only for
  present service sources. Current suite evidence is recorded above; absent
  component implementations, live deployment and G07 remain outside this branch.

- Revision 2026-10-08 (15): completed the React Web Marine experience on the
  existing three routes. Added accessible workflow navigation, clearer condition
  guidance and explanations of unchanged server results, factor-grouped cited
  profile forms and exact shared range/precision/caution validation. History
  refresh retains independently successful datasets and labels unavailable data;
  saved profile reads have explicit retry. Fourteen new Web cases cover outcomes,
  boundaries, no-write validation, recovery and read-only access. Current Web
  evidence is 224/224 plus lint/build and contract gates; backend/mobile evidence
  above is retained from the preceding scoped checks. No new business endpoint,
  permission, client capability asymmetry or executable agent is introduced.

- Revision 2026-10-08 (16): corrected the Admin header to show home-section
  links only on the actual home route, removed the empty Admin mobile menu and
  added a read-permission-gated Marine entry to the account menu. The running
  Admin had zero Marine grants: another member's catalogue permissions occupied
  Marine's preferred seed IDs. The essential Auth integration fix identifies
  permissions by code and allocates unused IDs for missing codes, preserving
  all existing rows, grants and Auth flows. Ten new Web and three Auth cases
  pass in full suites (234/234 and 80/80). Refreshed local containers serve the
  corrected app; actual PostgreSQL confirms Admin's 16 grants include both
  Marine permissions and repeat startup preserves identities/counts. No schema,
  public API route, role shortcut or executable agent was added. Full evidence
  and qualified deployment/build limitations are in the test matrix.

- Revision 2026-10-09 (17): added distinct editorial heroes to the three Marine
  React workflows using newly generated local coastal imagery. Responsive
  Tailwind layout keeps guidance on a solid panel and preserves existing forms,
  shared labels, permissions and public requests. Six new hero cases cover
  image semantics, page binding and image/dependency failure; existing tests
  remain intact. Full Web evidence is 240/240 with one worker after host memory
  exhaustion, plus lint/build and contract checks. The rebuilt local frontend
  serves all three new WebP images verified against build bytes. This is visual
  presentation work; mobile/business contracts and executable AI are unchanged.

- Revision 2026-10-09 (18): Marine React routes now share a persistent heading
  and introduction, followed by the unchanged pill navigation above the stacked
  hero and content. Tabs stick below the site header; content changes animate
  with reduced-motion support. Initial reads during Marine tab switches use
  inline feedback, as requested; initial entry, explicit retries/refreshes,
  mutations and Auth retain normal loading behavior. Permission and cookie/API
  contracts are preserved. Six new workspace cases cover slow/rapid navigation,
  history traversal, scrolling, keyboard focus, authorization, read errors,
  recovery and re-entry. Full Web evidence is 246/246 plus lint/build and
  endpoint/UI gates, including 35 validator tests. Actual browser visual
  inspection remains separate from JSDOM structure and served-asset checks.

- Revision 2026-10-09 (19): completed native mobile Marine integration with one
  shell, permission-gated Dashboard/account entry, shared pinned labels, bundled
  coastal heroes and reduced-motion transitions. Added the narrow Auth credential
  accessor so public Marine calls use the selected secure account; the HTTP
  transport is app-owned and closed, and protected UI resets on account/grant
  changes. Restored the registered root onboarding route and standard Dashboard
  landing. Conditions now show sourced values/units and preserve UNKNOWN without
  evidence; UTC calendar input is validated and refresh reads without another
  assessment write. History datasets recover independently, with labelled
  last-loaded records, and profile reads have explicit retry and honest empty/
  unavailable/caution presentation. Existing profile draft/version, independent
  review and confirmed deactivation remain server-authorized. Full Flutter
  evidence is 118/118 and clean analysis, plus endpoint/UI gates and 35 validator
  cases. Android packaging/device verification is separate: the attempted debug
  build did not produce output and was stopped. No new backend endpoint, device
  capability, Activity CRUD ownership change or executable agent was introduced.
