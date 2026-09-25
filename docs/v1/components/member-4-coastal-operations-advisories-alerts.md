---
contract_id: v1.component.coastal-operations
contract_type: business_component
release: v1
implementation_status: target_not_implemented
owner_label: member_4
requirements: "PROJECT_REQUIREMENTS.md sections 18, 19, 20-27, 40, 41, 53"
non_crud_operation: approval_decision_and_controlled_execution
minimum_meaningful_public_api_endpoints: 4
agent_contract: "../agents/member-4-safety-operations-agent.md"
---

# Member 4 — Coastal Operations, Advisories & Alerts

**Contract status:** v1 target specification; the complete business workflow
is not present in the current foundation. **Ownership label:** Member 4 from
the frozen requirements, not an individual or account identity.

## 1. Purpose and user outcome

This component is the operational assessment and human-approval boundary for
BLUEVERSE-managed coastal activities and sessions. It connects an operator's
request to authoritative experience and marine context, the four-role AI
workflow, deterministic application validation, a human decision, any allowed
BLUEVERSE-managed operational state change, an advisory/alert and auditable
history.

An operator must be able to submit an objective and follow the result. A
reviewer must be able to see what was assessed, which evidence and checks
support the proposal, what the AI proposed, what deterministic validation
concluded, whether approval is required, and what action was actually taken.
The reviewer—not the model—decides a high-impact proposal. The system must
make pending, rejected, revised, executed and failed outcomes distinguishable.

## 2. Ownership and legal/operational boundary

Member 4 owns:

- operational assessment records and their workflow linkage;
- proposed operational recommendations and affected BLUEVERSE object;
- authoritative state for the activities, offerings or sessions represented
  and managed within BLUEVERSE;
- reviewer decisions and the approval lifecycle;
- BLUEVERSE advisories/alerts, their state and association to the operation;
- transactional application of an eligible approved change; and
- operational history/audit of proposal, decision and execution.

It consumes source information owned by Members 1–3, but it does not rewrite
their catalogue, environmental measurements or planner results. It does not
claim the authority to close a public beach, issue governmental/legal
emergency orders, control navigation, dispatch emergency services, or replace
professional maritime advice. An operational action changes only records
that BLUEVERSE manages.

## 3. Users and permissions

An authorized Coastal Operator initiates and tracks an assessment. An
authorized Operations Reviewer inspects the queue and may approve, reject or
request revision. Other permitted users can inspect only the information and
alerts allowed by their effective permission. React and Flutter provide the
same business capabilities for the same permission set.

The API's authenticated principal, role-to-permission resolution, resource
scope and authorization are authoritative. The client can hide unavailable
controls for clarity but cannot be the security boundary. Exact permission
codes, whether separation of duties is required, and whether an initiator can
review their own proposal are design decisions that must be explicit before
release; do not silently assume an answer.

## 4. Core records and relationships

These concepts specify what the system must explain and persist, not final
table or wire names.

| Concept | Meaning |
|---|---|
| Operational assessment | A workflow initiated for a BLUEVERSE-managed destination/activity/offering/session and time, with validated objective, initiator, current state reference and evidence context. |
| Workflow | The shared durable Agentic AI execution record and workflow ID, including plan, progress, outputs, deterministic validation, errors, approval and final result as needed. |
| Proposal | The structured agent recommendation, factors, affected object, proposed outcome/action, optional alert/advisory, uncertainty and supporting evidence references. It is not an executed change. |
| Validation result | Application-owned result checking schema, required evidence, configured safety rules, freshness, availability, current operation state, legal state transition, permission and approval requirement. |
| Approval decision | A permission-checked reviewer decision: approve, reject, or request revision, with actor, time, referenced proposal/version and optional allowed explanation. |
| Operational state | The authoritative state of a managed operation. Candidate values include OPEN, CAUTION, TEMPORARILY_SUSPENDED, CANCELLED and COMPLETED; final states and transitions are unresolved until formally documented. |
| Advisory / alert | A BLUEVERSE-managed message related to an assessment or operation, with severity, content, scope, status, validity period and provenance as defined by implementation. A high-severity alert may itself be high impact. |
| Execution history | Auditable record of the accepted decision, revalidation, state transition, result and any alert action. It distinguishes proposals and decisions from actual execution. |

