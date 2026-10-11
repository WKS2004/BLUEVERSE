# AI Usage Contribution Log — Sanuda Abeysinghe

GitHub Username: `sanudaabey` (mapped in [`ai-team-members.md`](../project/ai-team-members.md)) —
Member 2, Marine Conditions & Safety Intelligence, branch `features/marine-safety`.

## 2026-09-27 — Member 2 marine-safety backend implementation

- Date/time or time range: 2026-09-27 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Buffy
- Tool/App: Codebuff (Freebuff desktop)
- AI Model: GLM
- Summary of the user's request: Implement the complete Member 2 backend only
  (marine conditions, condition snapshots, safety profiles, deterministic
  suitability evaluation, Open-Meteo integration, tests) on
  `features/marine-safety`; no frontend, no Agentic AI.
- Summary of what the AI Agent did: Inspected the repository and v1 contracts;
  created the private `services/marine-safety` service (entities, EF Core
  model + migration, Open-Meteo adapter, condition/profile/suitability
  services, controllers with permission authorization, Program/config);
  added the two marine permission codes to the Auth seeder (user-approved);
  registered the gateway YARP route and Compose wiring; registered endpoints
  in the endpoint catalog and regenerated the Markdown; wrote unit and
  integration tests; created the G00 decision record
  `docs/v1/g00/member-2-marine-safety-decisions.md` and linked it from the
  readiness doc; audited the implementation against the G00 record.
- AI output accepted/changed/rejected: Accepted the service scaffold, CRUD and
  evaluation logic, provider adapter, tests and catalog registration after
  verification. Changed during review: adapter URL building (latitude/longitude
  were joined incorrectly, causing provider 400s); DTO validation-attribute
  targeting; permission resolution moved from a mapped keyless view to a
  read-only SQL resolver; marine-outage handling re-implemented so a marine
  failure degrades marine fields instead of discarding weather evidence, with
  regression test M2-COND-009. Rejected: implementing a CAUTION tier and
  live numeric thresholds (not defined in v1 contracts).
- Verification/evidence: `dotnet build` clean for api, auth and marine-safety;
  35/35 `Blueverse.MarineSafety.Tests` pass; Auth and API suites pass;
  migration `InitialMarineSafetySchema` applied to the Compose PostgreSQL and
  constraints/seed verified via `psql`; live end-to-end Swagger checks with a
  real Open-Meteo round trip (UNSUITABLE against a 1.5 m wave limit);
  `validate_endpoint_catalog.py` and `validate_ui_integrations.py` pass.
  Uncommitted working tree on `features/marine-safety`.

## 2026-09-27 — G00 record, audit and re-alignment for marine-safety

- Date/time or time range: 2026-09-27 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Buffy
- Tool/App: Codebuff (Freebuff desktop)
- AI Model: GLM
- Summary of the user's request: Create the G00 implementation plan for the
  marine-safety component, verify the implemented setup against it, and
  re-implement any deviation to match the plan; update the AI-usage log.
- Summary of what the AI Agent did: Authored and linked the G00 decision
  record; audited identifiers, routes, permissions, seed GUIDs, persistence
  keys, freshness window, provider seam and Compose networks against it;
  found one code deviation (marine outage discarded weather evidence) and
  re-implemented `OpenMeteoClient` degradation, added regression test
  M2-COND-009, and updated this log.
- AI output accepted/changed/rejected: Accepted the G00 record text and the
  degradation fix; changed an over-complicated first regression-test draft to
  assert the persisted snapshot contract directly; rejected inventing
  CAUTION thresholds or freshness limits beyond the documented configuration.
- Verification/evidence: 35/35 marine-safety tests pass including M2-COND-009;
  `dotnet build` clean for the marine-safety service; Compose network
  placement, seed GUIDs `…3301`–`…3305`, `MissingFields text[]` and the
  15-minute reuse window re-verified against the G00 record text.

## 2026-09-27 — Route shortening, Swagger aggregation and Auth revert

- Date/time or time range: 2026-09-27 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Buffy
- Tool/App: Codebuff (Freebuff desktop)
- AI Model: GLM
- Summary of the user's request: Re-check the implementation against the G00
  plan; replace the long `/api/marine-conditions` URL prefix with a shorter
  one; connect the marine-safety Swagger into the main API Swagger UI; keep
  API/Auth changes to the essential integration surface only and revert the
  earlier Auth edits; update the AI-usage log.
- Summary of what the AI Agent did: Reverted all Auth changes (`git checkout`
  of `services/auth`); moved the two marine permission codes into the
  marine-safety service and added an idempotent startup provisioner that
  inserts the grants into the Auth-owned identity tables and grants them to
  Admin (additive only); shortened the base route to `/api/marine` across
  controllers, YARP config, tests, catalog and docs; removed the
  `marine-conditions` Swagger document name in favour of `/api/marine/swagger`;
  added the marine document to the public API Swagger UI definition picker
  (one-line change in `services/api/Program.cs`); updated the G00 record,
  README, database docs and endpoint catalog; appended this log entry.
- AI output accepted/changed/rejected: Accepted the revert, the provisioner
  design and the new route. Changed during verification: the first
  catalog entry for the aggregated document duplicated the marine service's
  own route and was folded into the existing gateway `public-swagger-ui`
  entry after the validator flagged the duplicate. Rejected: keeping any Auth
  source change now that provisioning covers the grant path.
- Verification/evidence: Endpoint catalog validator OK (45 endpoints); UI
  integration validator OK; 35/35 marine-safety, 21/21 API and 77/77 Auth
  tests pass (Auth on untouched source); live host-run E2E against the Compose
  PostgreSQL showed the provisioner inserting both marine grants
  (`granted-to-Admin` verified via psql), `/api/marine/current` returning real
  Open-Meteo data (200, FRESH, source Open-Meteo), anonymous 401, invalid
  coordinate 400, profile create 201, evaluate returning UNSUITABLE with
  wave/swell violations against real 2.42 m/1.56 m conditions, history 200,
  and the marine Swagger document (`7 paths, title BLUEVERSE Marine Conditions
  & Safety API`) reachable under the new prefix.

## 2026-09-27 — Restore Auth-seeded marine permission grants

- Date/time or time range: 2026-09-27 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Buffy
- Tool/App: Codebuff (Freebuff desktop)
- AI Model: GLM
- Summary of the user's request: The user confirmed the original Auth approach
  (marine permission codes defined in Auth's PermissionCodes and seeded by the
  Auth seeder) had been correct after all, and asked to re-apply it, removing
  the marine-owned startup provisioner added in the previous session, and to
  update the documentation.
- Summary of what the AI Agent did: Re-applied the two marine permission codes
  to `services/auth/Authorization/PermissionCodes.cs` and the Auth seeder
  (fixed grant GUIDs `…1113`/`…1114`, Admin role), updated the Auth seeder
  test counts 12→14 with documentation; deleted the marine
  `MarinePermissionProvisioner` and restored the local `PermissionCodes`
  constants in the marine-safety authorization file; updated the G00 record,
  README and database schema docs to the restored seeding approach.
- AI output accepted/changed/rejected: Accepted the restoration unchanged;
  rejected keeping both mechanisms (provisioner plus seeding) to avoid
  duplicated grant logic. Supersedes the provisioner approach recorded in the
  previous entry, which is retained as history.
- Verification/evidence: Endpoint catalog validator OK (45 endpoints); UI
  integration validator OK (its PermissionCodes resolution reads Auth's
  restored file); marine-safety and Auth builds clean; full test suites pass
  (35/35 marine-safety, 77/77 Auth); no live rerun needed because the grant
  path equals the one verified in the first E2E session.

## 2026-09-27 — Marine health endpoint with database probe

- Date/time or time range: 2026-09-27 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Buffy
- Tool/App: Codebuff (Freebuff desktop)
- AI Model: GLM
- Summary of the user's request: Add a health check endpoint to the
  marine-safety service through HealthController.cs, analysing the Auth
  health controller to preserve its structure, including a database
  connectivity check.
