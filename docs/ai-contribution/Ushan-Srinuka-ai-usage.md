# Ushan Srinuka — AI Usage Contributions

## 2026-10-04 — Experience & Biodiversity Flutter Mobile implementation and comprehensive test suite validation

- Date/time or time range: 2026-10-04 19:35 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Antigravity
- Tool/App: Google Antigravity
- AI Model: Gemini 2.5 Pro
- Summary of the user's request:
  1. Recheck the entire Flutter Mobile App for the `experience-biodiversity` component to ensure all required components and workflows have been implemented successfully according to canonical requirements and contract specifications.
  2. Implement all unit and widget test cases for the Flutter Mobile App of the `experience-biodiversity` component across all 23 public API endpoints and interactive screens.
- Summary of what the AI Agent did:
  - Verified and confirmed that the 5 core mobile screens are fully implemented and compliant with `docs/contracts/ui-integration.json`:
    - `ExperiencesDiscoveryScreen` (`/experiences`): multi-filter search, region chips, category chips, interactive map tab with OSM vector preview and nearby hotspots.
    - `DestinationDetailScreen` (`/experiences/destinations/:id`): live marine sensor conditions, focal species biodiversity telemetry, operational advisories, and child offerings list.
    - `OfferingDetailScreen` (`/experiences/offerings/:id`): live deterministic availability evaluation seam, guest party size selector, date picker, schedule windows.
    - `FavouritesScreen` (`/experiences/favourites`): wishlist management with filtering by target type (`ALL`, `DESTINATION`, `OFFERING`) and item deletion.
    - `CatalogueManagementScreen` (`/experiences/manage`): role-permission gating (`experiences.catalogue.manage`), tabbed management for Destinations, Activities, Offerings, and Diagnostics Seam (health checks and pre-G07 read-only context).
  - Expanded unit test coverage in `apps/mobile/test/experience_api_service_test.dart`:
    - Added tests covering destination CRUD mutations (`createDestination`, `updateDestination`, `deleteDestination`, `updateDestinationPublication`).
    - Added tests covering telemetry parsing (`getDestinationOperationalAdvisories`, `getDestinationBiodiversity`).
    - Added tests covering activities full lifecycle (`getActivities` with filters, `getActivityById`, `createActivity`, `updateActivity`, `evaluateActivityPublication`, `updateActivityPublication`, `deleteActivity`).
    - Added tests covering offerings and schedules lifecycle (`getOfferings`, `getOfferingById`, `createOffering`, `updateOffering`, `evaluateOfferingPublication`, `updateOfferingPublication`, `deleteOffering`, `getOfferingSchedules`, `addOfferingSchedule`, `updateOfferingSchedule`, `deleteOfferingSchedule`).
    - Added structured error handling tests asserting `ExperienceApiException` throws on HTTP 400, 401, 403, 404, and 500 status codes.
  - Expanded widget test coverage in `apps/mobile/test/experience_workflow_widget_test.dart` (`MOB-EXP-001` through `MOB-EXP-013`):
    - `MOB-EXP-001`: discovery catalog cards rendering for destinations and offerings.
    - `MOB-EXP-002`: coastal map tab layout, search field, and nearby hotspots.
    - `MOB-EXP-003`: destination detail conditions and biodiversity telemetry.
    - `MOB-EXP-004`: offering detail availability evaluation.
    - `MOB-EXP-005`: favourites wishlist rendering.
    - `MOB-EXP-006`: catalogue management tabs rendering for authorized managers.
    - `MOB-EXP-007`: discovery search text and region/category filter chips interactions.
    - `MOB-EXP-008`: coastal map place search and "Near Me" trigger interactions.
    - `MOB-EXP-009`: destination detail resilient handling for missing/errored marine sensor conditions.
    - `MOB-EXP-010`: offering detail unavailable slot result rendering with operational restrictions.
    - `MOB-EXP-011`: favourites screen filter chips and item removal.
    - `MOB-EXP-012`: catalogue management access-denied state for unprivileged users lacking manage permissions.
    - `MOB-EXP-013`: catalogue management destination creation modal dialog form interactions.
- AI output accepted/changed/rejected: Accepted full unit and widget test suite expansions, verified strict UI contract compliance, and maintained zero regressions across all 114 mobile tests.
- Verification/evidence:
  - `python scripts/validation/validate_ui_integrations.py`: UI integration contract: OK (exit code 0).
  - `flutter test --no-pub test/experience_api_service_test.dart test/experience_workflow_widget_test.dart`: 27 experience tests passed cleanly.
  - `flutter test --no-pub`: all 114 mobile tests passed cleanly across the application.


## 2026-10-02 — Experiences discovery UI consolidation: removal of inline CRUD and Nearby tab, unified catalogue search, and deep-link routing

- Date/time or time range: 2026-10-02 01:25 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Antigravity
- Tool/App: Google Antigravity
- AI Model: Gemini 2.5 Pro
- Summary of the user's request:
  1. In `http://localhost/experiences` (`ExperiencesPage.tsx`):
     - Remove inline mutation modals/forms (`destModalOpen`, `offModalOpen`, `deleteDialog`, and related submission handlers).
     - Remove all Delete buttons from Destinations and Offerings cards.
     - Keep the "Add Destination" button: change its behavior to redirect/link to `/experiences/manage#destinations`.
     - Keep the "Add Offering" button: update it to redirect/link to `/experiences/manage#offerings`.
     - Keep the "Edit" buttons on Destination and Offering cards: update their behavior to redirect/link to `/experiences/manage?editDestination={dest.id}#destinations` and `/experiences/manage?editOffering={off.id}#offerings`.
  2. Remove the "Nearby Proximity Search" tab and its dedicated view entirely.
  3. Combine natural keyword searching (places, coastal spots, activities, tags) into the primary search bar in the "Destinations & Offerings" tab.
  4. In `CatalogueManagementPage.tsx`, support deep-linking via URL query params (`editDestination`, `editOffering`, `tab`) to automatically open the respective item's edit modal.
  5. In the Interactive Coastal Map view, update the "Find Experiences Nearby" button to direct the user to explore experiences in the unified catalogue tab with the selected spot's name.
