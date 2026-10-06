# BLUEVERSE routes and API endpoints

Generated from [endpoint-catalog.json](endpoint-catalog.json). Edit the JSON
when routes change, then run
`python .agents/scripts/validate_endpoint_catalog.py --write-markdown`.
The source route declarations remain authoritative. This page is the
quick index; [UI integration](../contracts/ui-integration.json) contains
the smaller shared-client workflow contract.

## Frontend routes

| Client | Route | Workflow | Use |
|---|---|---|---|
| react | `/` | `foundation-home` | Introduces BLUEVERSE's coastal purpose and restores the signed-in account. Profile and dashboard links are available from the shared account menu; account switching is kept there rather than presented as a home session-management section. |
| react | `/signin` | `auth-session-management` | Sign-in route for the React client. Uses the public Auth API and preserves a safe post-sign-in destination. |
| react | `/signup` | `auth-registration` | Account creation route for the React client. Registration opens Profile after the new account is signed in. |
| react | `/profile` | `auth-profile-management` | Shows and updates editable profile details, changes the password, and reviews or ends login sessions through the public Auth API. |
| react | `/dashboard` | `coastal-overview-dashboard` | Shows the current account overview and clearly marked future BLUEVERSE coastal service areas. Login sessions remain in Profile. |
| flutter | `/` | `foundation-home` | Restores the saved session at launch and opens Dashboard for an authenticated user. Signed-out users see a full-screen coastal onboarding carousel; Skip opens the final Sign in/Create account choices, and signing out of the final active device account returns to the carousel. |
| flutter | `/signin` | `auth-session-management` | Sign-in route for Flutter. Uses the shared public Auth API and saved-account switching. |
| flutter | `/signup` | `auth-registration` | Account creation route for Flutter. Registration opens Profile after the new account is signed in. |
| flutter | `/profile` | `auth-profile-management` | Shows and updates profile details, changes the password, and reviews or ends login sessions through the public Auth API. |
| flutter | `/dashboard` | `coastal-overview-dashboard` | Shows the current account overview and clearly marked future BLUEVERSE coastal service areas. Login sessions remain in Profile. |
| react | `/admin` | `auth-admin-entry` | Routes authorized accounts to the first administration area allowed by current profile grants. |
| react | `/admin/permissions` | `auth-permission-administration` | Lists application-defined permission codes and directs permitted role editors to manage grants. |
| react | `/admin/roles` | `auth-role-administration` | Lists roles and provides permission-checked role create, update, permission-assignment and delete actions. |
| react | `/admin/users` | `auth-user-administration` | Lists accounts and provides permission-checked account, password, active-state and role-assignment actions. |
| flutter | `/admin` | `auth-admin-entry` | Routes authorized accounts to the first administration area allowed by current profile grants. |
| flutter | `/admin/permissions` | `auth-permission-administration` | Lists application-defined permission codes and directs permitted role editors to manage grants. |
| flutter | `/admin/roles` | `auth-role-administration` | Lists roles and provides permission-checked role create, update, permission-assignment and delete actions. |
| flutter | `/admin/users` | `auth-user-administration` | Lists accounts and provides permission-checked account, password, active-state and role-assignment actions. |
| react | `/404` | `not-found-recovery` | Explains the missing page in coastal language while preserving the requested browser path, then offers home and sign-in actions. |
| react | `/500` | `server-error-recovery` | Explains the temporary interruption between the shared header and footer, provides retry and home actions, and keeps implementation details hidden. |
| flutter | `/404` | `not-found-recovery` | Explains the missing page in coastal language and routes users back to the home page. |
| flutter | `/500` | `server-error-recovery` | Explains the temporary interruption, provides retry and home actions, and keeps implementation details hidden. |
| react | `/experiences` | `experience-discovery` | Browses coastal destinations, activities, and offerings with filters, Haversine distance, and OpenFreeMap vector maps. |
| react | `/experiences/destinations/:id` | `experience-destination-detail` | Displays comprehensive destination intelligence including real-time sea telemetry, harbor advisories, and species predictions with provenance. |
| react | `/experiences/offerings/:id` | `experience-offering-detail` | Evaluates departure slot availability in real time against operational limits and capacity constraints. |
| react | `/experiences/favourites` | `experience-favourites-management` | Manages user-saved coastal items with personal notes and one-click removal. |
| react | `/experiences/manage` | `experience-catalogue-management` | Provides pre-flight publication evaluation audits, lifecycle transitions, and timetable management. |
| flutter | `/experiences` | `experience-discovery` | Cross-platform mobile discovery route for destinations, activities, and offerings. |
| flutter | `/experiences/destinations/:id` | `experience-destination-detail` | Mobile destination view with marine conditions, operational advisories, and biodiversity insights. |
| flutter | `/experiences/offerings/:id` | `experience-offering-detail` | Mobile offering view with departure timetables and availability checks. |
| flutter | `/experiences/favourites` | `experience-favourites-management` | Mobile wishlist manager for coastal experiences. |
| flutter | `/experiences/manage` | `experience-catalogue-management` | Mobile operational workspace for catalogue authoring and publication audits. |