- Summary of what the AI Agent did: Mirrored Auth's HealthController shape
  (anonymous GET, 2-second bounded `CanConnectAsync` probe, service/status/
  database body, 503 on failure) for the marine service at
  `/api/marine/health`; added three requirement-linked tests (healthy contract
  M2-HEALTH-001, anonymous reachability M2-HEALTH-002, and database-unreachable
  503 M2-HEALTH-003 mirroring the Auth health failure test); registered the
  endpoint in the endpoint catalog, regenerated the Markdown and updated the
  G00 record's operation-set and health rows.
- AI output accepted/changed/rejected: Accepted the Auth-derived structure
  unchanged apart from the service name and DbContext type. Rejected adding
  Open-Meteo availability to the health body because provider outages are
  already reported as structured 503s with Retry-After by marine operations
  themselves and the G00 record keeps health as liveness + database
  readiness.
- Verification/evidence: marine-safety build clean (0 warnings); 38/38
  marine-safety tests pass including the three new health tests; endpoint
  catalog validator OK (46 endpoints); UI integration validator OK.

## 2026-09-27 — Full marine-safety re-evaluation and defect fixes

- Date/time or time range: 2026-09-27, 20:35–21:30 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Buffy
- Tool/App: Codebuff (Freebuff desktop)
- AI Model: GLM
- Summary of the user's request: Re-evaluate the whole marine-safety service
  once again and fix any errors, issues or bugs found.
- Summary of what the AI Agent did: Read every marine-safety source file
  (Program, providers, services, controllers, data, authorization, DTOs,
  migrations) and the full test suite, then fixed three real defects: (1)
  request DateTime values without an offset were parsed in the host's local
  time zone (machine in Asia/Colombo, UTC+5:30), contradicting G00's frozen
  UTC time semantics — normalized via a new `MarineTime.ToUtc` used by
  ConditionService, SuitabilityService and OpenMeteoClient; (2)
  SafetyProfileService.CreateProfileAsync computed the next profile Version
  from active rows only, so recreating a profile after the activity had no
  active profile could reuse version 1 and corrupt assessment history
  interpretability — now takes MAX(Version) over all of the activity's rows;
  (3) UpdateProfileAsync could set isActive=true while another active profile
  existed, which on PostgreSQL violates the partial unique
  (ActivityId, IsActive) index and contradicts the one-active-profile
  invariant — the update now supersedes the other active rows exactly like
  create. Also removed three unused DTO records (ActivityDto,
  ConditionsQueryDto, AssessmentHistoryDto) and appended revision (3) to the
  G00 decision record. Deliberately not changed: the Open-Meteo retry loop's
  cancellation filter (verified correct) and all frozen contracts.
- AI output accepted/changed/rejected: Accepted the three defect fixes plus
  regression tests M2-PROF-010/011/012 and M2-SUIT-011 (requirement-derived,
  host-timezone-sensitive). Rejected two tempting non-fixes: treating
  equal-to-limit values as violations (contract says greater-than violates)
  and adding an invented freshness band for the create-only supersede path.
  One corrupted intermediate edit to a test file was caught by re-reading the
  file and fully restored before running.
- Verification/evidence: marine-safety 42/42 tests pass (38 existing + 4 new
  regression tests, none weakened or deleted); marine, api and auth builds
  clean; endpoint catalog validator OK (46 endpoints); UI integration
  validator OK; host timezone confirmed UTC+5:30 so the naive-time regression
  test exercises the defect it targets.

## 2026-09-27 — Marine-safety test-suite expansion (42 → 87 tests)

- Date/time or time range: 2026-09-27, 21:35–23:10 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Buffy
- Tool/App: Codebuff (Freebuff desktop)
- AI Model: GLM
- Summary of the user's request: Fully implement all test cases for the
  marine-safety service — analyze the whole service and add every test case
  that can be implemented.
- Summary of what the AI Agent did: Mapped coverage gaps against the service
  surface and added 45 tests: 13 direct OpenMeteoClient adapter tests
  (M2-PROVIDER-001..013) over a scripted HTTP handler covering typed
  unavailability, deterministic-vs-transient retry, per-attempt timeout,
  malformed JSON, empty series, marine-only degradation, exact URL/coordinate
  window construction, negative-value rejection and forecast-hour alignment;
  6 condition endpoint tests (naive vs offset time semantics, snapshot reuse
  across rounded coordinates, snapshot 404, history ordering, read-time
  staleness reclassification); 7 suitability tests (rain disclosure semantics,
  multi-factor violations, violation/caution independence, strict-profile
  boundary equality, persisted provenance chain, inactive-activity rejection);
  8 profile tests (inactive-activity rejection, by-activity/update/deactivate
  404s, idempotent deactivate, caution-band and non-positive-limit rejections
  on update); 5 token-level security tests (M2-AUTH-001..005: expired,
  foreign-key, wrong issuer/audience, database-authoritative permission
  resolution over JWT claims, subject-less tokens); an OpenAPI contract test
  asserting the swagger document enumerates all 11 cataloged operations with
  the bearer scheme and anonymous health; two freshness unit tests; two
  additional health tests; and a CaseId-uniqueness meta-test. Fixture changes
  were additive only (stub records last request, factory gained an optional
  token expiry). Two implementation defects were surfaced by the new tests and
  fixed: an absent weather_code was not reported in MissingFields (OpenMeteo
  client), and profile creation for an inactive activity returned 400 instead
  of the contract-consistent 409. No existing test was weakened or deleted.
- AI output accepted/changed/rejected: Accepted the two defect fixes and the
  three requirement-derived test corrections (rain is disclosed but has no
  rule-bearing limit so its absence cannot force UNKNOWN; caution factors are
  reported independently of the decided status; OpenAPI 3.0.4 is the document
  version Swashbuckle 10 emits). Rejected inventing a rain rule or suppressing
  caution factors on UNSUITABLE results because both would contradict the
  frozen G00 rule vocabulary. One corrupted intermediate test edit was caught
  by re-reading the file and fully repaired before running.
- Verification/evidence: 87/87 marine-safety tests pass (42 existing + 45
  new, none deleted or weakened); marine-safety service build clean (0
  warnings); endpoint catalog validator OK (46 endpoints); UI integration
  validator OK; M2-DOC-001 uniqueness scan confirmed live (>= 60 traced case
  IDs, all unique); G00 revision (4) records the two contract-surface fixes.

## 2026-09-27 — Final marine-safety backend review and hardening

- Date/time or time range: 2026-09-27, 23:15–23:45 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Buffy
- Tool/App: Codebuff (Freebuff desktop)
- AI Model: GLM
- Summary of the user's request: Analyze the implemented marine-safety service
  one last time and finalize the backend.
