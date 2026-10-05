# Coastal Operations record navigation and detailed activity

Date: 2026-10-02. Owner: Wanshaja Sooriyabandara (`WKS2004`).
Requirements recorded before source changes. Supersedes the one-second search
and cards-per-page wording in the prior Logs contract. Shared G00/G07 Pending.

## Workspace contract

All three collections use 500 ms since the last character, local loading and
retained results. Assessments/Alerts and Logs offer 5/10/25/50/100 records per
page (default25), labeled Assessments per page or Alerts per page. Show current
page/count and server cursor Previous/Next; reset pagination after filter/size
changes. Assessment/Alert route tabs and Logs category tabs remain pinned below
the shared header while their collection scrolls. Full-workspace forms retain
shared chrome and Back. Keep assessment empty copy exactly: “No assessments to
show yet” / “Saved drafts and submitted reviews will appear here.” Use the
corresponding advisory empty copy when the alert section is selected.

Use a shared white rounded record card on all collections: target type, title,
status, UUID, period, created/updated dates; alert description/severity/visibility
remain. Open the existing full record details from Logs and return to Logs.
View activity is an independent action, without nested clickable controls.
No extra read/action grant is obtained by originating from Logs.

Persist the view through registered route query parameters (`view=create`,
`view=edit&id=…`, `view=detail&id=…`; Logs also `kind=assessments|alerts`). Refresh
restores that view after authentication, fetches current server data for edits,
and rechecks permission, ownership and draft state. Missing/stale/revoked or
published edit targets show a recovery state. Do not auto-submit or store form
contents/evidence/credentials in the URL or browser storage. Refresh preserves
the workspace, not unsaved field values. Use the same authorized outcomes in
React and Flutter; native/mobile route settings retain the same view intent.

## Detailed activity

Add nullable actor name, role snapshots, record title, readable action summary
and an allowlisted before/after field-change array to OperationsAudit. Capture
these in the same SaveChanges transaction as the existing audit/state changes.
Record titles, objective/description, associations, dates/time zone, status,
severity/visibility, version and evidence metadata as applicable. Exclude image
bytes, credentials, provider payloads and hidden AI reasoning. Decisions include
outcome/explanation; lifecycle state transitions remain controlled operations.
Render clear action text, person/roles/time and labeled before→after changes;
technical UUID/correlation details are available in an expandable reference.

The essential API integration is a bounded, signed identity snapshot header
sourced only from verified JWT name/role claims. Strip client-supplied identity
headers and include the snapshot in the existing HMAC canonical envelope. The
private service validates it before creating claims. Missing identity keeps
legacy envelope compatibility and marks name/roles unavailable; no Auth flow
or hard-coded role authorization changes. Background events identify System.
Older rows retain honest unavailable before/after/identity details, never a
reconstruction from current record state. Additive nullable/jsonb migration;
keep original event IDs and scopes. No clients call Auth/private services.

## Acceptance

User approved affected existing-test updates on 2026-10-02. Cover 499/500 ms
boundary, paging choices/reset/stale/retry, sticky tabs, shared cards, Logs detail
Back, refresh create/edit restoration and invalid/revoked/non-draft targets.
Backend cases cover audited before/after values, verified person/roles,
system/legacy fallbacks, tampered identity, atomic save failure and no sensitive
fields. Real PostgreSQL migration/transactions need an opt-in connection;
provider-independent test doubles are not provider acceptance evidence.

## Implementation and verification — 2026-10-02

Implemented in the private service and both clients. [ADR-0024](../adr/ADR-0024-coastal-detailed-audit-snapshots.md)
records the identity/audit decision. Migration `20261002081218_CoastalDetailedAudit`
uses safe empty-array defaults for old rows; model drift check and generated
idempotent SQL inspection pass. Rebuild/restart API and Coastal Operations
together with the existing Compose configuration and actor-context key.

- Complete web suite: 193 passed, 0 failed; serial execution on Windows.
- Complete Flutter suite: 121 passed, 0 failed; analysis has no issues.
- Coastal Operations suite: 331 passed, 6 PostgreSQL opt-in skipped, 0 failed.
- API suite with signed identity regression: 23 passed, 0 failed.
- TypeScript, changed-source ESLint and production Vite build passed.
- Endpoint/UI registries and agent-resource validation passed; all 35
  validator regression tests passed. Catalogue: 59 public endpoints,
  28 frontend routes, no implemented Agentic AI endpoint. Diff check passed.
