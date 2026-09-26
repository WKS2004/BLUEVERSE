---
contract_id: v1.agent.planning-coordination
contract_type: agent_role
release: v1
implementation_status: target_not_implemented
owner_label: member_3
owner_full_name: "Adithya Gunawardana"
owner_github_username: "AdithyaGunawardana"
feature_branch: "features/coastal-planner"
agentic_ai_branch: "agentic-ai/planning-coordination"
requirements: "PROJECT_REQUIREMENTS.md sections 17, 20-27, 40-41"
business_component: "../components/member-3-smart-coastal-planner-itinerary-management.md"
---

# Adithya Gunawardana (Member 3) — Planning & Coordination Agent

**Target status:** no executable v1 planner, orchestration runtime or agent
tools are present in the current foundation. This is the workflow coordinator
contract, not an implementation claim.

**Assigned owner:** Adithya Gunawardana (`@AdithyaGunawardana`), requirement
trace label Member 3. Component branch: `features/coastal-planner`; actual
agent branch, after G07: `agentic-ai/planning-coordination`.

**Owning business component:** [Smart Coastal Planner & Itinerary Management](../components/member-3-smart-coastal-planner-itinerary-management.md).

**Implementation sequence:** do not implement an executable agent, model
call or tool before all four business components pass G07. After that gate,
follow the post-G07 sequence in the [member branch and integration workflow](../member-branch-workflow.md),
including the validated specialist-output handoff from Ushan Srinuka and Sanuda Abeysinghe.
Before G07, the owning Adithya Gunawardana (Member 3) feature branch prepares only the public
business workflow, private backend adapter and not-connected/unavailable
behavior defined in the [member integration boundary](../agentic-ai-integration-boundary.md).
Implement this actual planner agent on an `agentic-ai/**` branch after G07.

## 1. Responsibility and distinction

Interpret a validated coastal-tourism or operational-assessment objective,
produce a structured plan, delegate only to configured specialist agents,
respect dependencies, verify expected outputs and assemble validated context
for application validation. Persisted plan and progress make the workflow
visible and recoverable.

This is the only v1 planning/coordinating agent. It does not duplicate the
Marine Conditions, Coastal Experience & Biodiversity, or Safety & Operations
role. The distinct role requires its own input/output contract, configured
delegation permissions and visible participation; renaming one prompt four
ways is not four agents.

## 2. Invocation and validated input

React and Flutter call only the public ASP.NET Core API. Before private
orchestration starts, the API applies the existing caller authentication and
permission checks, then routes the operation to Adithya Gunawardana's private component
service. That service validates the domain request, persists the allowed
objective and creates the durable workflow ID. The planner receives the
minimum validated step context required to coordinate that workflow; it does
not perform authorization from raw role names or secrets.

The logical input contract includes:

| Input concept | Meaning |
|---|---|
| Workflow identity | Stable workflow ID, workflow type, initiator reference/scope permitted for the task, and current durable workflow status. |
| Validated objective | The coastal recommendation or operational assessment goal and allowed task scope. Free text is untrusted data and cannot change governing instructions. |
| Domain references | Validated destination, activity/offering and time/period identifiers needed for the workflow. |
| Constraints | Minimum planning preferences and business context needed for candidate selection, including validated user-selected date/time, duration and applicable time-zone semantics; no unrelated personal data. The agent does not infer or invent a requested time. |
| Available specialist roles | Fixed configured role identities and report types for this workflow class. The model cannot invent a role or swap in a generic clone. |
| Tool and policy configuration | Server-owned allowlists, output schemas, dependency rules and execution bounds. These are immutable to the planner. |
| Resume context | If resumed, previously validated completed-step references/results, failed step and permitted retry state. Do not reconstruct missing state from model memory. |

This table is semantic, not a frozen serialized schema. Define exact typed
input properties, workflow-type validation and contract version during
implementation. Never include passwords, provider credentials, hidden model
reasoning, or an access token as natural-language context.
The client controls for those time inputs are specified in the shared
[device-capability contract](../device-capabilities.md); validated values
remain business input and do not give the agent permission to alter the
requested period.

## 3. Structured plan output

Produce a typed, application-validated plan whose meaning includes:

| Plan field concept | Contract meaning |
|---|---|
| Objective | Restatement of the permitted workflow objective, tied to the validated request. |
| Steps | Finite ordered or dependency-linked operations needed to complete this workflow type. |
| Assignment | Configured distinct agent identity per specialist step; the model cannot define a new role. |
| Dependencies | Which validated outputs must exist before another step can start. Unmet required dependency blocks downstream use. |
| Required tools | Only tools already allowed for the selected assigned agent and step. A plan is rejected if it asks for any other tool. |
| Expected output | Specific structured report type/schema expected from each step and how the next application stage consumes it. |
| Status/progress | Current/complete/blocked steps, dependency outcomes and the reason a step is incomplete. The Adithya Gunawardana (Member 3) component service owns final durable business workflow state; post-G07 Agentic AI execution state has a separate owner. |
| Error/recovery | Structured tool/agent failure reference and a bounded next action if allowed; no open-ended retry loop. |

An illustrative operational plan retrieves experience and marine context,
then produces a safety/operations proposal, then enters application-owned
deterministic validation and (if required) human approval. The tourist plan
uses experience and marine context, then assembles a recommendation under the
same deterministic candidate constraints. Deterministic validation,
authorization, approval and protected execution are application stages, not
LLM plan steps that the model can omit.

Final serialized property names, plan enum vocabulary, versioning and storage
schema remain implementation decisions. The application rejects unknown
agents, tools, report types, cycles, missing dependencies, unbounded plans and
malformed output before step execution.

## 4. Delegation and orchestration authority

| Configured specialist | When planner delegates | Expected business output |
|---|---|---|
| Marine Conditions Intelligence | The objective depends on time/location-specific weather or marine context. | Sourced Marine Conditions Report with timestamps, freshness, factors and gaps. |
| Coastal Experience & Biodiversity | The objective depends on destination/activity/offering/schedule context; biodiversity is included only when relevant. | Experience & Biodiversity Context Report with publication/availability and optional prediction status/provenance. |
| Safety & Operations | The workflow is an operational assessment and validated inputs are available. | Structured recommendation/proposed action, affected object, evidence references and uncertainty. |

The planner coordinates configured agent invocations. Delegation is not
permission to call arbitrary services. It cannot:

- add, remove or grant an agent/tool permission;
- call a tool directly unless a separately documented allowlist permits that
  exact action;
- skip a required agent/dependency because a model response says it is
  unnecessary;
- treat a malformed, failed, stale or unauthorized output as successful;
- perform deterministic business checks by language-model judgment;
- bypass a blocked validator result or human approval; or
- execute or authorize any operational mutation.

## 5. Assembly and deterministic boundary

The planner may summarize only validated specialist results and may link the
summary to its source evidence. It cannot change source status, timestamps,
uncertainty, units, deterministic suitability, availability or operational
state. Before a final result is released, application logic independently
validates required schemas and business constraints.

For tourism, a deterministic `UNSUITABLE` activity cannot reappear after
ranking, summarization or retry. Missing required marine data stays explicit.
For operations, the Safety & Operations proposal proceeds to deterministic
validation, not straight to a mutation. Schema validation, freshness,
configured safety profile, current state, legal transition, authorization
and approval requirements are owned by application/business code.

## 6. Workflow identity and durable state

Use one shared workflow ID from API initiation through both clients' status
views. Persist only information required to run, resume and audit the process:
workflow type and initiator reference, allowed objective, structured plan,
step status/dependencies, structured outputs or auditable summaries,
validation results, correlated tool-call outcomes and elapsed times,
errors/retries, approval decision, final result and timestamps where
relevant. Never persist hidden reasoning, chain-of-thought,
API keys, passwords, bearer tokens or unrelated personal data.

The Adithya Gunawardana (Member 3) service's durable workflow store and public API, not an in-memory
prompt transcript, are the source of truth for status. The exact workflow state machine must be designed
and tested. At minimum, states must distinguish a created/active workflow,
work in progress, a dependency/error blockage, pending approval, completion,
rejection/revision and unrecoverable safe failure as the product requires.
These descriptions do not freeze enum strings. React and Flutter retrieve the
same public API status; they do not exchange it directly.

## 7. Recovery, boundedness and safe failure

