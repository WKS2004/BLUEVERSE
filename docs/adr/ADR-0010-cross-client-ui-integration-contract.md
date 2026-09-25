# ADR-0010 — Cross-client UI/API integration contract

## Status

Accepted for the v0 foundation; enforced as product workflows are added.
The workflow-exception language below is superseded for v1 and later
stakeholders by [ADR-0016](ADR-0016-equal-client-capability-for-all-roles.md):
every authorized role, business action and workflow must be available in
both React and Flutter.

## Context

React and Flutter represent different interaction contexts, but both are
first-class product surfaces for clients, staff and administrators. They must
use the same server-owned authorization and API contract. A screen can compile
while still navigating to an undeclared route, calling the wrong service,
bypassing the public gateway or using an API shape that the other client does
not share.
Both clients and the public API/Auth foundation are checked in. The contract
must represent their working routes without claiming that future domain
endpoints exist.

## Decision

Maintain `docs/contracts/ui-integration.json` as the canonical registry of
shared workflow IDs, React routes, Flutter routes and public API references.
Run `scripts/validation/validate_ui_integrations.py` in a dedicated CI gate and
from both client CI workflows. The validator must reject undeclared frontend
routes, undeclared public API literals, direct internal-service targets and
`/api/v1`-style paths.

The registry contains only implemented or explicitly marked starter surfaces.
An API call may be added only when its public `/api/...` contract and owning
backend service are registered in the same change. Relative API paths are
preferred; absolute URLs require an allowlisted host in the registry, and
dynamic network targets fail closed because CI cannot prove which service they
resolve to. The clients do not call one another; a shared workflow ID is the
connection between their corresponding surfaces. As amended by ADR-0016,
every authorized product workflow and business action has both client
surfaces.

## Consequences

- Cross-platform integration is reviewable before a full backend stack exists.
- Web and mobile changes cannot silently drift to different routes or service
  endpoints.
- Web and mobile may adapt layout and interaction to their strengths without
  assigning stakeholder groups to a single platform.
- Layout and device-integrated interactions may differ, while both client
  surfaces preserve the same authorized business capability.
- The current foundation registry contains the implemented Auth
  session-management endpoints but no domain API endpoints until the
  corresponding ASP.NET Core source and OpenAPI contract are checked in.
- The static validator complements, but does not replace, API HTTP integration,
  gateway and end-to-end tests once executable services are available.

## Rejected alternatives

- Allowing each client to maintain an independent route/API list would permit
  contract drift.
- Calling internal services directly from a client would bypass the public API,
  permission model and audit boundary.
- Treating lint/build success as integration evidence would miss wrong routes,
  wrong services and missing failure-state behavior.
