# Ushan Srinuka — AI Usage Contributions

## 2026-09-27 — Integrated authentic geographic Sri Lanka map and dynamic database destination resolution

- Date/time or time range: 2026-09-27 23:30 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Antigravity
- Tool/App: Google Antigravity
- AI Model: Gemini 2.5 Pro
- Summary of the user's request:
  1. Fix the Map view: remove the hand-drawn SVG approximation and implement a proper, genuine, geographically accurate map of Sri Lanka.
  2. Eliminate all hardcoded coastal locations from the UI; fetch all destinations, quick-pick highlights, and coordinates dynamically from the backend API and PostgreSQL database.
- Summary of what the AI Agent did:
  - Replaced the static SVG map with an authentic, high-resolution OpenStreetMap cartographic embed of Sri Lanka (`https://www.openstreetmap.org/export/embed.html`):
    - Geographically exact representation of Sri Lanka's island shape, bays, natural harbours, peninsulas (Jaffna, Kalpitiya), and surrounding marine bodies (Indian Ocean, Bay of Bengal, Gulf of Mannar).
    - Dynamic regional view switching for All Coastlines, Southern Coast, Eastern Coast, Western Coast, and Northern Coast.
    - Dynamic bounding box and pinpoint marker calculation whenever a destination is selected or searched.
    - Integrated direct full-screen map link to OpenStreetMap and discreet bottom attribution badge.
  - Removed all hardcoded destination arrays (`PRESET_SPOTS` in Nearby tab, and static button list in Map tab) in `ExperiencesPage.tsx`:
    - Both the quick coordinates in the Nearby Proximity Search and the coastal highlights in the Map view now dynamically map over `activeDestinations`, populated directly from the backend API `/api/experiences/destinations` (the PostgreSQL database).
    - Preserved fallback definitions for initial load and unit-test environments where destination responses are mocked empty.
  - Rebuilt the Docker frontend container `blueverse-frontend` with the latest build.
  - Verified with automated tests and CI contract validators:
    - 167/167 web tests passing in `apps/web`.
    - Both `validate_ui_integrations.py` and `validate_endpoint_catalog.py` pass with 0 errors.
- AI output accepted/changed/rejected: Accepted live OpenStreetMap geographic embed for authentic coastline rendering and zero client library overhead; accepted dynamic mapping of database-seeded destinations.
- Verification/evidence:
  - `npm run build` in `apps/web` passed with exit code 0 (`tsc -b && vite build` built cleanly);
  - All 10 experience component tests passed (`WEB-EXP-001` through `WEB-EXP-010`);
  - `docker compose build frontend; docker compose up -d frontend` succeeded;
  - `python scripts/validation/validate_ui_integrations.py` passed with 0 errors (`UI integration contract: OK`);
  - `python .agents/scripts/validate_endpoint_catalog.py` passed with 0 errors (`Endpoint catalog: OK`).

## 2026-09-27 — Map UX overhaul: visual interactive Sri Lanka coastal map, geocoding fixes, and technical content cleanup

- Date/time or time range: 2026-09-27 23:05 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Antigravity
- Tool/App: Google Antigravity
- AI Model: Gemini 2.5 Pro
- Summary of the user's request: Fix major UI/UX issues in the Map & Place Lookup view:
  1. Fix the geocoding place search error ("Search query parameter 'q' must be at least 2 characters") when searching for places like "mirissa" or "mirissa beach".
  2. Implement an actual visual interactive map rather than plain text cards.
  3. Remove developer-facing technical jargon ("server-side", "canonical", "OpenFreeMap vector tile schemas", "Tile Endpoint", etc.) and replace with user-friendly coastal copy.
  4. Remove the huge "Vector Tile Configuration" and oversized attribution cards that damaged the UX, replacing them with a sleek visual map and subtle, standard corner attribution.