- Summary of what the AI Agent did: Ran the final verification sweep (api,
  auth and marine builds clean; 87/87 marine tests; both validators OK;
  workspace contains exactly the intended modified/new files; no edit
  artifacts or stray helpers remain in test sources; README marine setup
  wording checked). Applied one hardening change before finalization: per the
  data-access rule ("InMemory is not evidence that PostgreSQL behavior is
  correct"), both profile-supersede paths (create, and PUT reactivation) now
  commit the deactivations in a separate SaveChanges before the activation,
  so every intermediate statement satisfies the real PostgreSQL partial
  unique (ActivityId, IsActive) index instead of relying on EF's internal
  command ordering within a single SaveChanges (confirmed by Microsoft
  community sources as observed behavior, not a documented guarantee). No
  contracts, routes, DTOs or status codes changed; the G00 record gained
  revision (5).
- AI output accepted/changed/rejected: Accepted the two-phase supersede
  hardening and verified the 87-test suite still passes unchanged. Rejected
  restructuring the supersede logic into an explicit transaction wrapper —
  separate sequential saves already make each intermediate state valid, and
  the added transaction scope would complicate the retry-on-failure path
  without changing the guarantee. No test was modified or deleted.
- Verification/evidence: 87/87 marine-safety tests pass; api/auth/marine
  builds clean; endpoint-catalog validator OK (46 endpoints); UI validator
  OK; 87 unique CaseId traits confirmed by count; artifact sweep of test
  sources clean; git status shows only intended files.

## 2026-09-27 — Final backend checkup before the React Web App phase

- Date/time or time range: 2026-09-27, 23:50–00:05 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Buffy
- Tool/App: Codebuff (Freebuff desktop)
- AI Model: GLM
- Summary of the user's request: Recheck all the tests and do the final last
  backend checkup right now, because the project moves directly on to
  creating the React Web App afterwards.
- Summary of what the AI Agent did: Performed a from-clean final checkup:
  wiped test bin/obj artifacts, rebuilt all three services individually
  (api, auth, marine-safety — each 0 warnings/0 errors), ran every test
  project in the repository (marine-safety 87, auth 77, api 21 = 185/185
  passed), ran both contract validators, and cross-checked the marine
  contract surfaces end to end: YARP route in services/api/appsettings.json
  targets http://marine-safety:8080, the public API Swagger UI lists the
  marine document at /api/marine/swagger/v1/swagger.json, the marine service
  declares its swagger RouteTemplate/RoutePrefix, compose.yaml wires the
  marine-safety service on both required networks, and the endpoint catalog
  carries the registered marine entries. Confirmed exactly three test
  projects exist repo-wide and all are green. Git workspace contains only
  this session's intended files (16 modified + 4 new test files).
- AI output accepted/changed/rejected: Accepted the no-code-change outcome —
  the checkup surfaced no new defects, so nothing was edited, preserving the
  finalized state verified in the previous entry. No test was modified or
  deleted.
- Verification/evidence: 185/185 tests pass across all three suites from
  clean artifacts; endpoint-catalog validator OK (46 public endpoints, 22
  frontend routes); UI-integration validator OK; YARP/Swagger/compose/catalog
  marine wiring all confirmed by direct source inspection; backend declared
  ready as the contract baseline for the React Web App phase.

## 2026-09-28 — React Web App implementation for marine-safety

- Date/time or time range: 2026-09-28 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Buffy
- Tool/App: Codebuff (Freebuff desktop)
- AI Model: GLM
- Summary of the user's request: Implement the React Web App part of the
  marine-safety service on the existing backend, following DESIGN.md and the
  original web theme; do not modify the architecture of already-implemented
  UIs, only make necessary additive integrations.
- Summary of what the AI Agent did: Analyzed the marine-safety service
  contract and the web app conventions; added `apps/web/src/features/marine/`
  (typed `marineApi.ts` adapter over the frozen public marine operations plus
  `marineActivities.ts` with the frozen G00 activity reference identities) and
  `apps/web/src/pages/marine/MarinePages.tsx` with three pages in the
  established coastal design system (conditions + deterministic suitability,
  condition history, safety-profile management); registered
  `/marine/conditions`, `/marine/history` and `/marine/safety-profiles` with
  `marine.profile.read` permission gating in `routes.tsx`; additively extended
  `AccountAreaNavigation` with a permission-gated Marine & safety group and
  the dashboard quick-access card with a marine link; registered the three
  shared workflow IDs, six React/Flutter route rows and seven marine
  endpoints in `docs/contracts/ui-integration.json` and
  `docs/api/endpoint-catalog.json`, regenerating the catalog Markdown; wrote
  30 requirement-linked tests (19 request-contract, 13 component covering
  normal, invalid, empty, malformed, denied, provider-outage and
  transport-failure paths); updated the component contract status, the G00
  record (revision 6), `apps/web/README.md` and the root README.
- AI output accepted/changed/rejected: Accepted the member-specific module
  layout and permission-gated route design. Changed during verification: the
  create/edit safety-profile forms initially shared duplicate field IDs and
  the create form was nested inside another form causing a double submit —
  both fixed with unique `idPrefix` IDs and a single form element; history
  date filters initially treated an optional window as required. Test-side,
  corrected my own fixture bugs (empty user-event type, ambiguous FRESH text
  match) and replaced an invented client-side caution-band rule with the
  server's 400 rejection, keeping threshold validation server-owned. Rejected:
  recomputing suitability in the browser, hard-coding role checks, adding
  client-side thresholds, and any refactor of existing v0 UI architecture.
- Verification/evidence: web suite 187/187 pass (157 pre-existing + 30 new,
  none deleted or weakened); `npm run lint` and `npm run build` clean;
  `validate_ui_integrations.py` OK; `validate_endpoint_catalog.py` OK
  (46 public endpoints, 28 frontend routes); 35/35 registry validator unit
  tests pass. Backend sources, tests and routes untouched.

## 2026-09-28 — G00 full-CRUD amendment and marine-safety backend patch

- Date/time or time range: 2026-09-28 10:00-12:30 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Buffy
- Tool/App: Codebuff (Freebuff desktop)
- AI Model: GLM
- Summary of the user's request: Update the G00 record to include a full CRUD
  for the marine-safety component, then fully patch the backend service to
  match the changed documentation.
- Summary of what the AI Agent did: Expanded the frozen marine contract with
  the two real gaps: (a) the locally-owned MarineActivities reference table
  had no API surface and (b) persisted suitability assessments were not
  queryable. Updated the G00 record with revision (7) (section 3 operation
  set gains GET|POST /api/marine/activities, GET|PUT|DELETE
  /api/marine/activities/{id}, GET /api/marine/assessments, GET
  /api/marine/assessments/{id}; section 2 activity-identity row amended to
  the manager-maintained reference table; permission and error/status rows
  extended) and patched the backend to match: reintroduced live Activity DTOs
  and AssessmentHistoryDto, added IActivityService/ActivityService (unique
  name 409 conflicts, deactivate-only delete) and assessment query methods on
  ISuitabilityService (newest-first, Take(200), from/to on RequestedTime),
  added ActivitiesController (read/manage permission pair like profiles) and
  read-only AssessmentsController, registered 7 new endpoint-catalog entries
  and regenerated the Markdown, updated M2-SWAG-001's DocumentedOperations,
  and wrote 16 new requirement-linked tests (M2-ACT-001..010,
  M2-ASMT-001..006).
- AI output accepted/changed/rejected: Accepted deactivate-only delete
  semantics (assessment FK restrict + profile cascade references survive) and
  reusing the existing marine.profile.read/manage codes without Auth changes.
  Changed during verification: my first assessment-history window filtered on
  EvaluatedAt, which is the wall-clock evaluation moment rather than the
  domain-meaningful requested forecast time - corrected to RequestedTime and
  documented in the service comment; three of my new tests carried wrong
  expectations copied from a misread contract (evaluate on an inactive
  activity is 404 per G00 revision (4), not 409; HasData seeds are wiped by
  the per-test database reset so rename-conflict tests must create their own
  rows; two SUITABLE rows exist in the filter test) - the requirement-derived
  expectations were corrected, no existing test was weakened; fixed two
  expression-tree compile errors in EF queries. Rejected: hard-deleting
  activities, reactivating activities through PUT, new permission codes, and
  any change to pre-existing endpoint shapes.
- Verification/evidence: marine suite 103/103 pass (87 pre-existing + 16
  new, none deleted or weakened), auth 77/77, api 21/21; marine/api/auth
  builds 0 warnings/0 errors; validate_endpoint_catalog.py OK (53 public
  endpoints, 28 frontend routes) after --write-markdown regeneration;
  validate_ui_integrations.py OK; M2-DOC-001 CaseId uniqueness meta-test
  green. Nothing committed (repo rule: commit only on explicit request).

## 2026-10-03 — Marine-safety authentication and authorization hardening

- Date/time or time range: 2026-10-03 17:35–17:44 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Fix Authentication and Authorization issues
  affecting the marine-safety component and update its documentation.
- Summary of what the AI Agent did: Aligned marine JWT authentication with
  Auth-managed bearer and selected-account cookie sessions; added current
  account/token/session validation; retained live role-to-permission checks;
  propagated request cancellation; and updated the marine component, G00,
  database, phase and branch-status documentation.
- AI output accepted/changed/rejected: Accepted component-owned, read-only
  Auth session validation and the existing marine permission codes. Updated
  the docs to describe current behavior. No public routes or permission codes
  changed.
- Verification/evidence: Endpoint catalog validator passed (53 public
  endpoints, 28 frontend routes); UI integration validator passed;
  `git diff --check` passed. The marine build could not restore because the
  sandbox denied access to `C:\Users\MSI\AppData\Roaming\NuGet\NuGet.Config`;
  tests were not run. No commit created.

## 2026-10-03 — Marine-safety authentication verification recheck

- Date/time or time range: 2026-10-03 17:52–18:00 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Recheck whether the marine-safety
  authentication/authorization fixes and documentation are complete.
- Summary of what the AI Agent did: Re-ran the marine suite and documentation
  validators; reviewed Auth cookie/schema contracts; fixed bearer-header
  precedence and malformed selected-account fallback; added seven focused
  cookie regression cases; corrected the docs and test comments to state the
  PostgreSQL verification boundary.
- AI output accepted/changed/rejected: Accepted the existing Auth token and
  session claim contract and marine permission codes; changed cookie selection
  to preserve explicit bearer tokens and fail closed on malformed account
  selection. No public routes or permission codes changed.
- Verification/evidence: `dotnet test` passed 110/110; endpoint catalog and
  UI integration validators passed; `git diff --check` passed. PostgreSQL
  query verification remains outstanding: Docker is unavailable, localhost
  port 5432 is closed, and `BLUEVERSE_AUTH_POSTGRES_TEST_CONNECTION` is unset.
  No commit created.

## 2026-10-03 — Marine-safety backend and React test updates

- Date/time or time range: 2026-10-03 18:26 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Update all marine-safety tests in the backend
  service and React Web App.
- Summary of what the AI Agent did: Added React API and component cases for
  expired-session 401 handling, safe recovery, profile validation and state;
  corrected the profile-validation test to assert the initial GET rather than
  treating the read as a mutation; synchronized the Web README and test matrix.
- AI output accepted/changed/rejected: Kept the server-owned permission and
  identity checks; kept the existing public API surface; changed only test
  expectations and test documentation for this request.
- Verification/evidence: Marine backend suite passed 117/117; full React suite
  passed 198/198 when run with one worker; React lint and UI integration
  validation passed; `git diff --check` passed. The default parallel Node test
  run exhausted this host's memory, so the same full suite was rerun serially.
  No commit created.

## 2026-10-03 — Marine-safety documentation reconciliation

- Date/time or time range: 2026-10-03 18:58 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Update all documentation about the
  marine-safety component again after the implementation and test updates.
- Summary of what the AI Agent did: Reconciled the component contract, G00
  record, branch tracker, roadmap, foundation review, Agentic AI boundary,
  architecture, API/UI catalogs, local setup, deployment, test documentation
  and component READMEs with the current branch evidence. Corrected stale
  statements about implemented domain routes, React coverage, G00-deferred AI
  integration, and the Marine public health-route alias used by CI.
- AI output accepted/changed/rejected: Accepted current implementation,
  catalog, test and G00 evidence as the source for status. Changed outdated
  documentation while retaining dated historical records and target
  specifications. No implementation, test or CI source was changed in this
  documentation pass.
- Verification/evidence: Endpoint catalog validator passed (53 public
  endpoints, 28 frontend routes); UI integration validator passed; agent
  resource validator passed (23 skills); `git diff --check` passed. The docs
  continue to identify production Auth SQL/PostgreSQL verification and the
  Docker stack-health route alias as open evidence. No commit created.

## 2026-10-06 — Marine-safety continuation, verification and status update

- Date/time or time range: 2026-10-06 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-5
- Summary of the user's request: Continue the halted marine-safety backend
  and React Web repair task and update the AI-usage record as Sanuda.
- Summary of what the AI Agent did: Repaired the remaining React marine test
  expectations for the single server evaluation response and explicit legacy
  evidence state; verified the full React suite and Web production build;
  reran the marine API-contract tests; validated the endpoint catalog and UI
  integration registry; built the marine backend test project; reconciled the
  current marine component, testing, setup and branch-status documentation
  with the implemented React, Flutter and backend state.
- AI output accepted/changed/rejected: Accepted the server-owned provenance
  and independent-review behavior, including blocking evaluation until every
  wind, wave and swell criterion has a cited source and rationale. Changed
  outdated test expectations and documentation counts/statuses. No commit or
  push was created.
- Verification/evidence: `npm run build` passed; serial `npm run test:ci --
  --test-concurrency=1` passed 201/201, including 44 marine cases; the marine
  API request-contract file passed 21/21; endpoint catalog validation passed
  with 54 public endpoints and 28 frontend routes; UI integration validation
  passed; the backend test project built with 0 warnings and 0 errors and has
  118 authored cases. A backend `dotnet test` attempt timed out after the
  VSTest testhost failed to connect within 90 seconds, so no new backend test
  pass count is claimed. Flutter analysis/formatting did not complete because
  the local SDK/dependency setup did not start. `git diff --check` passed.

## 2026-10-06 — Marine hook lint and mobile client correction

- Date/time or time range: 2026-10-06 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Continue the halted marine-safety repair,
  complete verification and update the AI-usage record as Sanuda.
- Summary of what the AI Agent did: Removed a React effect that synchronised
  derived profile editor state and replaced it with event-driven state
  updates; corrected the Flutter HTTP response extension to use
  `this.statusCode`; removed an unused marine model import; formatted the
  affected mobile sources and reran the full Web verification.
- AI output accepted/changed/rejected: Accepted the hook-lint correction and
  the mobile client fix after source review. Kept the documented dependency
  limitation for Flutter runtime analysis; no tests were weakened or skipped.
- Verification/evidence: React serial suite passed 201/201 after the refactor;
  marine API and UI focused suites passed 44/44; `npm run lint` and
  `npm run build` passed; endpoint catalog validation passed with 54 public
  endpoints and 28 frontend routes; UI integration validation passed; direct
  Dart formatting completed with only the expected missing `flutter_lints`
  package-resolution warning. Full Flutter analysis remains blocked by the
  absent `.dart_tool/package_config.json` and cached packages. `git diff
  --check` passed. No commit or push was created.

## 2026-10-06 — Marine backend test-fixture correction

- Date/time or time range: 2026-10-06 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Finish the resumed marine-safety repair and
  correct remaining backend or test issues found during review.
- Summary of what the AI Agent did: Corrected the marine integration-test
  seed so the unprofiled activity is inserted independently of the
  model-seeded activity. This keeps the missing-profile evaluation case from
  accidentally testing an unknown activity.
- AI output accepted/changed/rejected: Accepted the fixture correction after
  source review; no production behavior or expected test result was weakened.
- Verification/evidence: The marine test project rebuilt successfully with 0
  warnings and 0 errors after the fixture change. VSTest still reaches test
  startup but does not return within the host timeout, so no new backend test
  pass count is claimed. No commit or push was created.

## 2026-10-06 — Marine mobile not-found handling

- Date/time or time range: 2026-10-06 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Continue the marine-safety repair and fix
  remaining client errors found during the resumed review.
- Summary of what the AI Agent did: Fixed the Flutter marine API client so
  profile lookup returns `null` for a public 404 and profile deactivation
  reports a stable not-found exception, despite the shared request helper
  throwing for non-success responses.
- AI output accepted/changed/rejected: Accepted the correction after tracing
  the helper control flow; no error handling was removed or hidden.
- Verification/evidence: Dart formatting completed for all ten changed marine
  sources with zero formatting changes after the final pass; package-resolution
  warnings remain because Flutter dependencies are not installed locally.
  Full Flutter analysis and tests remain unavailable for that same environment
  reason. No commit or push was created.

## 2026-10-06 — Marine mobile gateway fallback correction

- Date/time or time range: 2026-10-06 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Continue fixing the marine-safety component
  across its backend and clients after the task was interrupted.
- Summary of what the AI Agent did: Corrected the Flutter marine client’s host
  fallback loop so connectivity failures try the ordered gateway candidates,
  successful hosts are preferred on later requests, and every request has a
  three-second timeout before the next candidate is attempted.
- AI output accepted/changed/rejected: Accepted the correction after comparing
  the marine client with the existing Auth client’s gateway behavior; HTTP
  error responses still stop without unsafe fallback.
- Verification/evidence: All ten changed Dart marine sources were formatted;
  the final backend service and test-project builds passed with 0 warnings and
  0 errors; endpoint catalog, UI integration and diff checks passed. Flutter
  analysis/tests remain blocked by the missing package cache. No commit or push
  was created.


## 2026-10-08 — Marine Safety ownership and integration corrections

- Date/time or time range: 2026-10-08 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Recheck and implement the necessary Member 2
  corrections, recognizing that Member 1 already owns Activity CRUD. The user
  explicitly approved affected test updates and recording AI usage as Sanuda.
- Summary of what the AI Agent did: Replaced duplicated Marine activity writes
  and fixed frontend IDs with a validated read-only adapter for Member 1's
  actual published activity contract. Retained safety-profile CRUD with
  immutable drafts, independent review, deactivation and assessment history.
  Added the narrow API/Auth actor-verification integration and signed
  request-bound internal context; removed Marine's direct Auth-table queries.
  Added the canonical projection migration, provider time/numeric boundary
  handling, equivalent client loading/empty/outage/retry states and independent
  conditions-only queries. Updated Docker build contexts, signing-key setup,
  shared-contract CI triggers, public health alias, endpoint/UI registries,
  ADR-0021, setup documentation and component handoff/status records.
- AI output accepted/changed/rejected: Retained the corrected ownership and
  gateway boundary after comparing the source with the component requirements
  and Member 1's local remote branch. Updated affected tests with the user's
  permission; kept authorization, state, evidence and side-effect assertions.
  Corrected M2-SUIT-019's fixture to use an unprofiled activity so its expected
  review gate is independent of the seeded approved profile. Rejected fixed
  activity IDs, local catalogue fallback and implicit safe provider values.
  Existing contribution records and the student's reflection were not edited.
- Verification/evidence: Marine backend 145/145 and API 47/47 passed with zero
  skipped tests; React 207/207 and Flutter 86/86 passed; Web production build
  and lint passed; Flutter analysis reported no issues. Endpoint catalog passed
  (51 public endpoints, 28 frontend routes), UI integration validation passed,
  CI helpers passed 8/8 and UI validator tests passed 35/35. Four changed
  workflow YAML files and 18 Bash blocks passed syntax checks. Compose config
  validation and idempotent EF migration SQL generation passed. Local result
  logs, final TRX files and generated migration SQL are retained under
  `local/marine-verification-2026-10-08/`. Backend tests use EF InMemory and
  controlled Auth/catalogue/provider transports; they do not establish real
  PostgreSQL or live Member 1 deployment compatibility. The Docker daemon was
  not running; live stack, physical-device/provider deployment evidence and
  G07 acceptance remain outstanding. No executable agents, commit or push
  were created.


## 2026-10-08 — Full Marine Safety branch recheck

- Date/time or time range: 2026-10-08 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Recheck the whole branch again after the
  Member 2 implementation corrections.
- Summary of what the AI Agent did: Reviewed the committed branch and current
  working changes across Marine, API/Auth, both clients, EF migrations,
  Docker/CI, contracts and readiness documents. Built local-only reproduction
  harnesses for coordinate editing, timeout/fallback writes, reader navigation,
  cache-time matching, profile precision and extreme timestamp arithmetic.
  Recorded seven remaining findings and corrective actions in
  `docs/testing/marine-branch-review-2026-10-08.md`; linked the review from G00
  and corrected the CI document's Docker-trigger claim.
- AI output accepted/changed/rejected: Retained findings supported by source
  or local reproduction. Distinguished a same-draft review concurrency risk
  from proven database behavior; rejected the inference that passing isolated
  tests establishes release readiness. This pass records a review and does
  not repair the reported application defects. Existing application tests,
  earlier contribution entries and the student's reflection were not changed.
- Verification/evidence: Marine 145/145, API 47/47, Auth 77/77, React 207/207,
  Flutter 86/86, CI helpers 8/8 and UI validator tests 35/35 passed. All those
  completed default suites reported zero failures and zero skipped cases;
  optional Auth PostgreSQL tests remain excluded by existing configuration.
  Web lint/build, Flutter analysis, endpoint/UI contracts, foundation structure,
  agent resources (23 skills), Compose config and EF migration/model consistency
  passed. A validation-generated Python cache was removed before rechecking
  agent resources. Reproduction checks confirm the observed defects and are
  distinct from successful product regression cases. Evidence is retained in
  `local/marine-verification-2026-10-08/`. Docker runtime, PostgreSQL constraints,
  transactions/concurrency and live member/device deployment remain unverified;
  Docker's daemon is stopped. No commit or push was created.


## 2026-10-08 — Repair the seven Marine Safety branch findings

- Date/time or time range: 2026-10-08 18:25 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Fix the issues found in the repeated
  marine-safety branch review, using the existing approval for affected tests
  and recording the contribution under Sanuda.
- Summary of what the AI Agent did: Preserved and validated signed decimal
  mobile coordinates; exposed profile reads to authorized readers; set the
  mobile request budget to 45 seconds and prevented automatic replay of
  ambiguous writes. Unified provider/cache forecast applicability and guarded
  retrieval recency, rejected excess safety-limit precision before persistence,
  added structured extreme-time validation to current/evaluate and the provider
  adapter, and added the shared actor-contract path to both Docker event filters.
  Added 22 backend, 14 Flutter and one CI regression cases, including actual
  Flutter application-shell navigation via optional repository/service injection.
  Synchronized the endpoint catalog, shared UI registry, G00, handoff, setup,
  test matrix and branch-review resolution record.
- AI output accepted/changed/rejected: Accepted the seven demonstrated defect
  corrections and independent requirement-based assertions. Initial regression
  failures required adding the missing assessment timestamp handler and showing
  the actual history filter error; expected outcomes were retained. Rejected
  automatic retry of a potentially completed mutation and silent rounding of
  reviewed safety criteria. Existing behavior checks were retained; no assertions
  were removed, skipped or relaxed. Earlier contribution records and the
  student's assessed reflection were not edited.
- Verification/evidence: Marine 167/167, API 47/47, default provider-independent
  Auth 77/77, React 207/207, Flutter 100/100, CI helpers 9/9 and UI/catalog
  validator tests 35/35 passed with zero failures/skipped cases. Web lint/build,
  Flutter analysis, endpoint catalog (51 public endpoints, 28 frontend routes),
  shared UI registry, agent resources (23 skills), foundation structure, Compose
  configuration and EF migration/model consistency passed. Workflow YAML/Bash
  syntax was checked. CI/validator tests initially encountered sandbox temporary
  file and Git Bash restrictions; unchanged tests passed outside that sandbox.
  Final logs/TRX and temporary validation tooling are retained under
  `local/marine-verification-2026-10-08/`; Flutter-generated registrants were
  restored to their pre-validation content. PostgreSQL migration/constraints
  and same-draft review concurrency, live member integration and physical-device
  deployment remain unverified because Docker's daemon is stopped. Optional
  Auth PostgreSQL source tests were not enabled. No executable agents, commit
  or push were created.

## 2026-10-08 — Independent Marine dependency availability

- Date/time or time range: 2026-10-08 19:10 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Update the Marine branch to withstand absent
  member services, describe missing data as unavailable without temporal
  wording, and depend on other components only through required API data.
- Summary of what the AI Agent did: Added structured unavailable response fields
  and safe dependency reason codes; isolated optional catalogue configuration
  from startup/readiness; retained independent condition queries, stored evidence
  and profile deactivation. Preserved valid empty catalogue responses and blocked
  only drafts/edits/reviews/evaluations requiring published activity data. Both
  clients retain typed error metadata, saved records and independent controls,
  then restore dependent actions on retry. Added narrow public API handling for
  unavailable Auth verification and unreachable/timed-out Marine destinations,
  with conservative mutation retry semantics. Corrected stack CI OpenAPI checks
  to require only member services whose source exists. Added ADR-0022 and the
  availability contract, and synchronized setup, G00, handoff, endpoint/UI
  registries and test evidence.
- AI output accepted/changed/rejected: Accepted operation-level missing-data
  metadata and fault isolation. Retained the existing Auth/permission model,
  approved-profile rules, successful response shapes and safe write policy.
  Rejected fabricated catalogue IDs, cached publication authority, unavailable
  data represented as a successful empty list and automatic replay of ambiguous
  writes. Updated affected existing tests under the user's earlier approval,
  preserving and strengthening behavior assertions. Earlier contribution
  records and the student's assessed reflection were not edited.
- Verification/evidence: Marine 183/183, public API 54/54, default Auth 77/77,
  React 210/210, Flutter 103/103, CI helpers 10/10 and contract-validator tests
  35/35 pass with zero failures/skipped cases. New regressions cover missing
  configuration, cancellation/timeout, empty/malformed data, state/audit
  preservation, recovery, typed metadata, independent controls and actual
  workflow OpenAPI selection. Web lint/build, Flutter analysis, endpoint catalog
  (51 public endpoints, 28 frontend routes), UI registry, 23 agent resources,
  foundation structure, Compose configuration and EF migration/model consistency
  pass; five workflow YAML files and 21 Bash run blocks pass syntax checks.
  Logs/TRX and isolated validation tooling remain under
  `local/marine-verification-2026-10-08/`; validation-generated Flutter registrants
  were restored. Tests use EF InMemory and deterministic HTTP/UI doubles, not
  live PostgreSQL or deployed member/device integration. Docker's daemon is
  stopped; optional Auth PostgreSQL source tests were not enabled. No executable
  agents, commit or push were created.


## 2026-10-08 — Complete the Marine Safety React Web workflows

- Date/time or time range: 2026-10-08 20:18 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Implement the React Web application for the
  marine-safety component after the dependency-availability corrections.
- Summary of what the AI Agent did: Extended the existing three registered Web
  workflows with accessible current-page navigation, a location/time guide,
  plain-language explanations of the four unchanged server outcomes and explicit
  missing-evidence disclosure. Grouped each profile factor's maximum, caution,
  citation and rationale; matched the shared two-decimal, range and caution
  contract, including the 1000 km/h storage upper bound. Made initial/filter/
  refresh history requests retain independently successful datasets and label
  unavailable or last-loaded data. Added recovery for failed profile reads while
  retaining read/manage permissions and independent catalogue-outage actions.
  Added 14 component cases and synchronized Web/root guides, G00, endpoint/UI
  registries and the test matrix.
- AI output accepted/changed/rejected: Accepted the clearer Web presentation and
  recovery paths on the existing public API contract. Retained server-owned
  classifications, independent profile approval, canonical activity consumption,
  confirmed deactivation, and equal Flutter business capabilities. The full
  suite rejected an empty-selector placeholder; it was removed to preserve the
  zero-option empty-catalogue requirement. The earlier caution test expected a
  server call because the browser lacked relation validation; it now requires
  client rejection without mutation, under the existing user test-edit approval.
  A separate case preserves server-error, single-submit and draft-retention
  coverage. No failing expectation was weakened to accept incorrect behavior.
  Earlier contribution entries and the student's reflection were not edited.
- Verification/evidence: The complete React suite passes 224/224 with zero
  failures, cancellations or skipped cases; Marine contributes 67 cases (24
  request/error and 43 page/workflow cases). Web lint and production build pass.
  Endpoint catalog (51 public endpoints, 28 frontend routes), UI integration and
  all 35 validator tests pass; diff checks are clean. Evidence is retained in
  `local/marine-verification-2026-10-08/web-workflow-*.log`. Tests use synthetic
  criteria, JSDOM and public API doubles; live browser, provider and deployed
  gateway/member-service behavior remain separate evidence. Backend and Flutter
  source were not changed by this task. No executable agent, commit or push
  was created.


## 2026-10-08 — Restore Marine Web entry and correct Admin navigation

- Date/time or time range: 2026-10-08 22:53 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Make implemented Marine workflows accessible
  in React and remove the annotated home-section links from Administration.
- Summary of what the AI Agent did: Used the actual route to restrict marketing
  links to Home, removed the empty Admin mobile toggle, preserved account
  actions and added a Marine account-menu link guarded by the read permission.
  Investigated running public assets and read-only database metadata: the app
  already contained Marine routes, but Admin lacked Marine grants and another
  member's permission codes occupied the preferred seed IDs. Corrected normal
  Auth bootstrap to preserve existing permission identities and allocate free
  IDs for missing codes. Added ten Web and three Auth regression cases, updated
  endpoint/UI registries and setup/database/G00/handoff/testing documentation,
  then refreshed local Auth/frontend containers with existing data preserved.
- AI output accepted/changed/rejected: Accepted route-based navigation and the
  narrowly justified code-based permission seeding fix. Preserved current
  role-to-permission authorization and Auth/session flows. Rejected role-name
  access shortcuts, overwriting other member grants and database resets.
  Existing tests and historical contribution records were retained; the
  student's assessed reflection was not authored or changed.
- Verification/evidence: Full React 234/234 and default Auth 80/80 pass with no
  failures/skipped cases; focused navigation 25/25, Web lint/production build,
  endpoint/UI validators and all 35 UI validator tests pass. Actual public API,
  Auth and Marine health return 200; frontend serves the corrected hashed
  bundle. PostgreSQL before/after readbacks show 14 → 16 permissions/Admin
  grants with both Marine codes added, existing IDs/grants and account/session
  counts preserved; repeat Auth startup retains identical identities/counts.
  Source tests use JSDOM/transport doubles and EF InMemory. Live database
  evidence is limited to startup permission repair; optional Auth PostgreSQL
  tests and cross-member/provider/device workflows were not run. Browser
  automation failed to initialize, so no real-browser interaction is claimed.
  Docker frontend build passed; Auth container restore stalled and was
  cancelled. Installed-SDK cached publish was packaged into the selected DHI
  ASP.NET runtime for the local refresh. Web build used `--emptyOutDir=false`
  after a locked output directory blocked cleanup. Evidence remains under
  `local/marine-verification-2026-10-08/`. No executable agent, commit or push
  was created.


## 2026-10-08 — Align Marine sidebar and workflow tab names

- Date/time or time range: 2026-10-08 23:43 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Use one creative, area-appropriate set of names
  in both the highlighted Marine sidebar and page tabs.
- Summary of what the AI Agent did: Applied Coastal conditions, Coastal evidence
  and Activity safety limits through a shared display-label module. Updated
  existing navigation expectations and added same-label/destination assertions
  for both desktop/mobile sidebar layouts against the page tabs. Synchronized
  Web guidance, UI registry, endpoint catalog and focused test evidence, then
  rebuilt and refreshed only the running frontend.
- AI output accepted/changed/rejected: Accepted descriptive coastal labels and
  a shared source to prevent drift. Retained the three existing routes and
  permission checks. Updated affected test text using the existing test-edit
  authorization while preserving assertions. Rejected removing the navigation
  after the user clarified both menus should remain. An initial public-bundle
  check was too broad because Saved evidence also appears in explanatory copy;
  verification was scoped to the shared navigation object instead. Historical
  contribution entries and the student's assessed reflection were unchanged.
- Verification/evidence: Affected navigation/workflow suites pass 24/24, no
  failures or skips; Web lint and production build pass. Endpoint catalog, UI
  registry and all 35 UI validator tests pass. Docker frontend build passed,
  and localhost serves `index-BvjgAZvJ.js` with the shared labels. Component
  checks use JSDOM; public-asset verification does not claim browser interaction.
  The complete suite was not rerun for the label-only change. Evidence remains
  under `local/marine-verification-2026-10-08/web-labels-*`. Backend/mobile source,
  API contracts and Auth data were unchanged. No commit or push was created.


## 2026-10-09 — Add distinct Marine & Safety page heroes

- Date/time or time range: 2026-10-08–09, completed 2026-10-09 00:05 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex; built-in image generation
- AI Model: GPT-6; built-in image-generation model identifier unavailable
- Summary of the user's request: Add attractive hero sections to each Marine &
  Safety UI using new imagery aligned with the page content and BLUEVERSE theme.
- Summary of what the AI Agent did: Generated three distinct Sri Lanka-inspired
  coastal editorial scenes: ocean swell/shoreline, a field notebook/compass and
  kayak/safety equipment preparation. Encoded the unchanged compositions as
  project-local WebP assets and recorded full prompts/provenance. Added a shared
  responsive Tailwind hero with page-specific imagery, readable solid-panel
  guidance, decorative image semantics and intrinsic dimensions/priority
  loading. Preserved existing forms, workflow labels, permissions and requests.
  Added six hero regressions, synchronized Web/contract/G00/testing records,
  rebuilt the frontend and refreshed the local site.
- AI output accepted/changed/rejected: Accepted new page-specific photography and
  the coastal editorial split layout with stacked mobile composition. Rejected
  reusing existing assets, fictional measured conditions or using scenery as
  proof of safety. Kept H1 titles and existing business behavior intact.
  Parallel validation exhausted host memory; reran the complete suite with one
  worker and Docker separately with constrained CLI concurrency. No tests were
  skipped or assertions weakened, and earlier usage records/reflection were
  unchanged.
- Verification/evidence: Focused 30/30 and full React 240/240 pass with zero
  failures, cancellations/skips; Web lint and production build pass. Endpoint
  catalog, UI registry and all 35 validator tests pass. Docker frontend build
  succeeds after the memory-failure retry; localhost serves the new hashed
  bundle and all three new images with 200 responses, with image bytes matching
  the validated build. New tests assert page/image association, semantic copy,
  image error resilience, decorative treatment, dimensions/loading and workflow
  navigation during dependency failures. JSDOM and public assets are evidence;
  browser automation failed to initialize, so no visual browser inspection is
  claimed. Full prompts and filenames are in
  `apps/web/src/assets/coastal/marine/README.md`; logs and runtime metadata are
  under `local/marine-verification-2026-10-08/web-hero-*`. Backend/mobile source,
  Auth data and public API behavior were unchanged. No commit/push was created.


## 2026-10-09 — Stack Marine hero images above copy at every width

- Date/time or time range: 2026-10-09 00:17 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Apply the screenshot's narrow-screen hero
  structure to all Marine & Safety hero sections at every screen resolution.
- Summary of what the AI Agent did: Removed the desktop two-column and ordering
  overrides from the shared hero, placed the image first in the DOM and kept
  copy below it. Retained the photo banner, rounded card, wave badge, eyebrow,
  heading, description and divider/details; normalized wide-screen padding/type
  to the narrow layout. Updated existing assertions to enforce the new structure,
  synchronized Web/registry/catalog/test documentation and refreshed the local
  frontend using the existing DHI Dockerfile.
- AI output accepted/changed/rejected: Accepted the user's screenshot as the
  layout reference and image-first single-column presentation at all widths.
  Rejected retaining a desktop split. Kept the three previously approved images,
  content and business contracts. Strengthened affected existing tests under the
  continuing user authorization; no assertions were relaxed. Earlier contribution
  records and the student's assessed reflection were unchanged.
- Verification/evidence: Affected Marine tests pass 30/30, zero failures/skips;
  Web lint/production build, endpoint catalog/UI validation and all 35 validator
  tests pass. Structural tests check first image panel, subsequent copy, single
  grid column and absence of breakpoint column/order overrides, retaining image,
  accessibility and dependency-failure assertions. Local Docker build/refresh
  passed; all three Marine URLs return 200 and their JS/CSS match the validated
  build bytes. JSDOM/static layout declarations and served assets are evidence,
  not screenshots at every resolution. The prior full 240-case suite was not
  rerun for this layout-only change. Logs/runtime metadata are under
  `local/marine-verification-2026-10-08/web-hero-stack-*`. Backend/mobile source,
  API behavior and Auth data were unchanged. No commit/push was created.


## 2026-10-09 — Persistent Marine workspace and animated tab switching

- Date/time or time range: 2026-10-09 02:44 (Asia/Colombo; completion)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Follow the supplied three-tab page structure,
  retain the annotated Marine pill navigation design, keep the tabs visible on
  scroll, animate tab changes and remove the full-screen loading screen when
  switching Marine tabs. Resume after the interrupted session and local security
  software access block.
- Summary of what the AI Agent did: Added a persistent shared layout with one
  page heading and introduction, existing pill tabs above each stacked hero and
  content, and sticky positioning below the site header. Added a 260 ms panel
  entrance that respects reduced motion. Scoped inline bootstrap read feedback
  to Marine tab changes, preserving first-entry, explicit retry/refresh, mutation
  and Auth loading. Preserved live permission guards, public paths, cookie
  credentials, error semantics, business controls and the three approved images.
  Adjusted scroll restoration for Marine switches, added six route-level tests,
  synchronized registries/catalog/documentation, rebuilt the frontend and
  refreshed only its local container.
- AI output accepted/changed/rejected: Accepted the reference order of heading,
  tabs, hero and content, and the existing pill styling. Changed hero headings to
  H2 under the shared H1. Existing hero/workflow tests now compose the layout and
  retain their earlier image, business and error assertions under the user's
  continuing affected-test authorization. Rejected a full-screen loader on
  Marine tab bootstrap reads; reduced-motion users receive no panel animation.
  Earlier AI records and the student's reflection were preserved.
- Verification/evidence: Full React suite passes 246/246 with zero failures or
  skips; all six new workspace cases pass, including rapid unresolved reads,
  back/forward, deep/near-top scrolling, keyboard focus, denied permissions with
  no requests, recoverable errors, retry and re-entry. Web lint/build, endpoint
  catalog/UI checks and all 35 validator tests pass. Validator fixtures initially
  failed due to sandbox temporary-directory access; the same tests passed with
  a writable workspace temp directory. The resumed executor responded after
  the user resolved Kaspersky access blocking. DHI frontend build/refresh passes;
  all three localhost Marine URLs return 200, and JS/CSS/image bytes match the
  validated build. JSDOM and served assets are evidence; no real-browser visual
  inspection is claimed. Logs and runtime hashes are under
  `local/marine-verification-2026-10-08/web-tabs-*`. Backend/mobile behavior and
  Auth data were unchanged in this task. No commit/push was created.


## 2026-10-09 — Complete the Marine native mobile experience

- Date/time or time range: 2026-10-09 05:23 (Asia/Colombo; completion)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Implement the necessary Mobile App work for
  the marine-safety component after the React Marine UI changes.
- Summary of what the AI Agent did: Reviewed existing Flutter workflows and
  public contracts, connected Marine requests to the selected secure account's
  existing Auth header seam, scoped/closed the app-owned transport and made
  permission/account changes rebuild or remove protected UI. Added a single
  native Marine shell, permission-gated Dashboard/account-menu entry, pinned
  shared labels, bundled approved coastal heroes and reduced-motion transitions.
  Restored the registered root onboarding route and standard Dashboard landing.
  Displayed sourced condition readings/units and practical server-result
  explanations, preserved UNKNOWN without a condition payload, normalized UTC
  input and prevented pull refresh from creating another assessment. Made saved
  datasets recover independently with labelled retained records and retries;
  added profile read retry and honest missing caution/empty/unavailable states.
  Preserved manager draft/version, independent review, confirmed deactivation,
  API boundaries and no-replay behavior. Synchronized registry/catalog, mobile/
  root guides, component/decision records and test evidence.
- AI output accepted/changed/rejected: Accepted native adaptations of the Web
  labels, palette and approved Marine imagery; no new image generation was
  requested. Changed the existing reader-navigation test under the continuing
  affected-test approval to enter from Dashboard and select the shared pill,
  retaining its permission/API assertions. Added fifteen requirement-based
  cases. Fixed the overflow exposed by enlarged-text testing rather than
  weakening layout expectations; corrected a new synthetic fixture's missing
  contract field. Rejected anonymous production Marine requests, silent malformed
  row skipping, loss of UNKNOWN results and refresh-triggered assessment writes.
  Historical AI records and the student's reflection were preserved.
- Verification/evidence: Successful Flutter dependency resolution, clean
  analysis and full suite 118/118 with no failed/skipped cases. New cases cover
  secure selected-account/rotated headers, missing auth without transport,
  request denial and loading cleanup, malformed records, UTC invalid/boundary
  input, narrow enlarged-text UI, pinned navigation/reduced motion, slow inline
  reads, live permission removal, partial data failure/retry, labelled retained
  records, UNKNOWN, read-only refresh, profile failure, entry grants and real-app
  signed-out onboarding. Endpoint/UI validation and all 35 validator tests pass.
  The Android debug build attempt emitted no output and was stopped; no new APK,
  packaging or physical-device success is claimed. Evidence is under
  `local/marine-verification-2026-10-08/mobile-workspace-*`. Shared edits are the
  mobile router/entries, one Auth credential accessor, asset declaration and
  documentation/registries; backend/React behavior and Auth data were unchanged.
  No commit/push was created.

## 2026-10-10 — Recheck the Marine Flutter implementation

- Date/time or time range: 2026-10-10 06:41–07:01 (Asia/Colombo)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Recheck whether the untried Marine Flutter
  implementation is correct and can run, including native build validation.
- Summary of what the AI Agent did: Reviewed production routing, selected-account
  authorization, public DTO/request boundaries, Android configuration and test
  discovery. Corrected the machine-specific Android fallback to the standard
  emulator address. Fixed unknown-path recovery, removed an unregistered detail
  destination from the navigation helper, guarded profile dialog actions after
  page removal and prevented older reads from replacing refreshed data. Enabled
  discovery of the excluded Marine test file and added portable-network,
  lifecycle/race and complete native profile-workflow checks. Synchronized the
  catalog, setup guides, component contract and test evidence. Rechecked SDK
  licences/device authorization and attempted a native debug build.
- AI output accepted/changed/rejected: Accepted the existing native layout,
  permission grants, secure selected-account headers and public API ownership.
  Changed the excluded tests only after the user's explicit approval in this
  task: the registry has no separate detail route, a malformed response cannot
  be an idle success, and dropdown items must be inspected through the dropdown
  widget. Kept route/form/activity checks and enabled normal CI discovery.
  Preserved requirement-based crash/race assertions and fixed the implementation.
  Corrected new test API-path/widget-access assumptions and advanced the shared
  feedback timer deterministically. Rejected claims that mock/widget checks
  prove an APK, live provider workflow or physical-device operation. Historical
  contribution records and the student's reflection were preserved.
- Verification/evidence: Complete default Flutter suite **129/129**, no failures
  or skips; clean analysis. Endpoint/UI validators and **35/35** validator tests
  pass. The new profile workflow checks invalid input without requests, cited
  draft creation, new immutable version, blocked self-review, separate review,
  cancelled deactivation without DELETE and confirmed deactivation, with exact
  public methods/paths, evidence values and states. Lifecycle tests first
  reproduced disposed-widget confirmation and stale-response defects. The
  excluded file initially failed six of seven tests when run explicitly.
  Refreshed `flutter doctor -v` reports no issues; the attached Android 16 phone
  is authorized and SDK licences are accepted. Public gateway/API/Marine health
  returns 200 and unauthenticated profiles return 401. Android packaging reached
  native compilation/JNI extraction but failed with insufficient disk space:
  C: had zero free bytes, D: approximately 2.2 GiB. No APK or phone runtime pass
  is claimed; the user was asked to free build-cache space for the remaining
  check. Logs are under `local/marine-verification-2026-10-10/`. Focused diff
  whitespace checks pass. No commit or push was created.

## 2026-10-10 — Relocate Flutter build caches and validate Android packaging

- Date/time or time range: 2026-10-10 07:24 (Asia/Colombo; completion)
- GitHub Username: `sanudaabey`
- Team Member Name (actual): Sanuda Abeysinghe
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-6
- Summary of the user's request: Remove the Gradle cache from C: and use the
  existing `D:\WKS\FlutterBuildCache` directory, checking whether its cached
  artifacts align with the current Flutter build.
- Summary of what the AI Agent did: Inspected both cache locations and current
  wrapper/plugin/engine versions, stopped the old Gradle daemon, verified the
  exact ordinary C: cache directory and removed it under the user's explicit
  authorization. Persisted user-level Gradle and pub cache paths to the existing
  D: folders and selected its temporary directory for build processes. Reused
  compatible artifacts and resolved missing pinned pub dependencies. Built an
  ARM64 debug APK, installed it while preserving app data, configured the USB
  gateway tunnel and launched the native app. Updated root/mobile setup guides,
  component status and test evidence.
- AI output accepted/changed/rejected: Accepted reuse of the existing D: caches
  after verifying Gradle 9.3.1, Android plugin 9.1.0, Kotlin 2.4.0 and the exact
  Flutter engine artifacts. Changed only user cache settings; temporary settings
  apply to the build process. Retained older compatible cached versions and the
  unchanged dependency lockfile. Discarded generated platform-file normalization
  only after confirming it had no substantive diff and was clean before this
  task. Rejected claims that the signed-out device launch proves authenticated
  Marine or live-provider workflows. Preserved earlier contribution records.
- Verification/evidence: Removed 3.59 GiB of C: Gradle cache and recovered
  approximately 3.7 GiB; the old cache remains absent after the successful build.
  User settings and generated package references select the D: caches. Dependency
  resolution succeeds, analysis is clean and the complete Flutter suite passes
  **129/129** with no failures/skips. ARM64 debug packaging succeeds in 207.5
  seconds; the SDK XML-version warning is nonfatal. APK size is 87,152,051 bytes
  and SHA-256 is
  `6B3AB055FDED0818BF0CFCB71F97A0E69E98E8B3FE2321F8B56490056D2926F7`.
  Installation succeeds on the attached Android 16 phone, its signed-out coastal
  welcome screen renders, the app remains the resumed activity and scoped
  Flutter/AndroidRuntime error output is empty. Authenticated Marine phone
  workflows remain unverified. Logs and screenshot are under
  `local/marine-verification-2026-10-10/`, including `android-d-cache-build.log`,
  `d-cache-analysis.log`, `d-cache-tests.log`, `android-install.log`,
  `android-launch.log`, `android-runtime-errors.log` and `android-launch.png`.
  Focused diff whitespace checks pass. No commit or push was created.
