# G00 Decisions — Coastal Operations (Member 4)

The [2026-10-03 record navigation and audit follow-up](../coastal-operations-record-navigation-and-audit.md) is the latest behavior contract: it sets 500 ms search and record-specific pagination across all collections, refresh-safe views and detailed activity. Shared G00/G07 and live integration gates remain Pending.

The [record experience follow-up](../coastal-operations-record-experience.md)
adds titles, draft-inclusive discovery, named associations and server-resolved
time zones. It supersedes UUID entry and separate offset fields; shared G00
acceptance remains Pending.
The [focused workspace follow-up](../coastal-operations-focused-workspaces.md)
adds separate hero images, integrated compact live search with quiet collection
loading and full workspace creation/detail views within the existing routes.

| Field | Value |
|---|---|
| Status | Member 4 owner proposal for shared acceptance; global G00 remains **Pending** until all four owners agree. |
| Owner | Wanshaja Sooriyabandara (`@WKS2004`) |
| Feature branch | `features/coastal-operations` |
| Component contract | [Coastal Operations, Advisories & Alerts](../components/member-4-coastal-operations-advisories-alerts.md) |

This record defines the Member 4 proposal for G00. The team has not accepted
the shared G00 contract; global G00 remains **Pending** until all four owners
agree after the component branches are brought together. Branch-local work may
proceed against this provisional proposal, as directed by the user, and does
not imply shared acceptance.

The `features/coastal-operations` branch now contains a partial private
backend: assessment draft creation, update, cancellation and submission;
assessment reads and guarded decision handling; target status/history reads;
alert draft/lifecycle operations; persistence and migrations; API gateway
integration; health checks; and a Swagger document available at
`/api/swagger`. Draft creation stays local and makes no peer or AI calls.
Submission makes bounded read-only requests to the provisional Member 1–3
endpoints, records their outcomes, and reports dependency status without
gating startup or readiness. The implemented routes and schema are evidence
for branch progress, not a freeze of the shared request/response, identity,
target-handoff or database-provisioning contracts. Branch-local image
evidence is implemented against [ADR-0018](../../adr/ADR-0018-assessment-evidence-storage-boundary.md);
shared G00 acceptance is still required. Production proposal generation
remains gated by G07.

This branch implements full CRUD for caller-owned assessment drafts and
proposed alert drafts. Assessment creation is local; explicit submission
collects the bounded peer outcomes. Draft deletion is logical and audited so
the system retains the actor, time and prior version. Submitted assessments,
reviewer decisions, operational state transitions, evidence and active or
terminal alerts are not ordinary CRUD records and cannot be edited or
physically deleted through these draft operations. These behaviors are
implemented against Member 4's provisional G00 proposal; they do not imply
shared G00 acceptance.

## Decisions proposed by Member 4

### 2026-10-01 publication, permissions, search and UI

The [publication contract](../coastal-operations-publication-and-ui.md) records
the requested update before code. Publish assessment uses submit/SUBMITTED and
atomically records a complete Member 4 delivery envelope; before G07 production
stays NOT_CONNECTED. Alert publication is independent. Specific alert action
grants and audit.read supplement compatible manage/decide grants. Add scoped
search/filter/audit routes and separate equal Assessment/Alert client pages.
Future private AI endpoint/authentication and peer handoffs remain acceptance
work. See [ADR-0022](../../adr/ADR-0022-coastal-assessment-publication-dispatch.md).

### Ownership, identities and time