## Gateway and server routes

| Method | Path | Destination | Use |
|---|---|---|---|
| `ANY` | `/` | `frontend` | Serves browser UI traffic and forwards non-API frontend requests to the frontend container. |
| `ANY` | `/` | `frontend` | Serves static files and falls back to index.html for browser navigation. |
| `GET` | `/health` | `frontend` | Used by Docker-stack health checks to confirm the frontend server is reachable. |
| `ANY` | `/api/` | `api` | Forwards client API requests to the API gateway; clients must not target Auth, Agentic AI, database, or other internal hosts directly. |
| `ANY` | `/api/auth/{**catch-all}` | `auth` | Keeps Auth internal while exposing its approved endpoints through the public API boundary. |
| `ANY` | `/api/experiences/health` | `experience-biodiversity` | Forwards the health endpoint without requiring an Auth-issued JWT; it reports database availability while keeping the private service host inaccessible to clients. |
| `ANY` | `/api/experiences/swagger/{documentName}/swagger.json` | `experience-biodiversity` | Exposes the service contract to the unified Swagger UI without requiring a JWT or exposing the private service host. |
| `ANY` | `/api/experiences/{**catch-all}` | `experience-biodiversity` | Requires an Auth-issued JWT validated by the API gateway and private service for component paths not matched by the public-read or named-permission routes. |
| `GET` | `/api/swagger` | `api` | Provides interactive API documentation for local development and contract inspection. |
| `ANY` | `/404.html` | `edge-nginx` | Converts /404.html to /404 so the error asset filename is not exposed as the browser route. |
| `ANY` | `/500.html` | `edge-nginx` | Converts /500.html to /500 so the error asset filename is not exposed as the browser route. |
| `ANY` | `/404.html` | `frontend-nginx` | Converts /404.html to /404 so the error asset filename is not exposed as the browser route. |
| `ANY` | `/500.html` | `frontend-nginx` | Converts /500.html to /500 so the error asset filename is not exposed as the browser route. |
| `ANY` | `/_blueverse/errors/404` | `edge-nginx` | Shows the branded recovery page without changing the requested browser URL or exposing a file extension. |
| `ANY` | `/_blueverse/errors/500` | `edge-nginx` | Displays a friendly recovery page if the frontend service is unavailable; API upstream errors retain their structured responses. |
| `ANY` | `/_blueverse/errors/404` | `frontend` | Serves the branded not-found page for frontend Nginx 404 responses while SPA routes continue to load through index.html. |
| `ANY` | `/_blueverse/errors/500` | `frontend` | Serves the branded temporary-error page when the frontend Nginx layer raises a server error. |
| `ANY` | `/api/experiences/destinations/{**catch-all}` | `experience-biodiversity` | Published destination reads remain anonymous; draft or archived records are filtered by the service. POST, PUT, PATCH and DELETE requests require catalogue-management or system-role permission. |
| `ANY` | `/api/experiences/activities/{**catch-all}` | `experience-biodiversity` | Published activity reads remain anonymous; POST, PUT, PATCH and DELETE requests require catalogue-management or system-role permission. |
| `ANY` | `/api/experiences/offerings/{**catch-all}` | `experience-biodiversity` | Published offering and schedule reads remain anonymous; POST, PUT, PATCH and DELETE requests require catalogue-management or system-role permission. |
| `GET` | `/api/experiences/map/{**catch-all}` | `experience-biodiversity` | Forwards the published map configuration and location search through the API without exposing the private service host. |
| `GET` | `/api/experiences/nearby` | `experience-biodiversity` | Forwards public nearby discovery through the API while keeping component-service and provider addresses private. |
| `POST` | `/api/experiences/availability/evaluations` | `experience-biodiversity` | Requires an Auth-issued JWT with catalogue-read, catalogue-manage or system-role-manage permission before evaluating availability. |
| `ANY` | `/api/experiences/favourites/{**catch-all}` | `experience-biodiversity` | Requires an Auth-issued JWT; the private service derives the favourite owner only from the validated token subject. |

