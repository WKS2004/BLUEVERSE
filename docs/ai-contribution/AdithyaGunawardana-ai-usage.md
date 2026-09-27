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
