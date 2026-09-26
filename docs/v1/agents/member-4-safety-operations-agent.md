---
contract_id: v1.agent.safety-operations
contract_type: agent_role
release: v1
implementation_status: target_not_implemented
owner_label: member_4
requirements: "PROJECT_REQUIREMENTS.md sections 19-27, 40-41"
business_component: "../components/member-4-coastal-operations-advisories-alerts.md"
---

# Member 4 — Safety & Operations Agent

**Target status:** there is no executable v1 Safety & Operations agent/runtime
in the current foundation. This contract describes recommendation behavior;
it does not claim the operations service, approval workflow or tool set is
implemented.

**Owning business component:** [Coastal Operations, Advisories & Alerts](../components/member-4-coastal-operations-advisories-alerts.md).

**Implementation sequence:** do not implement an executable agent, model
call or tool before all four business components pass G07. After that gate,
follow the post-G07 sequence in the [member branch and integration workflow](../member-branch-workflow.md); this
agent consumes validated planner/specialist evidence and never approves or
executes its own proposal.
Before G07, the owning Member 4 feature branch prepares only the public
assessment/proposal contract, private backend adapter and
not-connected/unavailable behavior defined in the [member integration boundary](../agentic-ai-integration-boundary.md).
Implement this actual proposal agent on an `agentic-ai/**` branch after G07.

## 1. Responsibility and strict separation

Consume validated marine, experience, operational and workflow evidence, then
produce a structured recommendation for a BLUEVERSE-managed coastal activity,
offering or session. Explain the factors and uncertainty, identify a proposed
operational action where relevant, and indicate that review may be needed.

This agent proposes; it never decides authorization, human approval,
deterministic validation, the authoritative operational status, or execution.
It has no mutation tool. A language-model recommendation must never directly
suspend an offering, cancel a managed session, change operational state, or
publish an alert.

The agent is a distinct fourth responsibility: it does not replace the
planning coordinator that forms/delegates workflow steps, marine agent that
reports environmental context, or experience agent that reports offerings
and biodiversity context.

## 2. Invocation and typed input

The private orchestrator invokes the role only for a validated operational
assessment plan step. ASP.NET Core has already authenticated and authorized
the initiator, validated the objective, assigned the workflow ID and
persisted workflow state.

The minimum logical input is:

| Input concept | Meaning |
|---|---|
| Workflow/step correlation | Shared workflow ID/type, plan step and report schema expected. |
| Objective and scope | Validated reason, destination/location, activity/offering/session reference and relevant time. Free text remains untrusted data. |
| Experience report | Validated Member 1 report for the affected object, publication, schedule, availability and applicable experience constraints; optional biodiversity status remains contextual. |
| Marine report | Validated Member 2 factors with source, period, freshness and missing data. Preserve its deterministic suitability result and do not replace it. |
| Current operational state | Authoritative Member 4 managed state and permitted transition context as read by the application/tool. |
| Deterministic profile/result | Configured safety profile and the application-owned environmental assessment/evidence relevant to the objective. |
| Active notices | Relevant active alerts, restrictions or operational constraints from their authoritative source. |
| Policy envelope | Allowed recommendations/actions, output schema, tool allowlist and approval policy context; cannot be modified by the model. |

Optional operator image attachments belong to the human-review evidence
record. This agent does not receive image bytes, direct storage access, a
storage URL or unvalidated text extracted from an image. Any later image
interpretation requires a separate reviewed post-G07 capability; see the
[device and evidence-media contract](../device-capabilities.md) and
[ADR-0018](../../adr/ADR-0018-assessment-evidence-storage-boundary.md).

This list describes semantic content, not serialized DTO fields or the final
prompt. The implementation must define a typed/versioned input schema,
reject mismatched target references, and exclude secrets, unrelated personal
data, hidden reasoning and any unnecessary bearer credential.

## 3. Candidate allowlisted read-only tools

The candidate names describe future controlled capabilities; they are not
production tools, endpoint paths or permissions. Tool invocation occurs
through backend-owned services and is governed by the
[shared tool contract](../../agentic-ai/tools.md).

