# Auth, identity and access control

## Responsibility and source

`services/auth` is the internal ASP.NET Core authentication service.
Its controllers implement registration, sign-in, refresh, profile and
password changes, account and session logout, session listing/revocation,
and permission-gated user, role and permission administration. The full
method/path inventory is in the [API reference](../../api/README.md) and
[endpoint catalog](../../api/endpoint-catalog.md); clients reach it only
through the public API's `/api/auth/...` routes.

`Services/AuthService.cs` owns the identity and session operations.
`Authorization/` defines named permission policies and handlers.
`Data/AuthDataSeeder.cs` initializes the bootstrap administrator from
environment configuration. Passwords are verified through PBKDF2 hashing;
raw passwords and signing keys are never stored in source or logs.

## Permission contract

Roles group permissions. Business operations check named permissions rather
than comparing hard-coded role names. The implemented Auth codes include
`auth.user.read`, `auth.user.create`, `auth.user.update`, `auth.user.delete`,
`auth.user.manage`, `auth.role.read`, `auth.role.create`, `auth.role.update`,
`auth.role.delete`, `auth.role.manage`, `auth.permission.read` and
`auth.role.system.manage`. Read access is combined with the relevant create,
update or delete grant for each management operation. Permission assignment
also requires permission-catalogue read access. The authorization handler
checks the active account and its current database role assignments on every
request; token permission claims are informational snapshots. New roles begin
without business permissions. System-role assignment or modification requires
its dedicated permission. The server remains authoritative regardless of what
either client displays.

## Installation and session contract

Auth issues an opaque installation ID and separate device proof key when
needed. Browser clients use protected HttpOnly cookies; native clients use
secure storage and bearer tokens. One installation supports at most five
active account sessions. One account supports at most five active sessions
across installations; a sixth account session evicts its oldest active
session. Repeated sign-in to the same account on one installation reuses its
session row.

Access JWTs last 15 minutes by default. Sessions expire after one day by
default or 30 days with Remember Me. Refresh tokens are hashed, one-time
and rotated without extending the absolute session expiry. Replay revokes
the session. `POST /api/auth/logout` ends the authenticated account's session
on the current device. The account-specific current-device routes cannot target
another account. `POST /api/auth/logout-all-devices` verifies the current
password before ending every session for that account. Ending a remote session
through the session-management endpoint also requires current-password
verification; the current session can be ended directly. Security changes
revoke or archive the relevant active sessions. `ActiveSessions` authorizes current sessions;
`UserSessionLogs` records ended sessions and never authenticates a user.
See [session ADRs](../../adr/README.md) and the
[database component](postgresql-ef-core.md) for lifecycle and constraints.

## Extension and verification

Add any permission to the shared Auth model and enforce it on the owning
server operation. Preserve both client surfaces for every authorized
workflow, and update the route catalog and UI registry when needed.
`services/auth/tests` owns endpoint, authorization, session, error and
persistence cases. Provider-specific constraints and concurrency need the
explicit real-PostgreSQL test path described in the
[Auth test README](../../../services/auth/tests/README.md).
