# Agentic AI architecture — v1 target

**Implementation status:** the repository has no executable Agentic AI
service, agent or tool. This is the v1 target contract derived from
[PROJECT_REQUIREMENTS.md](../../PROJECT_REQUIREMENTS.md). The
[v1 index](../v1/README.md) links separate contracts for each member's agent.
Before selecting a runtime, model/provider or retrieval design, use the
[implementation blueprint](implementation-blueprint.md) as the shared
human-and-agent handoff; it distinguishes binding safety/workflow requirements
from decisions still open in ADRs.

**Implementation boundary:** use the [v1 component relationship map](../v1/component-relationships.md)
to understand dependencies, and the [member branch and integration workflow](../v1/member-branch-workflow.md)
for the G07 gate and post-gate AI sequence.
The four member features first implement their complete business behavior and
the public API/private backend integration seam for their paired future AI
role, including not-connected/unavailable status and a bounded server-side
dependency check. The canonical [member integration boundary](../v1/agentic-ai-integration-boundary.md)
defines that preparation. No executable agent, prompt/model call, tool
execution, orchestration or agent-owned execution state is built until all
four feature PRs are merged, compatibility is accepted on `dev`, and the team
passes G07. Test-only proposal fixtures may exercise Member 4's boundary;
they are not a production AI substitute. After G07, implement the actual
runtime and agents through `agentic-ai/**` branches: build the Member 1 and
Member 2 specialist agents in parallel, integrate them through Member 3's
planner, then add Member 4's read-only proposal agent and the complete
evaluations.

## Four distinct responsibilities

| Owner | Agent | Responsibility |
|---|---|---|
| Member 3 | [Planning & Coordination](../v1/agents/member-3-planning-coordination-agent.md) | Interpret the objective, persist a structured plan, delegate, track dependencies and assemble validated information. |
| Member 2 | [Marine Conditions Intelligence](../v1/agents/member-2-marine-conditions-intelligence-agent.md) | Return sourced and time-aware weather/marine context with freshness and gaps. |
| Member 1 | [Coastal Experience & Biodiversity](../v1/agents/member-1-coastal-experience-biodiversity-agent.md) | Return destination, offering, schedule and optional biodiversity context. |
| Member 4 | [Safety & Operations](../v1/agents/member-4-safety-operations-agent.md) | Produce a structured operational recommendation and proposed action. |

A distinct agent needs an identifiable responsibility, validated input,
structured output, controlled tool permissions and visible participation.
Renaming or cloning one prompt is insufficient.

## Execution boundary

React and Flutter call only the public ASP.NET Core API. The API applies the
existing authentication/permission boundary and routes the public operation
to its owning private member service. That service validates domain input,
owns business workflow persistence and, after G07, dispatches the private
Agentic AI runtime. The planner delegates to the marine and experience agents;
the safety agent consumes their structured results for operational
assessment. The owning member service performs deterministic validation. A
high-impact proposal pauses for authorized human approval; the Member 4
service revalidates and executes an eligible approved action and records its
business/audit history.

Before G07, each member service implements its business workflow and typed
private adapter/availability behavior. No agent implementation is needed for
the member branches to report `not connected` or `unavailable`. The owning
member service checks its configured private AI dependency and exposes only
the accepted coarse workflow/readiness state through the public contract;
the private probe remains server-to-server. Keep this distinct from
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
