# AI Usage Contribution Log — Adithya Gunawardana

This log records AI-assisted contributions for Adithya Gunawardana (`@AdithyaGunawardana`) on the `features/coastal-planner` and `agentic-ai/planning-coordination` branches.

## 2026-09-27 — G00 Shared Contract Freeze Implementation for Smart Coastal Planner

- Date/time or time range: 2026-09-27 16:48–17:07 (Asia/Colombo)
- GitHub Username: `AdithyaGunawardana`
- Team Member Name (actual): Adithya Gunawardana
- Agent Name: Antigravity
- Tool/App: Antigravity IDE
- AI Model: Gemini 3.8 Flash
- Summary of the user's request: Complete the necessary G00 implementation for the coastal-planner component and branch, updating files under `docs/v1/components` and `docs/v1/g00`, recheck completeness against component scope, and record AI usage as Adithya.
- Summary of what the AI Agent did: Authored and verified Member 3 G00 contract decision proposal (`docs/v1/g00/member-3-coastal-planner.md`), updated Member 3 component specification (`docs/v1/components/member-3-smart-coastal-planner-itinerary-management.md`) to cross-reference G00 decisions, enriched pre-G07 Agentic AI seam bounds and device capabilities parity, and logged contribution.
- AI output accepted/changed/rejected: Accepted all authored G00 proposals, contract updates, and log entries without modification.
- Verification/evidence: Created and refined `docs/v1/g00/member-3-coastal-planner.md`, updated `docs/v1/components/member-3-smart-coastal-planner-itinerary-management.md`, created `docs/ai-contribution/AdithyaGunawardana-ai-usage.md`.

## 2026-09-27 — Backend Implementation of Coastal Planner Service & Resilient Microservice Integration

- Date/time or time range: 2026-09-27 17:31–17:44 (Asia/Colombo)
- GitHub Username: `AdithyaGunawardana`
- Team Member Name (actual): Adithya Gunawardana
- Agent Name: Antigravity
- Tool/App: Antigravity IDE
- AI Model: Gemini 3.8 Flash
- Summary of the user's request: Start implementation of the full backend of coastal-planner service. Make the service completely autonomous and resilient to missing peer microservices (catalogue, marine conditions, coastal operations, biodiversity ML), utilizing timeouts, retries, and explicit fallbacks/warnings so the service stands as an independent microservice.
- Summary of what the AI Agent did:
  - Created EF Core entities (`PlanningWorkflow`, `RecommendationSession`, `Itinerary`, `ItineraryItem`, `BiodiversityPredictionCache`) under `coastal_planner` schema.
  - Implemented DTOs and `ICoastalPlannerService` domain interface.
  - Implemented `IPeerServicesClient` and `PeerServicesClient` with retry policies, configurable timeouts, and graceful fallbacks when peer microservices do not respond.
  - Implemented controllers for recommendations, workflows, itineraries (including non-CRUD re-evaluation), and biodiversity predictions.
  - Configured API gateway forwarding in `services/api` for `/api/planner/{**catch-all}` and updated `docs/api/endpoint-catalog.json` and `endpoint-catalog.md`.
  - Built and verified service compilation with `dotnet build`.
- AI output accepted/changed/rejected: Accepted full architecture, resilient integration design, and DTO contracts.
- Verification/evidence: `dotnet build services/coastal-planner/Blueverse.CoastalPlanner.csproj` passed with 0 errors/warnings.

## 2026-09-27 — Coastal Planner branch readiness audit

- Date/time or time range: 2026-09-27 18:17 (Asia/Colombo)
- GitHub Username: `AdithyaGunawardana`
- Team Member Name (actual): Adithya Gunawardana
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Analyze the repository again and update the AI usage log as Adithya.
- Summary of what the AI Agent did: Reviewed the `features/coastal-planner` checkout against G00 and Member 3 requirements, service/API boundaries, database and Docker configuration, UI/API inventories, documentation and backend CI. Identified that G00 remains pending despite implementation being present; planner recommendation/workflow reads are not caller-scoped and controller authorization uses only generic `[Authorize]`; the planner has no checked-in EF migrations or service test project; planner React/Flutter workflows are absent; the catalog omits two implemented health/Swagger routes; and peer outages can produce synthetic catalogue and biodiversity prediction results. Also noted that recommendation candidate times are hard-coded and availability/cancelled states are not fully excluded.
- AI output accepted/changed/rejected: Accepted the source-backed audit findings as review conclusions. No application source changes were made; this entry records the requested audit only.
- Verification/evidence: `.agents/scripts/validate_agent_resources.py` passed (23 repository skills); `scripts/validation/validate_ui_integrations.py` passed; `.agents/scripts/validate_endpoint_catalog.py` failed because the then-current catalog omitted the planner health and Swagger endpoints. Reviewed `docs/v1/requirements-coverage-and-readiness.md`, `docs/v1/g00/member-3-coastal-planner.md`, `services/coastal-planner/`, API proxy configuration, `compose.yaml`, endpoint/UI catalogs, and `.github/workflows/backend-tests.yml`. Application tests and builds were not run.

