# ADR-0014: Separate Active Sessions from Session Lifecycle Logs

**Status:** Accepted

## Context

The Auth service must support bounded multi-account sessions while retaining
security and support evidence for logout, expiry, capacity eviction, password
changes, role changes and refresh-token replay. Keeping every revoked or
expired row in the operational active-session table makes active-session
queries larger and makes the table's meaning ambiguous.

## Decision

Map `UserSession` to the `ActiveSessions` table. It contains only sessions that
can still authenticate, subject to their absolute `ExpiresAt` value. The
`(UserId, DeviceId)` unique constraint remains the active account/device slot
boundary.

Map `UserSessionLog` to the append-only `UserSessionLogs` table. When a session
leaves the active set, Auth copies its lifecycle fields into the log with an
`EndedAt` timestamp and a bounded `EndReason`, then removes the active row.
This happens for manual logout/revocation, expiry, capacity eviction,
account-wide security changes and refresh-token replay detection.

Refresh tokens retain their audit records. An active token references
`ActiveSessions`; when its session is archived, the token reference moves to
the matching `UserSessionLogs` row and the token is revoked. A database check
constraint requires each refresh token to reference exactly one of the two
session tables.

Migration `20260921133008_ActiveSessionsAndSessionLogs` preserves existing
data: valid rows move to `ActiveSessions`, stale rows move to
`UserSessionLogs`, and refresh-token links follow the corresponding row.

## Consequences

- Authentication, capacity and session-list queries operate on a small active
  table without `RevokedAt` filters.
- Session history remains available for audit and support without making stale
  records eligible for authentication.
- Refresh-token replay and revocation evidence is retained after the active
  session is removed.
- Logs are not an authentication source and are not returned by the active
  session-management endpoint.
- Log retention/partitioning can be introduced later without changing active
  authentication state.