| Contract item | Member 4 proposal for G00 |
|---|---|
| Private service | `services/coastal-operations/`; .NET project `Blueverse.CoastalOperations`; Compose/DNS identity `coastal-operations`; internal port `8080`, with no host-published port. The service owns operational assessments, proposals, reviewer decisions, managed operational state, alerts/advisories and audit history. Discover service-local `services/coastal-operations/tests/Blueverse.CoastalOperations.Tests/` in CI. |
| Persistence | Own PostgreSQL schema `coastal_operations` with least-privilege role `coastal_operations_app`; do not read or write another component's schema. Shared database provisioning, migration ownership and credential delivery must use the team-wide G00 decision. Record immutable audit actor, action, object/proposal version, correlation ID and UTC time; never store hidden reasoning or credentials. |
| Identifiers | Propose UUIDs for Member 4-created `workflowId`, `assessmentId`, `proposalId`, `decisionId`, `evidenceId` and `alertId`. The Member 4 service creates `workflowId` for its business assessment. An optional `sourceWorkflowId` links an Adithya Gunawardana (Member 3) planning workflow using Member 3's agreed ID format. Business workflow IDs never stand for Agentic AI execution IDs. |
| Target identity | `targetType` is `DESTINATION`, `ACTIVITY`, `OFFERING` or `SESSION`; published `targetId` uses the canonical Ushan Srinuka (Member 1) identifier. New titled drafts may omit the target and store the explicit `Guid.Empty` unlinked sentinel; submission/publication rejects it. Users select real named references after producer integration. Member 4 does not create a second catalogue. |
| Time | New clients send local ISO date/time values and one active database `timeZoneId` for both ends of the half-open interval `[start, end)`. The service resolves each date using platform IANA rules and stores UTC plus the zone; ambiguous/nonexistent times are rejected. Legacy RFC3339 requests without a zone remain supported. Never infer a period from device/server time. Preserve observation/retrieval/validity times and revalidate freshness before execution. |

### Member-to-member handoff proposal

Member 4 consumes the canonical target and experience/availability evidence
from Member 1, time-aware condition and deterministic suitability evidence
from Member 2, and an optional business `sourceWorkflowId`, objective and
itinerary/recommendation context from Member 3. Member 4 does not copy or
become authoritative for those records. Keep source evidence as a typed
reference containing its source component, source record ID/version,
observation/retrieval times, validity boundary when supplied, and explicit
missing/unknown/stale meaning. Member 4 must preserve `UNSUITABLE` and
`UNKNOWN`; neither an operational proposal nor a later agent may weaken them.

The proposed Member 4 consumer response is `OperationalStatusResponse` with
`targetType`, `targetId`, `operationalState`, applicable restrictions,
`stateVersion`, and effective/update timestamps. It is read-only to Members 1
and 3. The shared G00 review must align these reference and freshness fields
with producer-owned schemas and agree the authenticated private handoff for
service consumers before cross-component integration. The branch's public
status route currently reads only Member 4's own managed operational state; it
does not yet consume Member 1's authoritative target or expose the private
member-to-member handoff.

The branch currently uses these provisional, read-only peer endpoints when an
assessment draft is explicitly submitted. They are assumptions for independent
component work, not accepted shared routes; align paths, authentication, DTOs,
identifiers and freshness rules with each owner before integration:

| Source | Provisional request | Expected bounded JSON fields |
|---|---|---|
| Member 1 Experience & Biodiversity | `GET /api/experiences/{targetType}/{targetId}/availability?periodStartsAt={RFC3339}&periodEndsAt={RFC3339}` | `targetType`, `targetId`, `availabilityStatus` (`AVAILABLE`, `UNAVAILABLE`, `UNKNOWN`), optional `targetVersion`, `evaluatedAt`, optional `validUntil` |
| Member 2 Marine Conditions & Safety | `POST /api/marine-safety/suitability-assessments` with `targetType`, `targetId`, `periodStartsAt`, `periodEndsAt` | `targetType`, `targetId`, `classification` (`SUITABLE`, `CAUTION`, `UNSUITABLE`, `UNKNOWN`), `profileVersion`, `assessedAt`, optional `validUntil`, `reasonCodes` |
| Member 3 Coastal Planning | `GET /api/coastal-planner/workflows/{workflowId}`, only when the assessment has a `sourceWorkflowId` | `workflowId`, `status`, `updatedAt` |