| Candidate tool | Read purpose | Restriction |
|---|---|---|
| `safety_profile_lookup` | Read configured activity profile or a validated profile summary relevant to the selected operation. | Cannot create, edit, activate or invent profile criteria. |
| `operational_status_lookup` | Read current state and allowed context for the BLUEVERSE-managed target. | Cannot change status or decide that a transition is authorized. |
| `active_alert_lookup` | Read relevant active BLUEVERSE alerts/advisories. | Cannot publish, resolve or elevate an alert. |
| `operational_constraint_lookup` | Read applicable restrictions or business constraints. | Cannot widen scope or override an authoritative restriction. |

Every implemented tool defines agent allowlist, typed input/output, service
owner, authorization, validation, timeouts, bounded retries, failure behavior
and an auditable execution summary with workflow/step correlation and elapsed
time. Tool responses are untrusted and revalidated.
The Safety & Operations Agent receives read access only. No mutation or
approval-decision tool is allowed.

## 4. Structured recommendation contract

Return a typed **Safety & Operations Recommendation** with semantic fields
that application code can validate:

| Output concept | Required meaning |
|---|---|
| Correlation and target | Workflow/step correlation, target managed object and relevant period from validated input. |
| Assessed factors | Evidence-backed condition, availability, operational-state and constraint factors; reference the source/time. |
| Recommendation | Structured interpretation of whether to continue, use caution, propose a restrictive action, or report insufficient evidence. Candidate labels include CONTINUE, CONTINUE_WITH_CAUTION, TEMPORARILY_SUSPEND, CANCEL_SESSION and INSUFFICIENT_DATA; final vocabulary is a technical decision. |
| Proposed action | Optional action from the application-configured set and the affected BLUEVERSE-managed object. This is a proposal only, not a command or authorized transition. |
| Proposed notice | Optional advisory/alert content and suggested severity where the configured schema permits; never imply publication. |
| Evidence and uncertainty | Supporting validated report references, stale/missing information, contradictions and limitations. |
| Approval suggestion | The model may flag that a proposed action appears high impact, but this is advisory metadata only. The deterministic application policy computes whether approval is required. |
| Outcome | Structured complete/insufficient/invalid result according to the runtime schema. |

Exact property names, enum values, severity bands, action catalog and schema
version remain design decisions. The application must reject unsupported
actions, unknown targets, missing evidence, invalid references, contradictory
claims and malformed outputs. A generated narrative is not sufficient proof
that the proposal satisfies policy.

## 5. Deterministic rules and high-impact boundary

Application/business code independently evaluates the proposal against:

- output schema and required fields;
- marine-source validity, requested-period match and freshness;
- deterministic activity suitability/profile result;
- destination/offering publication, schedule and availability;
- current authoritative operational state and allowed transitions;
- active restrictions/alerts;
- caller/reviewer permission and resource scope; and
- high-impact approval requirements.

The model cannot change an `UNSUITABLE`, `UNKNOWN` or blocked result, invent a
threshold, mark a stale report fresh, or turn missing evidence into a positive
assessment. A high-impact proposal enters a pending-approval workflow. The
authorized human decision belongs to the public API workflow, not the agent.
Rejection/revision is recorded and has no protected side effect. Even after
approval, ASP.NET Core rechecks current state, permission and proposal
applicability before transactional execution.

High-impact examples include suspension of a BLUEVERSE-managed offering,
cancellation of a managed session, another restrictive state, and a
high-severity BLUEVERSE alert. Exact policy must be finalized by the
operations contract. The agent cannot make governmental beach closures,
emergency orders, professional navigation decisions or changes to operations
BLUEVERSE does not manage.

## 6. Workflow participation and reviewer evidence

In the canonical assessment, the Planning & Coordination Agent calls this
role after the required marine and experience reports are available. The
agent returns one structured recommendation linked to the workflow and its
evidence. Deterministic validation runs after it. If high impact, the workflow
pauses while a permitted reviewer inspects the proposal in either React or
Flutter.

The reviewer-facing evidence must come from validated, auditable workflow
records: original objective, structured plan and step progress, agent/tool
summaries, source timestamps/freshness, availability, deterministic
suitability/validation, proposed action, affected object, current status and
uncertainty. Tool-call summaries include relevant outcome and timing without
exposing secrets or hidden reasoning. The UI must distinguish
recommendation, validation outcome, reviewer decision and actual execution.

