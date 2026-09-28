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
| react | `/marine/conditions` | `marine-conditions` | Queries current/forecast conditions and the server suitability result for a location, activity and requested UTC period through the public marine API; shows source, timestamps, freshness and missing fields without reclassifying. |
| react | `/marine/history` | `marine-condition-history` | Lists past snapshots with optional latitude, longitude and UTC window filters, keeping source, forecast/retrieval times, freshness and missing fields visible. |
| react | `/marine/safety-profiles` | `marine-safety-profile-management` | Reads configured deterministic limits per activity; accounts holding the marine manage grant create superseding versions, update limits and deactivate profiles through the public marine API. |
| flutter | `/marine/conditions` | `marine-conditions` | Registered under the shared workflow ID for the paired mobile implementation, which must provide the same authorized condition and suitability outcomes; not implemented yet. |
| flutter | `/marine/history` | `marine-condition-history` | Registered under the shared workflow ID for the paired mobile implementation, which must provide the same authorized history outcomes; not implemented yet. |
| flutter | `/marine/safety-profiles` | `marine-safety-profile-management` | Registered under the shared workflow ID for the paired mobile implementation, which must provide the same authorized profile-management outcomes; not implemented yet. |

## Gateway and server routes

| Method | Path | Destination | Use |
|---|---|---|---|
| `ANY` | `/` | `frontend` | Serves browser UI traffic and forwards non-API frontend requests to the frontend container. |
| `ANY` | `/` | `frontend` | Serves static files and falls back to index.html for browser navigation. |
| `GET` | `/health` | `frontend` | Used by Docker-stack health checks to confirm the frontend server is reachable. |
| `ANY` | `/api/` | `api` | Forwards client API requests to the API gateway; clients must not target Auth, Agentic AI, database, or other internal hosts directly. |
| `ANY` | `/api/marine/{**catch-all}` | `marine-safety` | Keeps the marine-safety component service private while exposing its approved operations through the public API boundary. |
| `ANY` | `/api/auth/{**catch-all}` | `auth` | Keeps Auth internal while exposing its approved endpoints through the public API boundary. |
| `GET` | `/api/swagger` | `api` | Provides interactive API documentation for local development and contract inspection. The marine-safety document is served through the /api/marine YARP route and listed in this UI. |
| `ANY` | `/404.html` | `edge-nginx` | Converts /404.html to /404 so the error asset filename is not exposed as the browser route. |
| `ANY` | `/500.html` | `edge-nginx` | Converts /500.html to /500 so the error asset filename is not exposed as the browser route. |
| `ANY` | `/404.html` | `frontend-nginx` | Converts /404.html to /404 so the error asset filename is not exposed as the browser route. |
| `ANY` | `/500.html` | `frontend-nginx` | Converts /500.html to /500 so the error asset filename is not exposed as the browser route. |
| `ANY` | `/_blueverse/errors/404` | `edge-nginx` | Shows the branded recovery page without changing the requested browser URL or exposing a file extension. |
| `ANY` | `/_blueverse/errors/500` | `edge-nginx` | Displays a friendly recovery page if the frontend service is unavailable; API upstream errors retain their structured responses. |
| `ANY` | `/_blueverse/errors/404` | `frontend` | Serves the branded not-found page for frontend Nginx 404 responses while SPA routes continue to load through index.html. |
| `ANY` | `/_blueverse/errors/500` | `frontend` | Serves the branded temporary-error page when the frontend Nginx layer raises a server error. |

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

### marine-safety