- Summary of what the AI Agent did:
  - In `apps/web/src/pages/experiences/CatalogueManagementPage.tsx`:
    - Added deep-linking `useEffect` evaluating URL query params (`editDestination`, `editOffering`, `tab`) against loaded catalogue items to automatically switch to the relevant tab and open the editing modal (`startEditDestination(target)` or `startEditOffering(target)`).
  - In `apps/web/src/pages/experiences/ExperiencesPage.tsx`:
    - Removed unused inline creation/editing modal state, handlers, and deletion dialogs.
    - Updated the "Add Destination" button to a `<Link to="/experiences/manage#destinations">` styled consistently with the design system.
    - Updated the "Add Offering" button to a `<Link to="/experiences/manage#offerings">`.
    - Removed all Delete buttons from Destination and Offering cards.
    - Updated the Destination card Edit button to `<Link to={`/experiences/manage?editDestination=${dest.id}#destinations`}>`.
    - Updated the Offering card Edit button to `<Link to={`/experiences/manage?editOffering=${off.id}#offerings`}>`.
    - Removed the "Nearby Proximity Search" tab button and its section (`activeTab === 'nearby'`) completely.
    - Integrated multi-facet searching and filtering across destinations, offerings, regions, and activities in the unified catalogue view.
    - Updated the Interactive Coastal Map card action from navigating to the removed nearby tab to setting the search query and activating the catalogue tab.
  - Testing & Validation:
    - Updated React component tests in `apps/web/src/pages/experiences/__tests__/experiences.component.test.js`:
      - `WEB-EXP-002`: verified unified keyword search across destinations and offerings.
      - `WEB-EXP-011`: verified location keyword search in the unified coastal discovery search bar.
      - `WEB-EXP-010`: verified interactive map action button "Explore Experiences Here" updates state.
      - Executed `node --test src/pages/experiences/__tests__/experiences.component.test.js`: all 12 tests passed cleanly (12 pass, 0 fail).
    - Added backend integration tests in `services/experience-biodiversity/tests/Blueverse.ExperienceBiodiversity.Tests/`:
      - `EXP-API-DEST-011` & `EXP-API-DEST-012` in `DestinationsApiTests.cs`: verified destination deletion (204 No Content) and 404 Not Found on non-existent targets.
      - `EXP-API-ACT-007` & `EXP-API-ACT-008` in `ActivitiesApiTests.cs`: verified activity deletion (204 No Content) and 404 Not Found on non-existent targets.
      - `EXP-API-OFF-013` & `EXP-API-OFF-014` in `OfferingsApiTests.cs`: verified offering deletion (204 No Content) and 404 Not Found on non-existent targets.
    - Executed `npm run build` in `apps/web` (passed cleanly; TypeScript and Vite bundle succeeded).
    - Executed `python scripts/validation/validate_ui_integrations.py` (passed with code 0).
    - Executed `python .agents/scripts/validate_endpoint_catalog.py` (passed with code 0; 72 endpoints, 32 routes).
- AI output accepted/changed/rejected: Accepted all structural UI updates, modal removals, deep-link handling, unified catalogue search, and automated test suite expansions across React Web and Backend Service.
- Verification/evidence:
  - `npm run build` completed with code 0 (`vite v8.3.0 building client environment for production... built in 1.70s`);
  - `node --test src/pages/experiences/__tests__/experiences.component.test.js` executed 12 tests with 12 passing (0 failing);
  - `python scripts/validation/validate_ui_integrations.py` returned OK;
  - `python .agents/scripts/validate_endpoint_catalog.py` returned OK.

## 2026-09-28 — Role and permission-based catalogue access control, backend authorization enforcement, and UI protection

- Date/time or time range: 2026-09-28 23:35 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Antigravity
- Tool/App: Google Antigravity
- AI Model: Gemini 2.5 Pro
- Summary of the user's request:
  1. Fix the permission-based access control: anonymous or unprivileged users must not be able to perform catalogue mutations or access unauthorized administrative functions.
  2. Enforce permission checks across backend services: only users with `experiences.catalogue.manage`, `auth.role.system.manage`, or the `Admin` system role can create, update, delete, or transition publication status.
  3. Ensure non-published status filters (e.g. `status=DRAFT` or `status=ARCHIVED`) are protected with permission checks, while public catalog queries remain open for tourist exploration.
  4. Secure the React Web UI: guard the `/experiences/manage` route with `RequireAnyAdminPermission`, conditionally render creation forms and action buttons based on `canManage`, and display a clean read-only banner for users who only possess `experiences.catalogue.read`.
