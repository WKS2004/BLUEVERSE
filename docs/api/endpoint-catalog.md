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
| react | `/` | `foundation-home` | Loads the foundation entry screen; it currently has no public API dependency. |
| react | `/login` | `auth-session-management` | Uses the public Auth API for login, refresh, current-user, session listing, logout, and logout-all-devices operations. |
| flutter | `/` | `foundation-home` | Loads the foundation entry screen; it currently has no public API dependency. |
| flutter | `/login` | `auth-session-management` | Uses the same public Auth API contract as the React workflow. |

## Gateway and server routes

| Method | Path | Destination | Use |
|---|---|---|---|
| `ANY` | `/` | `frontend` | Serves browser UI traffic and forwards non-API frontend requests to the frontend container. |
| `ANY` | `/` | `frontend` | Serves static files and falls back to index.html for browser navigation. |
| `GET` | `/health` | `frontend` | Used by Docker-stack health checks to confirm the frontend server is reachable. |
| `ANY` | `/api/` | `api` | Forwards client API requests to the API gateway; clients must not target Auth, Agentic AI, database, or other internal hosts directly. |
| `ANY` | `/api/auth/{**catch-all}` | `auth` | Keeps Auth internal while exposing its approved endpoints through the public API boundary. |
| `GET` | `/api/swagger` | `api` | Provides interactive API documentation for local development and contract inspection. |

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
| `POST` | `/api/auth/register` | `anonymous` | Creates a new user account and establishes the initial authentication result. Used by an onboarding client flow when registration is enabled. |
| `POST` | `/api/auth/login` | `anonymous` | Authenticates credentials and returns the access and refresh session result. Primary sign-in operation for the React and Flutter Auth workflow. |
| `POST` | `/api/auth/refresh` | `anonymous` | Refreshes an access token using an approved refresh token. Keeps an existing client session alive without asking for credentials again. |
| `POST` | `/api/auth/logout` | `authenticated` | Logs out all signed-in accounts on the current device and archives their sessions. Used when a signed-in React or Flutter user signs out of the current device. |
| `POST` | `/api/auth/logout/{id:guid}` | `authenticated` | Ends the selected account session identified by a user ID. Supports an authenticated account-management operation when a specific account session must be closed. |
| `POST` | `/api/auth/logout-all-devices` | `authenticated` | Revokes all sessions for the authenticated account across devices. Used by the React and Flutter Auth workflow for account-wide session protection. |
| `GET` | `/api/auth/sessions` | `authenticated` | Lists active sessions for the authenticated account. Used by the React and Flutter Auth workflow to display device/session state. |
| `DELETE` | `/api/auth/sessions/{sessionId:guid}` | `authenticated` | Revokes one selected authenticated session. Supports an account-management screen that lets a user remove one device/session. |
| `POST` | `/api/auth/change-password` | `authenticated` | Changes the authenticated user's password after validating the current credential. Used by an authenticated account-security flow. |
| `GET` | `/api/auth/me` | `authenticated` | Returns the current authenticated user's profile and authorization context. Used by the React and Flutter Auth workflow to restore the signed-in account. |
| `PUT` | `/api/auth/me` | `authenticated` | Updates editable profile data for the current authenticated user. Supports an authenticated profile-management flow. |
| `DELETE` | `/api/auth/me` | `authenticated` | Deletes or deactivates the current authenticated account according to the Auth service policy. Supports an authenticated account-deletion flow. |
| `GET` | `/api/auth/health` | `anonymous` | Reports Auth service and database readiness. Used by the API gateway and Docker-stack health diagnostics. |
| `GET` | `/api/auth/permissions` | `permission:auth.permission.read` | Lists permissions available to an authorized administration caller. Supports authorization administration and permission discovery. |
| `GET` | `/api/auth/permissions/{id:guid}` | `permission:auth.permission.read` | Returns one permission by identifier. Supports authorization administration and permission detail views. |
| `GET` | `/api/auth/roles` | `permission:auth.role.read` | Lists roles for authorized administration callers. Supports role administration and authorization inspection. |
| `GET` | `/api/auth/roles/{id:guid}` | `permission:auth.role.read` | Returns one role by identifier. Supports role detail and authorization administration screens. |
| `POST` | `/api/auth/roles` | `permission:auth.role.manage` | Creates a role with the supplied authorization metadata. Supports authorized role administration. |
| `PUT` | `/api/auth/roles/{id:guid}` | `permission:auth.role.manage` | Updates an existing role. Supports authorized role administration. |
| `DELETE` | `/api/auth/roles/{id:guid}` | `permission:auth.role.manage` | Deletes a role when it is safe to do so under authorization policy. Supports authorized role administration. |
| `POST` | `/api/auth/roles/{id:guid}/permissions` | `permission:auth.role.manage` | Assigns permissions to a role. Supports authorized role-to-permission administration. |
| `GET` | `/api/auth/users` | `permission:auth.user.read` | Lists users for authorized administration callers. Supports user administration and authorization inspection. |
| `GET` | `/api/auth/users/{id:guid}` | `permission:auth.user.read` | Returns one user by identifier. Supports authorized user administration detail views. |
| `POST` | `/api/auth/users` | `permission:auth.user.manage` | Creates a user through the authorized administration boundary. Supports authorized user administration. |
| `PUT` | `/api/auth/users/{id:guid}` | `permission:auth.user.manage` | Updates an existing user through the authorized administration boundary. Supports authorized user administration. |
| `DELETE` | `/api/auth/users/{id:guid}` | `permission:auth.user.manage` | Deletes a user through the authorized administration boundary. Supports authorized user administration. |
| `POST` | `/api/auth/users/{id:guid}/roles` | `permission:auth.user.manage` | Assigns roles to a user. Supports authorized user-to-role administration. |

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