## Public API endpoints

### api

| Method | Path | Authorization | Purpose and use |
|---|---|---|---|
| `GET` | `/api/health` | `anonymous` | Reports that the public API process is healthy. Used by local stack health checks and operational diagnostics. |
| `GET` | `/api/swagger/{documentName}.json` | `anonymous` | Returns the API service OpenAPI document. Used by Swagger UI and tooling that needs the public API contract. |
| `GET` | `/api/swagger/{documentName}/swagger.json` | `anonymous` | Returns the API service Swagger document at the Swagger UI-compatible path. Used by the mounted API Swagger UI and local contract inspection. |
| `GET` | `/api/swagger` | `anonymous` | Serves the interactive Swagger UI for the public API. Used by developers and contract tooling to inspect the API without accessing an internal service host. |

### auth

| Method | Path | Authorization | Purpose and use |
|---|---|---|---|
| `GET` | `/api/auth/swagger/{documentName}/swagger.json` | `anonymous` | Returns the Auth service OpenAPI document through its public documentation path. Used for Auth contract inspection without exposing the Auth service host directly. |
| `POST` | `/api/auth/register` | `anonymous` | Creates a new user account and establishes the initial authentication result. Creates accounts from the shared React and Flutter registration workflow and returns the first session through browser cookies or native credentials. |
| `POST` | `/api/auth/login` | `anonymous` | Authenticates credentials and returns the access and refresh session result. Primary sign-in operation for the React and Flutter Auth workflow. |
| `POST` | `/api/auth/refresh` | `anonymous` | Refreshes an access token using an approved refresh token and can activate a previously signed-in browser account. Keeps an existing client session alive and lets the React browser account switcher select one of its protected account-scoped cookie sessions without replacing other accounts. |
| `POST` | `/api/auth/logout` | `authenticated` | Ends only the authenticated account's current-device session. Signs out the active account from this device while leaving other accounts on the same browser and this account's sessions on other devices active. |
| `POST` | `/api/auth/logout/{id:guid}` | `authenticated` | Ends the authenticated account's session on the current device; an account cannot end a different account's session. Provides a guarded account-specific current-device sign-out route for the authenticated account only. |
| `POST` | `/api/auth/logout-account` | `authenticated` | Ends only the authenticated account's session on the current device; other signed-in accounts must sign out separately. Used by the React and Flutter account menus to sign out the active account while keeping other saved accounts and sessions available; requests targeting a different account are forbidden. |
| `POST` | `/api/auth/logout-all-devices` | `authenticated` | Revokes all sessions for the authenticated account across devices after verifying the current password. Available only from Login Sessions in the Profile workflows; password verification is required before any session is revoked. |
| `GET` | `/api/auth/sessions` | `authenticated` | Lists active sessions for the authenticated account. Used by Profile in React Web and Flutter Mobile to review and end login sessions for the current account. |
| `DELETE` | `/api/auth/sessions/{sessionId:guid}` | `authenticated` | Revokes one selected authenticated session; the current session may be ended directly, while another device session requires current-password verification. Supports Profile in React Web and Flutter Mobile with session-scoped end actions and server-verified password confirmation for remote devices. |
| `POST` | `/api/auth/change-password` | `authenticated` | Changes the authenticated user's password after validating the current credential. Used by the Profile account-security section in React Web and Flutter Mobile. |
| `GET` | `/api/auth/me` | `authenticated` | Returns the current authenticated user's profile and authorization context. Used by the React and Flutter Auth workflow to restore the signed-in account, by profile management to show server-owned details, and by the dashboard to personalize the account overview. |
| `PUT` | `/api/auth/me` | `authenticated` | Updates editable profile data for the current authenticated user. Updates the current user's editable full name from the shared React and Flutter profile-management workflow. |
| `DELETE` | `/api/auth/me` | `authenticated` | Deletes the current authenticated account, except accounts assigned a system role, which are prohibited from self-deletion. Called after the user confirms the red account-deletion warning in Profile; a denied deletion names the system role or roles assigned to that account. |
| `GET` | `/api/auth/health` | `anonymous` | Reports Auth service and database readiness. Used by the API gateway and Docker-stack health diagnostics. |
| `GET` | `/api/auth/permissions` | `permission:auth.permission.read` | Lists permissions available to an authorized administration caller. Supports authorization administration and permission discovery. |
| `GET` | `/api/auth/permissions/{id:guid}` | `permission:auth.permission.read` | Returns one permission by identifier. Supports authorization administration and permission detail views. |
| `GET` | `/api/auth/roles` | `permission:auth.role.read` | Lists roles for authorized administration callers. Supports role administration and authorization inspection. |
| `GET` | `/api/auth/roles/{id:guid}` | `permission:auth.role.read` | Returns one role by identifier. Supports role detail and authorization administration screens. |
| `POST` | `/api/auth/roles` | `permission:all(auth.role.read,auth.role.create)` | Creates a role when the caller has both role-read and role-create permission. Supports authorized role administration. |
| `PUT` | `/api/auth/roles/{id:guid}` | `permission:all(auth.role.read,auth.role.update)` | Updates role details when the caller has both role-read and role-update permission. Supports authorized role administration. |
| `DELETE` | `/api/auth/roles/{id:guid}` | `permission:all(auth.role.read,auth.role.delete)` | Deletes an eligible non-system role when the caller has both role-read and role-delete permission. Supports authorized role administration. |
| `POST` | `/api/auth/roles/{id:guid}/permissions` | `permission:all(auth.role.read,auth.role.update,auth.permission.read)` | Replaces a non-system role’s permission set. Requires role read, role update and permission catalogue read together; system-role grants remain protected. Role editors can grant or remove permissions from eligible non-system roles. The Auth service checks the caller’s current role assignments and invalidates affected user sessions. |
| `GET` | `/api/auth/users` | `permission:auth.user.read` | Lists users for authorized administration callers. Supports user administration and authorization inspection. |
| `GET` | `/api/auth/users/{id:guid}` | `permission:auth.user.read` | Returns one user by identifier. Supports authorized user administration detail views. |
| `POST` | `/api/auth/users` | `permission:all(auth.user.read,auth.user.create)` | Creates an account with user read and user create grants together; assigning initial roles also requires role read and any applicable system-role grant. Supports authorized user administration. |
| `PUT` | `/api/auth/users/{id:guid}` | `permission:all(auth.user.read,auth.user.update)` | Updates account details, active state or password with user read and user update grants together. Supports authorized user administration. |
| `DELETE` | `/api/auth/users/{id:guid}` | `permission:all(auth.user.read,auth.user.delete)` | Deletes an account with user read and user delete grants together, subject to protected system-role rules. Supports authorized user administration; a protected account's assigned system role names are included in the deletion rejection. |
| `POST` | `/api/auth/users/{id:guid}/roles` | `permission:all(auth.user.read,auth.user.update,auth.role.read)` | Replaces an account’s roles with user read, user update and role read grants together; system-role changes require the dedicated grant. Authorized user editors can add or remove user roles; the service checks current caller grants, system-role constraints and invalidates the affected account sessions. |