Workflow state and operational state are different. For example, a workflow
may be pending approval while the managed activity remains OPEN. User-facing
status must not merge them into one ambiguous badge.

## 5. Canonical operational-assessment flow

1. **Initiate.** An authorized operator selects a destination, activity or
   offering/session and relevant period, adds a constrained objective, and
   submits through React or Flutter.
2. **Establish authority.** ASP.NET Core authenticates the caller, checks
   permission and resource eligibility, validates request fields, stores the
   objective and creates a shared workflow ID. Clients never call agents or
   internal services directly.
3. **Plan.** The Planning & Coordination Agent persists a structured plan
   with specialist assignments, dependencies, required allowlisted tools and
   expected outputs.
4. **Gather evidence.** Marine Conditions and Experience & Biodiversity
   agents retrieve context. Provider source, timestamps, freshness,
   availability, missing fields and unavailable prediction state remain
   visible. Tool results are validated as untrusted.
5. **Propose.** The Safety & Operations Agent returns a structured
   recommendation and, where relevant, an action or alert proposal. It cannot
   mutate the operation.
6. **Validate deterministically.** Application code checks schema, evidence,
   profile/suitability, freshness, current availability and operational
   state, allowed transition, current permission and required approval. An
   unsupported or unsafe output is blocked, returned for revision, or safely
   failed; it cannot become executable because a model says so.
7. **Pause for review.** A high-impact proposal enters a pending-approval
   workflow state. An authorized reviewer inspects the objective, structured
   plan, progress, agent/tool summaries, supporting evidence, deterministic
   result, proposal and affected object in either client.
8. **Record the choice.** Approve, reject and request-revision decisions are
   permission checked and recorded against the proposal/version. Reject and
   request-revision do not execute the proposed protected change. Revision
   starts or resumes a constrained analysis path without erasing prior
   decision history.
9. **Revalidate and execute.** For an eligible approval, the public API
   re-checks permission, freshness/eligibility as required, current state,
   transition legality and proposal applicability. It performs the permitted
   change transactionally where needed and writes audit/history. The agent
   never commits a mutation.
10. **Return status.** The workflow result and new authoritative state are
    available through the public API to authorized viewers in either client.

The assessed demonstration may start in Flutter and review in React, but the
operator and reviewer flows must each work in both clients.

## 6. High-impact controls and decision lifecycle

High-impact examples from the requirements are:

- temporarily suspend a BLUEVERSE-managed activity or offering;
- cancel a BLUEVERSE-managed session;
- place an offering into another restrictive operational state; or
- publish a high-severity BLUEVERSE operational alert.

The minimum control sequence is:

```text
AI proposal
  → deterministic validation
  → pending authorized human approval
  → approve / reject / request revision
  → (approval only) server-side revalidation
  → eligible transactional business execution
  → audit/history and returned status
```

The system must define a decision/proposal lifecycle separately from the
operational state machine. It must define transition preconditions, how a
pending proposal expires or becomes stale, whether revision creates a new
proposal version, how a rejected proposal is closed, and what evidence the
reviewer sees. No numeric expiration, role code, or transition is invented by
this target document.

### Stale, duplicate and concurrent decisions

Approval is valid only for the proposal and state the reviewer inspected.
Immediately before execution, the API revalidates the target's current state,
proposal version/applicability, permissions and transition. Duplicate
decisions must be handled idempotently or rejected as a conflict under a
documented contract. Concurrent reviewers must not cause both decisions or
transitions to apply. Database concurrency control and transaction boundaries
must protect the final state and audit record together. A stale proposal
returns an explicit non-execution result and a safe next action.

## 7. Alerts, advisories and history

