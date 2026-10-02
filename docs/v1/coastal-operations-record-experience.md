# Coastal Operations titles, selection and local time

Date: 2026-10-01. Owner: Wanshaja Sooriyabandara (`WKS2004`).
This contract is recorded before implementation. Shared G00 and G07 remain
Pending; existing approval and private-service boundaries apply.
The [focused workspace follow-up](coastal-operations-focused-workspaces.md)
supersedes the separate search panel and inline form/detail presentation with
compact quiet live search and exclusive workspace views.

## Records and discovery

Assessments gain a trimmed title (1–160 characters); alerts retain their title.
Record/workflow IDs remain server-generated. Duplicate titles are allowed.
Migration backfills existing assessment titles from their objective. Legacy
requests without title retain this compatibility behavior.
Normal search matches titles and defaults to all coastal record types. “All”
includes the caller's drafts even with reviewer queue access; other people's
drafts stay private. Drafts/Published/History retain their scopes. UUID matching
uses a collapsed Advanced search with separate recordId/targetId filters.
Results show title, UUID and created date; ID possession grants no access.
Remove the bottom “Check a coastal record” section; retain status/history in
selected assessment detail. Stack hero above search at every width. Remove
Coastal Operations from the web header; retain account navigation/page tabs.

## Named associations

Forms use title plus named coastal record, related plan and linked assessment
dropdowns. Selecting an option passes its opaque ID internally; no UUID typing.
Never fabricate canonical targets or implement a second catalogue.
New titled drafts may omit a target while the catalogue is unavailable;
Guid.Empty is the non-null schema's explicit unlinked draft sentinel, never a
canonical ID. Submission/publication reject unlinked targets. Drafts may link
a target later; published content stays immutable. Linked assessment/alert
targets must match. Legacy valid target requests remain supported; legacy empty
target requests without the new title remain invalid.

`GET /api/operations/form-options` supplies database-backed time zones, scoped
named assessment choices and typed target/plan availability/choices. Its policy
derives from existing Coastal Operations grants. The owning experience and
planner services supply named choices through the private reference port after
their contracts/services exist. Until then selectors show unavailable, retain
existing associations for edits, and permit “No related plan”. No invented
plan data, client-to-private-service calls or client-to-database calls.

## Time zone catalogue

Create `coastal_operations.TimeZoneLocations`, seeded from IANA zone.tab and
iso3166.tab with identifier, country/location, coordinates, description,
catalogue source/version and active flag. Include UTC.
The checked-in migration seeds 419 entries from the official IANA **2026e**
release (2026-09-29), using `zone.tab` and `iso3166.tab`:
[release archive](https://data.iana.org/time-zones/releases/tzdata2026e.tar.gz).
The seed is embedded in the service; deployments do not download it at startup.
Selection conventions:
[IANA database](https://www.iana.org/time-zones) and
[selection metadata](https://www.iana.org/time-zones/theory).
Installed platform rules compute date-specific offsets. A fixed base offset
must not stand in for DST rules. Keep platform tzdata current at deployment;
refresh catalogue metadata through reviewed migrations when IANA changes it.

One location/time-zone dropdown applies to both times. New clients send local
ISO values plus timeZoneId. The server validates an active catalogue ID, rejects
nonexistent/ambiguous local times, resolves each date independently, validates
ordering and stores UTC plus the selected zone. Legacy RFC3339 instants remain
supported without a selected zone. Legacy edits prefill UTC-local values/UTC.
Both clients prefill new records from the server's local response fields so
browser and server time-zone database versions cannot change an edited period.
Never interpret an unqualified time using the server's local time zone.

## Evidence

Add regressions for titles/duplicates, draft-inclusive scoped search, ID
filtering, unlinked publication rejection, selector scopes/availability,
catalogue provenance, DST/invalid local times, and equivalent clients.
Existing tests change only after approval. Record results/provider limitations
after implementation and update the acting member's AI usage log.

## Branch implementation and verification — 2026-10-01

Implemented in the private Coastal Operations service and both clients:

- Assessment titles and title-based collection filters; reviewer All includes
  caller-owned drafts. Exact record/target IDs use separate Advanced filters.
- Server-generated record/workflow IDs; named target/plan/assessment selectors
  through public form-options, with private typed producer availability. No
  draft form asks for a UUID. Cards show title, ID and creation time.
- Migration `20261001123722_CoastalOperationsNamedDraftsAndTimeZones` creates
  the 419-location catalogue, selected-zone references/indexes and title
  backfill. One selector applies to both dates. Current offset labels are
  informational; the service calculates the offset for each entered date.
- Native Flutter date/time pickers and React datetime-local fields, scoped
  choices, option-load retry and blocked save while choices fail to load.
- Hero above search at all widths, no bottom record lookup, and no Coastal
  Operations item in the web header. Account navigation and page tabs remain.
- New publication snapshots retain title/zone/UTC context. Existing request
  digests without the new fields remain compatible with persisted replays.

Final checks: **299 Coastal Operations cases passed**, with three opt-in
PostgreSQL skips; **178 React cases**, **107 Flutter cases** and **35 UI/catalog
validator cases** passed. TypeScript, Vite build, changed-source ESLint and
Flutter analysis passed. The endpoint catalogue has 56 public endpoints and
26 frontend routes, with no implemented AI endpoint; UI integration validation
passed. EF reports no pending model changes. Generated migration SQL was
inspected for title backfill, all 419 seeds, zone indexes and restrictive FKs.
Controlled browser review checked desktop assessment hero/search order,
phone-width Alert page/form, named selectors and the single zone choice;
the 390-pixel viewport had no document horizontal overflow.

The user approved updates to the affected existing client tests. Their prior
UUID-entry/two-offset/lookup/search assumptions were replaced; unrelated
assertions were preserved. Added tests cover draft scope, title bounds and
duplicates, idempotency compatibility, linking/editing/publication/audit,
fractional offsets, a DST-crossing period, nonexistent/ambiguous/invalid local
times, selector availability and equivalent client outcomes.

No dedicated `BLUEVERSE_CO_POSTGRES_TEST_CONNECTION` was configured. These
checks do not prove live migration execution, PostgreSQL query translation,
transactions/concurrency, Docker time-zone availability or live producer/agent
handoffs. Apply the migration through normal service startup after review.
Catalogue/planner choices remain `NOT_CONNECTED` in production until their
owner services and contracts exist. Shared G00/G07 remain Pending, and the
production Agentic AI port remains disconnected. No commit or push was made.