- Summary of what the AI Agent did:
  - Backend `experience-biodiversity` Authorization:
    - Updated `IUserContext` and `UserContext` (`Services/IUserContext.cs`) to implement `HasPermission(string)` and `HasAnyPermission(params string[])`, checking token permissions, roles, and forwarded gateway headers (`X-User-Roles`, `X-User-Permissions`).
    - Enforced permission checks in `DestinationsController.cs`:
      - Filter `status != PublicationStatus.Published` returns 403 Forbidden unless the caller holds `experiences.catalogue.read`, `experiences.catalogue.manage`, or `Admin`.
      - `POST` (create), `PUT` (update), `DELETE` (delete), and `PATCH /publication` require authentication (401) and catalogue manage permission (403).
      - `POST /publication-evaluations` requires catalogue inspection or manage permissions (401/403).
    - Enforced permission checks in `ActivitiesController.cs`:
      - Filter `status != PublicationStatus.Published` returns 403 Forbidden for unauthorized callers.
      - `POST` (create), `PUT` (update), `DELETE` (delete), `PATCH /publication`, and `POST /publication-evaluations` require authentication (401) and catalogue manage permission (403).
    - Enforced permission checks in `OfferingsController.cs`:
      - Filter `status != PublicationStatus.Published` returns 403 Forbidden for unauthorized callers.
      - `POST` (create), `PUT` (update), `DELETE` (delete), `PATCH /publication`, `POST /publication-evaluations`, and schedule endpoints (`POST`, `PUT`, `DELETE /schedules`) require authentication (401) and catalogue manage permission (403).
    - Added unit tests `EXP-UNIT-USR-007` and `EXP-UNIT-USR-008` in `UserContextTests.cs` verifying permission evaluation logic for admin and non-admin callers.
    - Added integration tests `EXP-API-DEST-009` and `EXP-API-DEST-010` in `DestinationsApiTests.cs` asserting 401 Unauthorized for anonymous mutations and 403 Forbidden for unprivileged draft status queries.
  - Frontend React Web Security (`apps/web`):
    - In `routes.tsx`: wrapped `/experiences/manage` with `<RequireAnyAdminPermission permissions={['experiences.catalogue.read', 'experiences.catalogue.manage', 'auth.role.system.manage']}>`.
    - In `ExperiencesPage.tsx`: guarded the "Catalogue Management" button link to appear only for authenticated admin/permission holders.
    - In `CatalogueManagementPage.tsx`:
      - Injected `useAuthSession` and computed `canManage` based on `hasAnyPermission(user, ['experiences.catalogue.manage', 'auth.role.system.manage']) || user?.roles?.includes('Admin')`.
      - Conditionally rendered destination, activity, offering, and schedule creation forms only when `canManage` is true.
      - Displayed an informative read-only banner when the user has read-only permission (`experiences.catalogue.read`).
      - Protected Edit, Delete, and Publish/Unpublish buttons to render only for authorized managers, while keeping "Audit Publish" available for audit inspection.
    - Updated `experiences.component.test.js`:
      - Updated `WEB-EXP-009` to verify full workspace capabilities for authorized manager users.
      - Added `WEB-EXP-009B` to test and assert the read-only boundary for users without manage permissions.
  - Verification & Contract Validation:
    - `python scripts/validation/validate_ui_integrations.py` passed with 0 errors.
    - `python .agents/scripts/validate_endpoint_catalog.py` passed with 0 errors (72 public endpoints, 32 frontend routes).
    - `npm run build` in `apps/web` passed with exit code 0 (`tsc -b && vite build` built cleanly).
    - `node --test src/pages/experiences/__tests__/experiences.component.test.js` passed all 12 component tests cleanly (12 pass, 0 fail).
- AI output accepted/changed/rejected: Accepted complete end-to-end authorization controls across backend controllers, user context claims processing, and React route/workspace guards.
- Verification/evidence:
  - `python scripts/validation/validate_ui_integrations.py`: UI integration contract: OK;
  - `python .agents/scripts/validate_endpoint_catalog.py`: Endpoint catalog: OK;
  - All 12 experience component tests passed (`WEB-EXP-001` through `WEB-EXP-011` + `WEB-EXP-009B`);
  - `npm run build` succeeded without compile or bundle errors.

## 2026-09-28 — Experiences & Biodiversity Admin CRUD, permission seeding, and account navigation integration

- Date/time or time range: 2026-09-28 01:25 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Antigravity
- Tool/App: Google Antigravity
- AI Model: Gemini 2.5 Pro
- Summary of the user's request:
  1. Add missing permissions for Destinations, Activities, and Offerings/Schedules CRUD and seed them to the `Admin` system role.
  2. Implement full working CRUD (Create, Read, Update, Delete) capability for Destinations, Activities, and Offerings/Schedules.
  3. Provide access to the CRUD capabilities through the left navigation bar (`AccountAreaNavigation`) present on Profile, Dashboard, and Administration pages for authorized role/permission holders.
  4. Preserve the architectural structure of the API, Auth, and React Web UI without making unnecessary modifications, maintaining the original coastal theme and `DESIGN.md`.