- Summary of what the AI Agent did:
  - Updated `MapController.SearchPlaces` in `services/experience-biodiversity/Controllers/MapController.cs` to accept both `q` and `query` parameters and updated `experienceApi.ts` `searchMapPlaces` to pass `?q=...`, resolving query parameter mismatch.
  - Rebuilt the Docker image `blueverse-experience-biodiversity:latest` and recreated the container, confirming live curl queries to `/api/experiences/map/search?q=mirissa` return matching coastal destinations.
  - Completely replaced the technical "Vector Tile Configuration" and developer cards in `ExperiencesPage.tsx` with a responsive, visual, interactive SVG Coastal Map of Sri Lanka:
    - Cartographic representation of Sri Lanka's coastline, shallow marine reef shelf, bathymetry ripples, and compass rose.
    - Interactive pan and zoom controls (`+`, `−`, `⟲`) with coastal zone focus presets (South Coast, East Coast, West Coast, North & Islands).
    - Interactive markers for all destinations with pulsating selection radar, coordinate tooltips, and dynamic selection cards.
    - Seamless integration between place search results and map pins (auto-centers and marks search matches on the map).
    - Reduced attribution to a standard, discrete 1-line badge (`Map data © OpenStreetMap contributors · OpenFreeMap`) in the bottom corner of the map.
  - Replaced technical developer jargon across the entire experience with welcoming, user-friendly coastal phrasing.
  - Added test case `WEB-EXP-010` in `experiences.component.test.js` verifying the interactive map rendering and place search flow.
- AI output accepted/changed/rejected: Accepted custom responsive SVG vector map adhering to `DESIGN.md` coastal palette without external mapping script dependencies; accepted non-intrusive bottom corner attribution.
- Verification/evidence:
  - `npm run build` in `apps/web` succeeded (`tsc -b && vite build` bundled in 1.80s);
  - All 167 web unit and component tests passed (`WEB-EXP-001` through `WEB-EXP-010`);
  - 75/75 backend tests passed in Docker container (`Failed: 0, Passed: 75, Skipped: 0`);
  - `python scripts/validation/validate_ui_integrations.py` passed with 0 errors (`UI integration contract: OK`);
  - `python .agents/scripts/validate_endpoint_catalog.py` passed with 0 errors (`Endpoint catalog: OK (69 public endpoints, 32 frontend routes)`).

## 2026-09-27 — Implementation of React Web App for experience-biodiversity service

- Date/time or time range: 2026-09-27 22:05 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Antigravity
- Tool/App: Google Antigravity
- AI Model: Gemini 2.5 Pro
- Summary of the user's request: Analyze the experience-biodiversity service and implement the React Web App part of the experience-biodiversity service. Stick strictly to DESIGN.md and the original theme of the React Web App (`apps/web`). Do not modify the architecture of any UI that has been currently implemented; perform only necessary additions for continuity and integration.
- Summary of what the AI Agent did:
  - Adjusted API Gateway reverse-proxy route in `services/api/appsettings.json` so experience discovery endpoints support anonymous visitor access while securely forwarding authenticated tokens.
  - Authored typed TypeScript client API service in `apps/web/src/features/experiences/experienceApi.ts` covering all 34 public experience endpoints, RFC 7807 structured error parsing (`ExperienceApiError`), and `withLoadingScreen` integration.
  - Implemented 5 React Web pages adhering to the coastal editorial theme (`coast-paper`, `coast-sand`, `coast-deep`, `coast-teal`, `coast-pearl`, `coast-sage`, `coast-glass`):
    - `ExperiencesPage.tsx`: Multi-tab coastal discovery hub featuring catalogue search & filtering (destinations, activities, offerings), location-aware proximity search with Haversine distance calculations and preset coastal spots, and OpenFreeMap / Photon map geocoding lookup.
    - `DestinationDetailPage.tsx`: Detailed destination profile showing geographical coordinates, real-time Marine Safety conditions (water condition, wave height, wind speed), Coastal Operations harbor advisories, and Member 3 Biodiversity ML predictions (focal species, occurrence probabilities, habitat suitability, model provenance, and uncertainty disclaimer).
    - `OfferingDetailPage.tsx`: Complete experience package details, scheduled departures timetable, and interactive real-time availability evaluation (`POST /api/experiences/availability/evaluations`) testing slot capacity and operational restrictions.
    - `FavouritesPage.tsx`: Saved wishlist manager for destinations, activities, and offerings with guest sign-in prompts and bookmark management.
    - `CatalogueManagementPage.tsx`: Curation workspace for destinations, activities, offerings, and schedules with pre-flight publication evaluation audits (`.../publication-evaluations`), publication lifecycle transitions, and peer microservices / Agent Seam diagnostics (`/api/experiences/dependencies/status`, `/api/experiences/agent/context`).
  - Added clean navigation links in `SiteHeader.tsx` and `SiteFooter.tsx` preserving preexisting architecture.
  - Registered web routes in `apps/web/src/app/routes.tsx` and parallel mobile routes in `apps/mobile/lib/main.dart` for cross-platform parity.
  - Registered all 5 UI workflows and 34 endpoints in `docs/contracts/ui-integration.json`, updated `docs/api/endpoint-catalog.json`, and regenerated `docs/api/endpoint-catalog.md`.
  - Authored 9 component tests in `apps/web/src/pages/experiences/__tests__/experiences.component.test.js` covering test cases `WEB-EXP-001` through `WEB-EXP-009`.
  - Resolved TypeScript `verbatimModuleSyntax` and unused imports to ensure clean production builds (`npm run build`).
