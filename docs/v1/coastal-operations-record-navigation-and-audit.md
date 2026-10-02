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