The default internal base addresses are `experience-biodiversity:8080`,
`marine-safety:8080` and `coastal-planner:8080`, configurable per peer. Each
request has a 2-second timeout, two retries (three attempts maximum) for
timeouts, network failures, HTTP 408/429 and 5xx, short bounded exponential
backoff, and a 32 KiB response limit. A missing or invalid route/schema,
rejected request, exhausted retries or unconfigured address is returned as an
explicit dependency result and recorded with the assessment; responses retain
only validated contract fields. Results use `RESPONDED`, `STALE`,
`UNAVAILABLE`, `ENDPOINT_NOT_FOUND`, `REJECTED`, `INVALID_RESPONSE`,
`MISCONFIGURED` or `NOT_REQUESTED`; health reports `NOT_CHECKED` until a
source has an outcome.
The three peer requests run concurrently.
Member 3 is `NOT_REQUESTED` when no source workflow is supplied. No peer is
probed during startup, and peer outcomes do not affect liveness or readiness;
readiness depends only on Coastal Operations' own PostgreSQL connection and
schema. The latest bounded outcomes are also included in the anonymous health
response, while assessment-level evidence is returned to authorized callers.

These provisional peer calls currently use the Docker internal network. A
shared service-to-service authentication contract has not been accepted; the
peer owners must agree it before integration. Until then, authorization
rejections remain explicit dependency outcomes and cannot be treated as
evidence.

Unavailable, missing, stale or contradictory producer evidence must remain an
explicit dependency/evidence result and cannot authorize a proposal or
protected transition. Existing authorized status/history reads remain usable
when the assessment evidence path is unavailable. Optional IT3091 prediction
context is never safety authority and its absence cannot be converted into a
positive safety finding.

### Public operation and permission proposal

These remain candidate public contracts for shared G00 review. A subset is
implemented on this branch and recorded in the endpoint catalog; their current
DTOs and behavior remain provisional pending cross-component agreement.
`services/api` remains the only client-facing boundary and forwards to the
private service. Evidence routes are implemented branch-locally, with the
storage and media limits recorded in ADR-0018.

Use the shared UI workflow ID `coastal-operations-assessment` for both
clients, with proposed React and Flutter route `/operations/assessments`.
That workflow owns assessment initiation, monitoring, queue/evidence review,
decisions, alerts and history for every role with the relevant permission.
Register its routes and only implemented public endpoints in the shared
registries when the component UI/API is built.

