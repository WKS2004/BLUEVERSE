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
| react | `/operations/assessments` | `coastal-operations-assessment` | 500ms live title search, record-specific 5/10/25/50/100 pagination and pinned section tabs. Query view=create or view=edit\|detail&id=UUID restores the focused workspace on refresh; Logs uses kind=assessments\|alerts and returns to Logs. |
| flutter | `/operations/assessments` | `coastal-operations-assessment` | 500ms live title search, record-specific 5/10/25/50/100 pagination and pinned section tabs. Query view=create or view=edit\|detail&id=UUID restores the focused workspace on refresh; Logs uses kind=assessments\|alerts and returns to Logs. |
| react | `/operations/alerts` | `coastal-operations-alerts` | 500ms live title search, record-specific 5/10/25/50/100 pagination and pinned section tabs. Query view=create or view=edit\|detail&id=UUID restores the focused workspace on refresh; Logs uses kind=assessments\|alerts and returns to Logs. |
| flutter | `/operations/alerts` | `coastal-operations-alerts` | 500ms live title search, record-specific 5/10/25/50/100 pagination and pinned section tabs. Query view=create or view=edit\|detail&id=UUID restores the focused workspace on refresh; Logs uses kind=assessments\|alerts and returns to Logs. |
| react | `/operations/logs` | `coastal-operations-logs` | 500ms live title search, record-specific 5/10/25/50/100 pagination and pinned section tabs. Query view=create or view=edit\|detail&id=UUID restores the focused workspace on refresh; Logs uses kind=assessments\|alerts and returns to Logs. |
| flutter | `/operations/logs` | `coastal-operations-logs` | 500ms live title search, record-specific 5/10/25/50/100 pagination and pinned section tabs. Query view=create or view=edit\|detail&id=UUID restores the focused workspace on refresh; Logs uses kind=assessments\|alerts and returns to Logs. |

## Gateway and server routes

| Method | Path | Destination | Use |
|---|---|---|---|
| `ANY` | `/` | `frontend` | Serves browser UI traffic and forwards non-API frontend requests to the frontend container. |
| `ANY` | `/` | `frontend` | Serves static files and falls back to index.html for browser navigation. |
| `GET` | `/health` | `frontend` | Used by Docker-stack health checks to confirm the frontend server is reachable. |
| `ANY` | `/api/` | `api` | Forwards client API requests to the API gateway; clients must not target Auth, Agentic AI, database, or other internal hosts directly. |
| `ANY` | `/api/operations/assessments/` | `api` | Forwards assessment item requests to the public API gateway and permits up to 6 MiB at the edge for the service's validated 5 MiB PNG evidence uploads plus multipart framing. |
| `ANY` | `/api/auth/{**catch-all}` | `auth` | Keeps Auth internal while exposing its approved endpoints through the public API boundary. |
| `ANY` | `/api/operations/{**catch-all}` | `coastal-operations` | Keeps the Coastal Operations service private while exposing its approved routes through the public API boundary. |
| `GET` | `/api/swagger` | `api` | Provides interactive API documentation for local development and contract inspection. |
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
| `GET` | `/api/swagger` | `anonymous` | Serves the interactive Swagger UI for the public API. Used by developers and contract tooling to inspect the public API, Auth and Coastal Operations contracts without accessing internal service hosts. |

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
| `POST` | `/api/auth/users/{id:guid}/roles` | `permission:all(auth.user.read,auth.user.update,auth.role.read)` | Replaces an account’s roles with user read, user update and role read grants together; system-role changes require the dedicated grant, and an Admin cannot remove its own Admin system role. Authorized user editors can add or remove user roles; the service checks current caller grants, rejects Admin self-demotion before changing assignments or sessions, applies system-role constraints and invalidates sessions after accepted changes. |

### coastal-operations

