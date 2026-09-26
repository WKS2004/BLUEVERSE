# Public API and gateway

## Responsibility and source

`services/api` is the ASP.NET Core 10 public API and the only application
boundary called by React and Flutter. `Program.cs` configures controllers,
JWT bearer and access-cookie validation, CORS, forwarded headers, OpenAPI,
Swagger UI, RFC 7807-style unexpected-error handling and YARP reverse
proxying. `appsettings.json` maps `/api/auth/{**catch-all}` to internal
Auth. `Controllers/HealthController.cs` owns `GET /api/health`.

The API validates signing key, issuer, audience, expiry and HS256 algorithm.
Auth remains responsible for checking active account/session state on its
protected routes. Do not infer that public API signature validation alone
provides immediate Auth session revocation for every future domain endpoint;
the [Auth boundary ADR](../../adr/ADR-0011-authentication-service-boundary.md)
records this distinction.

## Public contract

All client-facing operations use `/api/...` without a path-version segment.
The API forwards Auth under `/api/auth/...` while keeping its container
private. The public Swagger UI at `/api/swagger` includes API and Auth
documents. The [endpoint catalog](../../api/endpoint-catalog.md) is the exact
current inventory; the [API reference](../../api/README.md) explains
transport and session semantics.

New domain operations are implemented in the owning private member service:
that service owns domain DTO handling, validation, business/application
logic, persistence and service-local tests. `services/api` keeps only the
necessary public route, existing authentication/permission integration,
service routing/forwarding and gateway error/OpenAPI integration. A new
internal service is registered behind that boundary; a client must never call
its container hostname.

## Liveness and future dependency availability

The current `GET /api/health` is API process liveness. Auth's
`GET /api/auth/health` reports Auth and database readiness. Neither signal
establishes availability of an optional internal dependency. Keep these
signals separate when v1 adds the member-owned Agentic AI backend
integration seam. That seam must probe the configured private AI dependency
server-side with a bounded check and report a safe not-connected/unavailable
state through the accepted workflow/readiness contract. It must not change
API liveness, be reported as a database failure, block service startup when
the optional runtime has not yet been deployed, or expose the private probe
to React or Flutter. The exact readiness surface and response schema remain
part of the [v1 integration contract](../../v1/agentic-ai-integration-boundary.md)
and must be finalized before an endpoint is implemented.

## Verification

Use `services/api/tests` for gateway, CORS, JWT, health, Swagger,
exception and reverse-proxy tests. Verify source/catalog parity and the
UI registry whenever a route or client target changes. In a live stack,
probe public health and both OpenAPI documents through edge-nginx. The
[service test README](../../../services/api/tests/README.md) gives the
package command and test structure.
