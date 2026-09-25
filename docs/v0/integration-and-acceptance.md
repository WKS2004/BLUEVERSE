# v0 integration and acceptance

This is the cross-component acceptance path for the
[v0 foundation](README.md). The [endpoint catalog](../api/endpoint-catalog.md)
is the current route inventory; the
[UI registry](../contracts/ui-integration.json) binds the React and Flutter
routes to their shared workflow.

## Sign-in and session flow

1. A returning user signs in at `/signin` in either client; a new user
   registers at `/signup`. Sign-in may include the optional 30-day Remember Me
   choice.
2. The client calls `POST /api/auth/login` or `POST /api/auth/register`
   through the public API. Auth validates or creates the account and creates
   or reuses its installation session in PostgreSQL.
3. Browser Auth stores access, refresh and installation credentials in
   protected cookies. Native Flutter stores the returned credentials in
   platform secure storage. Both transports use the same server-owned
   identity and permission model.
4. The client reads `GET /api/auth/me` and
   `GET /api/auth/sessions`. A 401 can trigger the authorized refresh path
   through `POST /api/auth/refresh`.
5. The user can end the authenticated account's current-device session through
   `POST /api/auth/logout`. The account-menu action
   `POST /api/auth/logout-account` ends a selected saved account while leaving
   other accounts on that device signed in. `POST /api/auth/logout-all-devices`
   verifies the current password before ending every session for the
   authenticated account across devices. The server
   revokes credentials, archives ended sessions and updates observable state.

The Auth service also exposes account, role, permission, profile and finer
session-management operations through the same public boundary. Their route,
permission and response contracts live in the
[API reference](../api/README.md). A client interface for an authorized
operation must be available in both React and Flutter when that product
workflow is introduced; backend routes alone do not prove a client screen.

## Invariants for extension

- React and Flutter call only registered public `/api/...` endpoints and
  expose the same permitted business outcomes to each role.
- ASP.NET Core validates authentication and permissions. Client visibility
  and navigation never replace server authorization.
- Auth owns account and session persistence. The public API validates JWTs
  and forwards Auth requests; clients do not call Auth or PostgreSQL directly.
- Session refresh rotates tokens within the original absolute expiry.
  Revoked or ended sessions cannot authenticate at Auth.
- Route changes synchronize the endpoint catalog and, for UI workflows,
  the shared UI registry in the same implementation change.
- Tests assert response content, permission semantics, persisted state and
  side effects in addition to status.

## Verification map

| Boundary | Evidence |
|---|---|
| Client contract | Run the [UI integration validator](../development/ui-integration.md) and client tests for both surfaces. |
| API/Auth route catalog | Run the [endpoint-catalog validator](../api/endpoint-catalog.md) and owning service tests. |
| Persistence | Use Auth's deterministic tests and the explicit real-PostgreSQL path for migrations, constraints and concurrency. |
| Local stack | Check gateway, API and Auth health plus both OpenAPI documents through the public entry point. |
| Agent guidance | Run the [resource validator](../development/agent-resources.md) when instruction resources change. |

The [test matrix](../testing/test-matrix.md) records current executed-suite
evidence. This document states the contract and verification path; it is not a
substitute for a fresh run in the target environment.