The agent output contributes to a shared workflow ID and status consumed by
both clients through the public API. Client-to-client synchronization is not
part of this contract.

## 7. Failure and recovery behavior

Handle absent/stale/malformed marine evidence, unavailable or restricted
offering, missing operational state, profile absence, conflicting alerts,
unsupported action, malformed recommendation, tool timeout, orchestration
failure and retry exhaustion. Return a structured insufficient-data or
failure outcome and leave application policy to block or request revision.
Never fill gaps with invented facts or default to a permissive action.

After safe failure, invalid output, blocked validation, lost authorization,
rejection, revision or stale target, the proposed protected action must not
execute. No retry may bypass a human decision, reuse obsolete evidence as
current or create duplicate side effects. The API—not the agent—owns final
revalidation and idempotency/concurrency behavior.

Persist only the structured recommendation or necessary auditable summary,
referenced validated evidence, tool outcomes, errors/retries and timestamps.
Do not persist hidden reasoning, credentials, access tokens or secrets.

## 8. Security and prompt-injection controls

Objectives, user-provided context, catalogue text, external weather values,
biodiversity metadata, active-alert content, any future approved derived
media text and tool output are untrusted. Raw image attachments are not agent
inputs under this contract.
They cannot alter role instructions, create a tool, authorize a proposal,
bypass a deterministic result, bypass reviewer approval, reveal secrets or
cause direct mutation. Validate every tool input and output and enforce
least-privilege read-only access outside the model.

The agent must not return secrets or unnecessary personal data in its report.
Audit logs should record the invoked tool, workflow/step, result/failure and
safe summary—not hidden chain-of-thought.

## 9. Acceptance and evaluation

Use deterministic fixtures and complete behavioral assertions to verify:

- valid input links the correct managed target and all reports to the
  workflow/step;
- the Safety & Operations Agent is distinct and runs after prerequisite
  reports in the canonical workflow;
- missing, stale, contradictory or unsuitable data cannot produce an
  unqualified permissive recommendation;
- output schema rejects unsupported actions and malformed target references;
- the model cannot override suitability, freshness, operational status,
  permissions or approval rules;
- no mutation or approval tool is callable by this agent;
- high-impact proposal pauses for an authorized human decision;
- rejection and revision are recorded without state change/alert publication;
- duplicate, concurrent, stale and unauthorized decisions are rejected or
  handled idempotently by the application with consistent audit state;
- approval requires server revalidation before mutation;
- tool timeout, prompt injection, unavailable context and safe failure produce
  no unsafe side effects; and
- both clients show source evidence, validation and actual final status rather
  than presenting the agent text as an executed action.

The full golden-case evaluation includes schema, permissions, workflow state,
validation, reviewer decision, audit, side effects and absence of forbidden
effects. LLM-as-judge may supplement, but cannot replace deterministic
assertions or reviewer inspection.

## 10. Implementation decisions

Specify versioned input/output schemas; supported recommendation/action set;
what information is required per assessment type; how an alert proposal is
structured; data freshness requirements; deterministic approval policy;
tool permissions and error outcomes; workflow/decision state machine;
reviewer identity/separation-of-duties policy; proposal expiry/versioning;
audit retention; and measurable evaluation release gates. The exact approval
requirement must be determined by
application policy, even if the output includes a suggested flag.

## 11. Related contracts

- Requirements: [sections 18–27, 40–41](../../../PROJECT_REQUIREMENTS.md).
- Owning component: [Coastal Operations, Advisories & Alerts](../components/member-4-coastal-operations-advisories-alerts.md).
- Shared behavior: [canonical workflow](../workflows.md), [requirements coverage and readiness](../requirements-coverage-and-readiness.md), [Agentic AI architecture](../../agentic-ai/architecture.md), [implementation blueprint](../../agentic-ai/implementation-blueprint.md), [tools](../../agentic-ai/tools.md), [safety](../../agentic-ai/safety.md), [evaluation](../../agentic-ai/evaluation.md).