- AI output accepted/changed/rejected: Accepted coastal editorial design system without third-party UI component libraries; accepted strict literal API call paths for static contract verification; preserved preexisting header, footer, and navigation patterns without modification to existing UI architecture.
- Verification/evidence:
  - `npm run build` in `apps/web` passed (`tsc -b && vite build` bundled in 2.84s with 0 errors);
  - All 9 experience component tests passed (`WEB-EXP-001` to `WEB-EXP-009`) with zero key prop warnings;
  - `python scripts/validation/validate_ui_integrations.py` passed with 0 errors (`UI integration contract: OK`);
  - `python .agents/scripts/validate_endpoint_catalog.py` passed with 0 errors (`Endpoint catalog: OK (69 public endpoints, 32 frontend routes)`).

## 2026-09-27 — Implementation of full test suite for experience-biodiversity backend service

- Date/time or time range: 2026-09-27 20:15 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Antigravity
- Tool/App: Google Antigravity
- AI Model: Gemini 3.8 Flash
- Summary of the user's request: Implement all test cases that can be implemented for experience-biodiversity backend service.
- Summary of what the AI Agent did:
  - Authored a comprehensive test suite for the `Blueverse.ExperienceBiodiversity` backend service containing 75 unit and integration tests across 12 distinct test classes.
  - Implemented unit tests for `UserContext` covering claim extraction (`sub`, `NameIdentifier`), `X-User-Id` header parsing, Bearer JWT payload decoding (arrays and strings for roles and permissions), Cookie JWT parsing (`blueverse_access_token`), unauthenticated context, and malformed JWT resilience.
  - Implemented unit tests for `DataSeeder` verifying canonical seed data insertion (5 destinations, 4 activities, 3 offerings, 3 schedules) and idempotency across repeated runs.
  - Expanded `DestinationsApiTests` covering non-existent destination 404 ProblemDetails, updating destination properties, query filtering by region/status/pagination, and peer integration endpoints (`/marine-conditions` and `/operational-advisories`).
  - Expanded `ActivitiesApiTests` covering non-existent activity 404 ProblemDetails, updating activity properties, duplicate code conflicts (409), and category query filtering.
  - Expanded `OfferingsApiTests` covering invalid activity validation (400), updating offerings, schedule interval validation (`EndsAt <= StartsAt` returning 400 ProblemDetails), schedule update lifecycle, schedule deletion, and non-existent schedule error handling.
  - Expanded `FavouritesApiTests` covering unauthenticated requests (401), invalid target types (400), non-existent target items (404), deleting non-existent targets (204 idempotent), and favouriting Activity and Offering targets alongside Destination targets.
  - Preserved and verified all other integration suites: `MapApiTests`, `NearbyApiTests`, `BiodiversityApiTests`, `AvailabilityApiTests`, `AgentSeamApiTests`, `DependenciesApiTests`, `HealthApiTests`, and `MicroserviceResilienceTests`.
  - Executed all 75 automated test cases in the pinned .NET 10 SDK Alpine container (`dhi.io/dotnet:10-sdk-alpine`) with 100% pass rate.
  - Validated repository contracts using `validate_endpoint_catalog.py` and `validate_ui_integrations.py`.
- AI output accepted/changed/rejected: Accepted all test cases, RFC 7807 error schema assertions, resilience test cases, and boundary/validation scenarios; preserved non-negotiable architectural boundaries and verified isolation of in-memory test databases.
- Verification/evidence: 75/75 tests passed in the .NET 10 container (`Passed: 75, Failed: 0, Skipped: 0`, execution time: ~23.8s); `validate_endpoint_catalog.py` passed (69 public endpoints, 22 frontend routes); `validate_ui_integrations.py` passed with 0 errors.

## 2026-09-27 — Backend re-evaluation, bug fixes, test suite expansion, and finalization