- Summary of what the AI Agent did:
  - Auth Service & Permissions Seeding:
    - Added permission constants in `PermissionCodes.cs`: `ExperienceRead = "experiences.catalogue.read"` and `ExperienceManage = "experiences.catalogue.manage"`.
    - Seeded both permissions (`permExpReadId`, `permExpManageId`) in `AuthDbContext.cs` and associated them with the seeded `Admin` system role (`22222222-2222-2222-2222-222222222201`) in `RolePermissions`.
    - Added both permissions to `AuthDataSeeder.cs` so existing and new database initializations automatically grant catalogue management permissions to system administrators.
  - Backend `experience-biodiversity` Endpoints:
    - Added `DELETE /api/experiences/destinations/{id:guid}` in `DestinationsController.cs` for destination removal.
    - Added `DELETE /api/experiences/activities/{id:guid}` in `ActivitiesController.cs`, ensuring child offerings are cleanly removed to uphold relational integrity.
    - Added `DELETE /api/experiences/offerings/{id:guid}` in `OfferingsController.cs` for offering removal.
    - (Create `POST`, Read `GET`, Update `PUT` and `PATCH` publication endpoints were already implemented and verified).
  - Frontend API Client (`apps/web/src/features/experiences/experienceApi.ts`):
    - Added `deleteDestination(id)`, `deleteActivity(id)`, and `deleteOffering(id)` exported API client functions.
  - Frontend Authorization & Navigation (`AccountAreaNavigation.tsx` & `permissions.ts`):
    - Added `'experiences'` area to `AccountAreaNavigation.tsx` and mapped permission holders of `experiences.catalogue.read`, `experiences.catalogue.manage`, or `auth.role.system.manage` to a dedicated navigation group titled "Experiences & Biodiversity" (`/experiences/manage`) with subnav items: Destinations (`#destinations`), Activities (`#activities`), Offerings & Schedules (`#offerings`), and Diagnostics & Seam (`#diagnostics`).
    - Added permission alias in `permissions.ts` ensuring `experiences.catalogue.manage` satisfies `experiences.catalogue.read`.
  - Catalogue Management Workspace UI (`CatalogueManagementPage.tsx`):
    - Embedded `AccountAreaNavigation active="experiences"` in the standard 2-column account grid frame (`lg:grid-cols-[15rem_minmax(0,1fr)]`), matching Profile, Dashboard, and Administration page layouts without architectural disruption.
    - Added hash location synchronization (`#destinations`, `#activities`, `#offerings`, `#diagnostics`) allowing instant deep-linking from navigation menus.
    - Added inline Edit and Delete action buttons with confirmation prompts across all destinations, activities, and offerings.
    - Implemented modal dialogs for updating destination metadata/coordinates, activity naming/category/description, and offering pricing/duration/capacity/description.
  - Contract & Endpoint Verification:
    - Added `experience-destination-delete`, `experience-activity-delete`, and `experience-offering-delete` to `endpoint-catalog.json` and regenerated `endpoint-catalog.md`.
    - `python scripts/validation/validate_ui_integrations.py` passed with 0 errors.
    - `python .agents/scripts/validate_endpoint_catalog.py` passed with 0 errors (72 public endpoints, 32 frontend routes).
    - `npm run build` in `apps/web` passed with exit code 0 (`tsc -b && vite build` built cleanly).
    - `node --test src/pages/experiences/__tests__/experiences.component.test.js` passed all 11 component tests cleanly.
- AI output accepted/changed/rejected: Accepted complete CRUD with backend and frontend integration, seeded Auth permissions, and responsive 2-column layout embedding the left account navigation bar.
- Verification/evidence:
  - `python scripts/validation/validate_ui_integrations.py`: UI integration contract: OK;
  - `python .agents/scripts/validate_endpoint_catalog.py`: Endpoint catalog: OK (72 public endpoints, 32 frontend routes; AI endpoints: none implemented);
  - All 11 experience component tests passed (`WEB-EXP-001` through `WEB-EXP-011`);
  - `npm run build` succeeded without compile or bundle errors.

## 2026-09-28 — Natural-language coastal proximity discovery and offline Sri Lanka geocoding fallback

- Date/time or time range: 2026-09-28 00:20 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Antigravity
- Tool/App: Google Antigravity
- AI Model: Gemini 2.5 Pro
- Summary of the user's request:
  1. Fix the "Nearby Proximity Search" at `http://localhost/experiences`: eliminate the requirement for ordinary visitors to manually enter technical Latitude and Longitude coordinates.
  2. Implement natural-language keyword and place-name discovery (`Mirissa`, `Weligama`, `Galle`, `Colombo`, `Pigeon Island`, etc.), resolving location coordinates on the backend and computing Haversine distance matches across published coastal destinations.
  3. Keep search radius in kilometers as a primary user input, and demote raw geographic coordinates to an optional, collapsible advanced settings toggle.
- Summary of what the AI Agent did:
  - Updated `NearbyController.cs` in `services/experience-biodiversity`:
    - Extended `GET /api/experiences/nearby` to accept `[FromQuery] string? q` and `[FromQuery] string? location` in addition to existing latitude/longitude coordinates.
    - Added database-first matching against published coastal destinations (`Name` or `Region`).
    - Added fallback geocoding through `IMapProviderService.SearchPlacesAsync` and enriched it with a curated Sri Lankan coastal coordinates dictionary (`Galle`, `Colombo`, `Trincomalee`, `Bentota`, `Tangalle`, `Negombo`, `Jaffna`, `Matara`, `Hambantota`, `Weligama`, `Unawatuna`, `Beruwala`, `Kalutara`, `Mount Lavinia`, `Pasikuda`, `Batticaloa`, `Mannar`) ensuring instant, zero-latency resolution even in offline/containerized environments.
    - Updated query payload response to include `resolvedLocation` and resolved coordinates.
  - Updated `NearbyApiTests.cs` with test case `EXP-API-NRB-005` verifying keyword queries return expected destinations.
  - Updated React Web client in `apps/web`:
    - Updated `experienceApi.ts` `getNearbyExperiences` with overload supporting `{ location, radiusMeters, limit }`.
    - Transformed "Nearby Proximity Search" tab in `ExperiencesPage.tsx`:
      - Primary input: "COASTAL LOCATION OR PLACE NAME" (`nearLocation`) with clear coastal suggestions.
      - Primary input: "SEARCH RADIUS (KM)" (`nearRadiusKm`).
      - Collapsible "Advanced Geographic Coordinates" toggle for developers/scientific users who want manual coordinates.
      - Quick coastal destination chips now populate both the location name and coordinate presets.
      - Added dynamic resolved-location banner indicating resolved place and coordinates.
    - Added component test `WEB-EXP-011` in `experiences.component.test.js` verifying the keyword proximity search flow.
  - Rebuilt and restarted both `blueverse-frontend` and `blueverse-experience-biodiversity` Docker containers; verified live endpoint returns 200 OK with resolved coordinates and destinations for natural place names (e.g. `galle`, `mirissa`, `colombo`).
  - Validated repository contracts: `validate_ui_integrations.py` passed with 0 errors; `validate_endpoint_catalog.py` passed with 0 errors.
