# Agentic AI architecture — v1 target

**Implementation status:** the repository has no executable Agentic AI
service, agent or tool. This is the v1 target contract derived from
[PROJECT_REQUIREMENTS.md](../../PROJECT_REQUIREMENTS.md). The
[v1 index](../v1/README.md) links separate contracts for each member's agent.

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

React and Flutter call only the public ASP.NET Core API. ASP.NET Core
authenticates, authorizes, validates and persists a workflow before invoking
private orchestration. The planning agent delegates to the marine and
experience agents; the safety agent consumes their structured results for
the operational-assessment path. Application code then performs
deterministic validation. A high-impact proposal pauses for authorized
human approval. ASP.NET Core alone executes an eligible approved action
and records history.

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

Persist only the workflow ID/type, initiator, objective, structured plan,
status, steps, structured outputs or auditable tool summaries, validation,
errors/retries, approval and final result with timestamps as needed. Do not
persist hidden reasoning, credentials or unnecessary sensitive data.
Exact schemas and service boundaries are implementation decisions to be
recorded in the relevant ADRs and database documentation.

The eventual orchestration framework remains undecided in
[ADR-0007](../adr/ADR-0007-agentic-ai-framework.md). Whether the agents run
in one private process or several does not change the public API, tool,
validation or approval boundaries.