- Date/time or time range: 2026-09-27 19:55 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Antigravity
- Tool/App: Google Antigravity
- AI Model: Gemini 3.8 Flash
- Summary of the user's request: Re-evaluate the whole backend once again, fix any errors, issues, or bugs found, and finalize the backend.
- Summary of what the AI Agent did:
  - Conducted an exhaustive review of all controllers, DTOs, domain models, services, error handling, and test fixtures across the `experience-biodiversity` microservice.
  - Standardized all 404 responses across `DestinationsController`, `ActivitiesController`, and `OfferingsController` to return structured RFC 7807 `ProblemDetails` consistently instead of bare `NotFound()`.
  - Added query validation in `MapController.SearchPlaces` ensuring minimum search term length of 2 characters.
  - Enriched `FavouritesController.AddFavourite` to resolve and return `targetTitle` and `targetStatus` immediately upon creation and idempotent addition, avoiding redundant lookups.
  - Enhanced `UserContext` to support HTTP-only cookie session fallback (`blueverse_access_token`) alongside Bearer headers, ensuring parity between React Web and Flutter Mobile clients.
  - Expanded automated test coverage by adding `ActivitiesApiTests` and `OfferingsApiTests`, bringing the suite to 35 comprehensive unit and integration tests covering CRUD, conflict handling, lifecycle transitions, and schedules.
  - Verified Docker container rebuild and live endpoint responses through the API gateway.
- AI output accepted/changed/rejected: Accepted structured RFC 7807 consistency, cookie fallback, enriched favourite DTOs, and expanded test suites; maintained strict service boundaries without touching external services.
- Verification/evidence: All 35 tests passed in the .NET 10 test container (`Passed: 35, Failed: 0, Skipped: 0`); live endpoint verification confirmed 200, 204, 400, 401, 404 statuses with zero 500 errors; `validate_endpoint_catalog.py` passed (69 public endpoints, 22 frontend routes); `validate_ui_integrations.py` passed with 0 errors.

## 2026-09-27 — Fix 500 missing database tables, audit endpoint status codes (200/400/401), and enhance user context

- Date/time or time range: 2026-09-27 18:45 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Antigravity
- Tool/App: Google Antigravity
- AI Model: Gemini 3.8 Flash
- Summary of the user's request: Diagnose and resolve the HTTP 500 error on `GET /api/experiences/activities` (`relation "activities" does not exist`), audit endpoint status codes where some endpoints returned 200 and others 401 when tested via Swagger without required fields or tokens, and restart and continue the process where it stopped.
- Summary of what the AI Agent did:
  - Identified the root cause of the 500 error: EF Core migration files were generated on the host but the running Docker container had not yet been rebuilt with them, leaving PostgreSQL without domain tables.
  - Rebuilt `blueverse-experience-biodiversity:latest` Docker image cleanly with `--no-cache` to ensure valid runtime configuration and migration artifacts.
  - Recreated and started the container, confirming via logs that `MigrateAsync()` created all domain tables (`destinations`, `activities`, `offerings`, `schedules`, `favourites`) and seeded canonical Sri Lankan coastal experience records.
  - Enhanced `UserContext` with resilient fallback extraction of user identity (`sub`), roles, and permissions from the forwarded JWT Bearer token payload or `X-User-Id` header so private microservice endpoints (like `/favourites`) correctly authenticate requests forwarded by the gateway.
  - Thoroughly tested and verified all endpoints via gateway with curl: confirmed 200 OK on data queries, 400 Bad Request on missing required fields (`/nearby` without coordinates, `/map/search` without query), 401 Unauthorized on unauthenticated requests (`/favourites` without token), 404 Not Found on non-existent IDs, and graceful resilient fallback on peer service calls.
- AI output accepted/changed/rejected: Accepted full database migration and seed execution; accepted Bearer token claim parsing in `UserContext`; preserved all existing endpoint contracts.
- Verification/evidence: Live endpoint tests via curl returned expected status codes (200, 400, 401, 404, 204); 0 internal server errors (500); 28 unit and integration tests passed in Docker; `validate_endpoint_catalog.py` passed (69 public endpoints, 22 frontend routes); `validate_ui_integrations.py` passed with 0 errors.

## 2026-09-27 — Docker network port 8080 alignment, Docker image creation, and environment configuration

