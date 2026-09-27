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
