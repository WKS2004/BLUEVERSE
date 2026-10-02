# Coastal Operations publication, permissions and client experience

The [record experience follow-up](coastal-operations-record-experience.md)
supersedes side-by-side hero/search, objective-first search and UUID-entry forms
with stacked layouts, titles, named selectors and one database time-zone choice.

Date: 2026-10-01. Owner: Wanshaja Sooriyabandara (`WKS2004`). This is the
user-directed change contract documented before code. Shared G00 acceptance
remains Pending; executable Agentic AI waits for G07.

## Lifecycles

Assessment creation saves an owner-scoped DRAFT. Draft CRUD makes no AI calls.
**Publish assessment** uses the existing `POST /api/operations/assessments/{assessmentId}/submit`
and `operations.assessment.submit` grant, closes draft editing and retains the
SUBMITTED wire status. It collects bounded peer outcomes and atomically stores
the typed delivery envelope for the future Member 4 runtime. Publication does
not mean public visibility, approval or completed analysis.

Alerts start PROPOSED (displayed as Draft) and have separate draft CRUD.
An authorized PUBLISH decision activates the notice for its audience/period.
Active/terminal content is immutable through draft APIs; resolve/expire it or
create a new proposal. HIGH/CRITICAL publication requires a different reviewer
from the drafter and linked assessment initiator. Alert publication never
starts an agent workflow. Assessments may propose alerts; humans may draft them
independently. Cancelled/withdrawn rows retain their audit history.

## Permissions

Use Auth role-permission grants, never role-name checks. Preserve Auth flows
and explicit Admin grants; assign other roles deliberately in administration.

| Capability | Grant |
|---|---|
| Assessment draft CRUD | `operations.assessment.create`, `.read`, `.update`, `.delete` respectively |
| Review queue/cancelled history | `operations.assessment.queue.read` |
| Own draft publication | `operations.assessment.submit` |
| Proposal human decisions | `operations.assessment.decide` |
| Target status/history | `operations.target.status.read`, `operations.target.history.read` |
| Private evidence upload/read | `operations.evidence.upload`, `operations.evidence.read` |
| Active public alert reading | `operations.alert.read` |
| Alert draft create/update/delete | `operations.alert.create`, `operations.alert.update`, `operations.alert.delete` |
| Alert publish/resolve | `operations.alert.publish`, `operations.alert.resolve` |
| Scoped activity timeline | `operations.audit.read` |

Existing alert.manage remains compatible with draft CRUD/manager reading;
alert.decide remains compatible with publish/resolve/manager reading. A specific
alert mutation grant permits manager reading for that action, never another
mutation. The decisions route checks the actual PUBLISH/RESOLVE grant. Queue
access cannot edit or inspect another person's unpublished draft. The Drafts
tab uses `onlyMine` so queue readers can also manage their own drafts.

## Search and audit

Assessment collection adds search (trimmed, max 160 chars), targetType,
targetId, recordId, onlyMine, publishedOnly; retains workflowStatus/includeCancelled/cursor/pageSize
(1–100). Normal search matches titles case-insensitively; exact recordId and
targetId are separate Advanced filters. Reviewer All includes caller-owned
drafts, while other owners' drafts remain private. Alert collection adds search,
recordId, targetType, severity, visibility, history to lifecycle/targetId/cursor/pageSize;
normal search matches titles. Scope/filters apply before
pagination. Public readers only receive currently valid ACTIVE PUBLIC alerts,
including when requesting internal/draft filters. Invalid filters return
structured errors. Filter changes reset pagination; both clients use Load more.

New `GET /api/operations/assessments/{assessmentId}/audit` and
`GET /api/operations/alerts/{alertId}/audit` require operations.audit.read with
owner/manager scope. They return paginated action, actor UUID, correlation and
UTC time, ordered by persisted time then ID with an opaque composite cursor. Log creation/update/logical deletion, submission, attempted delivery
and outcomes, evidence changes, decisions, target changes and alert publication,
resolution/expiry. Replay does not duplicate events. Searches do not create
mutation events. Exclude credentials/raw photos/storage URLs/hidden reasoning.

## Typed delivery seam

[ADR-0022](../adr/ADR-0022-coastal-assessment-publication-dispatch.md) defines
one immutable service-owned envelope per published assessment. Payload (maximum 128 KiB of UTF-8 JSON):
dispatch/workflow/assessment IDs, published version, canonical target, UTC
period, title, objective, selected time zone, optional planner workflow, actor/correlation, validated peer
outcomes and safe evidence metadata. Exclude raw media/storage URLs. This is
business delivery state, not agent execution state.