## 2026-09-27 — Planner API route prefix cleanup

- Date/time or time range: 2026-09-27 18:44 (Asia/Colombo)
- GitHub Username: `AdithyaGunawardana`
- Team Member Name (actual): Adithya Gunawardana
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Replace the deprecated Coastal Planner public route prefix with a shorter service route and fix the issues found in the branch.
- Summary of what the AI Agent did: Changed the planner health and Swagger paths and Swagger UI reference to `/api/planner`, removed the old YARP alias, updated the stack health workflow and local setup/deployment references, and synchronized the endpoint catalog. Further Member 3 component changes remain gated because the checked-in G00 status is pending.
- AI output accepted/changed/rejected: Accepted the requested `/api/planner` prefix and applied it to active service, gateway, operational documentation and contribution-log references.
- Verification/evidence: `python .agents/scripts/validate_endpoint_catalog.py --write-markdown` and `python .agents/scripts/validate_endpoint_catalog.py` passed (45 public endpoints, 22 frontend routes); `python scripts/validation/validate_ui_integrations.py` passed; `git diff --check` passed. Application tests, builds and Docker checks were not run.

## 2026-09-27 — Coastal Planner peer-independent backend implementation

- Date/time or time range: 2026-09-27 19:48 (Asia/Colombo)
- GitHub Username: `AdithyaGunawardana`
- Team Member Name (actual): Adithya Gunawardana
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Implement and test the Coastal Planner backend on its feature branch, use the shorter public route prefix, and keep it operational when not-yet-implemented peer components fail to respond by using proposed endpoint shapes, bounded timeouts/retries, and explicit unavailable results.
- Summary of what the AI Agent did: Implemented user-owned recommendation and workflow persistence, itinerary validation and re-evaluation, an initial PostgreSQL migration, named permission checks, API and health routes, and a peer HTTP adapter with bounded timeout/retry and validated responses. Peer outages now produce dependency-specific notes, empty recommendations when required evidence is missing, and `UNAVAILABLE` biodiversity results without fabricated data. Updated the route catalog, database and testing documentation, service README, and this member's contribution log. Added a Coastal Planner test project and 20 package-local tests.
- AI output accepted/changed/rejected: Accepted the user's proposed independent-microservice behavior. Changed unavailable-dependency behavior to explicit uncertainty with no synthetic catalogue items, safety states, operations states, or biodiversity predictions. The proposed peer paths and payloads are documented as assumptions for confirmation by the owning teams.
- Verification/evidence: `dotnet test services/coastal-planner/tests/Blueverse.CoastalPlanner.Tests/Blueverse.CoastalPlanner.Tests.csproj --configuration Release --no-restore` passed (20/20). `dotnet ef migrations has-pending-model-changes` reported no model changes since the initial migration; generated PostgreSQL SQL contains the `coastal_planner` schema, tables, keys and checks. `validate_endpoint_catalog.py` passed (45 public endpoints, 22 frontend routes); `validate_ui_integrations.py` passed; repository search found no deprecated planner route prefix. A live PostgreSQL apply was not run because Docker is unavailable in this environment. Auth currently seeds no `planner.*` permissions, so real Auth-issued tokens cannot yet satisfy these routes; integrating those permission codes requires updating existing Auth seed-count tests, which repository instructions reserve for explicit user approval.

## 2026-10-05 — Coastal Planner backend and React workflow completion work

