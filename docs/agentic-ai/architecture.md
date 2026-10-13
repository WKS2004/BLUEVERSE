# Agentic AI architecture — v1 target

**Implementation status:** the Member 1 Experience & Biodiversity specialist
runtime and its read-only tools are implemented under
`services/agents/experience-biodiversity`. The remaining three roles and the
end-to-end orchestration workflow are not implemented. This is the v1 target contract derived from
[PROJECT_REQUIREMENTS.md](../../PROJECT_REQUIREMENTS.md). The
[v1 index](../v1/README.md) links separate contracts for each member's agent.
Before selecting a runtime, model/provider or retrieval design, use the
[implementation blueprint](implementation-blueprint.md) as the shared
human-and-agent handoff; it distinguishes binding safety/workflow requirements
from decisions still open in ADRs.

**Implementation boundary:** use the [v1 component relationship map](../v1/component-relationships.md)
and [member branch workflow](../v1/member-branch-workflow.md) for ownership
and integration evidence. The team owner reported G07 acceptance on
2026-10-13; the repository tracker records that report. The Member 1 runtime
is a stateless private Python HTTP service that reads through the accepted
member-service contracts. It does not own durable workflow state or provide
cross-agent orchestration. The broad runtime/framework and shared state choices
remain open in ADR-0007/0008. The other specialist agents, planner
orchestration, safety proposal workflow and golden evaluations remain to be
implemented on their assigned `agentic-ai/**` branches. See the
[canonical owner map](../project/ai-team-members.md).

## Four distinct responsibilities

| Owner | Agent | Responsibility |
|---|---|---|
| Adithya Gunawardana (Member 3) | [Planning & Coordination](../v1/agents/member-3-planning-coordination-agent.md) | Interpret the objective, persist a structured plan, delegate, track dependencies and assemble validated information. |
| Sanuda Abeysinghe (Member 2) | [Marine Conditions Intelligence](../v1/agents/member-2-marine-conditions-intelligence-agent.md) | Return sourced and time-aware weather/marine context with freshness and gaps. |
| Ushan Srinuka (Member 1) | [Coastal Experience & Biodiversity](../v1/agents/member-1-coastal-experience-biodiversity-agent.md) | Return destination, offering, schedule and optional biodiversity context. |
| Wanshaja Sooriyabandara (Member 4) | [Safety & Operations](../v1/agents/member-4-safety-operations-agent.md) | Produce a structured operational recommendation and proposed action. |

A distinct agent needs an identifiable responsibility, validated input,
structured output, controlled tool permissions and visible participation.
Renaming or cloning one prompt is insufficient.

## Execution boundary

React and Flutter call only the public ASP.NET Core API. The API applies the
existing authentication/permission boundary and routes the public operation
to its owning private member service. That service validates domain input,
owns business workflow persistence and dispatches its private Agentic AI
integration. The planner delegates to the marine and experience agents;
the safety agent consumes their structured results for operational
assessment. The owning member service performs deterministic validation. A
high-impact proposal pauses for authorized human approval; the Wanshaja Sooriyabandara (Member 4)
service revalidates and executes an eligible approved action and records its
business/audit history.

Member services implement their business workflows and typed private
adapters/availability behavior. The current Member 1 role is stateless and
does not persist agent-owned execution state. The owning member service checks
its configured private AI dependency and exposes only the accepted coarse
workflow/readiness state through the public contract; the private probe
remains server-to-server. Keep this distinct from
`GET /api/health` liveness and database readiness. A healthy probe does not
guarantee a later dispatch will succeed; dispatches need bounded timeouts and
safe persisted outcomes.

The [canonical assessed flow](../v1/workflows.md) demonstrates Flutter
operator initiation and React reviewer approval, but both roles' permitted
actions work in both clients. The tourist recommendation path uses planning,
marine and experience context under deterministic constraints without
routine staff approval.

Deterministic validation is not an LLM agent. It checks schemas, required
fields, safety profiles, source freshness, availability, current state,
allowed transitions, permissions and approval requirements. AI output
cannot override a blocked result or directly mutate protected state.

## Durable state and service design

The member service owns durable business workflow state. The Agentic AI
runtime owns the logical execution state after G07; its physical storage
boundary is still subject to ADR-0008. Persist only required workflow
references, structured plan and step state, validated output/tool summaries,
validation, errors/retries, approval linkage and final outcome with necessary
timestamps. Do not persist hidden reasoning, credentials or unnecessary
sensitive data. Exact schemas and transport boundaries belong in the
accepted ADRs and data/service contracts.

The eventual orchestration framework remains undecided in
[ADR-0007](../adr/ADR-0007-agentic-ai-framework.md). Whether the agents run
in one private process or several does not change the public API, tool,
validation or approval boundaries.
