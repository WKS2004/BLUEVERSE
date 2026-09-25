# UI integration contract and verification

BLUEVERSE treats a UI as a cross-layer change, not only a screen change. Every
new, generated or modified React or Flutter surface must be connected to the
same workflow contract, the correct frontend route and the public ASP.NET Core
API endpoint that owns its data or action.

The machine-readable source of truth is
[`../contracts/ui-integration.json`](../contracts/ui-integration.json). It is
validated by `scripts/validation/validate_ui_integrations.py` and by the
`UI Integration Contract` GitHub Actions workflow.

## Cross-platform product scope

React Web and Flutter Mobile are equally complete surfaces for tourists,
coastal operators, operations reviewers and platform administrators. Every
permitted workflow and business action is available in both clients. The
same rule applies to future stakeholder roles. Screen size, input method
and device capability may alter presentation, but neither platform owns or
prioritizes a role, workflow or outcome. Both preserve the same workflow
intent, role → permission behavior and public API contract. See the
[project experience standard](../project/ui-experience-principles.md) and
the applicable component documentation in the
[documentation index](../README.md).

The user-facing quality bar is defined in
[`../project/ui-experience-principles.md`](../project/ui-experience-principles.md).
Interfaces should feel approachable and realistic for the coastal domain,
not like generic technical or analytical dashboards.

React Web uses Tailwind CSS utility classes for page and component styling,
with shared design tokens and base rules in the web Tailwind entry stylesheet.
Use the [BLUEVERSE Design System](../../DESIGN.md) as the shared
reference for both clients' coastal palette, type hierarchy, imagery, spacing,
component character and interaction states. Keep shared site navigation and
footer behavior in reusable React components. Flutter maps the same system
into native widgets and `ThemeData`; visual layout may adapt to the device while
preserving the same workflow and business outcome.

For a quick inventory of every current frontend route, gateway mapping,
public API/Auth endpoint, test-only route and Agentic AI endpoint status, read
the repository-local [endpoint catalog](../api/endpoint-catalog.md). The UI
registry is intentionally only the workflow-facing subset. Any API or route
change must update the endpoint catalog in the same change; the catalog
validator checks its source declarations and this UI registry for drift.
Update the catalog JSON, then regenerate its Markdown view with
`python .agents/scripts/validate_endpoint_catalog.py --write-markdown`.

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

Both client surfaces and every permitted role/action are required for each
product workflow. Device-specific input is allowed, but the underlying
business capability must still work in the other client. Never infer that
administration, review, tourism or field operations belong to one platform.

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
workflow, the endpoint catalog, the registry or either validator changes. The
web and mobile CI
workflows also run the same validator for every affected client change. A
failure is a merge blocker: an undeclared route, undeclared `/api/...` call,
dynamic/unverifiable network target, unapproved absolute API host, direct
internal target, versioned API path or missing shared surface must be fixed in
the change that introduced it. A client may not hide its API host or path in
an unresolved variable and rely on CI to infer the intended service.

The current clients register the shared `foundation-home` entry and implement
the `auth-session-management`, `auth-registration`, `auth-profile-management`,
`coastal-overview-dashboard`, `auth-admin-entry`,
`auth-permission-administration`, `auth-role-administration`,
`auth-user-administration`, `not-found-recovery` and
`server-error-recovery` workflows in both React and Flutter. Profile
management includes profile editing, password changes, session review and
revocation, and account deletion. Administration routes expose permission-
checked user, role and permission actions. The dashboard uses current-account
information and clearly marks coastal service areas as future work because
those services are not available yet. Registration uses the same public
`POST /api/auth/register` endpoint in both clients; the clients preserve their
platform-specific cookie and secure-storage session handling. Each public Auth
endpoint used by an implemented workflow is listed in the same manifest.

Run locally from the repository root:

```text
<bundled-python> .agents/scripts/validate_endpoint_catalog.py
<bundled-python> scripts/validation/validate_ui_integrations.py
<bundled-python> -m unittest discover -s scripts/validation/tests -p "test_*.py"
```

The repository's bundled Python path is reported by the workspace dependency
loader. If a system Python is available, `python` can be used instead.
