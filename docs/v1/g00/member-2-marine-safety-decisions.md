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

## Latest implementation verification — 2026-10-03

The marine service test suite passes **117/117**. The full React Web suite
passes **198/198**, including **41 marine-safety cases**; React lint and the
shared UI-integration validator pass. The service tests use an in-memory
database and deterministic Auth/provider doubles, so this run does not verify
the production Auth-table SQL against PostgreSQL. Flutter and executable
Agentic AI remain unimplemented, and component PR/merge plus G07 evidence are
not recorded. The historical revision notes below retain their original
counts and dates.

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
| Activity identity | Member 1 owns the canonical coastal activity taxonomy. Until that component's service exists, Member 2 keeps a locally-owned `MarineActivities` reference table seeded with the agreed coastal activities (Surfing, Snorkeling, Scuba Diving, Whale & Dolphin Watching, Coastal Boat Tour) with fixed seed GUIDs (`33333333-3333-…-3301`…`3305`). Revision (7) expanded the reference table to a manager-maintained CRUD surface (see section 3): rows beyond the seeds can be added, renamed, retyped and deactivated, while the table stays a reference table — no competing catalogue features (descriptions, publication, scheduling) are added. When Member 1's canonical IDs are available, the reference table and its seed are replaced by a mapping to those IDs. |
| Activity reference to suitability | Profiles and assessments reference `ActivityId` (GUID). Consumer handoffs (Member 3 planner, Member 4 operations) receive `activityId`, the activity name, and the assessment result/evidence; they never re-derive suitability. |
| Condition/suitability data authority | Member 2 is the sole source of condition snapshots, freshness classification and deterministic suitability results. Consumers preserve `UNSUITABLE`/`UNKNOWN`/missing/stale meanings; no consumer recalculates or softens them. |
| Workflow identity | Marine suitability assessments are point-in-time evaluations, not long-running workflows; they carry an `assessmentId` (GUID) and `snapshotId` for correlation. No shared long-running workflow ID is required for this component pre-G07. |
| Open-Meteo ownership | Member 2 exclusively owns the Open-Meteo Weather and Marine adapter; no other component or client calls the provider. |

## 3. Public and private contracts (exit criterion 3)

