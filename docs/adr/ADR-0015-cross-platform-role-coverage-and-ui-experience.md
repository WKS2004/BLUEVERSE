# ADR-0015: Cross-platform Role Coverage and User-Centered UI Experience

**Status:** Superseded by [ADR-0016](ADR-0016-equal-client-capability-for-all-roles.md).

This record preserves the earlier decision and its rationale. ADR-0016
defines the current v1 rule: equal capability coverage in both clients
without a stakeholder or workflow emphasis.

## Context

The initial product framing assigned administration and staff work primarily to
React Web and client-facing or field work primarily to Flutter Mobile. That
split would make the platform choice depend on a stakeholder role, limit
mobility for staff and administrators, and force product planning to decide
which frontend owns a capability before deciding how the workflow should help
the user.

BLUEVERSE already has one public API, one role → permission model and a shared
React/Flutter workflow registry. The project needs a scope that preserves that
contract while making the product more flexible and approachable.

## Decision

React Web and Flutter Mobile are co-equal product surfaces for clients, staff
and administrators. New business workflows are cross-platform by default:
they use one server-owned workflow and permission contract, have a usable route
in both clients, and are registered with both routes and their public API
references in `docs/contracts/ui-integration.json`.

The platforms may adapt the same capability to their interaction strengths.
React can emphasize wide browser workspaces, keyboard operation and review;
Flutter can emphasize quick mobile actions, field use, location, camera,
offline-aware drafts and notifications. These are experience optimizations,
not role ownership rules. A genuinely platform-specific workflow requires an
explicit product decision and a follow-up ADR.

All UI work must be user-friendly, scope-aligned and realistic for the coastal
tourism and marine-resilience domain. Technical or analytical detail is
progressively disclosed only when it supports the user's actual task. Internal
service names, raw payloads and model internals are not the default product
experience.

## Consequences

- Clients, staff and administrators can choose the surface that fits their
  context without losing access to a workflow.
- React and Flutter feature planning, API contracts, permission behavior and
  tests remain aligned through one shared workflow ID.
- Teams must design and test two adapted experiences for each shared workflow,
  increasing delivery effort but reducing role-based platform lock-in.
- The UI registry, client READMEs, test expectations, roadmap and product
  guidance must consistently describe both clients as first-class surfaces.
- Server-side authorization remains the source of truth; client visibility is
  a usability aid, not a security boundary.

## Rejected alternatives

- Keeping administration/staff permanently web-only and client experiences
  permanently mobile-only would reduce flexibility and make role ownership
  drive product design.
- Duplicating business rules separately in each client would create behavioral
  drift and weaken the public API boundary.
- Making every surface a dense technical dashboard would obscure the real
  coastal tasks and create a less credible user experience.