| Method and proposed `/api/...` path | Operation and success contract | Permission |
|---|---|---|
| `POST /api/operations/assessments` | `CreateAssessmentDraftRequest` → `AssessmentWorkflowResponse` (`201`); creates a caller-owned `DRAFT` with canonical target, explicit period, objective and optional `sourceWorkflowId`. Requires `Idempotency-Key`. Does not call peer components or dispatch AI until submission. | `operations.assessment.create` |
| `GET /api/operations/assessments` | `AssessmentQueueResponse` (`200`); includes caller-owned drafts and permitted submitted work for reviewers. Normal search matches titles; explicit recordId/targetId are Advanced filters. Other owners' drafts remain private. Cursor pagination and cancelled-history scope apply. | `operations.assessment.read` or `operations.assessment.queue.read` |
| `GET /api/operations/assessments/{assessmentId}` | `AssessmentDetailResponse` (`200`); workflow/dependency status, authorized proposal, decision, validation, progress, audit/history and result summary. The owner may inspect their cancelled draft. Future agent plan/step summaries appear only after G07 and successful dispatch. | `operations.assessment.read` or `operations.assessment.queue.read`, with resource scope |
| `GET /api/operations/form-options` | Database time-zone locations, caller-scoped named assessments and private reference-port target/plan choices with explicit availability. No-store. Producer services are currently absent; target/plan arrays remain empty rather than fabricated. | `operations.form.options.read` policy derived from existing Coastal Operations grants; no new seeded Auth permission |
| `PATCH /api/operations/assessments/{assessmentId}` | `UpdateAssessmentDraftRequest` → `AssessmentWorkflowResponse` (`200`); updates only the caller's `DRAFT`, requires the expected assessment version, and cannot change server-owned IDs, audit fields or workflow status. | `operations.assessment.update` |
| `DELETE /api/operations/assessments/{assessmentId}` | Logically cancels only the caller's `DRAFT`, returns its `CANCELLED` status (`200`), and writes an audit tombstone. Requires expected assessment version and `Idempotency-Key`; it never physically erases the assessment. Submitted or otherwise closed assessments return `409`. | `operations.assessment.delete` |
| `POST /api/operations/assessments/{assessmentId}/submit` | Validates and submits the current draft (`200`); requires expected assessment version and `Idempotency-Key`. On success, records the bounded Member 1–3 peer outcomes. Before G07 it remains `SUBMITTED` with `aiDependencyStatus: NOT_CONNECTED` and no proposal. | `operations.assessment.submit` |
| `POST /api/operations/assessments/{assessmentId}/decisions` | `ReviewerDecisionRequest` → `ReviewerDecisionResponse` (`200`); requires decision, exact `proposalId`/`proposalVersion`, expected target-state version and `Idempotency-Key`. | `operations.assessment.decide` |
| `GET /api/operations/targets/{targetType}/{targetId}/status` | `OperationalStatusResponse` (`200`); current authoritative state/version for authorized clients. Private Member 1 and Member 3 consumers use the separately agreed service handoff. | `operations.target.status.read` |
| `GET /api/operations/targets/{targetType}/{targetId}/history` | `OperationalHistoryResponse` (`200`); authorized, cursor-paginated state changes and decision/audit summaries without private audit payloads. | `operations.target.history.read` |
| `POST /api/operations/assessments/{assessmentId}/evidence` | Multipart image upload → `EvidenceUploadResponse` (`201` only after private persistence and inspection). Evidence is an immutable attachment; draft edits or deletion do not overwrite or physically delete it. | `operations.evidence.upload` |
| `GET /api/operations/assessments/{assessmentId}/evidence/{evidenceId}` | Authorized image content (`200`); never return a storage URL. | `operations.evidence.read` |
| `GET /api/operations/alerts` | `AlertQueueResponse` (`200`); alerts visible to the caller, filtered by target, status and validity. | `operations.alert.read` |
| `POST /api/operations/alerts` | `CreateAlertDraftRequest` → `AlertResponse` (`201`); authorized creation of an unpublished advisory/alert for a managed target and effective period. | `operations.alert.create` or legacy `operations.alert.manage` |
| `PATCH /api/operations/alerts/{alertId}` | `UpdateAlertDraftRequest` → `AlertResponse` (`200`); change only a `PROPOSED` draft using its expected version; active content requires a new audited proposal. | `operations.alert.update` or legacy `operations.alert.manage` |
| `DELETE /api/operations/alerts/{alertId}` | Logically withdraws only a `PROPOSED` draft, returns its `WITHDRAWN` lifecycle (`200`), and records an audit tombstone. Requires expected alert version and `Idempotency-Key`. Active or terminal alerts cannot be deleted; they must follow their lifecycle. | `operations.alert.delete` or legacy `operations.alert.manage` |
| `POST /api/operations/alerts/{alertId}/decisions` | `AlertDecisionRequest` → `AlertDecisionResponse` (`200`); authorized activation or resolution with expected alert version and `Idempotency-Key`. | Action-specific `operations.alert.publish` / `operations.alert.resolve`, or legacy `operations.alert.decide` |
| `GET /api/operations/assessments/{assessmentId}/audit` | Paginated safe activity metadata; assessment owner or authorized reviewer of a published record. | `operations.audit.read` plus resource scope |
| `GET /api/operations/alerts/{alertId}/audit` | Paginated safe activity metadata; alert creator or authorized alert manager. | `operations.audit.read` plus resource scope |

Assessment drafts provide the component's full CRUD lifecycle: create, read,
update and logically delete before submission. Submission closes that editing
window and starts the business workflow. Alert drafts likewise support create,
read, update and logical delete while `PROPOSED`; publishing, resolving,
expiring or superseding an alert uses its separately authorized lifecycle
decision. `DELETE` never means erasing an audit-bearing row. Proposals,
reviewer decisions, evidence, operational history and executed target state
remain immutable or append-only; corrections use a new version or workflow.

Assessment responses must expose the business `workflowId`, `assessmentId`,
resource scope, current version, UTC timestamps and applicable status/error
fields. Target and alert responses expose their own IDs, scope and current
version; an alert links to its assessment when one exists. Queue and history
responses use a bounded cursor. The shared G00 review must ratify exact DTO
fields, validation bounds and error bodies before integration; the current
branch DTOs are not frozen shared wire schemas.

