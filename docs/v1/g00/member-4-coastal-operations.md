# G00 Decisions — Coastal Operations (Member 4)

| Field | Value |
|---|---|
| Status | Member 4 owner proposal for shared acceptance; global G00 remains **Pending** until all four owners agree. |
| Owner | Wanshaja Sooriyabandara (`@WKS2004`) |
| Feature branch | `features/coastal-operations` |
| Component contract | [Coastal Operations, Advisories & Alerts](../components/member-4-coastal-operations-advisories-alerts.md) |

This record defines the Member 4 contribution to G00. Candidate values below are concrete review decisions for team acceptance; they do not claim that the full business service, candidate business endpoints or domain schema have been implemented.

The feature branch now includes the initial private service foundation, Compose
and API-gateway wiring, PostgreSQL connectivity/readiness, and a JWT-enabled
OpenAPI document available in the shared Swagger UI. This slice does not
implement the proposed assessment, decision, alert or evidence operations and
does not change the global G00 status.

## Decisions proposed by Member 4

### Ownership, identities and time

| Contract item | Member 4 proposal for G00 |
|---|---|
| Private service | `services/coastal-operations/`; .NET project `Blueverse.CoastalOperations`; Compose/DNS identity `coastal-operations`; internal port `8080`, with no host-published port. The service owns operational assessments, proposals, reviewer decisions, managed operational state, alerts/advisories and audit history. Discover service-local `services/coastal-operations/tests/Blueverse.CoastalOperations.Tests/` in CI. |
| Persistence | Own PostgreSQL schema `coastal_operations` with least-privilege role `coastal_operations_app`; do not read or write another component's schema. Shared database provisioning, migration ownership and credential delivery must use the team-wide G00 decision. Record immutable audit actor, action, object/proposal version, correlation ID and UTC time; never store hidden reasoning or credentials. |
| Identifiers | Propose UUIDs for Member 4-created `workflowId`, `assessmentId`, `proposalId`, `decisionId`, `evidenceId` and `alertId`. The Member 4 service creates `workflowId` for its business assessment. An optional `sourceWorkflowId` links an Adithya Gunawardana (Member 3) planning workflow using Member 3's agreed ID format. Business workflow IDs never stand for Agentic AI execution IDs. |
| Target identity | `targetType` is `DESTINATION`, `ACTIVITY`, `OFFERING` or `SESSION`; `targetId` uses the canonical Ushan Srinuka (Member 1) identifier and type agreed at shared G00. Member 4 owns only its operational state/restrictions for that reference, not a second catalogue. |
| Time | Request periods use RFC 3339 timestamps with explicit offsets and a half-open interval `[start, end)`, normalized and persisted as UTC instants. Supply an IANA time-zone ID when a user-entered local schedule needs to retain its civil-time meaning. Never infer a period from device time. Preserve source observation, retrieval and validity timestamps; revalidate freshness immediately before execution. |

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

Member 4's consumer response is `OperationalStatusResponse` with
`targetType`, `targetId`, `operationalState`, applicable restrictions,
`stateVersion`, and effective/update timestamps. It is read-only to Members 1
and 3. The shared G00 review must align these reference and freshness fields
with the producer-owned schemas and agree the authenticated private handoff
for service consumers before any service code is written. The public status
route below serves authorized clients; it does not by itself define how a
private member service authenticates a cross-service read.

Unavailable, missing, stale or contradictory producer evidence must remain an
explicit dependency/evidence result and cannot authorize a proposal or
protected transition. Existing authorized status/history reads remain usable
when the assessment evidence path is unavailable. Optional IT3091 prediction
context is never safety authority and its absence cannot be converted into a
positive safety finding.

### Public operation and permission proposal

These are candidate public contracts for the G00 review, not implemented
endpoints. When implemented, `services/api` remains the only client-facing
boundary and forwards to the private service.

Use the shared UI workflow ID `coastal-operations-assessment` for both
clients, with proposed React and Flutter route `/operations/assessments`.
That workflow owns assessment initiation, monitoring, queue/evidence review,
decisions, alerts and history for every role with the relevant permission.
Register its routes and only implemented public endpoints in the shared
registries when the component UI/API is built.