- Date/time or time range: 2026-09-27 16:10 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Antigravity
- Tool/App: Google Antigravity
- AI Model: Gemini 2.5 Pro
- Summary of the user's request: Align all peer microservice URLs in the Docker
  network to port 8080. Confirm that the Docker image can be built and tested
  via the public API Swagger interface. Update .env matching .env.example with
  missing variables.
- Summary of what the AI Agent did: Updated default peer microservice URLs in
  appsettings.json, IMarineSafetyConsumerService, IBiodiversityConsumerService,
  IOperationalStatusConsumerService, IDependenciesDiagnosticsService, and compose.yaml
  to port 8080. Built the Docker image `blueverse-experience-biodiversity:latest`
  using DHI base images. Compared and updated .env and .env.example with missing
  variables (EXPERIENCE_BIODIVERSITY_SERVICE_URL, MARINE_SAFETY_SERVICE_URL,
  PLANNER_SERVICE_URL, OPERATIONS_SERVICE_URL). Verified that the API gateway's
  Swagger UI (`/api/swagger`) mounts `/api/experiences/swagger/v1/swagger.json`.
- AI output accepted/changed/rejected: Unified container ports to 8080; verified
  Swagger routing via YARP without requiring React or Flutter clients.
- Verification/evidence: `docker build` succeeded with tag
  `blueverse-experience-biodiversity:latest`; 28 unit and integration tests passed;
  validate_endpoint_catalog.py passed (69 public endpoints, 22 frontend routes);
  validate_ui_integrations.py passed with 0 errors.

## 2026-09-27 — Independent microservice resilience, retries, timeouts, and peer dependency tracking

- Date/time or time range: 2026-09-27 15:45 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Antigravity
- Tool/App: Google Antigravity
- AI Model: Gemini 2.5 Pro
- Summary of the user's request: Ensure experience-biodiversity runs as an independent
  microservice that never falls or fails just because peer microservices are not present.
  Assume basic endpoint structures for other services (marine-safety, coastal-planner,
  coastal-operations). Request data from them with bounded timeouts and retries, and
  explicitly notice and mention whether the remote endpoints responded or not, returning
  safe fallbacks without failing.
- Summary of what the AI Agent did: Built `IResilientHttpExecutor` / `ResilientHttpExecutor`
  with per-attempt timeouts, exponential backoff retries, and comprehensive execution
  tracking (`Responded`, `TargetEndpoint`, `AttemptsCount`, `LatencyMs`, `RemoteStatus`,
  `Message`). Updated `BiodiversityConsumerService` (Member 3 integration) and
  `OperationalStatusConsumerService` (Member 4 integration) to use resilient calls with
  explicit response reporting. Created `IMarineSafetyConsumerService` (Member 2 integration)
  and `IDependenciesDiagnosticsService` for peer health and status reporting. Added
  `GET /api/experiences/destinations/{id}/marine-conditions`,
  `GET /api/experiences/destinations/{id}/operational-advisories`, and
  `GET /api/experiences/dependencies/status`. Configured microservice URLs in `appsettings.json`.
  Added 7 new unit and integration tests (28 tests total, all passing). Updated and
  validated `docs/api/endpoint-catalog.json` (69 public endpoints) and `endpoint-catalog.md`.
- AI output accepted/changed/rejected: Adopted bounded 2-second attempt timeouts with
  2-retry exponential backoff; ensured all fallback contexts explicitly record whether
  the remote service responded or not; preserved zero-crash microservice autonomy.
- Verification/evidence: 28 unit and integration tests passed in Docker (.NET 10 SDK)
  with 0 failures; validate_endpoint_catalog.py passed with 0 errors (69 public endpoints,
  22 frontend routes); validate_ui_integrations.py passed with 0 errors; git diff clean.

## 2026-09-27 — Map API evaluation and Experience & Biodiversity backend implementation

- Date/time or time range: 2026-09-27 15:20 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Antigravity
- Tool/App: Google Antigravity
- AI Model: Gemini 2.5 Pro
- Summary of the user's request: Evaluate almost 100% free Map API providers
  (including OpenFreeMap) and decide the Map provider for BLUEVERSE. In parallel,
  implement the full backend for the Experience & Biodiversity component.