Assign these permission codes through Auth's existing role-to-permission model;
do not branch on role names in service or client code. Coastal Operators may
create, read, update and logically delete their own assessment drafts, submit
them, and upload evidence within their permitted scope. Operations
Reviewers may read the permitted queue and assessment detail, decide proposals,
read target status/history and authorized evidence/alerts, and manage alert
drafts and lifecycle decisions. Tourist access to an alert requires its
explicit visibility scope and permission.
Platform Administrator has no implicit business permission. For local
provisioning, Auth explicitly assigns every permission currently registered
in its database, including the current `operations.*` set, to the `Admin`
system role through role-permission records. Coastal Operations still enforces
the named permission policies, and all other roles require explicit grants.
`operations.assessment.update`, `operations.assessment.delete` and
`operations.assessment.submit` are distinct grants; create/read permission
alone does not imply them. Alert draft deletion requires `operations.alert.delete`; legacy
`operations.alert.manage` remains an alternative. Alert create/update use their
specific grants or legacy manage. Publication and resolution require their
separate grants or legacy decide; manage alone cannot authorize either.
An assessment initiator cannot decide their own high-impact proposal; a
different authorized reviewer is required. React and Flutter expose the same
authorized actions and outcomes.

### State, decision and failure proposal

- Business `workflowStatus` values: `DRAFT`, `SUBMITTED`, `PROPOSAL_READY`,
  `PENDING_APPROVAL`, `REVISION_REQUESTED`, `REJECTED`, `APPROVED`,
  `EXECUTED`, `BLOCKED`, `SAFE_FAILURE`, `CANCELLED`. A new assessment starts
  as `DRAFT`; successful submission changes it to `SUBMITTED`. A draft delete
  changes it to terminal `CANCELLED` and preserves its audit tombstone. An
  assessment submitted before an AI runtime is connected remains `SUBMITTED`
  with no proposal. A timeout or
  invalid dispatch records its explicit AI outcome and `SAFE_FAILURE` for that
  AI-dependent stage; it cannot advance to fabricated approval or execution.
- Proposed workflow transitions: `DRAFT` to `SUBMITTED` or `CANCELLED`;
  `SUBMITTED` to `PROPOSAL_READY`,
  `PENDING_APPROVAL`, `BLOCKED` or `SAFE_FAILURE`; `PENDING_APPROVAL` to
  `APPROVED`, `REJECTED`, `REVISION_REQUESTED`, `BLOCKED` or `SAFE_FAILURE`;
  `REVISION_REQUESTED` to a new proposal version and then one of
  `PROPOSAL_READY`, `PENDING_APPROVAL`, `BLOCKED` or `SAFE_FAILURE`; and
  `APPROVED` to `EXECUTED`, `BLOCKED` or `SAFE_FAILURE`. A validated
  `PROPOSAL_READY` recommendation remains a read-only result when no action is
  proposed; any proposed mutation first moves to `PENDING_APPROVAL`.
  `CANCELLED`, `REJECTED`, `EXECUTED`, `BLOCKED`, `SAFE_FAILURE` and a read-only
  `PROPOSAL_READY` close that assessment. A revision retains the earlier
  decision and creates a new proposal version.
- `operationalState` values: `OPEN`, `CAUTION`, `TEMPORARILY_SUSPENDED`,
  `CANCELLED`, `COMPLETED`. For applicable target types, allow `OPEN` →
  `CAUTION`/`TEMPORARILY_SUSPENDED`, `CAUTION` →
  `OPEN`/`TEMPORARILY_SUSPENDED`, and `TEMPORARILY_SUSPENDED` →
  `OPEN`/`CAUTION`. A session may transition from any active state to
  `CANCELLED` or `COMPLETED`; both are terminal. Reject any state not supported
  by the target type. Keep this state separate from assessment status,
  publication/schedule state and alert lifecycle.
- Decision values are `APPROVE`, `REJECT`, `REQUEST_REVISION`. Suspending an
  activity/offering, cancelling a session, any other restrictive state change,
  and publishing a `HIGH` or `CRITICAL` alert require a separate authorized
  human approval. Rejection and revision never execute a change. Every
  revision creates a new proposal version and preserves earlier decisions.