| Method and proposed `/api/...` path | Operation and success contract | Permission |
|---|---|---|
| `POST /api/operations/assessments` | `CreateAssessmentRequest` → `AssessmentWorkflowResponse` (`201`); request includes canonical target, explicit period, objective and optional `sourceWorkflowId`, with `Idempotency-Key`. If AI is not connected, persist the assessment with `aiDependencyStatus: NOT_CONNECTED` and no proposal. | `operations.assessment.create` |
| `GET /api/operations/assessments` | `AssessmentQueueResponse` (`200`); resource-scoped operator list or authorized reviewer queue with cursor pagination. | `operations.assessment.read` or `operations.assessment.queue.read` |
| `GET /api/operations/assessments/{assessmentId}` | `AssessmentDetailResponse` (`200`); workflow/dependency status, authorized proposal, decision, validation, progress, audit/history and result summary. Future agent plan/step summaries appear only after G07 and successful dispatch. | `operations.assessment.read` |
| `POST /api/operations/assessments/{assessmentId}/decisions` | `ReviewerDecisionRequest` → `ReviewerDecisionResponse` (`200`); requires decision, exact `proposalId`/`proposalVersion`, expected target-state version and `Idempotency-Key`. | `operations.assessment.decide` |
| `GET /api/operations/targets/{targetType}/{targetId}/status` | `OperationalStatusResponse` (`200`); current authoritative state/version for authorized clients. Private Member 1 and Member 3 consumers use the separately agreed service handoff. | `operations.target.status.read` |
| `GET /api/operations/targets/{targetType}/{targetId}/history` | `OperationalHistoryResponse` (`200`); authorized, cursor-paginated state changes and decision/audit summaries without private audit payloads. | `operations.target.history.read` |
| `POST /api/operations/assessments/{assessmentId}/evidence` | Multipart image upload → `EvidenceUploadResponse` (`201` only after private persistence and inspection). | `operations.evidence.upload` |
| `GET /api/operations/assessments/{assessmentId}/evidence/{evidenceId}` | Authorized image content (`200`); never return a storage URL. | `operations.evidence.read` |
| `GET /api/operations/alerts` | `AlertQueueResponse` (`200`); alerts visible to the caller, filtered by target, status and validity. | `operations.alert.read` |
| `POST /api/operations/alerts` | `CreateAlertDraftRequest` → `AlertResponse` (`201`); authorized creation of an unpublished advisory/alert for a managed target and effective period. | `operations.alert.manage` |
| `PATCH /api/operations/alerts/{alertId}` | `UpdateAlertDraftRequest` → `AlertResponse` (`200`); change only a `PROPOSED` draft using its expected version; active content requires a new audited proposal. | `operations.alert.manage` |
| `POST /api/operations/alerts/{alertId}/decisions` | `AlertDecisionRequest` → `AlertDecisionResponse` (`200`); authorized activation or resolution with expected alert version and `Idempotency-Key`. | `operations.alert.decide` |

Assessment responses must expose the business `workflowId`, `assessmentId`,
resource scope, current version, UTC timestamps and applicable status/error
fields. Target and alert responses expose their own IDs, scope and current
version; an alert links to its assessment when one exists. Queue and history
responses use a bounded cursor. The shared G00 review must ratify exact DTO
fields, validation bounds and error bodies before coding; the type names above
alone are not frozen wire schemas.

Assign these permission codes through Auth's existing role-to-permission model;
do not branch on role names in service or client code. Coastal Operators may
create and read their in-scope assessments and upload evidence. Operations
Reviewers may read the permitted queue and assessment detail, decide proposals,
read target status/history and authorized evidence/alerts, and manage alert
drafts and lifecycle decisions. Tourist access to an alert requires its
explicit visibility scope and permission.
Platform Administrator has no implicit business permission. An assessment
initiator cannot decide their own high-impact proposal; a different authorized
reviewer is required. React and Flutter expose the same authorized actions and
outcomes.

### State, decision and failure proposal

- Business `workflowStatus` values: `SUBMITTED`, `PROPOSAL_READY`,
  `PENDING_APPROVAL`, `REVISION_REQUESTED`, `REJECTED`, `APPROVED`,
  `EXECUTED`, `BLOCKED`, `SAFE_FAILURE`. An assessment created before an AI
  runtime is connected remains `SUBMITTED` with no proposal. A timeout or
  invalid dispatch records its explicit AI outcome and `SAFE_FAILURE` for that
  AI-dependent stage; it cannot advance to fabricated approval or execution.
- Proposed workflow transitions: `SUBMITTED` to `PROPOSAL_READY`,
  `PENDING_APPROVAL`, `BLOCKED` or `SAFE_FAILURE`; `PENDING_APPROVAL` to
  `APPROVED`, `REJECTED`, `REVISION_REQUESTED`, `BLOCKED` or `SAFE_FAILURE`;
  `REVISION_REQUESTED` to a new proposal version and then one of
  `PROPOSAL_READY`, `PENDING_APPROVAL`, `BLOCKED` or `SAFE_FAILURE`; and
  `APPROVED` to `EXECUTED`, `BLOCKED` or `SAFE_FAILURE`. A validated
  `PROPOSAL_READY` recommendation remains a read-only result when no action is
  proposed; any proposed mutation first moves to `PENDING_APPROVAL`.
  `REJECTED`, `EXECUTED`, `BLOCKED`, `SAFE_FAILURE` and a read-only
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
  `Idempotency-Key` for assessment creation and decisions: a matching replay
  returns its original outcome even if state has since advanced; key reuse
  with a different payload or a first-time request with a stale version
  returns `409` without side effects.