- AI output accepted/changed/rejected: Accepted natural-language search with backend coordinate resolution, collapsible advanced coordinate inputs, and container-resilient coastal place fallback.
- Verification/evidence:
  - Live query `curl.exe -i -s "http://localhost/api/experiences/nearby?q=galle&radiusMeters=50000"` returns HTTP 200 with resolved location `Galle, Southern Province, Sri Lanka` (6.0535°N, 80.221°E) and 2 nearby destinations (`Hikkaduwa Marine Sanctuary`, `Mirissa Coastal Haven`);
  - `node --test src/pages/experiences/__tests__/experiences.component.test.js` passed all 11 tests cleanly;
  - `python scripts/validation/validate_ui_integrations.py` passed (0 errors);
  - `python .agents/scripts/validate_endpoint_catalog.py` passed (0 errors).

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

## 2026-10-02 — Experience & Biodiversity closeout fixes 1 and 5

- Date/time or time range: 2026-10-02 12:21 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Continue the Experience & Biodiversity
  closeout work for items 1 and 5, and update this contribution log as Ushan.
- Summary of what the AI Agent did: Hardened the public gateway and private
  component service around validated Auth-issued JWT claims and explicit
  catalogue permissions; removed caller-header, unsigned-token-payload and
  Admin-role permission fallbacks; and updated tests for spoofed
  headers, unsigned JWTs, read-only permission denial and denied-write side
  effects. Reconciled branch/G00/readiness and database documentation with
  source evidence, clarified anonymous published reads versus authenticated
  operations, and synchronized the endpoint catalog and shared signing-key
  configuration documentation.
- AI output accepted/changed/rejected: Applied the security and documentation
  changes after source review. Kept G00 and component acceptance pending and
  did not add executable Agentic AI behavior. Updated existing security tests
  with the user's approval. No tests or application builds were run.
- Verification/evidence: Endpoint catalog regeneration and validation passed
  (72 public endpoints, 32 frontend routes; no AI endpoints implemented), UI
  integration validation passed, API/catalog JSON parsing passed, and
  `git diff --check` passed. Docker CLI was unavailable, so Compose validation
  was not run. Runtime behavior remains unverified. See the current branch
  changes in `services/api/`, `services/experience-biodiversity/`,
  `docs/v1/`, `docs/database/` and `docs/api/`.

## 2026-10-03 — Experience & Biodiversity security test recheck

- Date/time or time range: 2026-10-03 00:41 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Recheck the preceding Experience &
  Biodiversity closeout fixes and update the backend service and React Web
  tests.
- Summary of what the AI Agent did: Removed the remaining Admin-role and
  unrelated role-management permission shortcuts from the Experience pages.
  Made service integration test clients anonymous by default with explicit
  signed actors, added a route-wide catalogue mutation authorization matrix
  and draft privacy coverage, expanded Experience API request-contract tests,
  and added UI authorization regressions. Configured the Windows test host to
  use console logging instead of the inaccessible Event Log sink.
- AI output accepted/changed/rejected: Retained permission-based catalogue
  access and the anonymous/read-only/manager test distinctions; corrected the
  prior tests that had implicitly used an Admin token. No Agentic AI runtime
  behavior was introduced.
- Verification/evidence: Experience & Biodiversity service tests passed
  (95/95), public API gateway tests passed (22/22), and the full React suite
  passed serially (177/177). Experience API contract tests passed (5/5), UI
  integration and endpoint catalog validation passed (72 public endpoints,
  32 frontend routes), and `git diff --check` passed.

## 2026-10-03 — Complete Flutter Mobile Experience & Biodiversity component implementation and test suite

- Date/time or time range: 2026-10-03 02:35 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Antigravity
- Tool/App: Google Antigravity
- AI Model: Gemini 2.5 Pro
- Summary of the user's request: Implement the whole Flutter Mobile App for the
  Experience & Biodiversity component and update/implement all test cases for
  the Flutter App.
- Summary of what the AI Agent did:
  - Created complete Dart models in `apps/mobile/lib/data/models/experience_models.dart`
    for Destinations, Activities, Offerings, Schedules, Availability Evaluations,
    Marine Conditions, Operational Advisories, Biodiversity telemetry/predictions,
    Wishlist Favourites, OpenStreetMap Tile Config, Map Search Places, Nearby Proximity,
    Dependencies status, and Agent Context Seam.
  - Implemented `ExperienceApiService` in `apps/mobile/lib/data/services/experience_api_service.dart`
    covering all 23 backend public API routes via the ASP.NET Core gateway, formatting
    query parameters cleanly to pass `scripts/validation/validate_ui_integrations.py`.
  - Implemented `ExperienceRepository` and `ExperienceViewModel` (`apps/mobile/lib/ui/experiences/experience_view_model.dart`)
    managing reactive state for catalog exploration, interactive map searches, live telemetry,
    availability evaluation, and CRUD operations.
  - Implemented all 5 Flutter mobile workflow screens:
    1. `ExperiencesDiscoveryScreen` (`/experiences`) featuring catalog filtering, chips, and OpenStreetMap corridor view.
    2. `DestinationDetailScreen` (`/experiences/destinations/:id`) presenting live marine conditions, operational advisories, biodiversity predictions, and destination offerings.
    3. `OfferingDetailScreen` (`/experiences/offerings/:id`) with schedule slots and interactive real-time availability evaluation.
    4. `FavouritesScreen` (`/experiences/favourites`) for saved wishlist management.
    5. `CatalogueManagementScreen` (`/experiences/manage`) providing authorized CRUD management and pre-G07 diagnostic seams.
  - Integrated routes and parameterized route handling into `main.dart` and added quick
    navigation links in `AuthDashboardScreen`.
  - Authored complete unit and widget test suites:
    1. `apps/mobile/test/experience_api_service_test.dart` (9/9 unit tests covering endpoint contracts and MockClient responses).
    2. `apps/mobile/test/experience_workflow_widget_test.dart` (6/6 widget tests covering MOB-EXP-001 through MOB-EXP-006 workflows).