Failures include invalid request/objective, planner timeout or malformed
plan, unauthorized tool/agent assignment, agent/tool timeout, invalid output,
missing required dependency, provider outage, unavailable optional
biodiversity, validation rejection, approval rejection/revision, persistence
failure and retry exhaustion.

For each tool/step, implementation policy sets a finite timeout/retry limit
and identifies whether retry is safe. After a retry, revalidate inputs and
outputs; do not apply a stale or duplicated result as fresh. Resume from
durable validated completed steps only where their freshness and workflow
state still permit it. If the process cannot safely continue, record an
explicit `SAFE_FAILURE` or defined failure outcome, describe the blocked step,
and stop downstream actions. No protected side effect may occur after safe
failure.

Do not catch and hide errors, falsely mark a step complete, repeat a mutation,
or degrade a required-data failure into a fabricated complete recommendation.
Approval rejection means no execution; revision returns to a controlled
planning/validation path and preserves the recorded decision.

## 8. Security and prompt-injection resistance

The objective, descriptions, external weather content, biodiversity output,
specialist reports and tool responses are untrusted data. Validate each before
it enters a plan or downstream context. Instructions embedded in any such
content cannot alter system policy, delegate to an unconfigured agent, grant
tools, reveal secrets, broaden data access or bypass validation/approval.

Use least-privilege agent/tool grants and typed inputs/outputs. Tool calls
must be authorized and auditable. Expose no arbitrary network capability. The
planner never receives unnecessary secrets and cannot use natural-language
reasoning as proof of authorization.

## 9. Evaluation and acceptance evidence

The canonical golden workflow must show a persisted plan, actual delegation
to each of the four distinct agent roles where required, correct dependency
order, validated outputs, deterministic validation, a pending high-impact
proposal, authorized reviewer choice and auditable final status. The tourist
golden workflow must show planning plus marine and experience delegation and
deterministic exclusions/uncertainty.

Deterministic fixtures must cover:

- valid plan schema and allowed specialist/tool assignment;
- malformed, empty, cyclic, unbounded or unknown step/agent/tool plan;
- required dependency missing or failed output reused downstream;
- duplicate delivery, timeout, retry, retry exhaustion and safe resume;
- stale/invalid specialist reports and persistence failure;
- prompt injection in objective and every delegated/tool result;
- attempt to skip deterministic validation or human approval;
- `UNSUITABLE` candidate reintroduction during assembly;
- approval rejection/revision and absence of protected side effects;
- durable status and audit after each failure/recovery path; and
- visible distinction between each specialist's output and planner assembly.

LLM-as-judge can supplement deterministic assertions and human review but
cannot be the sole evaluator. Record fixture, live model, provider and end-to-
end demonstration evidence separately. Both clients must show the same
workflow ID and authoritative status.

## 10. Implementation decisions

Document exact schemas/versioning, allowed agents per workflow, finite plan
and delegation policy, dependency validation, workflow state machine,
orchestrator/runtime choice (see [ADR-0007](../../adr/ADR-0007-agentic-ai-framework.md)),
workflow persistence and resume semantics, idempotency, timeout/retry limits,
failure mapping, audit event structure, observability and measurable
evaluation release gates. Resolve architecture
choices in ADRs and data-contract choices in API/database docs. The agent
contract cannot be considered implemented solely because the model produces
a good free-text plan.

## 11. Related contracts

- Requirements: [sections 17, 20–27, 40–41](../../../PROJECT_REQUIREMENTS.md).
- Owning component: [Smart Coastal Planner & Itinerary Management](../components/member-3-smart-coastal-planner-itinerary-management.md).
- Specialists: [Marine Conditions](member-2-marine-conditions-intelligence-agent.md), [Experience & Biodiversity](member-1-coastal-experience-biodiversity-agent.md), [Safety & Operations](member-4-safety-operations-agent.md).
- Shared behavior: [canonical workflows](../workflows.md), [requirements coverage and readiness](../requirements-coverage-and-readiness.md), [Agentic AI architecture](../../agentic-ai/architecture.md), [implementation blueprint](../../agentic-ai/implementation-blueprint.md), [tools](../../agentic-ai/tools.md), [safety](../../agentic-ai/safety.md), [evaluation](../../agentic-ai/evaluation.md).