| Method | Path | Authorization | Purpose and use |
|---|---|---|---|
| `GET` | `/api/operations/health` | `anonymous` | Reports Coastal Operations PostgreSQL connectivity and migration readiness plus the latest status of optional Member 1–3 peer requests. Used by the API gateway and Docker-stack readiness diagnostics; peer status is informational and does not gate readiness. |
| `GET` | `/api/operations/health/live` | `anonymous` | Reports Coastal Operations process liveness without probing optional dependencies. Used by container and orchestrator liveness checks. |
| `GET` | `/api/operations/health/ready` | `anonymous` | Reports Coastal Operations database connectivity and migration readiness plus the latest optional Member 1–3 dependency outcomes. Used to determine whether the required PostgreSQL schema is ready; absent or unhealthy peer components do not make Coastal Operations unready. |
| `GET` | `/api/operations/swagger/{documentName}/swagger.json` | `anonymous` | Returns the Coastal Operations OpenAPI document through the public API gateway. Loaded by the Coastal Operations entry in the shared Swagger UI at `/api/swagger`. |
| `POST` | `/api/operations/assessments` | `permission:operations.assessment.create` | Creates a caller-owned Coastal Operations assessment draft without calling peer services or the disconnected pre-G07 AI seam. Requires an Idempotency-Key. Returns DRAFT with NOT_CONNECTED AI status and no peer dependency results. Submit the draft separately to collect bounded Member 1–3 outcomes. |
| `PATCH` | `/api/operations/assessments/{assessmentId:guid}` | `permission:operations.assessment.update` | Updates a caller-owned assessment draft with optimistic version checking. Only DRAFT assessments can be updated. The request supplies expectedVersion; each accepted change increments the version and creates an audit record. |
| `DELETE` | `/api/operations/assessments/{assessmentId:guid}` | `permission:operations.assessment.delete` | Logically cancels a caller-owned assessment draft and retains its tombstone. Requires expectedVersion and Idempotency-Key. Returns CANCELLED with actor/time fields and records an audit event; submitted or closed assessments cannot be deleted. |
| `POST` | `/api/operations/assessments/{assessmentId:guid}/submit` | `permission:operations.assessment.submit` | Submits a caller-owned assessment draft and records bounded Member 1–3 dependency outcomes. Requires expectedVersion and Idempotency-Key. Peer calls have bounded timeouts/retries; dependency failure is recorded without failing the service. Before G07 the result remains SUBMITTED with AI status NOT_CONNECTED and no proposal. User-facing Publish assessment atomically stores a complete immutable business delivery envelope; no live AI endpoint exists before G07. |
| `GET` | `/api/operations/assessments` | `permission:operations.assessment.read` | Lists the caller's assessments or the authorized review queue with bounded cursor pagination. Title keyword search defaults to all types; recordId/targetId are advanced exact UUID filters. Authorized scope precedes filtering/pagination; reviewer All includes own drafts. Public alert readers see only valid ACTIVE PUBLIC alerts. |
| `GET` | `/api/operations/assessments/{assessmentId:guid}` | `permission:operations.assessment.read` | Returns an in-scope assessment and its recorded reviewer decisions. Assessment readers may inspect their own records, including their cancelled drafts; queue readers may inspect in-scope records. Evidence metadata is included only with operations.evidence.read. |
| `POST` | `/api/operations/assessments/{assessmentId:guid}/decisions` | `permission:operations.assessment.decide` | Records an authorized approve, reject or request-revision decision against a validated proposal version. Requires Idempotency-Key and expected target-state version. Returns a conflict without mutation when no pre-G07 proposal exists. |
| `GET` | `/api/operations/targets/{targetType}/{targetId:guid}/status` | `permission:operations.target.status.read` | Reads a known Coastal Operations managed target's operational state and concurrency version. A missing baseline is initialized once from a current, validated provisional Member 1 availability response; this branch-local producer contract remains pending shared G00 acceptance. |
| `GET` | `/api/operations/targets/{targetType}/{targetId:guid}/history` | `permission:operations.target.history.read` | Returns bounded, cursor-paginated operational state transitions for an authorized target. History is produced atomically with an approved target-state transition. |
| `GET` | `/api/operations/alerts` | `permission:operations.alert.read` | Lists authorized Coastal Operations alert records with lifecycle and target filters. Title keyword search defaults to all types; recordId/targetId are advanced exact UUID filters. Authorized scope precedes filtering/pagination; reviewer All includes own drafts. Public alert readers see only valid ACTIVE PUBLIC alerts. |
| `POST` | `/api/operations/alerts` | `permission:operations.alert.create` | Creates an unpublished, versioned operational alert draft. Validates its time period, target identity and optional matching assessment; no external alert delivery is performed. |
| `PATCH` | `/api/operations/alerts/{alertId:guid}` | `permission:operations.alert.update` | Updates a proposed alert draft using optimistic version checks. Only PROPOSED drafts can be edited; active alert content must be replaced by a new proposal. |
| `POST` | `/api/operations/alerts/{alertId:guid}/decisions` | `permission:operations.alert.decide` | Publishes or resolves an alert through an authorized, idempotent lifecycle decision. PUBLISH requires operations.alert.publish; RESOLVE requires operations.alert.resolve. Existing operations.alert.decide remains compatible with both. Enforces version/idempotency and separate reviewers for high-impact publication. |
| `DELETE` | `/api/operations/alerts/{alertId:guid}` | `permission:operations.alert.delete` | Logically withdraws a caller-owned proposed alert draft with an audited tombstone. Requires expectedVersion and Idempotency-Key. Only PROPOSED alerts can be withdrawn; the WITHDRAWN record and actor/time audit remain available to alert managers. |
| `POST` | `/api/operations/assessments/{assessmentId:guid}/evidence` | `permission:operations.evidence.upload` | Validates and privately stores one assessment image, then returns versioned evidence metadata. Accepts sanitized PNG up to 5 MiB only on an owned DRAFT; maximum five current attachments. Published evidence/content cannot be edited. |
| `GET` | `/api/operations/assessments/{assessmentId:guid}/evidence/{evidenceId:guid}` | `permission:operations.evidence.read` | Returns privately stored PNG evidence after owner/reviewer scope and content integrity checks. Returns no-store image content through the public API; callers receive no storage URL. Reviewers also need assessment queue access. Removed attachments return 410 even while byte cleanup is pending. |
| `GET` | `/api/operations/assessments/{assessmentId:guid}/audit` | `permission:operations.audit.read` | Returns scoped assessment activity history. Requires explicit audit.read and owner or authorized manager scope; cursor pagination; action, actor, time and correlation only. Includes captured actor name/roles, readable summary and allowlisted before/after changes when recorded; legacy rows explicitly lack snapshots. |
| `GET` | `/api/operations/alerts/{alertId:guid}/audit` | `permission:operations.audit.read` | Returns scoped alert activity history. Requires explicit audit.read and owner or authorized manager scope; cursor pagination; action, actor, time and correlation only. Includes captured actor name/roles, readable summary and allowlisted before/after changes when recorded; legacy rows explicitly lack snapshots. |
| `GET` | `/api/operations/form-options` | `permission:operations.form.options.read` | Named draft associations and database-backed global time-zone choices. Derived from existing Coastal Operations grants. Scoped assessment choices; absent owning catalogue/planner returns unavailable. One selected IANA zone resolves both local dates on the server. |
| `DELETE` | `/api/operations/assessments/{assessmentId:guid}/evidence/{evidenceId:guid}` | `permission:operations.evidence.upload` | Removes an owned draft attachment while retaining audit metadata. JSON expectedVersion; owner/DRAFT gate, optimistic version and REMOVED event; durable cleanup after commit; 409 after publication. |
| `GET` | `/api/operations/logs/assessments` | `permission:operations.audit.read` | Lists scoped assessment records including cancelled history. Also requires assessment read or queue read. Owner/queue scopes, typed filters and cursor pageSize 1–100. Audit timelines use existing record endpoints. |
| `GET` | `/api/operations/logs/alerts` | `permission:operations.audit.read` | Lists scoped alert records across retained lifecycles. Also requires alert read or existing management compatibility. Managers see retained records; ordinary audit readers see their own only. Cursor pageSize 1–100. |

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
