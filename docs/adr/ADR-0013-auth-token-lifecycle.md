# ADR-0013: Auth Token and Session Lifecycle

**Status:** Accepted

## Context

The Auth service needs a predictable session lifetime, short-lived access
credentials, refresh without re-entering a password, and safe recovery when a
browser or app loses its local installation state. A single long-lived JWT
cannot provide rotation, replay detection or useful session management.

## Decision

- Issue a 15-minute JWT access token containing the account and session claims.
- Issue a cryptographically random opaque refresh token at login and persist
  only its SHA-256 hash.
- Rotate the refresh token on every successful refresh. A consumed-token replay
  revokes the associated session and its refresh-token records.
- Give a session an absolute one-day lifetime by default or an absolute 30-day
  lifetime when `rememberMe` is true. Refreshing updates `LastSeenAt` but never
  extends `ExpiresAt`.
- For web transport, set `HttpOnly`, `SameSite=Lax` cookies for the access
  token, refresh token, device ID and device key, and omit raw secret values
  from the JSON response. For native transport, return the values so the
  client can store them in platform secure storage.
- Validate session state in Auth on every protected Auth request. The public
  gateway validates JWT signature, issuer, audience and lifetime and forwards
  the protected cookie when the request is routed to Auth.
- Keep currently usable sessions in `ActiveSessions`. When logout, expiry,
  capacity eviction, security invalidation or refresh-token replay ends a
  session, archive its lifecycle record in `UserSessionLogs`; archived rows
  are never eligible for authentication.

## Consequences

- Access-token theft has a short validity window; refresh-token theft is
  bounded by rotation and replay detection.
- Clearing browser storage can create a new installation; it is not a server-
  side logout for the lost installation. The old session expires or is
  archived by cleanup, remains visible only while active through
  `GET /api/auth/sessions`, and is bounded by the five-session account limit.
- Downstream services that need immediate revocation must adopt Auth
  introspection or shared session-revocation validation; JWT signature
  validation alone cannot observe database revocation.
- Password changes, account changes, logout scopes and capacity eviction
  revoke the affected refresh-token rows together with the session.
