# API Foundation

The routes in this page are the public gateway contract. The gateway is
implemented at `services/api` and forwards the internal Auth service at
`services/auth` without exposing its container directly.

For the fastest complete inventory of frontend routes, gateway mappings,
public API/Auth endpoints, test-only routes and Agentic AI endpoint status,
see the [readable endpoint catalog](endpoint-catalog.md). Its
[JSON source](endpoint-catalog.json) is checked against route declarations and
the UI contract. This page provides the longer API behavior and session
semantics reference; update the catalog with each route change.

## Gateway routes

| Route | Destination |
|---|---|
| `/api/*` | ASP.NET Core public API and gateway (`services/api`) |
| `/api/auth/*` | API gateway → internal Auth service (`services/auth`) |
| `/` | React frontend |

## Auth endpoint inventory

These are the public gateway paths backed by the internal Auth service:

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/api/auth/register` | Register a user and issue an access token for a device session |
| `POST` | `/api/auth/login` | Authenticate a user and issue an access token for a device session |
| `POST` | `/api/auth/refresh` | Rotate a refresh token and issue a new short-lived access token |
| `POST` | `/api/auth/logout` | Log out every signed-in account on the current device |
| `POST` | `/api/auth/logout/{id}` | Log out one account on the current device; `{id}` is the account user ID |
| `POST` | `/api/auth/logout-all-devices` | Log out the authenticated account from every device |
| `GET` | `/api/auth/sessions` | List the authenticated account’s active sessions without secrets |
| `DELETE` | `/api/auth/sessions/{sessionId}` | Revoke one active session for the authenticated account |
| `POST` | `/api/auth/change-password` | Change the authenticated user’s password and revoke existing tokens |
| `GET` | `/api/auth/me` | Read the authenticated user |
| `PUT` | `/api/auth/me` | Update the authenticated user’s profile |
| `DELETE` | `/api/auth/me` | Delete the authenticated user’s account when permitted |
| `GET` | `/api/auth/users` | List users with `auth.user.read` |
| `GET` | `/api/auth/users/{id}` | Read one user with `auth.user.read` |
| `POST` | `/api/auth/users` | Create a user with `auth.user.manage` |
| `PUT` | `/api/auth/users/{id}` | Update a user with `auth.user.manage` |
| `DELETE` | `/api/auth/users/{id}` | Delete a user with `auth.user.manage` |
| `POST` | `/api/auth/users/{id}/roles` | Replace a user’s roles with `auth.user.manage` |
| `GET` | `/api/auth/roles` | List roles with `auth.role.read` |
| `GET` | `/api/auth/roles/{id}` | Read one role with `auth.role.read` |
| `POST` | `/api/auth/roles` | Create a role with `auth.role.manage` |
| `PUT` | `/api/auth/roles/{id}` | Update a role with `auth.role.manage` |
| `DELETE` | `/api/auth/roles/{id}` | Delete a role with `auth.role.manage` |
| `POST` | `/api/auth/roles/{id}/permissions` | Replace role permissions with `auth.role.manage` |
| `GET` | `/api/auth/permissions` | List permissions with `auth.permission.read` |
| `GET` | `/api/auth/permissions/{id}` | Read one permission with `auth.permission.read` |
| `GET` | `/api/auth/health` | Auth service readiness/health |

Login and registration may omit `deviceId`. In that case Auth creates a
server-generated opaque installation ID and a high-entropy device key. Browser
clients should send `useCookies: true`; Auth stores the access token, refresh
token, device ID and device key in protected `HttpOnly` cookies and omits the
secret values from the JSON response. Native clients should send
`useCookies: false` and store `deviceId`, `deviceKey`, the access token and the
refresh token in platform secure storage. A server-issued installation ID must
be paired with its device key on later native requests. Older client-supplied
opaque IDs remain accepted as legacy installations during migration; they are
not hardware identifiers or MAC addresses.

Each installation can hold at most five active account sessions. Each account
can hold at most five simultaneous active sessions across installations. A
sixth account on one installation returns `409 Conflict`; a sixth session for
one account automatically revokes the oldest active session. Repeated login on
the same account/installation reuses its session row.

The default session and refresh-token lifetime is one day. `rememberMe: true`
selects an absolute 30-day lifetime. Refresh rotation never extends the
session beyond its original expiration. Access JWTs expire after 15 minutes.
Refresh tokens are stored only as hashes and are rotated on every successful
refresh; replay of a consumed token revokes its session.

Device-scoped logout archives the active sessions for the current device and
revokes their refresh tokens. The account-specific route archives only the
selected account on that device, while `logout-all-devices` archives every
session for the authenticated account and increments its token version.
Password, role, permission and account-state changes continue to use
account-wide token-version revocation and session archival.

Active authentication state is stored in `ActiveSessions`. When a session ends
through logout, expiry, capacity eviction, security invalidation or refresh
token replay, Auth moves its lifecycle record to `UserSessionLogs` with an end
timestamp and reason. The log is retained for audit/support queries but is
never an authentication source; the public session endpoint returns active
sessions only.

`PUT /api/auth/me` accepts profile fields only. Password changes use
`POST /api/auth/change-password`.

Password recovery and email verification are not exposed because this
repository has no corresponding delivery contract.

## API conventions

- RESTful HTTP methods
- DTO request/response models
- asynchronous operations
- server-side validation
- consistent error responses
- authorization policies/permissions
- Swagger/OpenAPI

## Client integration contract

React and Flutter endpoint usage is registered in
[`../contracts/ui-integration.json`](../contracts/ui-integration.json). A
client may use only a public `/api/...` path listed there and referenced by
its shared workflow. The registry is validated in CI; it does not authorize
direct calls to Auth, Agentic AI, PostgreSQL or other internal services.

## Health

The frontend health route is served by the frontend Nginx container and exposed
by the gateway at:

```text
GET /health
```

The intended public API health route is:

```text
GET /api/health
```

Auth health route through the public API boundary:

```text
GET /api/auth/health
```

## Error and validation contract

Auth validation and expected business failures return structured RFC 7807
responses with the relevant `4xx` status. Unexpected Auth failures are
sanitized to `500 Internal Server Error` with the media type
`application/problem+json`; exception text, signing material, password data and
database details are never returned to clients. The gateway applies the same
safe problem-details boundary to errors raised before a request reaches a
backend service.

The API and Auth contract suites are maintained with their owning services:

```powershell
dotnet test services/api/tests/Blueverse.Api.Tests/Blueverse.Api.Tests.csproj --configuration Release
dotnet test services/auth/tests/Blueverse.Auth.Tests/Blueverse.Auth.Tests.csproj --configuration Release
```

The current default evidence is 21 API cases and 67 Auth cases. The
PostgreSQL-backed Auth concurrency test is opt-in and must receive its
connection string through the environment; it is never committed to the
repository.

## Postman verification

Import the [v0 API verification collection](postman/blueverse-v0.postman_collection.json)
into Postman. It uses only the local public gateway at `http://localhost`.
Set `userEmail` and `userPassword` in Postman to an existing non-administrator
account, then run the collection in order. If the local stack has only the
bootstrap administrator, first create a separate normal account with
`POST /api/auth/register` and unique local credentials. The requests check frontend, API,
Auth and database health; both OpenAPI documents; unauthenticated and invalid
login responses; login, profile, active sessions, refresh-token rotation,
permission denial, logout and revoked-token rejection.

The collection contains no credentials. Keep the account password and tokens
in the local Postman session, and do not export or commit a collection after
populating its runtime variables. A complete collection run clears its saved
tokens; clear the Postman collection variables after an interrupted run.

Additional ASP.NET services expose health routes through the same gateway convention:

```text
GET /api/<service-name>/health
```

Swagger/OpenAPI is exposed through the API gateway at:

```text
GET /api/swagger
GET /api/swagger/v1/swagger.json
```

The Swagger UI is configured only in the API service and provides both the public API and Auth API documents in one interface. The Auth OpenAPI document is available through the API boundary at `/api/auth/swagger/v1/swagger.json`; Auth's container is internal-only.

Use the `Authorize` button in the unified UI to enter a native-client JWT as
`Bearer {token}`. Browser clients use the protected access-token cookie. The
bearer security definition is registered by the API service and is applied to
operations that require authentication or permission policies.