- Synthetic browser verification showed create/edit intent surviving refresh,
  current saved edit values, full details and rich person/role/field activity.
  Logs category tabs remained sticky at 80 px beneath the shared header.
  A 390 px viewport had 375 px content/client width without horizontal overflow.
  Temporary preview sources, server/tab and viewport override were cleaned up.

No dedicated PostgreSQL connection or Docker runtime was available. The six
provider tests and live migration/concurrency execution remain unverified.
No deployment, physical device, live producer or executable agent acceptance
is claimed. Legacy snapshots cannot be backfilled from current data; refresh
restores a workspace and saved record, not unsaved typing. G00/G07 Pending.

## UX follow-up contract — 2026-10-04

Keep the Assessment/Alert and Assessment Logs/Alert Logs tabs flush with the
shared 76 px header and opaque while sticky. At narrow widths, put this tab row
first in the main content directly after the header, before the account rail;
on desktop, align it over the Coastal Operations content column. The account
rail stays sticky below the header, sized to its content and capped at the
available viewport height so a short page section cannot push it out of view.
Use a short directional transition on the changing workspace panel only; the
shared header and footer remain still. Honor reduced-motion preferences.
Retain existing result cards while searches and detail lookups are in flight;
show request progress in the local controls instead of replacing the page with
the global loading screen. This applies to assessment/alert details, related
status/history, form-option reads and evidence previews.

Assessment and advisory details are readable, introductory summaries with
non-editable field values. The explicit Edit draft action opens the full form.
Draft forms do not render the activity timeline. Logs retain the existing
detail view and activity, but a record opened from Logs has no Edit, Withdraw,
Publish, Resolve, evidence-add or evidence-remove controls. Correlation
references are disclosed only in Logs, in a compact reference detail. Hide the
separate actor/“Who” line in Logs while retaining names and roles in the normal
Assessment/Alert activity views. Preserve the selected Logs category through
`kind=assessments|alerts` in the route query and browser refresh. React and
Flutter keep equivalent access and read-only outcomes.

## UX follow-up implementation and verification — 2026-10-04

Implemented header-aligned sticky tabs that precede the account rail on narrow
screens and align over the content column on desktop, directional section
transitions, quiet/local collection reads, read-only detail introductions and
no-mutation Logs detail behavior. Coastal Operations, Profile and Dashboard use
the shared account-navigation component directly in the main grid. The Coastal
pages use the same sticky position, viewport height, max-height and internal
overflow behavior as Profile and Dashboard. The Coastal tabs and workspace keep
their dedicated grid positions beside it. Logs category changes now update the route query;
Flutter uses the same query when replacing the active Logs screen. Correlation
references use a styled, Logs-only disclosure, with the separate actor/“Who”
line suppressed in Logs. Added `WEB-OPS-UX-001`–`004` and
`MOB-OPS-UX-001`–`002` regression coverage. The former Logs edit test expected
mutation controls in a read-only view and was updated, with approved test
changes, to check that edit/publish/cancel are unavailable and Back to Logs
works. The mobile Logs test fixture now resolves the category query route like
the app, so its persistence assertion exercises the route replacement flow.
The Logs category is now derived directly from the route query instead of being
mirrored into React state by an effect, avoiding a redundant render during tab
switches. Restored the shared loading helper import used by Coastal Operations
write requests after the full suite exposed its omission. Full package results
are recorded in the test matrix below.

### Coastal Operations Logs hero — 2026-10-04

Added a Logs-specific editorial hero above the Logs search and results. It uses
the same rounded coastal treatment, deep-ocean gradient and overlaid heading as
the Assessment/Alert workspaces, while introducing audit history as its own
context. Assessment Logs and Alert Logs share the same hero; switching category
only transitions the records panel.

The project asset is
[`operations-logs-hero.webp`](../../apps/web/src/assets/coastal/operations-logs-hero.webp).
It was generated with the built-in image tool from this prompt: “A small coastal
stewardship team reviewing field notes and printed shoreline records together
at an airy coastal field station, shoreline and dune grasses visible, candid
documentary editorial photography, muted ocean/sea-glass and warm paper palette,
wide panoramic framing with open space on the left for copy; no interface,
logos, legible writing or watermark.” The generated PNG was converted to WebP
for the app asset. `WEB-OPS-LOGS-004` checks the contextual image, accessible
description and placement above search/results.