| Decision | Record |
|---|---|
| Service identity | Folder `services/marine-safety`, project `Blueverse.MarineSafety`, container `blueverse-marine-safety`, internal base route `/api/marine` (short prefix chosen deliberately; the component keeps the `marine-safety` name for folder, project and container identity). |
| Public operation set | `GET /api/marine/health`, `GET /api/marine/current`, `GET /api/marine/snapshots/{id}`, `GET /api/marine/history`, `POST /api/marine/evaluate`, `GET /api/marine/assessments`, `GET /api/marine/assessments/{id}`, `GET|POST /api/marine/activities`, `GET|PUT|DELETE /api/marine/activities/{id}`, `GET|POST /api/marine/safety-profiles`, `GET|PUT|DELETE /api/marine/safety-profiles/{id}`, `GET /api/marine/safety-profiles/by-activity/{activityId}` — registered in `docs/api/endpoint-catalog.json`. Revision (7) added the activity reference-table CRUD and the read-only assessment-history surface; every other operation is unchanged. |
| Route/transport | `services/api` forwards `/api/marine/{**catch-all}` over YARP to `http://marine-safety:8080` on the private network; clients never address the component service. No `/api/v1`-style segments. |
| Permission codes | `marine.profile.read` (condition reads, history, evaluate, assessment reads, activity reads), `marine.profile.manage` in addition to read (profile and activity create/update/deactivate). Codes are defined in Auth's `PermissionCodes`, seeded for the Admin role by the Auth seeder (approved additive change), and resolved from current role assignments (never JWT claims). Enforced in the component service via the `PERMISSION:<code>` policy convention. The marine-safety service mirrors the constants locally for enforcement; provisioning belongs to Auth. |
| Authentication and session validity | The service accepts the Auth-issued signed JWT as a bearer token or through Auth browser cookies, preferring an explicit bearer header when both are present. `blueverse_active_account_id` selects `blueverse_access_token_<userIdN>`; a legacy token is accepted only if its subject matches that selection. A malformed selection fails closed; without a selection, the legacy single-account cookie remains supported. The service validates signature, issuer, audience, HS256 algorithm and lifetime, then checks the current Auth user (`IsActive`, `TokenVersion`) and exact unexpired `ActiveSessions` row (`session_id`, `session_version`). A revoked, expired or stale session is rejected with 401; a valid session without the current required permission receives 403. The component performs read-only checks; Auth remains the only identity/session writer. |
| Swagger | The component serves its OpenAPI document under `/api/marine/swagger` (anonymous, gateway-forwarded). The public API Swagger UI at `/api/swagger` lists it beside the public API and Auth documents; the gateway forwards document requests over the existing `/api/marine` route. The component service is not modified for aggregation. |
| Error/status behavior | RFC 7807 ProblemDetails. 400 invalid input (coordinates, ranges, missing fields), 401 unauthenticated or expired/revoked/stale Auth session, 403 an active caller lacking a required permission grant, 404 unknown activity/snapshot/profile/assessment, 409 no active profile for an activity or a duplicate activity name, 503 with `Retry-After` when Open-Meteo is unavailable. Profile and activity delete semantics: delete deactivates, never hard-deletes. |
| DTO ownership | Request/response DTOs live in the component service (`Blueverse.MarineSafety.Dtos`); EF entities are never exposed. Response bodies always carry source, forecast/retrieval times, freshness and missing fields where applicable. |
| Provider seam | `IOpenMeteoClient` is the only provider boundary. Result `MarineConditionsResult` carries nullable values plus `MissingFields`; failure is the typed `OpenMeteoUnavailableException`. Bounded retry (1, transient-only) and 10 s per-attempt timeout. |

## 4. Persistence and time (exit criterion 4)

| Decision | Record |
|---|---|
| Database sharing | One shared PostgreSQL 16 `blueverse` database. Each member service owns its own tables and EF Core migrations in the public schema; no separate database and no cross-service table writes. |
| Migration ownership | `services/marine-safety/Migrations` (initial: `InitialMarineSafetySchema`). Auth keeps identity migrations; the public API owns no domain tables. Migrations apply at service startup with a bounded timeout. |
| Table ownership | `MarineActivities`, `SafetyProfiles`, `ConditionSnapshots`, `SuitabilityAssessments` are owned by this service only. Other services must not read or write them; cross-component data moves through public API contracts. |
| Keys/constraints | GUID PKs; FK profile→activity (cascade); FK assessment→activity (restrict); CHECK constraints for positive limits and caution-below-max; partial unique index enforcing one active profile per activity; numeric precision `numeric(8,5)`/`(9,5)` coordinates, `(6,2)`/`(5,2)` environmental values; `MissingFields text[]`. |
| Time semantics | All persisted and exchanged times are UTC (`timestamptz`). Open-Meteo is queried with `timezone=UTC`. The requested period is an ordinary query input (`dateTime`, optional; defaults to now). Freshness compares retrieval recency and forecast validity against the configured window — retrieval time and forecast time are distinct facts and both are stored. |
| Freshness policy | `FRESH` when `now − RetrievedAt ≤ FreshnessMaxAge` and `now − ForecastTime ≤ FreshnessMaxAge`; `STALE` otherwise; `UNAVAILABLE` only where no snapshot exists. Default window 60 minutes via `OpenMeteo:FreshnessMaxAgeMinutes` (documented implementation decision; the component contract deliberately sets no numeric limit). |
| Missing data | A provider field that is absent, malformed, negative or non-finite is reported in `MissingFields` and stored as NULL. Missing values are never coerced to zero or another safe value, and a missing required factor forces `UNKNOWN`. |
| Snapshot retention/reuse | A stored snapshot may be reused for the same rounded coordinate and hour within a 15-minute retrieval-recency window; otherwise a fresh acquisition is made and persisted. History endpoints classify freshness at read time. |
| Suitability rule vocabulary | `SUITABLE`, `CAUTION`, `UNSUITABLE`, `UNKNOWN`. `CAUTION` is produced only when the applied profile configures explicit caution bands; strict profiles yield three-state results. No implicit percentage rule. |