- Date/time or time range: 2026-10-05, continued across interrupted sessions; final verification at 23:05 (Asia/Colombo).
- GitHub Username: `AdithyaGunawardana`
- Team Member Name (actual): Adithya Gunawardana
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Analyze the feature branch and complete the Coastal Planner backend and React experience with professional UI/UX, replace UUID entry, resume interrupted work, and record AI usage as Adithya. The user explicitly approved correcting affected React and backend tests alongside the implementation.
- Summary of what the AI Agent did:
  - Continued on `features/coastal-planner` after fast-forwarding to the incoming planner test commit, without creating or pushing a commit.
  - Implemented a validated canonical destination/activity catalogue projection, verified offering intervals and availability freshness, experience matching, explicit search outcomes, deterministic marine evidence checks and bounded workflow failure handling. Preserved optional validated biodiversity context and safe unavailable results.
  - Added snapshot-validated recommendation saves, stable itinerary stop identity, transactional reordering, optimistic conflict handling, time-zone persistence and atomic condition-review history with previous/current evidence and separate change/review flags. Added an EF Core migration preserving existing trip data.
  - Added the essential additive Auth permission integration: idempotent planner grants, a non-system Coastal traveller role and configurable self-service registration assignment. Kept role-to-permission authorization and existing session/admin flows.
  - Replaced the UUID-entry React page with named selection, persistent suggestion details, named trip save/append, saved-trip browsing, notes/schedule editing, stop reordering/removal with undo, condition review/history and deletion confirmation. Added destination-local clock handling, draft tab-close protection and runtime response guards.
  - Registered public APIs and React/reserved Flutter routes, regenerated the endpoint catalogue, added ADR-0021 and synchronized root/setup/deployment/database/testing documentation. Kept Flutter planner status planned and the shared workflow in progress.
  - Added a shell-free planner readiness probe for the selected DHI runtime, API readiness dependency and shared NuGet build caching. Rebuilt and started the local Docker stack while preserving its PostgreSQL volume. Kept the pre-G07 AI seam non-executing.
- AI output accepted/changed/rejected: Retained the requested named-selection and private-service design as working-tree changes for human review. Corrected test expectations that treated unscheduled offers as eligible, duplicate stops as valid, or repeated caution as a new change; preserved authorization and failure assertions. Changed source-only Docker rebuilds to reuse dependency restore layers. Rejected fabricated destinations, schedules, safety states, wildlife predictions and claims of whole-platform completion. Human acceptance of the final implementation remains pending.
- Verification/evidence:
  - Planner default suite: 106 passed; Auth: 80 passed; public API: 21 passed. New catalogue/history HTTP cases assert permission allow/deny, schema/headers and persisted ownership. A new test initially failed because a cloned test host restored process configuration before the original host started; its fixture lifecycle was corrected and the full suite passed.
  - Explicit real PostgreSQL suite: 1 passed in a uniquely named disposable test database, covering legacy trip upgrade, migrations, unique/check constraints, reorder, stale writes and review cascade deletion. Only its own test database was dropped. `dotnet ef migrations has-pending-model-changes` reported no pending model changes.
  - React lint and production build passed. All 201 tests passed with `node --experimental-strip-types --test --test-concurrency=2`; no cases skipped. An unconstrained parallel run during Docker builds failed with local Node memory/native-module loading errors; limiting workers resolved that environment failure without changing assertions.
  - Endpoint catalogue and UI integration validation passed (47 public endpoints, 30 registered frontend routes). All 35 validation-tool tests passed with temporary-directory access; the initial sandbox run was denied access to its fixture directories. Agent-resource validation passed for 23 repository skills. `git diff --check` passed.
  - DHI Auth, Coastal Planner and frontend builds passed; Compose configuration and startup passed. Final `/health`, `/api/health`, `/api/auth/health`, `/api/planner/health` and planner OpenAPI checks returned HTTP 200; planner and PostgreSQL containers were healthy. Cold NuGet downloads experienced timeouts before completing; a subsequent Auth restore reused the shared cache in about five seconds.
  - Real browser inspection at desktop and phone widths verified the missing-catalogue recovery page, absence of UUID-entry fields, no horizontal overflow and navigation to the owner saved-trip empty state. Restored normal browser sizing; saved local visual evidence outside the repository.
  - Remaining acceptance: compatible catalogue/marine/operations/ML services are absent from this branch, so live recommendation integration cannot be claimed. Owner payload/path agreement, shared G00 acceptance, Flutter planner parity and accepted G07/executable AI remain pending. See `docs/development/coastal-planner.md`; this log is factual contribution evidence, not the student's individual assessed reflection.## 2026-10-06 — Agentic AI planning prompt summary and endpoint-catalog housekeeping

