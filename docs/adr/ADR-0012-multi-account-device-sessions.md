# ADR-0012: Multi-Account Device Sessions

**Status:** Accepted

## Context

BLUEVERSE clients need account switching similar to the multi-account flows
used by major identity providers. A user can sign in to more than one account
in the same browser or app installation, but the device must have a bounded
number of active accounts. Logout also needs separate scopes: all accounts on
the current device, one selected account on the current device, and the
authenticated account on every device.

The existing Auth service used only per-user JWT token versions. That model can
revoke every token for one user but cannot distinguish accounts on one device
or revoke one device session without affecting another device.

## Decision

Persist one `UserSession` row for each `(UserId, DeviceId)` pair. Auth creates a
server-issued opaque `DeviceInstallation` with a separate high-entropy device
key when a client has no installation credentials. Browser clients retain both
values in protected cookies; native clients retain them in platform secure
storage. Older client-supplied IDs remain explicitly marked as legacy during
migration, but they are never treated as hardware identity. Auth allows at
most five active account sessions for one installation. Repeated login of an
already-active account on that installation reuses its session slot.

Auth also persists one-time hashed refresh-token records per session. Access
JWTs last 15 minutes. Sessions and refresh tokens last one day by default or
30 days when `rememberMe` is requested; rotation never extends the absolute
session expiry. Reuse of a consumed refresh token revokes the session.

Each JWT contains `session_id` and `session_version` claims. Auth validates the
active user, account `token_version`, active session and session version against
PostgreSQL. Auth stores currently usable rows in `ActiveSessions` and moves
ended sessions to `UserSessionLogs`; an archived session can never satisfy JWT
validation. See ADR-0014 for the active/archive persistence decision.

The public Auth routes are:

- `POST /api/auth/logout`: archive every active account session on the current
  device and revoke its refresh tokens;
- `POST /api/auth/logout/{id}`: archive the selected account’s session on the
  current device, where `id` is the account user ID;
- `POST /api/auth/logout-all-devices`: archive every session for the
  authenticated account, revoke its refresh tokens and increment its token
  version.

Password changes, role/permission changes, deactivation and other account-wide
security changes continue to increment the user token version and revoke the
user’s sessions. `PUT /api/auth/me` is profile-only; password changes use
`POST /api/auth/change-password`.

The device ID is an account-session grouping key, not hardware attestation. A
client that loses its installation cookies/storage receives a new server-issued
installation. Session expiration and the five-session account cap prevent
unbounded orphaned active sessions; the session-management endpoint exposes
the remaining active records so the user can revoke one explicitly.

## Consequences

- Account switching has explicit, bounded server-side state.
- Client-generated IDs are no longer authoritative for new installations, and
  device proof is separated from the public grouping ID.
- Cookie and native transports share the same refresh/rotation contract while
  keeping raw refresh tokens out of browser JSON responses and database rows.
- Device-scoped logout does not revoke the same account’s session on another
  device.
- Account-wide security changes remain simple and fail closed through the
  existing token-version check while their ended sessions are archived.
- A unique `(UserId, DeviceId)` index prevents duplicate active account rows;
  serializable relational transactions plus an in-process mutation gate
  protect both capacity checks and refresh rotation.
- The public API gateway still validates JWT signature, issuer, audience and
  lifetime without querying Auth session state. Immediate session revocation is
  therefore guaranteed for Auth-protected requests; downstream services need a
  shared introspection/revocation mechanism if they require the same property.