## 5. Service and delivery identity (exit criterion 5)

| Decision | Record |
|---|---|
| Compose service | `marine-safety`, image built from `infrastructure/docker/marine-safety/Dockerfile` (DHI `dhi.io/dotnet:10-sdk-alpine` / `aspnetcore:10-alpine`, exec-form entrypoint). |
| Networks | `blueverse_internal` (reachable by the API) + `blueverse_database` (PostgreSQL access). Not on the edge network; no published host port. |
| Configuration/secrets | `JWT_SIGNING_KEY`, `ConnectionStrings__DefaultConnection` via Compose environment; `OPEN_METEO_WEATHER_URL`, `OPEN_METEO_MARINE_URL`, `OPEN_METEO_FRESHNESS_MINUTES` optional with documented defaults. No provider API key required. No secrets in source or logs. |
| Health/readiness | `GET /api/marine/health` (anonymous, mirroring the Auth health contract) reports service liveness plus PostgreSQL connectivity with a bounded 2-second probe; database failure yields a structured 503. Marine operations' availability is additionally evidenced by their responses; dependency failures stay structured 503s rather than redefining API liveness. `GET /api/health` remains public-API liveness and is not redefined. The service Swagger UI under `/api/marine/swagger` is a development/contract-inspection surface registered in the catalog and listed in the public API Swagger UI. |
| Tests/CI | Test project `services/marine-safety/tests/Blueverse.MarineSafety.Tests` with requirement-linked case IDs (`M2-*`); discovered by the existing backend test workflows, which require every service directory to have a test project. |
| Local ports | Container-internal `8080`; no host port. Host-run verification may bind any free host port (e.g. 5085) and is not part of the deployment topology. |

## 6. Shared client and registry edits (exit criterion 6)

| Decision | Record |
|---|---|
| Registry edits | Endpoint catalog gains only the marine-safety entries; `backendSources` gains `services/marine-safety`. React Web registration (revision 6) adds the three marine workflow IDs, six React/Flutter route rows and seven workflow-linked endpoints to the same shared registries; no Auth or public API operation changed. |
| Client routes (before revision 6; superseded) | React and Flutter routes were to be registered under shared workflow IDs when client work was implemented. Revision 6 records the React implementation; the Flutter routes remain registered for the paired mobile implementation, which is not implemented yet. |
| Client routes (React, revision 6) | React owns `/marine/conditions`, `/marine/history` and `/marine/safety-profiles` under the shared workflow IDs `marine-conditions`, `marine-condition-history` and `marine-safety-profile-management`, permission-gated by `marine.profile.read` (+ `marine.profile.manage` for profile writes) resolved from current role assignments. Flutter routes are registered under the same workflow IDs for the paired mobile implementation; the mobile surface is not implemented yet. The browser submits `latitude`/`longitude`/`time` and evaluate `dateTime` in UTC and renders — never recomputes — the server classification, source, timestamps, freshness and missing fields. |
| Shared files | Edits limited to: endpoint catalog JSON+MD, `services/api/appsettings.json` (YARP route), `services/api/Program.cs` (one SwaggerUI endpoint entry listing the marine document), `compose.yaml`, `.env` documentation, Auth permission codes/seeder and seeder test counts (approved additive change), README and database docs. No reformatting of unrelated shared content. |

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
