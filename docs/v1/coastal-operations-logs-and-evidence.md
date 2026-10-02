# Coastal Operations logs and draft evidence controls

Date: 2026-10-02. Owner: Wanshaja Sooriyabandara (`WKS2004`).
User-directed requirements recorded before coding; provisional G00 and shared
G07 remain Pending. Its one-second search interval and generic cards-per-page
label have been superseded by the [record navigation and audit follow-up](coastal-operations-record-navigation-and-audit.md),
which sets 500 ms and Assessments/Alerts-specific labels on every collection.

## Previous-task recheck

The first unchanged focused runs passed 14/15 web and 10/13 mobile cases.
The web failure found a hidden collection search field during exclusive form
creation. Mobile tests still targeted text search controls, closed Advanced
filters, and an offscreen Save button in the former dialog layout. The user
approved affected existing-test updates on 2026-10-02. Preserve unrelated
assertions and add actual full-workspace/debounce/loading regressions.

## Logs workspace

Register `/operations/logs` in React and Flutter, nested under Coastal
Operations navigation. Require `operations.audit.read` plus the corresponding
record-read grant (assessment read/queue or alert read/management compatibility).
Use permitted Assessment/Alert switch buttons at the top, followed by the same
compact title search, optional filters/Advanced IDs and lifecycle tabs. Fetch
one second after the last character; retain cards and use a local bar spinner.
All defaults to every lifecycle the caller may audit, including retained
cancelled assessments and withdrawn/expired/resolved alerts. Normal pages keep
their established draft/published collections and scoped history filters.

The Logs public collections are `GET /api/operations/logs/assessments` and
`GET /api/operations/logs/alerts`. Each uses existing typed list DTOs and filter
validation. Assessment owners see their own records; queue readers additionally
see others' non-draft records. Alert managers see all retained alerts; ordinary
readers with audit access see only their own historical records. No privilege
may be obtained by submitting a filter or ID. Each card identifies title, UUID,
created/updated date and lifecycle and opens the existing scoped audit timeline.
Timelines include creation, edits, publication, cancellation/withdrawal,
decisions, evidence upload/removal and retention transitions where recorded.
They describe saved events, not invented historical content snapshots.

At the bottom, show cards per page with **5, 10, 25, 50, 100** choices, default
25, visible card count, page number and Previous/Next controls. Use server
cursor pagination and cache previous-page cursor positions; changing search,
section or page size resets to page one. Disable invalid paging while loading;
ignore stale responses. Do not fabricate a total when only a cursor is known.
Keep shared chrome and permission-aware navigation on both clients.

## Draft evidence and publication immutability

Only an owner with `operations.evidence.upload` can add/remove evidence in a
`DRAFT` assessment. Reuse this grant for managing draft attachments; no new
Auth behavior or grant is needed. Published assessments, including submitted
and revision-requested states, reject evidence addition/removal with 409.
Authored record fields already require a draft (assessment DRAFT/alert PROPOSED).
Authorized reviewer decisions, resolution/expiration and retention continue as
controlled lifecycle operations; they do not authorize editing published content.

Add `DELETE /api/operations/assessments/{assessmentId:guid}/evidence/{evidenceId:guid}`
with JSON `{ expectedVersion }`, owner/scope checks and optimistic concurrency.
Persist REMOVED evidence metadata, removal timestamp and a REMOVED audit event
atomically with an incremented assessment version. Retain metadata so earlier
upload/removal events remain discoverable. Removed content is immediately
unavailable (410) and excluded from current detail, attachment limits and
publication snapshots. Duplicate removal on the same owned draft is idempotent
and produces no extra version/audit event. A stale different change yields 409.

Delete private bytes after the database commit. Persist content-deletion time;
a bounded retention-worker pass retries pending removed content after storage
failure. Never delete bytes before committing the authorized state transition.
Use an additive EF migration for removal/deletion timestamps and REMOVED state.
See [ADR-0023](../adr/ADR-0023-coastal-draft-evidence-removal.md).

Both clients offer a clear removal confirmation and refresh the current draft
version, evidence and audit after success. Disable actions while busy, remove
any open image preview and preserve errors/retry. Cancellation/withdrawal,
evidence removal, their destructive confirmations and form Cancel controls use
red semantic styling with visible hover/focus; publication remains a primary
action. Back navigation remains neutral.

## Acceptance and evidence

Cover all pagination choices, valid/invalid search and IDs, complete retained
history scopes, grant/ownership denial, search timing/cancellation, stale
responses and retry, audit timelines, focused forms/details and published
immutability. Evidence tests assert metadata/version/audit, exclusion from
publication, removed-byte access, duplicate/stale/race and cleanup failure/retry.
Provider-independent doubles are not proof of PostgreSQL migration or concurrent
transaction behavior. Record actual checks and remaining integration limits.


### Final verification — 2026-10-02

- Coastal service suite: **322 passed, 5 skipped, 0 failed**. The skipped cases
  are opt-in PostgreSQL integration tests, including migration/log/removal and
  publication/removal concurrency cases. No dedicated PostgreSQL connection
  or local Docker runtime was available; no live migration was applied.
- Complete React suite: **185 passed, 0 failed**, run serially with
  `node --experimental-strip-types --test --test-concurrency=1`. An earlier
  parallel run hit Windows memory and Vite cache rename failures; the serial
  run completed without changing assertions or test discovery. TypeScript,
  changed-source ESLint and Vite production build passed.
- Complete Flutter suite: **113 passed, 0 failed**; formatting and full analysis
  passed with no issues using locked dependencies and the installed SDK.
- Endpoint catalogue and UI registry passed: **59 public endpoints, 28 frontend
  routes**, no implemented AI endpoint. Both validator suites passed **35**
  cases. Agent-resource validation passed for 23 repository skills; no agent
  guidance changed. `git diff --check` passed.
- EF reported no pending model changes; idempotent migration SQL was generated
  and inspected. Rollback preserves removed metadata as expired because removed
  private bytes cannot be restored. These checks do not prove live transactions.
- Synthetic browser review covered Logs, automatic search/reset, both categories,
  page-size selection, whole-workspace create/details/Back, retained chrome and
  red evidence confirmation. The phone review found and fixed Logs horizontal
  overflow; final 390px viewport content width was 375px. Temporary preview
  source, server and browser tab were removed. Preview data was synthetic.
- The approved previous-task recheck corrected hidden Advanced fields in focused
  web forms, mobile nested scrolling/test interactions, and creator-only saves
  that attempted a detail read without the read grant. Unrelated existing test
  assertions were preserved. Current cases include `WEB-OPS-WORKSPACE-001/002`,
  `WEB-OPS-LOGS-*`, `WEB-OPS-EVIDENCE-*`, `MOB-OPS-LOGS-*`,
  `MOB-OPS-EVIDENCE-001/002`, `COASTAL-LOGS-*`, `COASTAL-EVIDENCE-026`–`030`
  and `COASTAL-POSTGRES-004/005`.
- No deployment, real-device acceptance, live producer handoff or executable
  agent acceptance is claimed. Shared G00/G07 remain Pending. No commit or push
  was made.