- Summary of what the AI Agent did: Evaluated OpenFreeMap, OpenStreetMap/Nominatim,
  Photon, Stadia Maps, Mapbox, and Protomaps, selecting OpenFreeMap for client vector
  tiles and Photon for geocoding with database fallback. Implemented the complete
  Experience & Biodiversity backend in .NET 10: domain models (Destination, Activity,
  Offering, Schedule, Favourite, PublicationStatus, AvailabilityStatus), EF Core
  entity configurations with unique constraints, data seeder with canonical Sri Lankan
  destinations, business services (EvaluationService, MapProviderService,
  BiodiversityConsumerService with ADR-0019 safe degradation, OperationalStatusConsumerService,
  UserContext), and 9 controllers (Destinations, Activities, Offerings, Availability,
  Nearby, Map, Favourites, Biodiversity, AgentSeam) with 31 public endpoints.
  Created an automated test project with 21 unit and integration tests. Updated
  docs/api/endpoint-catalog.json and regenerated docs/api/endpoint-catalog.md.
- AI output accepted/changed/rejected: Accepted OpenFreeMap + Photon architecture;
  implemented all 31 endpoints following RESTful patterns and non-CRUD evaluation;
  preserved the pre-G07 read-only agent seam returning not_connected; kept all tests
  passing in isolated InMemory database test environment.
- Verification/evidence: 21 unit and integration tests passed in Docker (.NET 10 SDK)
  with 0 failures; validate_endpoint_catalog.py passed with 0 errors (66 public endpoints,
  22 frontend routes); validate_ui_integrations.py passed with 0 errors; git status
  cleanly tracked all new files.

## 2026-09-27 — Database status in component health check

- Date/time or time range: 2026-09-27 03:16 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Include PostgreSQL connection status in the
  Experience & Biodiversity health response, matching Auth.
- Summary of what the AI Agent did: Added a bounded, anonymous EF Core
  connectivity check to the health controller. It returns the Auth-style
  service/status/database fields and 503 when PostgreSQL is unavailable.
  Updated the G00 proposal, API endpoint catalog and API documentation.
- AI output accepted/changed/rejected: Mirrored Auth's response behavior and
  timeout; kept the endpoint anonymous. No other routes or client workflows
  were changed.
- Verification/evidence: The service `dotnet build --no-restore` passed with
  0 warnings and 0 errors; endpoint catalog generation/validation passed
  (35 public endpoints, 22 frontend routes); `git diff --check` passed. No
  tests were added or run.

## 2026-09-27 — Align component database setup with Auth

- Date/time or time range: 2026-09-27 02:51 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Replace the Experience & Biodiversity SQL
  initialization approach with the Auth service's EF Core setup and keep all
  services on one PostgreSQL database.
- Summary of what the AI Agent did: Removed the PostgreSQL init SQL mount and
  service-specific password setting. Configured the service to use the same
  database, login and password as Auth, the PostgreSQL default schema, and an
  independent EF migration-history table. Added bounded connection checking
  and startup migration handling. Updated deployment, database, architecture,
  readiness, CI and owner proposal documentation.
- AI output accepted/changed/rejected: Replaced the earlier dedicated login
  and schema bootstrap as requested. Kept G00 pending and added no domain
  entities, tables or migrations.
- Verification/evidence: The service `dotnet build --no-restore` passed with
  0 warnings and 0 errors; `appsettings.json` parsed and `git diff --check`
  passed. Docker is unavailable, so Compose/live database behavior was not
  verified. No tests were added or run.

## 2026-09-27 — Experience & Biodiversity PostgreSQL connection

- Date/time or time range: 2026-09-27 02:23 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Implement the Experience & Biodiversity
  service's database connection and update the relevant documentation.
- Summary of what the AI Agent did: Added EF Core/Npgsql configuration, a
  schema-scoped DbContext and design-time factory, and a bounded startup
  connection check. Added PostgreSQL role/schema provisioning for the service,
  Compose credentials/network/dependency wiring and the Docker stack-health
  workflow environment value. Updated setup, deployment, database, architecture
  and readiness documentation.
- AI output accepted/changed/rejected: Kept the work to connection and schema
  infrastructure; added no domain entities, business tables, migrations or
  workflows. G00 remains pending.
- Verification/evidence: `dotnet build` for the service project with
  `--no-restore` passed with 0 warnings and 0 errors; `appsettings.json` parsed
  successfully; `git diff --check` passed. Docker and `psql` are unavailable in
  this environment, so Compose and live PostgreSQL behavior were not verified.
  Python is not on `PATH`, so the repository catalog/route validators could not
  be rerun. No tests were added or run.

## 2026-09-27 — Swagger and gateway JWT security