- Alert severity values are `LOW`, `MODERATE`, `HIGH`, `CRITICAL`; lifecycle
  values are `PROPOSED`, `ACTIVE`, `RESOLVED`, `EXPIRED`, `SUPERSEDED`.
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
  absent/out-of-scope resource, `409` stale or conflicting decision, `413`
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
protected state transition and audit entry atomic. Exact foreign keys,
retention and the shared PostgreSQL provisioning/migration arrangement require
G00 agreement before EF Core models are written.

### Private service and optional AI boundary

Propose typed internal HTTP from `services/api` to `coastal-operations:8080`,
with Member 4 routes under `/internal/operations/...` on the private service
network. The API sends a request-scoped authenticated actor context containing
`actorId`, effective permission codes and `correlationId`; the service accepts
that context only from the authenticated API-to-service channel, never from
client fields, and does not call Auth. The shared G00 review must ratify the
service-authentication mechanism and common actor-context envelope before
implementation.

The current infrastructure slice configures API YARP to forward
`/api/operations/{**catch-all}` to the private service without a path rewrite;
the service currently serves only health and Swagger routes in that prefix.
Its port is not published to the host. The proposed `/internal/operations`
business route mapping and actor-context envelope remain unimplemented and
must be reconciled at shared G00 before business operations are added.

The future Agentic AI adapter proposes private `GET /internal/agentic/health`
and `POST /internal/agentic/safety-operations/dispatch` operations. These are
server-to-server contracts only; they are not public client routes and are not
implemented before G07. Register a private route in the endpoint catalog only
when its implementation source exists.

Expose internal `GET /health/live` for process liveness and
`GET /health/ready` for required database/schema readiness. Neither endpoint
depends on the optional Agentic service; its availability is reported only in
the authorized business workflow. An absent AI runtime must not block service
startup or healthy non-AI operations.

The future typed `SafetyOperationsDispatchRequest` uses `contractVersion: 1`
and contains the business workflow/assessment IDs, canonical target reference,
objective, explicit period, source workflow link where present, source
evidence references and correlation ID. It carries no raw image or storage
URL. The `SafetyOperationsDispatchResponse` uses the same versioned contract
and returns a structured proposal with proposed action/target, evidence
references, uncertainty and suggested approval need; Member 4 validates it
deterministically and alone can execute an approved business action.

Keep `aiDependencyStatus` (`NOT_CONNECTED`, `UNAVAILABLE`, `AVAILABLE`)
separate from business `workflowStatus`; an `AVAILABLE` probe does not prove a
later dispatch succeeded. Keep `aiDispatchOutcome` separately as
`NOT_REQUESTED`, `NOT_STARTED`, `SUCCEEDED`, `UNAVAILABLE` or `INVALID_RESULT`,
with an explicit retryable flag. Bound the private probe to one 2-second
attempt and dispatch to one 15-second attempt, with no automatic dispatch
retry. A timeout is unavailable and retryable; an invalid or unauthorized
response is invalid and not retryable. An absent runtime is not connected and
not retryable until configured. These outcomes produce no proposal or state
mutation. Record availability in the authorized assessment status; do not add
a public AI health route. Keep service liveness, database readiness and
optional AI availability separate.

For image evidence, the G00 minimum envelope is `evidenceId`, `assessmentId`,
assessment version, detected media type, byte length, content digest and
inspection state (`PENDING`, `AVAILABLE`, `REJECTED`). Only server-validated,
inspected bytes become reviewer visible; storage stays private, evidence is
versioned/audited, and raw images are never sent to the future agent. An
inspection/storage outage fails the optional upload without exposing the file
or claiming an attachment was accepted. Exact format/count/size limits,
inspection implementation, storage provider/configuration and
retention/deletion remain the Member 4 live-integration gate under
[ADR-0018](../../adr/ADR-0018-assessment-evidence-storage-boundary.md).

## G00 acceptance still required

This Member 4 proposal is not a complete shared freeze. Before any component
coding, the four owners must ratify:

- canonical Member 1 target ID/type and Member 3 workflow ID formats, source
  evidence versions/freshness meaning, and the private producer/consumer
  handoffs with Members 1–3;
- exact public and internal request/response schemas, validation bounds,
  resource scope and permission grants, status/error mappings, and the common
  authenticated actor/correlation envelope for API-to-service calls;
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