Bound delivery attempts, use optimistic concurrency/finite leases and stable
dispatch ID for receiver deduplication. Acceptance means transport acceptance,
never completed analysis/approval/execution. Before G07 production DI remains
disconnected and invokes no agent/model/tool endpoint. The exact private
route/authentication await accepted post-G07 transport contracts; do not invent
live AI catalog entries. After connection Member 4's entry point coordinates
the four agents; application rules/humans retain approval/execution authority.

## Equal client experience

Coastal Operations navigation contains separate Assessments and Alerts pages:
`/operations/assessments`, `/operations/alerts`. Both have sibling route tabs
and lifecycle tabs: Assessments All/Drafts/Published/History; Alerts
All/Drafts/Active/History. Tabs/buttons obey grants; each page loads only its
own collection. Arrange the coastal photo/task description above the
accessible search/filter panel at every width, then record cards with title,
UUID and created date, audience/time/status labels,
permitted actions, pagination and scoped activity. Include calm empty/error/
retry states and confirmations explaining closed drafts/retained history.
Publication messaging distinguishes durable submission from AI availability.
Use existing project imagery, meaningful alt text, shared web header/footer/
Tailwind and native Flutter widgets. Equal capabilities and API semantics apply.
Retain the combined component as an internal compatibility surface while app
routes use dedicated views. Remove the header Coastal Operations menu and the
bottom UUID lookup; use account navigation, page tabs and selected record detail.

## Acceptance

Add requirement-based tests for permissions/scopes, search/filter/cursors,
draft closure/version/idempotency, full context, safe disconnected/timeout
delivery, audit and both client routes/tabs/actions. Preserve existing tests.
Run owning checks and endpoint/UI validators. Real PostgreSQL is needed for
migration/translation/concurrency evidence; report unavailable access separately.

## Branch implementation and verification — 2026-10-01

Implemented on `features/coastal-operations` after the documentation update:

- Auth registers six additive grants (five alert actions and audit reading),
  explicitly assigned to the Admin system role through the existing seeder.
  Coastal policies and clients preserve legacy manage/decide compatibility.
- `AssessmentApplicationService` persists the complete publication envelope
  atomically with submission/audit/idempotency. `AssessmentDispatchDelivery`
  uses five-second calls, thirty-second leases/retry delay, at most three
  attempts and stable dispatch IDs. The production port remains disconnected.
- Migration `20261001102110_CoastalOperationsPublicationDispatch` creates the
  JSONB envelope table with a unique assessment key, restrictive assessment
  foreign key, status/attempt/version/lease constraints and concurrency token.
  Existing assessments receive no envelope/backfill or automatic redispatch.
- Both collection services filter within authorized scopes before pagination.
  Activity endpoints require audit.read and owner/manager scope. Activity pages
  order by persisted UTC time and ID; cursors retain both ordering values.
- Both app routers select dedicated pages with native/React search controls,
  lifecycle and sibling tabs, coastal photo heroes, granular draft/publication
  actions, activity, retry, empty states and cursor pagination. PROPOSED is shown
  as Draft; SUBMITTED as Published for assessment. The default combined screen
  remains available for compatibility with existing callers/tests.

Evidence: Coastal Operations 280 passing default cases; Auth 79; API 22;
React 171; Flutter 104; UI/catalog validator tests 35. React TypeScript,
production build and changed-source ESLint pass. Flutter analysis reports no
issues. Endpoint/UI validation passes (55 public endpoints, 26 frontend routes;
no implemented AI endpoint). EF reports no pending model changes; migration SQL
was generated and inspected. Browser review used synthetic records and checked
the dedicated web layouts, including a phone-width Alert page without overflow.

Three opt-in PostgreSQL cases remain skipped because
`BLUEVERSE_CO_POSTGRES_TEST_CONNECTION` is not configured. This does not prove
live PostgreSQL transactions, constraints, query translation, multi-process
lease concurrency, Docker startup or live peer/runtime handoffs. Shared G00,
G07 and final peer/runtime acceptance remain pending. The user approved updating
the existing Auth seed count from 26 to 32 with explicit new-grant assertions,
and adding dispatch-fixture cleanup to the existing PostgreSQL submission test;
its original assertions were preserved.