- AI output accepted/changed/rejected: Accepted full Flutter mobile implementation,
  clean UI styling aligned with `BlueversePalette`, and test coverage. No Git commits
  or branch pushes were performed.
- Verification/evidence:
  - `python scripts/validation/validate_ui_integrations.py` passed with exit code 0.
  - `flutter test --no-pub` passed all 102 tests (87 existing + 15 new tests) in 16 seconds.

## 2026-10-05 — Flutter Experience & Biodiversity reliability fixes

- Date/time or time range: 2026-10-05 22:31 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Continue the interrupted Flutter
  Experience & Biodiversity audit, fix mobile defects and gaps, refine the
  repository G00 proposal as needed, and record the contribution as Ushan.
- Summary of what the AI Agent did: Decoupled discovery data failures,
  corrected biodiversity and marine-condition unavailable states, added
  permission-on-demand approximate location and timezone-safe schedule flows,
  completed schedule and catalogue editing, made management load all
  permission-authorized publication states, protected account-scoped favourites
  against stale requests, and updated mobile guidance, UI integration metadata,
  G00 proposal details, and the approved MOB-EXP-009 assertions.
- AI output accepted/changed/rejected: Accepted source-backed reliability and
  UX changes. Pinned geolocator to compatible `14.0.2` after package solving
  showed that `14.1.x` conflicts with the current Windows secure-storage
  dependency. Updated Ushan's G00 input while retaining shared-owner G00
  agreement as pending. Did not add executable Agentic AI behavior. Existing
  Flutter tests were not run.
- Verification/evidence: `flutter pub get` passed;
  `flutter analyze lib` passed with no issues. Full `flutter analyze` reports
  one existing unused import in `test/experience_api_service_test.dart`; that
  separate test was left unchanged. `scripts/validation/validate_ui_integrations.py`
  passed, UI JSON and Android/iOS XML parsed successfully, and `git diff --check`
  passed. The Flutter test suite was not run. Relevant changes are in
  `apps/mobile/`, `docs/contracts/ui-integration.json` and
  `docs/v1/g00/member-1-experience-biodiversity.md`.

## 2026-10-06 — Diagnose Android Flutter run failure

- Date/time or time range: 2026-10-06 00:36 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Investigate the Android build failure from
  `flutter run` for the Experience & Biodiversity mobile component.
- Summary of what the AI Agent did: Traced the captured failures to a Google
  Maven read timeout followed by insufficient disk space while Gradle cached
  and transformed Android build artifacts. Compared the app's AGP, Gradle,
  Kotlin and compatibility flags against its installed Flutter SDK template;
  recommended recovering disk/cache capacity before changing version pins.
- AI output accepted/changed/rejected: Kept the repository build configuration
  unchanged because it matches Flutter 3.47.2's generated defaults; provided
  a PowerShell option to put Gradle and Flutter temporary files on D:.
- Verification/evidence: Read Flutter SDK version metadata and its Android
  Gradle template constants (Flutter 3.47.2, AGP 9.1.0, Gradle 9.3.1, Kotlin
  2.4.0), verified both `android.newDsl=false` and
  `android.builtInKotlin=false`, and inspected `geolocator_android` 5.1.1+1's
  AGP 9.0.1 dependency. The supplied log reports the Maven timeout and
  `There is not enough space on the disk`; no Android build was rerun.

## 2026-10-06 — Diagnose Kotlin cache failure across Windows drives

- Date/time or time range: 2026-10-06 00:46 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Investigate the follow-up Android
  `flutter run` failure after Gradle reached Kotlin compilation.
- Summary of what the AI Agent did: Located the failing
  `package_info_plus:compileDebugKotlin` incremental cache, confirmed its
  Kotlin source is under the C: Pub cache while the project and build cache
  are on D:, and matched the cross-root exception to Flutter's reported
  Windows build issue. Recommended moving `PUB_CACHE` to D:, cleaning
  generated Flutter build state, then fetching packages and retrying.
- AI output accepted/changed/rejected: Updated the prior diagnosis: the
  attached run has moved past the disk/download failure and now has a distinct
  cross-drive Kotlin incremental-cache failure. Kept Android Gradle versions
  unchanged and did not add a slower project-wide incremental-compilation
  workaround.
- Verification/evidence: Reviewed the full pasted log, `.dart_tool/package_config.json`,
  `.flutter-plugins-dependencies` and Android Gradle properties. Confirmed the
  C:/D: path split and checked Kotlin's documented incremental-compilation
  switch and Dart's `PUB_CACHE` setting. No Android build or tests were run.

## 2026-10-06 — Replace illustrative Flutter coastal map

- Date/time or time range: 2026-10-06 01:58–02:01 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Replace the inaccurate hand-drawn Sri Lankan
  map in the Flutter Experience & Biodiversity workflow with a real map fetched
  through the existing map configuration integration.
- Summary of what the AI Agent did: Added MapLibre rendering from the registered
  map configuration, catalogue-coordinate destination markers, map and tile
  loading/retry states, and replaced the silhouette UI. Updated the approved
  MOB-EXP-002 fixture/assertions and documented the current provider proposal
  while keeping shared G00 acceptance pending.
- AI output accepted/changed/rejected: Accepted the real map implementation;
  updated the map test to verify the configured OpenFreeMap style and
  attribution. No endpoint or route was added.
- Verification/evidence: `dart analyze lib test/experience_workflow_widget_test.dart`
  passed with no issues; the UI integration and endpoint catalog validators
  passed; `git diff --check` passed. The widget test and Android Gradle build
  were not run.