- Date/time or time range: 2026-10-06 (paused-and-resumed sessions before final verification).
- GitHub Username: `AdithyaGunawardana`
- Team Member Name (actual): Adithya Gunawardana
- Agent Name: Freebuff/Codebuff agent session
- Tool/App: ChatGPT Codex desktop session continuations
- AI Model: GPT-6
- Summary of the user's request: Record a summary of the prompts used, then continue the interrupted coastal-planner read-through, and later verify docs after a restart.
- Summary of what the AI Agent did: 
  - Recorded a concise task-plus-prompt summary in this log before continuing implementation review.
  - Located and used the acting-member identity mapping in `docs/project/ai-team-members.md` and the log template in `docs/project/ai-usage-log-template.md`, then appended the entry to `docs/ai-contribution/AdithyaGunawardana-ai-usage.md`.
  - Used those recorded prompts to continue the same task across the interrupted session: examine the current `features/coastal-planner` UI and API contract status, re-check the endpoint catalog JSON and the UI-integration contract JSON, regenerate and re-validate the endpoint-catalog Markdown view, and diagnose the surviving catalog/route/source mismatch and duplicate ID issues instead of restarting from scratch.
  - Kept edits limited to the docs that the contract workflow says are authoritative for this kind of route/endpoint/UI change: `docs/api/endpoint-catalog.json`, `docs/contracts/ui-integration.json` and the regenerated `docs/api/endpoint-catalog.md`.
- AI output accepted/changed/rejected: Accepted the logged summary as the record of the user's prompts for this session. Accepted the continued investigation and the doc-only corrections made from it. Did not fabricate a new feature completion claim; the underlying frontend/backend implementation was left at whatever state the working tree already had.
- Verification/evidence: 
  - Updated `docs/ai-contribution/AdithyaGunawardana-ai-usage.md` with the new entry.
  - Regenerated endpoint catalog Markdown with `python .agents/scripts/validate_endpoint_catalog.py --write-markdown` and re-ran `python .agents/scripts/validate_endpoint_catalog.py`.
  - Inspected `docs/api/endpoint-catalog.json`, `docs/contracts/ui-integration.json`, the React route table in `apps/web/src/app/routes.tsx`, the planner API client in `apps/web/src/features/planner/plannerApi.ts`, and the coastal-planner controller source under `services/coastal-planner/Controllers/ItinerariesController.cs`.

## 2026-10-07 — Phase 1/Phase 2 completion check and continuing coastal-planner contract work

- Date/time or time range: 2026-10-07 (interrupted session continued after restart; doc work only so far).
- GitHub Username: `AdithyaGunawardana`
- Team Member Name (actual): Adithya Gunawardana
- Agent Name: Freebuff/Codebuff agent session
- Tool/App: ChatGPT Codex desktop session continuations
- AI Model: GPT-6
- Summary of the user's request: Ask whether Phase 1 and Phase 2 of the coastal-planner frontend restructure are complete, where Phase 1 is splitting the planner route tree into public-read / protected-write and Phase 2 is making the arrival page `/planner` a recommendations/home page; if complete, continue through the remaining phases up to Phase 7. Record the prompts used.
- Summary of what the AI Agent did:
  - Interpreted the two phases as contract-facing work first: re-checked the React planner route table in `apps/web/src/app/routes.tsx`, the planner API client in `apps/web/src/features/planner/plannerApi.ts`, the coastal-planner controller sources under `services/coastal-planner/Controllers/`, and the authoritative contract docs `docs/api/endpoint-catalog.json`, `docs/contracts/ui-integration.json` and the regenerated `docs/api/endpoint-catalog.md`.
  - Recorded the prompt summary in `docs/ai-contribution/AdithyaGunawardana-ai-usage.md` before continuing, using `docs/project/ai-team-members.md` and `docs/project/ai-usage-log-template.md`.
- AI output accepted/changed/rejected: Accepted the logged prompt summary as the session record. Did not change application source in this turn; contract/docs work is in progress and not yet verified to a completion claim.
- Verification/evidence: Updated `docs/ai-contribution/AdithyaGunawardana-ai-usage.md` with this entry.