- Date/time or time range: 2026-09-27 01:29 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Remove the Swagger Servers field and protect
  the Experience & Biodiversity service using Auth JWT security while matching
  the API/Auth structure.
- Summary of what the AI Agent did: Replaced built-in OpenAPI generation with
  Swashbuckle and the API/Auth Bearer configuration. Protected the component
  YARP route with the API default JWT policy and kept only health and the
  Swagger document anonymous. Updated the public Swagger URL, endpoint
  catalog, security/deployment documentation and CI health check.
- AI output accepted/changed/rejected: Applied the existing API gateway JWT
  validation boundary; did not add duplicate JWT secrets or direct Auth calls
  to the private service. Kept G00 pending and added no business workflows.
- Verification/evidence: Service and API `dotnet build --no-restore` passed;
  endpoint catalog validation passed (35 public endpoints, 22 frontend routes),
  UI integration validation passed, and `git diff --check` passed. No tests were
  added or run.

## 2026-09-27 — Same-origin health check and concise route

- Date/time or time range: 2026-09-27 00:55 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Fix Swagger Try it out using the Docker-only
  service host and use the shorter `/api/experiences` public route for the
  Experience & Biodiversity health/OpenAPI endpoints.
- Summary of what the AI Agent did: Configured a relative `/` server URL in the
  component OpenAPI document, renamed its public gateway paths to
  `/api/experiences`, updated Swagger UI and the stack health workflow to use
  the public alias, and synchronized the endpoint catalog and documentation.
  Kept the internal Compose/YARP destination name `experience-biodiversity`.
- AI output accepted/changed/rejected: Applied the requested browser-visible
  route and same-origin behavior; left the service private and G00 pending.
  No domain workflow, persistence or Agentic AI execution was added.
- Verification/evidence: API and service `dotnet build --no-restore` passed;
  endpoint catalog validation passed (35 public endpoints, 22 frontend routes)
  and UI integration validation passed; `git diff --check` passed. Docker is
  unavailable in this environment, so Compose runtime behavior was not checked.
  No tests were added or run.

## 2026-09-27 — Experience & Biodiversity service host bootstrap

- Date/time or time range: 2026-09-27 00:26 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Initialize the Experience & Biodiversity
  service host, health controller and Docker setup on its assigned feature
  branch, following the existing API/Auth service structure.
- Summary of what the AI Agent did: Added controller/authorization/OpenAPI
  host setup, anonymous process liveness, structured unhandled-error responses,
  a DHI-based Dockerfile, private Compose networking, API YARP routing and a
  Swagger UI document entry. Updated endpoint catalog, service/readiness and
  deployment documentation to identify the scaffold as host-only.
- AI output accepted/changed/rejected: Used the service identity, route prefix
  and port proposed in Ushan's G00 input for this requested bootstrap while
  retaining G00 as pending; no shared agreement, domain workflow, persistence,
  provider integration or Agentic AI execution was claimed or implemented.
- Verification/evidence: Endpoint catalog generation and validation passed
  (35 public endpoints, 22 frontend routes; no AI endpoints). The service
  built successfully with `dotnet build ... --no-restore`. `git diff --check`
  passed. A restoring build was blocked by denied access to the user NuGet
  configuration; Docker/Compose validation was unavailable because Docker
  CLI is not installed. No tests were added or run.

## 2026-09-26 — Experience & Biodiversity G00 contract input

- Date/time or time range: 2026-09-26 22:55 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Prepare only G00 decisions for the
  experience-biodiversity component on its assigned feature branch and update
  the AI usage log as Ushan.
- Summary of what the AI Agent did: Reviewed the repository's G00 readiness,
  component contract, branch workflow, architecture and existing branch state.
  Added a Member 1 G00 proposal covering service/data identity, canonical IDs,
  candidate public operations and permissions, publication/availability and
  time semantics, UI workflows, and cross-component handoffs. Linked it from
  the component contract. Kept the shared gate pending and preserved existing
  untracked service starter files and the unrelated API health-controller
  deletion.
- AI output accepted/changed/rejected: Retained the component-specific
  contract proposals. Marked shared-owner items as pending review and the
  document as a proposal because G00 is still pending in the repository.
  No team agreement or implementation was represented as complete.
- Verification/evidence: `git diff --check` passed; relative Markdown links in
  the changed component documentation resolved. No tests or application
  validation were run for this documentation-only G00 proposal.
