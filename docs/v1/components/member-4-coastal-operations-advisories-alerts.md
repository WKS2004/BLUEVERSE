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
request to authoritative experience and marine context, the eventual
post-G07 four-role AI workflow, deterministic application validation, a
human decision, any allowed BLUEVERSE-managed operational state change, an
advisory/alert and auditable history. Before G07, the business API and
workflow exist without an executable agent or production proposal generator.

An operator must be able to submit an objective and follow the result. A
reviewer must be able to see what was assessed, which evidence and checks
support the proposal, what the AI proposed, what deterministic validation
concluded, whether approval is required, and what action was actually taken.
The reviewer—not the model—decides a high-impact proposal. The system must
make pending, rejected, revised, executed and failed outcomes distinguishable.

### 1.1 Cross-layer implementation overview

| Layer | Component responsibility and implementation contract |
|---|---|
| **Universal product idea** | Own the BLUEVERSE-managed operational-assessment and review lifecycle: operator request, evidence/proposal, deterministic validation, authorized human decision, revalidation, permitted managed-state action, advisory/alert and audit history. The workflow is decision support for BLUEVERSE-managed records only; it does not issue government closures or emergency orders. Before G07 the business workflow exists, but no production AI proposal generator exists. |
| **React Web** | Provide every authorized operator and reviewer action also available in Flutter: initiate/monitor, inspect evidence, decide approve/reject/request-revision, and view execution/alert/history state. Use React 19/TypeScript/Vite/React Router, reusable pages/components, Tailwind utilities and existing request/state separation. The browser never authorizes or executes a decision locally. See the [React component contract](../../v0/components/react-web-client.md), [UI integration guide](../../development/ui-integration.md), and [React state ADR](../../adr/ADR-0005-react-state-management.md). |
| **Flutter Mobile** | Provide the same authorized initiation, monitoring, review/decision and outcome behavior using native Dart/Material UI, the existing UI/logic/data layers, repository/API service and view-model pattern. Layout may suit mobile, but reviewer permissions, evidence requirements, validation and result must match React. See the [Flutter component contract](../../v0/components/flutter-client.md), [UI integration guide](../../development/ui-integration.md), and [Flutter state ADR](../../adr/ADR-0006-flutter-state-management.md). |
| **Member 4 .NET service and data** | A separate internal ASP.NET Core service in Member 4's own `services/<component-service>/` subfolder owns assessment/proposal/decision state, deterministic policy, target revalidation, authorized human-review outcomes, eligible protected mutations and audit/history. Its EF Core/PostgreSQL records are owned by this service. The existing `services/api` receives only authentication/permission and route/forwarding integration needed to expose public `/api/...` operations; it contains none of Member 4's operational business logic or persistence. The service is private; neither clients nor agents connect to its database or internal routes. Agree identifiers, route/DTO mapping, actor/permission propagation and data ownership at G00. |
| **Third-party integration** | No external alert publisher or government/emergency integration is assumed by this v1 component. It consumes Member 1 managed experience/location/availability, Member 2 sourced marine/suitability evidence and Member 3 business workflow identity. The map provider, Open-Meteo and biodiversity inference stay behind their owning backend components: Member 1 owns map integration, Member 2 owns Open-Meteo, and Member 3 owns the IT3091 inference adapter. Member 4 may consume biodiversity only as optional contextual information under an accepted contract; it is never safety evidence or operational authority. Member 4 consumes canonical Member 1 location data and never uses map results as operational authority. Optional assessment images use a private backend storage adapter, not a client-side provider integration; the provider/configuration is open under [ADR-0018](../../adr/ADR-0018-assessment-evidence-storage-boundary.md). Any future external notification/delivery provider needs an explicit requirement, privacy/security contract and ADR before becoming part of scope. |
| **Paired Agentic AI role** | The future Safety & Operations Agent receives validated context through read-only allowlisted tools and proposes a structured recommendation/action. Before G07, the Member 4 service implements the business contract, typed private dispatch seam and safe not-connected/unavailable state. After G07, the agent still has no approve, publish, suspend, cancel or execute tool; the authorized reviewer decides and the Member 4 service enforces and executes the permitted action after the public API authenticates/authorizes the caller. |
| **Component relationships** | Member 4 consumes Member 1 identity/schedule/availability, Member 2 source-timed conditions and deterministic suitability, and Member 3 workflow identity/objective/status. Member 4 is authoritative for BLUEVERSE operational restrictions and returns current status to Members 1 and 3. Every source fact stays owned by its producer. See the [producer/consumer relationship map](../component-relationships.md#producer-consumer-and-authority-map). |

All client workflows use the shared API, role-to-permission model, workflow
IDs and [UI integration registry](../../contracts/ui-integration.json).
The sections below are the detailed source for state transitions, decisions,
failures, audit and acceptance; this summary does not introduce routes or
freeze unresolved policy values.

Implement this component within the [v1 shared-foundation and file-ownership
rules](../member-branch-workflow.md#shared-foundation-and-file-ownership):
keep Member 4's business behavior in its own internal service, preserve
existing API/Auth flows, and limit `services/api`, shared client, registry and
infrastructure edits to the exact integration entries this component needs.

The shared [Agentic AI implementation blueprint](../../agentic-ai/implementation-blueprint.md)
defines common model, tool, retrieval, security, recovery and evaluation
requirements for this component's paired agent.

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

On `features/coastal-operations`, implement the authorized public assessment,
proposal and status contracts plus a typed private backend adapter for the
future Safety & Operations Agent. Persist business assessment/proposal
identity and explicit not-connected/unavailable status if the Agentic AI
dependency is absent; do not ship a fixture-backed production proposal
generator. The agent's eventual availability never grants permission or
executes an action. Keep this dependency status separate from API liveness and
database readiness. Follow the shared
[member integration boundary](../agentic-ai-integration-boundary.md).

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
| Operational assessment | A Member 4 business workflow initiated for a BLUEVERSE-managed destination/activity/offering/session and time, with validated objective, initiator, current-state reference and evidence context. It has a business workflow ID/status whether or not an AI run occurs. |
| Assessment image evidence | Optional image evidence captured or selected by an authorized operator and attached to a particular assessment version. Member 4 owns its permissions, metadata, private storage reference, reviewer access and audit lifecycle; the image supplements rather than overrides authoritative Member 1/2 evidence or deterministic validation. |
| Business workflow | The durable assessment, review and decision record owned by Member 4, linked to the shared workflow ID and Member 3 request where applicable. It tracks business status and proposal/decision references; it is distinct from post-G07 Agentic plan/step/tool execution state. |
| Proposal | The target structured recommendation from the future Safety & Operations Agent or a validated proposal input, with factors, affected object, proposed outcome/action, optional alert/advisory, uncertainty and supporting evidence. It is not an executed change and is not generated by a production fixture before G07. |
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
   offering/session and relevant period, adds a constrained objective, may
   attach optional image evidence, and submits through React or Flutter.
2. **Establish authority.** `services/api` authenticates the caller, applies
   the permission integration and routes to the private Member 4 service. The
   Member 4 service checks component resource eligibility, validates domain
   fields, stores the objective and creates a shared workflow ID. Clients
   never call agents or internal services directly.
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
9. **Revalidate and execute.** The public API authenticates and authorizes the
   decision request. For an eligible approval, the Member 4 service
   re-checks freshness/eligibility as required, current state, transition
   legality and proposal applicability. It performs the permitted change
   transactionally where needed and writes audit/history. The agent never
   commits a mutation.
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
Immediately before execution, the Member 4 service revalidates the target's
current state, proposal version/applicability and transition after the public
API has authenticated/authorized the caller. Duplicate
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
- optional image-evidence upload for an authorized assessment and retrieval of
  its versioned metadata/content by authorized reviewers;
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
| Reviewer evidence | Inspect objective, plan/dependencies, agent/tool summaries, marine and experience evidence, timestamps/freshness, validation, affected object, proposal and any authorized optional assessment images. |
| Decision | Approve, reject or request revision only when authorized and valid for the current proposal. Collect any required rationale under the decided policy. |
| Outcome | See the recorded reviewer decision, execution result, current operational state and audit/history summary. Distinguish proposal from applied change. |
| Alerts | Inspect and manage the same authorized advisory/alert lifecycle and scope. |
| Failure | Explain permission denial, stale proposal, conflict/duplicate decision, missing evidence, service outage and safe failure with appropriate retry/refresh path. |

React and Flutter can lay out queue, evidence and approval controls differently.
They cannot differ in who may act, which evidence is required, the result of
the action, or the source-of-truth status. A hidden/disabled button is not
authorization: the public API applies its existing caller authentication and
permission integration, while the Member 4 service validates the current
proposal, decision eligibility and execution preconditions.

The operator may capture/select and upload optional image evidence in Flutter
or choose an image file in React. Reviewers in either client see the same
authorized attachment metadata and content through the public API, which
routes to Member 4's private service. A submitted
assessment version's evidence is immutable; additions/corrections are
separately authorized and audited. The format/count/size limits, private
storage provider, inspection/sanitization method and retention policy must be
settled before implementation. Use [ADR-0018](../../adr/ADR-0018-assessment-evidence-storage-boundary.md)
and the [device-capability contract](../device-capabilities.md). The paired
Agentic AI agent receives no raw image or storage URL.

## 10. Cross-component and AI relationships

| Owner | Information Member 4 consumes or returns |
|---|---|
| Member 1 | Destination/activity/offering identity, publication, schedule, availability and experience constraints. |
| Member 2 | Source-timed condition report, data gaps and deterministic activity suitability. The operational proposal cannot turn `UNSUITABLE` into a permissive result. |
| Member 3 | Objective, structured plan, dependency status, itinerary/recommendation context where relevant. |
| Planning & Coordination Agent | Plan and delegation. It cannot change operational state or approval. |
| Safety & Operations Agent | Structured assessment factors, recommendation, proposed action/affected object, alert proposal, uncertainty and suggested approval need. Application logic independently decides validation and actual approval requirements. |
| Member 4 internal service | Deterministic checks, reviewer decision state, revalidation, protected mutation, transaction and audit after the public API's existing authentication/permission checks. This is the only execution authority. |

See the paired [Safety & Operations Agent contract](../agents/member-4-safety-operations-agent.md)
and [canonical workflow](../workflows.md). The LLM is a decision-support
producer. It is not the operational record owner, deterministic validator,
reviewer or executor.

## 11. Failure, security and audit behavior

Failure outcomes must cover malformed proposal, unsupported action, missing
or stale conditions, unavailable offering, invalid state transition,
unauthorized/expired approval, rejected or revised proposal, stale target,
duplicate/concurrent decision, provider or model timeout, persistence failure
retry exhaustion, rejected/malformed/oversized image, failed image inspection
or private-storage outage. Record enough structured outcome and error information
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
required. Operator image attachments are also untrusted; reviewer access is
authorized by the API, and raw image bytes, storage URLs and unvalidated
extracted image text are excluded from the future agent input contract. Any
later image interpretation needs separate approval, threat analysis and
evaluation under [ADR-0018](../../adr/ADR-0018-assessment-evidence-storage-boundary.md).

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
- optional assessment images are uploaded and viewed only through authorized
  API operations, remain private and versioned with the assessment, and are
  not exposed to the Agentic AI agent; and
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
- Component work areas on one member branch: [Member 4 phase plan](../phases/member-4-phase-plan.md); producer/consumer relationships: [component relationship map](../component-relationships.md); PR and G07 process: [member branch workflow](../member-branch-workflow.md).
- Device and evidence media: [v1 device-capability contract](../device-capabilities.md) and [ADR-0018](../../adr/ADR-0018-assessment-evidence-storage-boundary.md).
- Related contracts: [v1 workflows](../workflows.md), [member Agentic AI integration boundary](../agentic-ai-integration-boundary.md), [permissions and parity](../cross-platform-and-permissions.md), [quality and delivery](../quality-and-delivery.md), [requirements coverage and readiness](../requirements-coverage-and-readiness.md), [Agentic AI safety](../../agentic-ai/safety.md), [tool catalog](../../agentic-ai/tools.md), [endpoint catalog](../../api/endpoint-catalog.md), [UI integration](../../contracts/ui-integration.json).