- A proposal is valid for at most 30 minutes and expires sooner at the end of
  its assessed period or any relied-upon evidence validity boundary. Approval
  carries `proposalVersion` and the expected target-state version. The service
  rechecks actor permission, target version, evidence freshness and legal
  transition immediately before a transaction applies the target change and
  audit record together. Use optimistic concurrency. Require an
  `Idempotency-Key` for assessment draft creation, draft cancellation,
  submission, reviewer decisions and alert draft withdrawal: a matching replay
  returns its original outcome even if state has since advanced; key reuse
  with a different payload or a first-time request with a stale version
  returns `409` without side effects. Draft updates require the expected
  assessment version.
- Alert severity values are `LOW`, `MODERATE`, `HIGH`, `CRITICAL`; lifecycle
  values are `PROPOSED`, `ACTIVE`, `RESOLVED`, `EXPIRED`, `SUPERSEDED`,
  `WITHDRAWN`. Permit `PROPOSED` → `WITHDRAWN` only through the authorized
  draft-delete operation, retaining an audit tombstone. A `WITHDRAWN` alert is
  terminal and cannot be published; create a new draft to replace it.
  Permit `PROPOSED` → `ACTIVE` only through an authorized decision and
  deterministic validation. `HIGH`/`CRITICAL` publication requires an
  authorized human reviewer distinct from anyone who initiated its draft or
  associated assessment. Permit
  `PROPOSED` → `SUPERSEDED` on replacement and `ACTIVE` → `RESOLVED`,
  `EXPIRED` or `SUPERSEDED`; the last three are terminal. Publication is
  distinct from a proposal, and no external delivery channel is included in
  v1.
- Return API errors as the existing RFC 7807-style problem response: `400`
  malformed input, `401` unauthenticated, `403` missing permission, `404`
  absent/out-of-scope resource, `409` stale version, draft update/delete after
  submission, deletion of a non-`PROPOSED` alert, or conflicting decision, `413`
  oversized image, `415` unsupported image type, and `422` domain/evidence
  validation failure. An unavailable private image-inspection/storage
  dependency returns `503` and creates no reviewer-visible attachment. Keep
  private exception detail out of responses.

### Member 4 persistence invariants

Propose a primary key for each Member 4-owned record, a unique `workflowId`
per assessment, unique `(assessmentId, proposalVersion)` and at most one
effective reviewer decision per proposal version. Store the operational state
and concurrency version by canonical `(targetType, targetId)`. Scope each
idempotency key to actor and operation, persist its request digest and original
outcome, and reject a different-payload replay. Keep the accepted decision,
protected state transition and audit entry atomic. The branch now has EF Core
entities and migrations for assessments, proposals, reviewer decisions,
target operational states, operational history, alerts, alert decisions,
idempotency records and audit entries. These use the provisional
`coastal_operations` schema. Dedicated role provisioning, retention, migration
ownership and shared PostgreSQL credential delivery still require G00
agreement before integration.
Assessment draft edits use optimistic version checks. Draft cancellation and
alert withdrawal set lifecycle/status and deletion actor/time fields and append
an audit entry in one transaction; they do not cascade-delete evidence or
history. The retained tombstone is excluded from ordinary lists but can be
read through the authorized audit view.

### Private service and optional AI boundary

The branch implements API YARP forwarding of `/api/operations/{**catch-all}`
to `coastal-operations:8080` without a path rewrite. The service implements
business routes under that same path. For authenticated calls, API removes
incoming bearer, cookie and caller-supplied context headers, then signs the
actor UUID, effective `operations.*` permissions, correlation ID, method,
path/query, issue time and one-use nonce with the separate
`COASTAL_OPERATIONS_CONTEXT_KEY`. The private service verifies this envelope
and applies permission policies; it does not receive the user's JWT or call
Auth. The service port is not published to the host. This branch-local
mechanism is documented in [ADR-0021](../../adr/ADR-0021-coastal-operations-actor-context.md)
and remains provisional until shared G00 accepts the cross-service contract.

The future Agentic AI adapter proposes private `GET /internal/agentic/health`
and `POST /internal/agentic/safety-operations/dispatch` operations. These are
server-to-server contracts only; they are not public client routes and are not
implemented before G07. Register a private route in the endpoint catalog only
when its implementation source exists.