## 2026-10-06 — Restore mobile experience API availability

- Date/time or time range: 2026-10-06 02:17–02:22 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Diagnose and fix the mobile map, search, nearby, catalogue and diagnostics failures shown on the Redmi Note 14.
- Summary of what the AI Agent did: Confirmed the public gateway was reachable but the running Compose stack, launched from a sibling checkout, had no Experience & Biodiversity service container. Started only that service from this checkout using its cached image, without rebuilding or replacing the other services. Confirmed database initialization completed and reviewed the diagnostics endpoints' authentication boundary and mobile token forwarding.
- AI output accepted/changed/rejected: Accepted the missing service container as the shared cause of the 502 failures and restored it. No application source or existing tests were changed.
- Verification/evidence: Through the laptop Wi-Fi address, `/api/health`, `/api/experiences/health`, destinations, map configuration, map search and nearby returned HTTP 200; the service health response reported the database connected. Anonymous probes of protected diagnostics routes returned HTTP 401 as expected; the mobile client sends its stored bearer token. Flutter tests and device-side authenticated diagnostics were not run.

## 2026-10-06 — Improve coastal map interactions on mobile and web

- Date/time or time range: 2026-10-06 03:00–03:14 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Make the Flutter coastal map usable without tab swipes stealing map drags, add a current-location action to Flutter and React, and align the mobile map experience with the React web map.
- Summary of what the AI Agent did: Added MapLibre GL JS to React and lazy-loaded it on the map tab; wired configured map styles, catalogue markers, place selection, pan/zoom, current-location markers and nearby results to existing public API operations. Updated Flutter's map gesture ownership, map height, destination chips and details, selected-place camera focus and current-location control. Updated the approved WEB-EXP-010 and MOB-EXP-002 tests and synchronized the G00, device-location and ADR-0017 provider notes without recording shared G00 acceptance.
- AI output accepted/changed/rejected: Accepted the configured MapLibre renderer for React to match Flutter and the explicit location action in both clients. Kept device coordinates in memory and sent them only to the public nearby API after the user action. Added no endpoint or route.
- Verification/evidence: `npm run build` passed; targeted Dart analysis passed with no issues; the React test file passed `node --check`; `scripts/validation/validate_ui_integrations.py` passed; `git diff --check` passed with existing LF/CRLF conversion warnings. Test suites and device execution were not run.

## 2026-10-06 — Fix web coastal map layout and location control

- Date/time or time range: 2026-10-06 11:11 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Stop the coastal map from overlapping page content and add a Google Maps-style control for using the visitor's current location.
- Summary of what the AI Agent did: Added an accessible floating location button within the map viewport and reused the existing permission-aware browser geolocation and nearby-destination flow. Separated the full-size map layer from MapLibre's own container so its CSS cannot collapse the map canvas; rebuilt and restarted only the local frontend.
- AI output accepted/changed/rejected: Accepted the existing OpenFreeMap/MapLibre source and location flow. Removed the duplicate header location action and kept the map control available during loading or provider errors. No endpoint or route was added.
- Verification/evidence: `npm run build` passed; lint on the two changed web source files passed with one existing hook-dependency warning; the UI integration validator passed; `git diff --check` passed. The live localhost map showed loaded Sri Lanka tiles, the location control, map attribution within the rounded map, and the footer below the map without overlap. Full web lint still reports pre-existing errors in `CatalogueManagementPage.tsx`, `FavouritesPage.tsx` and the existing component test. Test suites were not run.

## 2026-10-06 — Localize coastal map refreshes and align Flutter discovery

- Date/time or time range: 2026-10-06 11:48–12:01 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Keep map refresh and error feedback within the relevant map sections, stop destination markers shifting on hover, and make Flutter's map experience more like React's.
- Summary of what the AI Agent did: Scoped web place-search feedback to the map search card and prevented stale map-search responses from overwriting a newer selection. Removed hover scaling from anchored map markers. Added Flutter coast-region camera presets, selected-place detail/actions, distance-based nearby results, separate GPS and nearby loading states, local map configuration and nearby refresh controls, stale-result retention during nearby refresh, and stale-search cancellation. Rebuilt and restarted only the local frontend container.
- AI output accepted/changed/rejected: Accepted the same public API workflows and existing OpenFreeMap/MapLibre provider. No endpoint, route or G00 contract changes were made. Updated the user-approved WEB-EXP-010 and MOB-EXP-002 assertions for map-local search feedback and Flutter map controls.
- Verification/evidence: Dart analysis of the changed Flutter source reported no issues; targeted ESLint and `npm run build` passed; the UI integration validator passed; `git diff --check` passed with existing line-ending conversion warnings. The frontend Docker image built and its local container restarted. Test suites and Flutter device execution were not run; Vite reported its existing large MapLibre chunk warning.

## 2026-10-06 — Keep coastal map updates local and consistent

- Date/time or time range: 2026-10-06 12:12–12:32 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Make the coastal map look consistent across browsers and stop map search and nearby updates from showing a full-page refresh.
- Summary of what the AI Agent did: Excluded map configuration, place-search and nearby requests from the shared page-wide loading screen; memoized the selected map focus so unrelated React updates do not recreate all map markers; kept MapLibre attribution expanded to avoid viewport-threshold changes; added pending-request assertions to approved WEB-EXP-010; rebuilt and restarted only the local frontend container.
- AI output accepted/changed/rejected: Kept the existing public map API, MapLibre provider and shared loader for non-map requests. No route, endpoint or G00 contract changed.
- Verification/evidence: `npm run build` passed with the known MapLibre large-chunk warning; targeted ESLint passed; the WEB-EXP-010 file passed `node --check`; the UI integration validator passed; `git diff --check` passed with existing LF/CRLF conversion warnings. The local in-app browser visibly loaded the tiles and markers, showed expanded attribution within the map, kept the footer below it, and updated the selected-place and nearby sections without a global loading overlay. Edge automation could not be inspected because the browser extension's request-header policy was unavailable. The test suite was not run.