Authorized callers can inspect active alerts/advisories and operational
history related to a managed activity, offering, session or assessment.
Permitted users can create/update/resolve them according to the finalized
permission model and business lifecycle. The UI must distinguish a proposed
alert from a published/active alert and an alert that has been resolved or
expired.

At minimum, implementation must establish the alert's source, affected scope,
severity, lifecycle, effective period, audience/visibility, relation to the
assessment and audit record. It must define what levels count as high impact
and whether any external notification integration exists. No external
delivery channel is assumed by this contract; such delivery would need its
own requirement, consent/security contract, failure behavior and route/docs
updates.

## 8. Public API capability contract

At least four meaningful public endpoints and non-CRUD approve/reject/request-
revision operations are required. This is a required capability set, not a
list of invented routes. Exact route paths, methods, typed request/response,
permission codes, status codes, idempotency/concurrency behavior and error
contract must be designed and recorded in the
[endpoint catalog](../../api/endpoint-catalog.md). New operations use `/api/...`
without a path-version segment.

The public API must make available, as authorized:

- assessment initiation, detail, queue/list, status and workflow progress;
- the associated objective, plan, structured specialist summaries, relevant
  source evidence, validation, proposal and error/status information;
- reviewer approve, reject and request-revision decisions;
- resulting operational state and its history;
- advisory/alert create/read/update/resolve actions under their permission
  and lifecycle; and
- search, filters, pagination and useful operational history where required.

These capabilities do not imply an endpoint per bullet. Only implemented
routes present in the catalog are current. Every React/Flutter workflow must
be registered in [`ui-integration.json`](../../contracts/ui-integration.json)
with the actual public API operations it uses. Agents and clients do not
access internal services or PostgreSQL directly.

## 9. React and Flutter parity

Both clients must support all actions authorized by the same permission set:

| User task | Required parity behavior |
|---|---|
| Operator initiation | Select the same eligible managed object/period, inspect relevant context, enter an objective, submit, and receive the same workflow ID/status. |
| Operator monitoring | Revisit plan progress, proposal/decision status, current managed state, alerts and safe failure/result. |
| Reviewer queue | Find permitted pending and prior assessments with useful search/filter/pagination. |
| Reviewer evidence | Inspect objective, plan/dependencies, agent/tool summaries, marine and experience evidence, timestamps/freshness, validation, affected object and proposal. |
| Decision | Approve, reject or request revision only when authorized and valid for the current proposal. Collect any required rationale under the decided policy. |
| Outcome | See the recorded reviewer decision, execution result, current operational state and audit/history summary. Distinguish proposal from applied change. |
| Alerts | Inspect and manage the same authorized advisory/alert lifecycle and scope. |
| Failure | Explain permission denial, stale proposal, conflict/duplicate decision, missing evidence, service outage and safe failure with appropriate retry/refresh path. |

React and Flutter can lay out queue, evidence and approval controls differently.
They cannot differ in who may act, which evidence is required, the result of
the action, or the source-of-truth status. A hidden/disabled button is not
authorization; the API checks each decision and execution.

## 10. Cross-component and AI relationships

| Owner | Information Member 4 consumes or returns |
|---|---|
| Member 1 | Destination/activity/offering identity, publication, schedule, availability and experience constraints. |
| Member 2 | Source-timed condition report, data gaps and deterministic activity suitability. The operational proposal cannot turn `UNSUITABLE` into a permissive result. |
| Member 3 | Objective, structured plan, dependency status, itinerary/recommendation context where relevant. |
| Planning & Coordination Agent | Plan and delegation. It cannot change operational state or approval. |
| Safety & Operations Agent | Structured assessment factors, recommendation, proposed action/affected object, alert proposal, uncertainty and suggested approval need. Application logic independently decides validation and actual approval requirements. |
| ASP.NET Core operations service | Authorization, deterministic checks, reviewer decision, revalidation, protected mutation, transaction and audit. This is the only execution authority. |