The branch exposes public gateway paths `GET /api/operations/health/live`
for process liveness and `GET /api/operations/health/ready` for required
database/schema readiness, as well as `/api/operations/health`. These routes
are anonymous and do not depend on the optional Agentic service; its
availability is reported only in the authorized business workflow. An absent
AI runtime does not block service startup or healthy non-AI operations.

The implemented `PublishedAssessmentDispatch` snapshot contains stable
dispatch/workflow/assessment IDs, published version, canonical target,
title, objective, UTC period and selected time zone, optional source workflow, actor/correlation,
publication time, validated peer outcomes and safe evidence metadata. Its
maximum serialized size is 128 KiB of UTF-8 JSON. It carries no raw image or
storage URL. The future HTTP transport and proposal-result contracts still
require shared acceptance after G07. Transport acceptance is distinct from a
validated proposal; Member 4 alone can execute an approved business action.

Keep `aiDependencyStatus` (`NOT_CONNECTED`, `UNAVAILABLE`, `AVAILABLE`)
separate from business `workflowStatus`; an `AVAILABLE` probe does not prove a
later dispatch succeeded. Keep `aiDispatchOutcome` separately as
`NOT_REQUESTED`, `NOT_STARTED`, `SUCCEEDED`, `UNAVAILABLE` or `INVALID_RESULT`,
with an explicit retryable flag. The service-owned delivery seam bounds both
availability and dispatch calls to five seconds, uses thirty-second leases
and retry delays, and permits at most three delivery attempts. A timeout is
unavailable and retryable within that limit; an invalid response is not
retryable. An absent runtime is not connected and receives no delivery until
configured. Exhaustion or invalid delivery moves a submitted assessment to
SAFE_FAILURE; acceptance leaves it SUBMITTED with no manufactured proposal,
decision or target mutation. Record availability in the authorized assessment status; do not add
a public AI health route. Keep service liveness, database readiness and
optional AI availability separate.

For image evidence, the branch response envelope is `evidenceId`,
`assessmentId`, assessment version, detected media type, byte length, content
digest and inspection state (`AVAILABLE`, `EXPIRED`). Invalid uploads produce
no attachment record. Only sanitized bytes become reviewer visible; storage
stays private, evidence is versioned/audited, and raw images are never sent to
the future agent. A storage failure returns an unavailable response without
claiming an attachment was accepted. The PNG subset, five-file/5 MiB limits,
private Compose volume and 365-day retention are branch-local choices in
[ADR-0018](../../adr/ADR-0018-assessment-evidence-storage-boundary.md); shared
G00 must ratify them before cross-component adoption.

## G00 acceptance still required

This Member 4 proposal is not a complete shared freeze. Before integrating the
branches into `dev` and marking shared G00 accepted, the four owners must
ratify:

- canonical Member 1 target ID/type and Member 3 workflow ID formats, source
  evidence versions/freshness meaning, and the private producer/consumer
  handoffs with Members 1–3;
- exact public and internal request/response schemas, validation bounds,
  resource scope and permission grants, status/error mappings, and the common
  authenticated actor/correlation envelope for API-to-service calls,
  including assessment draft CRUD permissions, alert draft deletion, submit
  transition, expected-version behavior and audit retention;
- shared PostgreSQL provisioning, schema credentials, migration ownership and
  time conventions, plus this service's identity, private network, environment
  configuration, startup/readiness and CI discovery;
- distinct React/Flutter workflow and route registrations, navigation and
  endpoint-catalog ownership across the four branches; and
- the common pre-G07 AI dispatch/availability schema and minimum evidence
  reference/failure contract.

The later Member 4 upload gate under ADR-0018 still settles concrete image
formats/count/size, inspection, storage configuration and retention before
uploads are implemented. After shared agreement, record accepted G00 values
in the owning contracts and only then mark G00 accepted in the
[branch tracker](../member-branch-workflow.md#component-branch-status).


The [2026-10-02 record navigation/activity follow-up](../coastal-operations-record-navigation-and-audit.md) specifies 500ms search, collection pagination, pinned tabs, refresh-safe view intent and signed actor/field-change audit snapshots. Shared G00/G07 acceptance remains Pending.