| Method | Path | Authorization | Purpose and use |
|---|---|---|---|
| `GET` | `/api/marine/swagger/{documentName}/swagger.json` | `anonymous` | Returns the marine-safety service OpenAPI document at its Swagger UI-compatible path. Serves the internal marine-safety service contract for local contract inspection; reachable through the public API gateway only. |
| `GET` | `/api/marine/swagger` | `anonymous` | Serves the marine-safety service interactive Swagger UI. Used by developers and contract tooling to inspect the marine-safety domain contract during backend development. |
| `GET` | `/api/marine/health` | `anonymous` | Reports marine-safety service and database readiness. Used by Docker-stack health diagnostics and operational checks; mirrors the Auth health contract. |
| `GET` | `/api/marine/current` | `permission:marine.profile.read` | Returns current or forecast marine/weather conditions for a location and optional time. Acquires conditions through the backend Open-Meteo adapter, reusing a fresh stored snapshot when available, and reports source, retrieval time, freshness and missing fields. Used by React and Flutter condition workflows and future suitability consumers. |
| `GET` | `/api/marine/snapshots/{id:guid}` | `permission:marine.profile.read` | Returns one stored condition snapshot with provenance and missing-field detail. Lets authorized callers inspect a persisted snapshot's source, timestamps, freshness and unavailable fields; snapshots are provider-derived and read-only. |
| `GET` | `/api/marine/history` | `permission:marine.profile.read` | Lists stored condition snapshots filtered by location and/or time window. Supports condition-history inspection with optional latitude, longitude, from and to query filters; each row carries its own source and freshness classification. |
| `POST` | `/api/marine/evaluate` | `permission:marine.profile.read` | Runs the deterministic activity/location/time suitability assessment. The Member 2 non-CRUD operation: validates the activity and its safety profile, gathers fresh condition evidence, applies the profile's deterministic limits, and returns SUITABLE, UNSUITABLE or UNKNOWN with violations, evidence, source and freshness. Missing profiles or activities are rejected; missing or stale evidence yields UNKNOWN rather than a fabricated result. |
| `GET` | `/api/marine/safety-profiles` | `permission:marine.profile.read` | Lists activity safety profiles with their configured limits and version history. Lets authorized callers inspect the deterministic safety configuration per activity, including caution bands and active/version state. |
| `GET` | `/api/marine/safety-profiles/{id:guid}` | `permission:marine.profile.read` | Returns one safety profile by identifier. Supports profile detail inspection for authorized callers. |
| `GET` | `/api/marine/safety-profiles/by-activity/{activityId:guid}` | `permission:marine.profile.read` | Returns the active safety profile configured for one activity. Supports activity-centric profile lookup for condition and suitability workflows. |
| `POST` | `/api/marine/safety-profiles` | `permission:all(marine.profile.read,marine.profile.manage)` | Creates a safety profile for an activity, superseding the previous active profile. Permission-gated management: validates the activity exists and is active, requires positive limits, and rejects caution bands above their hard limits. Prior active profiles are deactivated while history is retained. |
| `PUT` | `/api/marine/safety-profiles/{id:guid}` | `permission:all(marine.profile.read,marine.profile.manage)` | Updates a safety profile's limits and increments its version. Permission-gated management with the same validation as creation; version increments keep past assessments interpretable against the configuration that produced them. |
| `DELETE` | `/api/marine/safety-profiles/{id:guid}` | `permission:all(marine.profile.read,marine.profile.manage)` | Deactivates a safety profile without deleting it. Profiles are deactivated rather than deleted so assessment history keeps its profile reference; deactivation is idempotent. |
| `GET` | `/api/marine/activities` | `permission:marine.profile.read` | Lists the marine activity reference rows with their active state. Lets authorized callers resolve activity identities and names for profile, condition and suitability workflows; supports an optional isActive filter. |
| `GET` | `/api/marine/activities/{id:guid}` | `permission:marine.profile.read` | Returns one marine activity reference row by identifier. Supports activity detail inspection for condition, suitability and profile workflows. |
| `POST` | `/api/marine/activities` | `permission:all(marine.profile.read,marine.profile.manage)` | Creates a marine activity reference row with a unique name. Permission-gated management over the locally-owned activity reference table (placeholder for Member 1's canonical taxonomy); duplicate names are rejected as a state conflict. |
| `PUT` | `/api/marine/activities/{id:guid}` | `permission:all(marine.profile.read,marine.profile.manage)` | Renames or retypes a marine activity reference row. Permission-gated management; the name stays unique within the reference table and rows are never re-activated through this surface. |
| `DELETE` | `/api/marine/activities/{id:guid}` | `permission:all(marine.profile.read,marine.profile.manage)` | Deactivates a marine activity without deleting it. Deletion deactivates so assessment history keeps its restrict reference and profiles keep their cascade reference; an inactive activity cannot receive new profiles or evaluations. Deactivation is idempotent. |
| `GET` | `/api/marine/assessments` | `permission:marine.profile.read` | Lists persisted suitability assessments, newest first, filtered by activity, result and evaluation window. Makes the deterministic evaluate results queryable: every stored row carries the applied profile version, condition-snapshot reference, result, violations, caution factors, missing fields, source and freshness. Bounded like condition history. |
| `GET` | `/api/marine/assessments/{id:guid}` | `permission:marine.profile.read` | Returns one persisted suitability assessment with its full evidence references. Supports audit and explainability workflows: a past result stays interpretable after profiles or conditions change. |

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
