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
