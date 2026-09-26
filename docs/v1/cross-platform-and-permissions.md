# Equal client coverage and permissions

## Binding v1 interpretation

React Web and Flutter Mobile are equal product surfaces. Every role can perform
every business action for which that role has permission in either client.
There is no management-only web application and no tourist-only mobile
application. This rule also applies to roles added in v2 or v3.

Both clients share one server-owned workflow, public API, business data and
role-to-permission model. Responsive layout, screen size, input method and
device capabilities may change presentation, but may not remove a permitted
business workflow. Flutter GPS is the required v1 device feature; React must
still support location-aware discovery through an appropriate location input.
Flutter offers a manual alternative where practical when location permission
is denied. Member 3's date/time inputs for itinerary planning and Member 4's
optional assessment image evidence also have equivalent user outcomes in
both clients. Native controls and capture methods may differ. Member 2's
condition period remains an ordinary query input and is not assigned as a
separate device feature. See the shared
[device-capability contract](device-capabilities.md) for permission, upload,
privacy and failure behavior.

The SE3090 assignment describes React as primarily administrative/staff and
Flutter as primarily user-facing/operational. BLUEVERSE's accepted
[ADR-0016](../adr/ADR-0016-equal-client-capability-for-all-roles.md) resolves
that guidance for this product: both clients implement every permitted role
and business action. Platform-specific input, navigation and layout provide
the meaningful difference without making a role or workflow unavailable on
either client. The Flutter-to-React assessed demonstration is one sequence
through that shared capability, not the only supported sequence.

## v1 seed roles

| Role | Required capability in React and Flutter |
|---|---|
| Tourist | Discover destinations and activities, inspect availability, marine and safety information, biodiversity context and alerts; request recommendations; manage personal favourites and itineraries. |
| Coastal Operator | Manage permitted offerings or schedules; inspect conditions; initiate and follow operational assessments; inspect outcomes and alerts. |
| Operations Reviewer | Inspect assessment evidence, agent summaries and deterministic validation; approve, reject or request revision where permitted; manage authorized advisories and alerts. |
| Platform Administrator | Manage authorized users, roles, permissions and configuration; inspect authorized audit information. Business permissions are still required. |

Roles are configurable permission bundles, not business-code conditionals.
New roles start with zero business permissions. The exact v1 permission codes
and role assignments belong to an implementation contract; do not invent them
in UI code or treat a hidden control as authorization.

## Required integration contract

Every new or modified product workflow has one stable ID, a React route, a
Flutter route and all public API endpoint references in
[ui-integration.json](../contracts/ui-integration.json). The
[endpoint catalog](../api/endpoint-catalog.md) inventories actual routes and
their owners. Add entries when implementation sources exist; example paths in
the requirements are not implemented endpoints.

Clients call only the public ASP.NET Core /api/... boundary. They never call
each other, PostgreSQL, internal Auth, Agentic AI, ML or private service hosts.
The server enforces the same authorization and business rules for both.

The [canonical assessment](workflows.md) demonstrates Flutter initiation and
React review. It is a required demonstration sequence, not a restriction:
authorized initiation, review and status inspection must also work through
either client.

## Acceptance evidence

For every v1 workflow, verify both clients for each participating role,
including permitted and denied actions, loading and empty states, validation,
dependency failure and recovery. Preserve the same outcome and audit result
regardless of which client submits the action. Follow the user-facing
[experience principles](../project/ui-experience-principles.md).