See the paired [Safety & Operations Agent contract](../agents/member-4-safety-operations-agent.md)
and [canonical workflow](../workflows.md). The LLM is a decision-support
producer. It is not the operational record owner, deterministic validator,
reviewer or executor.

## 11. Failure, security and audit behavior

Failure outcomes must cover malformed proposal, unsupported action, missing
or stale conditions, unavailable offering, invalid state transition,
unauthorized/expired approval, rejected or revised proposal, stale target,
duplicate/concurrent decision, provider or model timeout, persistence failure
and retry exhaustion. Record enough structured outcome and error information
to explain what happened without persisting hidden reasoning, credentials,
tokens or unrelated sensitive content.

After `SAFE_FAILURE`, rejection, request revision, blocked validation, lost
authorization or stale proposal, the protected proposed action has no side
effect. If a transaction fails, neither a partially applied operational
change nor an inconsistent audit/event record may remain. Revalidation and
transaction behavior must be designed so a failure has an explicit outcome.

Objectives, tool results, external content and model output are untrusted. They
cannot rewrite instructions, grant tools, reveal secrets, bypass deterministic
rules/approval, or authorize an operation. Tool access for the Safety &
Operations agent is read-only. Least-privilege permissions and audit logs are
required.

## 12. Acceptance and evidence checklist

- the canonical operator → four distinct agent roles → deterministic
  validation → reviewer → API execution → shared status flow is demonstrated;
- only permitted users can initiate, inspect protected details, decide,
  execute or manage alerts;
- all required evidence and validation are visible before a high-impact
  decision;
- invalid or blocked outputs cannot be approved into execution;
- high-impact actions remain pending until an authorized human decision;
- rejection and request revision persist their decision but cause no protected
  state change or high-impact alert publication;
- approval is revalidated against current proposal version, permissions,
  target state and allowed transition at execution time;
- duplicate, stale and concurrent decisions cannot produce an invalid or
  double-applied state transition;
- state mutation and its required history/audit are transactionally
  consistent;
- proposal, approval decision, actual execution, active alert and resolved
  alert remain distinguishable in both clients;
- alerts are scoped to BLUEVERSE-managed operations and do not imply
  government closure or emergency authority;
- AI/tool/provider timeout, malformed output, dependency outage and retry
  exhaustion lead to explicit state and no unsafe side effects;
- both clients deliver equivalent authorized workflows; and
- endpoint catalog, UI registry, permissions, state machine, migrations,
  tests and operational documentation agree.

The Member 4 owner must retain attributable ASP.NET Core, PostgreSQL/EF Core,
React, Flutter, agent, test, documentation and Git/PR evidence. That evidence
must show the non-CRUD reviewer decision, a protected action applied only
after eligible approval, and the corresponding database/audit transition.

## 13. Decisions to finalize during implementation

Define operational and decision state machines; proposal versioning and
expiry; exact high-impact policy; reviewer permission and separation of
duties; decision idempotency and concurrency response; revalidation timing;
transaction/audit design; state transition rules by managed object type;
alert severity, audience, visibility and lifecycle; alert publication
approval; revision semantics; status/error vocabulary; and audit retention.
Architecture-impacting choices require an ADR. These decisions must be
resolved before claiming the workflow is ready; illustrative states/actions
in this document are not implemented authority.

## 14. Traceability

- Requirements: [sections 18–27, 40, 41 and 53](../../../PROJECT_REQUIREMENTS.md).
- Paired AI role: [Safety & Operations Agent](../agents/member-4-safety-operations-agent.md).
- Related contracts: [v1 workflows](../workflows.md), [permissions and parity](../cross-platform-and-permissions.md), [quality and delivery](../quality-and-delivery.md), [requirements coverage and readiness](../requirements-coverage-and-readiness.md), [Agentic AI safety](../../agentic-ai/safety.md), [tool catalog](../../agentic-ai/tools.md), [endpoint catalog](../../api/endpoint-catalog.md), [UI integration](../../contracts/ui-integration.json).
