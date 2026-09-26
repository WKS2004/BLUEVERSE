# Member feature integration with Agentic AI

This contract defines the work that belongs to the four member-owned business
features before the executable Agentic AI implementation begins. Use it with
the [component relationship map](component-relationships.md), the
[member branch and integration workflow](member-branch-workflow.md),
[Agentic AI architecture](../agentic-ai/architecture.md),
[shared workflow contract](workflows.md), and the owning
[component](README.md#component-and-agent-contract-map) and [agent](README.md#component-and-agent-contract-map)
contracts.

## Status and governing boundary

The current repository has no executable Agentic AI service, agent, tool or
AI endpoint in the endpoint catalog. This document describes required future
integration behavior; it does not claim that the integration or health check
already exists. The repository requirements and implemented sources remain
authoritative. Any exact public route, internal service URL, DTO, status code,
availability enum and retry policy must be finalized in the owning contract
at G00 and implemented in source before it is described as live.

The v1 work has two separate deliveries:

1. **Member feature delivery, before G07:** each member implements its own
   internal .NET component service and the minimum `services/api` integration
   needed to expose that service through the public API. The member service
   owns business/workflow identity and status persistence, its typed private
   adapter to the future Agentic AI service, dependency configuration and
   availability handling, and the safe unavailable response. The API owns
   only public authentication/permission and route/forwarding integration.
   The seam may be unconfigured or report `NOT_CONNECTED` until the service
   exists. A member may verify the seam with test doubles, but must not ship a
   fake agent or fabricated AI result as production behavior.
2. **Agentic AI delivery, only after G07:** implement the actual private
   Agentic AI runtime, agent roles, prompts/model calls, tools, orchestration,
   agent-owned execution state and evaluations through `agentic-ai/**`
   branches. The AI implementation then connects through the backend boundary
   prepared by the member features.

The Agentic AI service remains private. React and Flutter call only the
public ASP.NET Core API. No client calls a private AI hostname, tool, model
provider, member-service hostname or internal health endpoint. `services/api`
authenticates/authorizes the caller using the existing model and routes or
forwards the operation to the owning member service. That component service
validates domain input, persists its business workflow and mediates any future
private AI dispatch; its logic does not move into `services/api`. Use the
[member service boundary](../adr/ADR-0020-member-component-service-boundaries.md)
and G00 contracts for the internal transport and identity/permission context.

## What member branches implement

Each component owner prepares the complete backend-to-AI integration point
for its paired role while implementing the ordinary feature:

| Member feature | Paired future AI role | Member-branch integration work |
|---|---|---|
| Member 1 — Coastal Experience & Biodiversity Discovery | Experience & Biodiversity specialist | Authorized public workflow initiation/status contract; validated references to Member 1's catalogue and availability data; a typed private dispatch/result adapter; provenance and explicit not-connected/unavailable state for the optional Agentic report. |
| Member 2 — Marine Conditions & Safety Intelligence | Marine Conditions specialist | Authorized public workflow initiation/status contract; validated access to normalized conditions and deterministic suitability; typed private dispatch/result adapter; source/freshness references and explicit not-connected/unavailable state. The agent cannot set or override safety thresholds. |
| Member 3 — Smart Coastal Planner & Itinerary Management | Planning & Coordination agent | Public request, shared workflow ID/status/result and itinerary contracts; durable business request state; typed private orchestration adapter and dependency status; safe behavior when the planner or specialist dependencies are not connected. Deterministic recommendations and itineraries remain usable independently. |
| Member 4 — Coastal Operations, Advisories & Alerts | Safety & Operations agent | Public assessment/status/proposal contracts; durable business assessment and proposal references; typed private dispatch adapter; explicit unavailable status. The Member 4 service enforces authorized decisions and protected execution behind the public API boundary. |

Each member branch owns the public business contract and implements its
business operation and private-service client seam inside its own internal
component service. Connect that service through `services/api` using only
necessary public route/forwarding and identity integration; preserve existing
API and Auth flows. The later
`agentic-ai/**` implementation owns the actual runtime, agent execution and
agent-side contracts. Shared transport schemas, correlation fields, workflow
identifiers and status meanings must be agreed at G00 so independently
developed branches converge on the same contract. The detailed service and
client shared-file boundary is in the [member branch workflow](member-branch-workflow.md#shared-foundation-and-file-ownership).

## Keep business workflow state distinct from AI execution state

Before G07, Members 3 and 4 may persist ordinary business workflow records:
requests, assessment identity, initiator and permissions, status, proposal
references, outcome version, timestamps and safe dependency-failure status.
Members 1 and 2 persist their component-owned data and any public workflow
request/status needed for their paired report. These records are owned by the
business component and remain meaningful if no agent has ever run.

The pre-G07 feature work does **not** implement an AI plan, agent steps,
prompt/model history, tool-call execution, agent-owned recovery state or
orchestration. Those are Agentic AI work after G07. Do not label a business
request row as proof of an AI run. Keep the common workflow ID and public
status contract compatible with the future AI state, while representing
business status and AI dependency/execution status as distinguishable data.
Never persist hidden model reasoning, credentials or unnecessary personal
data.

## Dependency availability and safe failure

Treat Agentic AI as an optional, independently monitored backend dependency
while its runtime is absent or unavailable. The API must distinguish at least
these meanings in its accepted implementation contract:

- **Not connected:** no private endpoint/runtime is configured, deployed or
  registered yet. Before G07 this is an expected state. It is not a successful
  AI result and should not create a completed-agent status.
- **Unavailable:** a configured dependency cannot be reached, a bounded
  health check fails, or a dispatch times out, is refused or fails transport
  validation. Return a safe, structured failure/dependency state with an
  explicit retryability decision; do not invent output.
- **Available:** the private dependency passed its bounded availability
  check. A subsequent dispatch can still fail, so the dispatch result remains
  authoritative for that request.

Freeze the exact state names and mapping during G00. A public response or
workflow status must let an authorized client understand that the AI work is
not connected or is unavailable and what safe next step is supported. It
must not disclose private hostnames, credentials, stack traces, internal
network details or hidden reasoning. Keep correlation/workflow identity so
the same request can be inspected; do not mark it successful or persist a
fabricated recommendation, assessment or proposal.

The readiness behavior follows the repository's existing distinction:

- `GET /api/health` currently reports public API process liveness. Preserve
  that meaning; an optional AI outage must not make a live API appear dead.
- `GET /api/auth/health` currently reports Auth and database readiness. Use
  that service-owned dependency-status pattern as a reference, but keep
  database health separate from Agentic AI dependency health; an AI outage
  must not be reported as a database failure.
- The backend must add an AI-specific, server-side availability/readiness
  check against the configured private dependency, similar in purpose to a
  database dependency check. It should report a coarse connected,
  not-connected, unavailable or equivalent state, use a short bounded
  timeout, and avoid blocking service startup or unrelated requests when the
  optional runtime is absent.
- Decide at G00 whether the check is exposed through a general readiness
  diagnostic or the member workflow status only. Do not invent or publish a
  new public AI endpoint in this guide. If an implemented public route is
  added, register it in the endpoint catalog and any affected UI in the
  shared UI registry before it can pass CI. The private AI probe remains
  server-to-server.
- Check availability at dispatch as well as through readiness. A health
  result is a point-in-time observation and cannot guarantee that the next
  operation will succeed. Bound timeouts/retries, preserve idempotency, and
  record the final safe outcome.

Only the AI-dependent operation is unavailable when the AI service is not
connected. Continue ordinary catalogue, condition acquisition, deterministic
suitability, availability, deterministic recommendations, itineraries,
assessment review, approval and other authorized non-AI behavior when their
own dependencies are healthy. Never let an AI outage bypass Member 4's
authorization, revalidation, approval or audit rules.

## Required pre-G07 acceptance evidence

Each member's feature PR documents and verifies its own boundary with
contract-level tests or equivalent evidence for:

- unconfigured/not-connected private dependency;
- configured service that is unreachable or fails its bounded health probe;
- timeout/refusal during dispatch after a recent healthy probe;
- malformed, invalid or unauthorized dispatch/result data;
- safe status, retryability, correlation and persisted business-workflow
  behavior without a fabricated AI output;
- continued operation of unrelated deterministic feature behavior; and
- no protected operation, approval or state mutation caused by an unavailable,
  malformed or untrusted AI result.

Tests may use controlled transport doubles. They verify the member-owned
adapter and failure contract; they do not implement or count as the actual
agent/runtime. At G07, reviewers verify all four feature boundaries against
the same G00 contract. After G07, AI branches provide live integration and
golden-flow evidence through those boundaries.
