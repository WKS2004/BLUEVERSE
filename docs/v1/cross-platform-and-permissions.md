# Equal client coverage and permissions

The [navigation/activity update](coastal-operations-record-navigation-and-audit.md)
applies equally to React and Flutter: 500 ms search, pinned category controls,
all five page sizes, shared record cards, authorized full details/actions from
Logs and refresh-restored create/edit intent. Unsaved inputs reset on refresh.
Restoration rechecks the current grants and draft state; audit scope is unchanged.
Both timelines show recorded person/roles/time and exact before/after values,
without inventing missing historical details.

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
is denied. Adithya Gunawardana's date/time inputs for itinerary planning and Wanshaja Sooriyabandara's
optional assessment image evidence also have equivalent user outcomes in
both clients. Native controls and capture methods may differ. Sanuda Abeysinghe's
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
| Platform Administrator | Manage authorized users, roles, permissions and configuration; inspect authorized audit information. Auth startup explicitly assigns every currently registered permission to the Admin system role; business endpoints still enforce their named permission checks. |

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

Coastal Operations has separate Assessment and Alert routes and the same draft,
publication, search, pagination and activity actions in both clients. The
[2026-10-01 contract](coastal-operations-publication-and-ui.md) lists the exact
grants, legacy compatibility, owner/manager scopes and branch verification.
Publication retains SUBMITTED on the wire; Alert publication is an independent
human lifecycle action. This does not claim a connected Agentic AI runtime.
The [record experience follow-up](coastal-operations-record-experience.md)
adds title search, named target/plan/assessment selection and one DB time-zone
choice to both clients. Form-options access derives from existing component
grants; assessment choices still apply owner/reviewer scope. No client requires
UUID entry for draft creation or editing. Advanced search is the explicit ID
filter; all record types is the default normal search.

For every v1 workflow, verify both clients for each participating role,
including permitted and denied actions, loading and empty states, validation,
dependency failure and recovery. Preserve the same outcome and audit result
regardless of which client submits the action. Follow the user-facing
[experience principles](../project/ui-experience-principles.md).

## Logs and evidence permission parity — 2026-10-02

React and Flutter add `/operations/logs` under Coastal Operations. Both require
`operations.audit.read` plus assessment read/queue or alert read/management
compatibility. Lists and timelines enforce backend owner/reviewer/manager scopes,
including inactive retained records. No new Auth grant is added. Evidence
addition/removal reuses `operations.evidence.upload`, only on an owned DRAFT;
reading images retains its separate grant. Published authored fields and
attachments cannot be edited. The historical one-second search wording is
superseded by the [latest navigation/activity contract](coastal-operations-record-navigation-and-audit.md)
at 500 ms. Both clients offer 5/10/25/50/100 record-specific page sizes and red
destructive controls and confirmations.
See [latest contract](coastal-operations-logs-and-evidence.md).
