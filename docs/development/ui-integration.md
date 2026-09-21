# UI integration contract and verification

BLUEVERSE treats a UI as a cross-layer change, not only a screen change. Every
new, generated or modified React or Flutter surface must be connected to the
same workflow contract, the correct frontend route and the public ASP.NET Core
API endpoint that owns its data or action.

The machine-readable source of truth is
[`../contracts/ui-integration.json`](../contracts/ui-integration.json). It is
validated by `scripts/validation/validate_ui_integrations.py` and by the
`UI Integration Contract` GitHub Actions workflow.

## Boundary

```text
React route ───────┐
                   ├──> public gateway /api/... ───> ASP.NET Core API ───> owning service
Flutter route ─────┘
```

The two clients do not call one another. A shared workflow ID connects their
corresponding surfaces, while the public API endpoint references connect both
surfaces to the same server-owned contract. Direct calls to Auth, Agentic AI,
PostgreSQL or an internal service are invalid even when the call appears to
work locally.

## Required registry entry

When a product workflow is introduced, update the registry in the same change
with:

1. one stable workflow ID;
2. the React route and owning source file;
3. the Flutter route and owning source file;
4. every public API endpoint reference used by the workflow; and
5. the owning backend service and public `operationId`/contract reference for
   each endpoint.

Both client surfaces are required for a shared workflow. If a capability is
intentionally platform-specific, record it as a separate workflow with an
explicit product decision and do not pretend that the other client supports
it.

Endpoint paths use the gateway's `/api/...` namespace. Prefer relative
`/api/...` URLs. An absolute URL is valid only when its host is explicitly
listed in `publicApi.allowedAbsoluteHosts`; the foundation allowlist contains
loopback hosts for local smoke tests, not Docker service names. Do not add
`/api/v1` or call a service's Docker hostname. The public API remains
responsible for authentication, permission checks, validation, business rules
and forwarding to internal services.

## Implementation sequence

1. Identify the workflow and its server-owned permission/contract.
2. Confirm the public API route exists in the API source or OpenAPI document;
   create the backend endpoint and its tests first when it does not.
3. Add the workflow, routes and endpoint references to the registry.
4. Implement both clients through their API boundary/client adapter. Keep
   route names and API references tied to the registry instead of copying
   service URLs into components or widgets.
5. Add route, request-boundary and workflow tests for both clients. Cover
   loading, success, empty, malformed, validation, denied, timeout/retry and
   dependency-failure states when they apply.
6. Run the contract validator, client lint/build/analyze and the relevant
   client tests. For an implemented backend, run the API contract and gateway
   smoke tests against the same route set.

## CI gates

`ui-integration.yml` runs when either client, the public API, a service,
API/architecture contract documentation, Compose/Docker gateway routing, any
workflow, the registry or this validator changes. The web and mobile CI
workflows also run the same validator for every affected client change. A
failure is a merge blocker: an undeclared route, undeclared `/api/...` call,
dynamic/unverifiable network target, unapproved absolute API host, direct
internal target, versioned API path or missing shared surface must be fixed in
the change that introduced it. A client may not hide its API host or path in
an unresolved variable and rely on CI to infer the intended service.

The current v0 clients retain the `foundation-home` entry and also implement
the shared `auth-session-management` workflow. Its public Auth endpoint
references are registered in the same manifest; future screens must follow the
same pattern rather than adding unregistered calls.

Run locally from the repository root:

```text
<bundled-python> scripts/validation/validate_ui_integrations.py
<bundled-python> -m unittest discover -s scripts/validation/tests -p "test_*.py"
```

The repository's bundled Python path is reported by the workspace dependency
loader. If a system Python is available, `python` can be used instead.