## 2026-10-06 — Restore the coastal map attribution pill

- Date/time or time range: 2026-10-06 12:50 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Restore the earlier compact MapLibre attribution pill consistently across browsers after the expanded attribution appeared as an unwanted bar.
- Summary of what the AI Agent did: Set the React coastal map's attribution control to compact mode at all viewport sizes and rebuilt/restarted the local frontend container.
- AI output accepted/changed/rejected: Replaced the previous forced-expanded setting; kept the MapLibre provider-supplied credits and info toggle.
- Verification/evidence: `npm run build` passed with the known MapLibre large-chunk warning; targeted ESLint passed; `python scripts/validation/validate_ui_integrations.py` passed using the bundled Python runtime; `git diff --check` passed with existing line-ending warnings. The localhost map visibly showed the rounded credit pill and info toggle at a narrow viewport. Edge inspection was unavailable because its browser request-header policy could not load, and Chrome was not available. No test suite was run.

## 2026-10-06 — Start the map attribution collapsed

- Date/time or time range: 2026-10-06 13:07 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Keep the attribution collapsed on initial map load and expand its credit text only when the visitor clicks the info control.
- Summary of what the AI Agent did: Collapsed MapLibre's compact attribution control before removing the loading overlay, preserving its click-to-expand behavior, then rebuilt and restarted the local frontend.
- AI output accepted/changed/rejected: Kept the compact rounded control and provider credits; changed its initial state to collapsed.
- Verification/evidence: `npm run build` passed with the known MapLibre large-chunk warning; targeted ESLint passed; the UI integration validator passed; `git diff --check` passed with existing line-ending warnings. In the local browser, the loaded map exposed only the collapsed info control; clicking showed OpenFreeMap, OpenMapTiles and OpenStreetMap credit links, and clicking again collapsed it. No test suite was run.

## 2026-10-06 — Persist the selected coastal experience view

- Date/time or time range: 2026-10-06 13:07–13:31 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Keep the selected Destinations & Offerings or Interactive Coastal Map tab after refresh and animate transitions in both directions.
- Summary of what the AI Agent did: Stored the selected view in the `/experiences` query string (`?tab=map` for the map) so refresh and direct links restore the same tab. Added a brief, reduced-motion-aware fade/slide transition when switching tabs. Rebuilt the local frontend container.
- AI output accepted/changed/rejected: Accepted URL-backed view state after a live reload showed the session-only approach did not preserve the selection in the preview. Kept the existing route and API contracts unchanged.
- Verification/evidence: `npm run build`, targeted ESLint, the UI integration validator, and `git diff --check` passed. The rebuilt local frontend retained the map tab after a reload and removed the map query when switching back to Destinations & Offerings. An earlier preview load showed map tiles with collapsed attribution; the final reload restored the map tab but the map API reported its provider configuration unavailable, so final tile availability was not verified. No test suite was run.

## 2026-10-06 — Make experience discovery wording more inviting

- Date/time or time range: 2026-10-06 17:10–17:39 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Replace formal “Catalogue” wording with clearer, more interactive language throughout the experience-biodiversity component.
- Summary of what the AI Agent did: Updated React and Flutter discovery, management, favourites, offering-status, access, loading, and error copy to use terms such as “Explore,” “Manage Experiences,” and “coastal experiences.” Updated the user-approved existing WEB-EXP and MOB-EXP label assertions. Kept permission identifiers and internal implementation names unchanged, and rebuilt the local frontend preview.
- AI output accepted/changed/rejected: Accepted action-oriented labels and simpler explanations; retained domain permission codes and internal class/method names because they are not user-facing copy.
- Verification/evidence: `npm run build`, focused ESLint on `ExperiencesPage.tsx`, `OfferingDetailPage.tsx`, and `experienceApi.ts`, UI integration validation, and `git diff --check` passed. The local preview reloaded on `/experiences?tab=map` and showed “Explore coastal destinations.” Broader ESLint still reports React-hook issues in untouched lines of the management and favourites pages. Flutter analyze and formatting commands stalled without output and were stopped; Flutter static analysis and all test suites remain unverified.

## 2026-10-06 — Remove diagnostics from experience management screens

- Date/time or time range: 2026-10-06 17:40–17:58 +05:30 (Asia/Colombo)
- GitHub Username: `Ushan-Srinuka`
- Team Member Name (actual): Ushan Srinuka
- Agent Name: Codex
- Tool/App: ChatGPT Codex desktop
- AI Model: GPT-6
- Summary of the user's request: Review dependency and Agent Seam status logs and remove the unnecessary diagnostics tab from experience management.
- Summary of what the AI Agent did: Removed the diagnostics tab, panel, navigation link and automatic diagnostics fetch from React and Flutter. Updated the management workflow description and registry references while preserving backend diagnostics and the pre-G07 seam.
- AI output accepted/changed/rejected: Kept the backend safe-fallback behavior and expected pre-G07 `not_connected` seam. Client request adapters and existing test fixtures/assertions remain pending user approval to remove or update under the repository test-change rule.
- Verification/evidence: React production build passed; endpoint catalog generation and validation passed; `git diff --check` passed with existing LF/CRLF warnings. UI integration validation currently flags retained diagnostics endpoint literals in client adapters and existing test fixtures after their UI references were removed. Full web lint reports existing React-hook issues and an existing unused test parameter. Flutter analysis stalled without output and was stopped; test suites were not run.
