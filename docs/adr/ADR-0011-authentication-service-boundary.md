# ADR-0011: Internal Auth Service and Gateway Token Validation

**Status:** Accepted

## Context

BLUEVERSE clients need one public API boundary, while credentials, role and
permission persistence must remain private to the backend. Auth-issued tokens
also need a predictable invalidation mechanism when an account or permission
assignment changes.

## Decision

Keep Auth as an internal ASP.NET Core service behind the public API/YARP
gateway. Auth issues HS256 JWTs using the deployment-provided
`JWT_SIGNING_KEY`; the public API validates the same signing key, issuer,
audience and lifetime before serving protected API operations. Auth validates
the active user and a `token_version` claim against PostgreSQL on its own
protected endpoints. Sensitive user, role and permission changes increment the
stored token version.

System roles are marked in persistence and cannot be renamed, deleted or have
their permissions changed through ordinary administration endpoints. Granting
or assigning a system role requires the dedicated system-role management
permission.

## Consequences

- React and Flutter use only public `/api/...` routes.
- Auth and PostgreSQL remain on internal Docker networks.
- Revocation is immediate for Auth-protected requests; other API services must
  use the gateway's JWT validation and short token lifetime until a shared
  revocation/introspection mechanism is introduced.
- The signing key is an operational secret and must be configured consistently
  in API and Auth deployment environments.
