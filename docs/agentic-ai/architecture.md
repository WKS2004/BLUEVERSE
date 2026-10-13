# Agentic AI architecture — v1 target

**Implementation status (2026-10-13):** provisional private runtimes for all
four roles are present under `services/agents`. Experience & Biodiversity and
Marine Conditions provide specialist APIs; Planning & Coordination has a
private dispatch API, bounded plan orchestration and Planner-owned execution
checkpoints; Safety & Operations provides a proposal-only private API. The
single Gemini egress proxy and DHI Compose profile are also present. Role-local
suites pass, but Experience and Marine response contracts do not yet match
Planning's versioned specialist envelope, Safety is not connected to the
published-assessment workflow, and the golden four-agent workflow is not
integrated. G07 acceptance is recorded per the 2026-10-13 team-owner report;
meeting minutes and signatures are absent. AI-00 provider, shared-schema and
data-handling decisions and live provider/peer compatibility remain pending.
This is partial implementation, not v1 readiness. This v1 target
contract is derived from
[PROJECT_REQUIREMENTS.md](../../PROJECT_REQUIREMENTS.md). The
[v1 index](../v1/README.md) links separate contracts for each member's agent.
Google AI Studio Gemini is selected for the existing Safety Operations
adapter; the shared runtime/framework and provider choices for other agent
roles remain open. Use the [implementation blueprint](implementation-blueprint.md)
as the shared human-and-agent handoff; it distinguishes binding
safety/workflow requirements from decisions still open in ADRs.

**Implementation boundary:** use the [v1 component relationship map](../v1/component-relationships.md)
and [member branch workflow](../v1/member-branch-workflow.md) for ownership
and integration evidence. The team owner reported G07 acceptance on
2026-10-13; the repository tracker records that report, but meeting minutes
and signatures are absent. The Experience & Biodiversity runtime is a
stateless private Python HTTP service that reads through member-service
contracts. It does not own durable workflow state or provide cross-agent
orchestration. Planning persists execution checkpoints through the Planner
service; Safety produces a proposal but does not authorize or execute a
protected action. The broad runtime/framework and shared-state choices remain
open in ADR-0007/0008. The current implementations are provisional and do not
establish cross-agent compatibility or completed golden evaluations. See the
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

For Safety & Operations, the user's assessment submission is the workflow
entry point. The clients call only the authenticated public assessment-submit
operation; they do not call an agent endpoint, choose an agent, or send model
requests. Coastal Operations persists the submitted assessment and its
dispatch envelope, then its server-side delivery worker hands that envelope
to the private Agentic AI workflow after the post-G07 integration is enabled.
There is no public route for directly starting or invoking the Safety &
Operations Agent. Assessment status may report workflow availability and
outcome, but it does not expose a direct agent operation. The dispatch port
remains disconnected; a queued envelope must not be reported as an agent run
or proposal until the private Safety Operations integration is implemented
and enabled. The Member 1 role is a stateless private specialist and does not
persist agent-owned execution state. Member services expose only the accepted
coarse workflow/readiness state through the public contract; private probes
remain server-to-server. Keep this distinct from
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

The four provisional provider adapters use Google's Gemini `generateContent`
REST API with role-specific API keys. The Planning adapter also retains an
optional OpenAI-compatible implementation path. Agent-specific model calls
and Planner dispatch remain disabled by default; Safety additionally requires
an explicit data-handling approval and sends provider traffic through the
host-restricted egress proxy. Gemini is Adithya Gunawardana's selected
implementation preference, not a shared AI-00 approval. Shared provider/model
approval, data-processing review, specialist report schemas and runtime
decisions remain open in
[ADR-0007](../adr/ADR-0007-agentic-ai-framework.md) and
[ADR-0008](../adr/ADR-0008-agent-workflow-state.md). This adapter is a
provisional integration seam, not an accepted framework decision. Agent API
routes use `/internal/agentic/agents/{agent-id}/{operation}` namespaces. The
Planning Coordination dispatch route retains its established
`/internal/agentic/agents/planning-coordination/{health,dispatch}` routes;
private specialist routes now exist, but Experience and Marine response
contracts are not yet compatible with Planning's versioned specialist
envelope. The eventual orchestration framework remains undecided in
[ADR-0007](../adr/ADR-0007-agentic-ai-framework.md). Whether the agents run
in one private process or several does not change the public API, tool,
validation or approval boundaries.
