# ADR-0021: Signed API Actor Context for Coastal Operations

**Status:** Accepted for the Member 4 feature branch; pending shared G00
acceptance of the cross-service contract.

**Date:** 2026-09-27

## Context

The Coastal Operations service is private behind `services/api`. Revalidating
Auth JWTs independently in the component service would distribute the JWT
signing key to another service and duplicate API authentication behavior. The
service must authorize through Auth-issued permission claims without calling
Auth or trusting client-supplied actor headers.

## Decision

1. The API remains the JWT validation boundary. Its Coastal Operations YARP
   transform removes the incoming `Authorization` and `Cookie` headers and
   removes any client-supplied actor-context headers before forwarding.
2. For an authenticated request, the API signs a short-lived context containing
   the actor UUID, only `operations.*` permission claims, correlation ID,
   request method/path/query, Unix issue time and a unique nonce. The signature
   is HMAC-SHA256 over the canonical fields.
3. The API and Coastal Operations receive a separate Base64-encoded key with at
   least 32 random bytes through `COASTAL_OPERATIONS_CONTEXT_KEY`. Do not reuse
   `JWT_SIGNING_KEY`, commit the value or include it in logs.
4. Coastal Operations verifies the signature and request identity, rejects
   contexts older than 60 seconds or more than five seconds in the future, and
   accepts a nonce only once per service process. Controllers authorize using
   the signed permission claims. The service does not call Auth or accept a
   client bearer token as actor context.
5. Health and Swagger remain anonymous through the existing public API gateway
   paths. Business endpoints require the verified context and a named
   permission. The Coastal Operations container remains private and has no
   host-published port.

## Consequences

- Auth-issued role-to-permission behavior remains the authority; Coastal
  Operations checks permission claims and does not branch on role names.
- Compose adds one local secret shared only between the API and Coastal
  Operations containers. Local setup instructions explain how to generate it.
- The nonce cache is process-local for the current single-instance Compose
  service. A scaled deployment must use a shared replay store or ratify an
  equivalent G00-approved mechanism before enabling multiple service replicas.
- The exact signed envelope, key-delivery and scale-out behavior remain
  provisional until all component owners finalize G00.

## Related records

- [Member 4 G00 proposal](../v1/g00/member-4-coastal-operations.md)
- [Member 4 component contract](../v1/components/member-4-coastal-operations-advisories-alerts.md)
- [Member component service boundaries](ADR-0020-member-component-service-boundaries.md)