### experience-biodiversity

| Method | Path | Authorization | Purpose and use |
|---|---|---|---|
| `GET` | `/api/experiences/health` | `anonymous` | Reports Experience & Biodiversity service and PostgreSQL connectivity; returns 503 when the database is unavailable. Used by the API gateway and Docker-stack health diagnostics. The response includes service, overall status and database connection status without exposing the private service host. |
| `GET` | `/api/experiences/swagger/{documentName}/swagger.json` | `anonymous` | Returns the Experience & Biodiversity Swashbuckle document through the API gateway. Supports the unified Swagger UI and service contract inspection without exposing the private service host or requiring a JWT. |
| `GET` | `/api/experiences/activities` | `anonymous` | Lists published coastal activities or filters by destination, status, and search query. Browses available activities (snorkeling, whale watching, surfing) across coastal regions. |
| `GET` | `/api/experiences/activities/{id:guid}` | `anonymous` | Retrieves detailed information for a specific coastal activity. Loads detailed activity views including destination and offering summaries. |
| `POST` | `/api/experiences/activities` | `permission:experiences.catalogue.manage` | Creates a new coastal activity under an existing destination in DRAFT status. Used by authorized staff or operators to define new coastal activities. |
| `PUT` | `/api/experiences/activities/{id:guid}` | `permission:experiences.catalogue.manage` | Updates activity metadata, seasonal constraints, requirements, and tags. Edits existing coastal activity specifications. |
| `DELETE` | `/api/experiences/activities/{id:guid}` | `permission:experiences.catalogue.manage` | Deletes a coastal activity taxonomy record and its child offerings. Allows administrators to remove obsolete activity taxonomy categories. |
| `PATCH` | `/api/experiences/activities/{id:guid}/publication` | `permission:experiences.catalogue.manage` | Transitions an activity publication status with evaluation rules and audit reasons. Publishes, unpublishes, or archives an activity following lifecycle validation. |
| `POST` | `/api/experiences/activities/{id:guid}/publication-evaluations` | `permission:experiences.catalogue.manage` | Evaluates whether an activity meets publication readiness without applying changes. Pre-flight validation checks before publishing an activity. |
| `GET` | `/api/experiences/agent/context` | `authenticated` | Read-only private Agentic AI typed seam reporting component context and not-connected status. Pre-G07 integration seam providing typed schema and safe degradation for future AI orchestration. |
| `POST` | `/api/experiences/availability/evaluations` | `permission:experiences.catalogue.read` | Evaluates real-time offering availability against schedule coverage, capacity, and operational status. Core non-CRUD business operation called by clients and booking workflows to verify experience availability. |
| `GET` | `/api/experiences/destinations/{id:guid}/biodiversity` | `anonymous` | Consumes biodiversity data for a destination with safe degradation when Member 3 ML is unavailable. Retrieves ecological insights and species discovery information for coastal destinations. |
| `GET` | `/api/experiences/destinations` | `anonymous` | Lists published coastal destinations or filters by region and status. Browses coastal destinations (Mirissa, Nilaveli, etc.) on web and mobile directories. |
| `GET` | `/api/experiences/destinations/{id:guid}` | `anonymous` | Retrieves detailed information for a specific coastal destination. Loads destination profile views including coordinates, climate info, and active activities. |
| `POST` | `/api/experiences/destinations` | `permission:experiences.catalogue.manage` | Creates a new coastal destination in DRAFT status. Used by destination curators and administrators to register new coastal areas. |
| `PUT` | `/api/experiences/destinations/{id:guid}` | `permission:experiences.catalogue.manage` | Updates destination details, coordinates, boundaries, and tags. Maintains destination records and geographical data. |
| `DELETE` | `/api/experiences/destinations/{id:guid}` | `permission:experiences.catalogue.manage` | Deletes a coastal destination record. Allows administrators to remove destinations from the catalogue. |
| `PATCH` | `/api/experiences/destinations/{id:guid}/publication` | `permission:experiences.catalogue.manage` | Transitions a destination publication status with validation and audit logging. Publishes or archives coastal destination profiles. |
| `POST` | `/api/experiences/destinations/{id:guid}/publication-evaluations` | `permission:experiences.catalogue.manage` | Evaluates destination readiness for publication without altering current state. Pre-publication audit check verifying required coordinates and content completeness. |
| `GET` | `/api/experiences/favourites` | `authenticated` | Retrieves saved destinations, activities, and offerings for the calling user. Populates personal wishlist and bookmarked coastal experiences. |
| `PUT` | `/api/experiences/favourites/{targetType}/{targetId:guid}` | `authenticated` | Adds or updates a bookmark/favourite for a destination, activity, or offering with optional notes. Allows tourists to save coastal items to their personal wishlist. |
| `DELETE` | `/api/experiences/favourites/{targetType}/{targetId:guid}` | `authenticated` | Removes a bookmarked destination, activity, or offering from the user favourites. Removes items from tourist wishlists. |
| `GET` | `/api/experiences/map/config` | `anonymous` | Provides MapLibre-compatible vector tile configuration and style URLs for OpenFreeMap. Used by React and Flutter map components to render interactive coastal maps without third-party tokens. |
| `GET` | `/api/experiences/map/search` | `anonymous` | Searches coastal places using Photon geocoding with graceful fallback to destination database. Powers place name autocomplete and location lookups on interactive map screens. |
| `GET` | `/api/experiences/nearby` | `anonymous` | Finds destinations, activities, and offerings within a specified radius using Haversine calculation. Powers proximity discovery on map views and Experiences Near Me mobile workflows. |
| `GET` | `/api/experiences/offerings` | `anonymous` | Lists published offerings or filters by activity, provider, price range, and status. Displays bookable tour packages and activity offerings. |
| `GET` | `/api/experiences/offerings/{id:guid}` | `anonymous` | Retrieves detailed information for a specific experience offering. Loads offering details including pricing, cancellation policy, and schedule overview. |
| `POST` | `/api/experiences/offerings` | `permission:experiences.catalogue.manage` | Creates a new experience offering under an activity in DRAFT status. Allows tour operators and service providers to register new offering packages. |
| `PUT` | `/api/experiences/offerings/{id:guid}` | `permission:experiences.catalogue.manage` | Updates offering pricing, capacity, inclusions, and policies. Edits existing tour package specifications. |
| `DELETE` | `/api/experiences/offerings/{id:guid}` | `permission:experiences.catalogue.manage` | Deletes an experience offering package and its related schedule slots. Allows administrators to remove offering packages from the catalogue. |
| `PATCH` | `/api/experiences/offerings/{id:guid}/publication` | `permission:experiences.catalogue.manage` | Transitions an offering publication status with verification rules. Publishes, pauses, or archives tour offerings. |
| `POST` | `/api/experiences/offerings/{id:guid}/publication-evaluations` | `permission:experiences.catalogue.manage` | Evaluates offering readiness for publication without altering current state. Checks pricing validity, capacity constraints, and parent activity publication status. |
| `GET` | `/api/experiences/offerings/{id:guid}/schedules` | `anonymous` | Lists time slots and schedules for a specific offering within an optional date range. Displays available time slots on booking calendars. |
| `POST` | `/api/experiences/offerings/{id:guid}/schedules` | `permission:experiences.catalogue.manage` | Adds a new operational schedule slot with time window and capacity to an offering. Schedules departure times for coastal experiences. |
| `PUT` | `/api/experiences/offerings/{id:guid}/schedules/{scheduleId:guid}` | `permission:experiences.catalogue.manage` | Updates schedule capacity, booked seats, active flag, or time range. Manages schedule availability and operational capacity adjustments. |
| `DELETE` | `/api/experiences/offerings/{id:guid}/schedules/{scheduleId:guid}` | `permission:experiences.catalogue.manage` | Cancels or removes an operational schedule slot from an offering. Removes obsolete or cancelled departure times. |
| `GET` | `/api/experiences/dependencies/status` | `authenticated` | Inspects connectivity, response status, and latency of peer microservice dependencies with resilience reporting. Diagnostic and status seam verifying microservices architecture fault tolerance. |
| `GET` | `/api/experiences/destinations/{id:guid}/marine-conditions` | `anonymous` | Fetches real-time marine conditions from Member 2 Marine Safety service with fault-tolerant fallback and status mention. Displays sea conditions, surf safety, and wave telemetry for coastal destinations. |
| `GET` | `/api/experiences/destinations/{id:guid}/operational-advisories` | `anonymous` | Fetches active coastal advisories from Member 4 Coastal Operations service with safe fallback and status mention. Displays harbor and coastal authority notices for destinations. |

## Test host only

These routes exist in test fixtures and are not production endpoints.

| Method | Path | Use |
|---|---|---|
| `GET` | `/api/test-only/errors` | Used only by API integration tests; never a production route. |
| `GET` | `/api/test-only/request-context` | Used only by API integration tests; never a production route. |
| `GET` | `/api/test-only/auth` | Used only by API integration tests; never a production route. |
| `GET` | `/api/test-only/auth-errors` | Used only by Auth integration tests; never a production route. |

## Agentic AI endpoints

None implemented. Clients must use the public API; target architecture does not create a live route.
