# BLUEVERSE v1 shared workflows

This page describes target workflows from
[PROJECT_REQUIREMENTS.md](../../PROJECT_REQUIREMENTS.md), sections 20–27 and
41. No v1 Agentic AI service or Agentic AI endpoint is implemented in the
current foundation. The public API and persisted state remain authoritative.
Before G07, member features implement the public business-workflow API and
the private backend integration seam, including a structured not-connected
or unavailable outcome. They do not implement agents, orchestration or
agent-owned execution state. See the
[member integration boundary](agentic-ai-integration-boundary.md).

The operational-assessment and tourist workflows below describe the complete
post-G07 target, including the four executable agent roles. Before G07, run
the member-owned business behavior and represent the AI-dependent portion as
not connected/unavailable; do not create an agent step, model result or
AI-owned execution state. The [implementation blueprint](../agentic-ai/implementation-blueprint.md)
defines the post-gate model/tool/retrieval design work.

Platform-specific device controls and media-input boundaries are defined in
the [v1 device-capability contract](device-capabilities.md). They do not
change the shared workflow, permission or source-of-truth semantics below.

## Canonical assessed operational assessment

The evaluation path starts with a Coastal Operator using Flutter and ends with
an Operations Reviewer using React. Both roles' permitted actions and status
views must also be available in both clients.

1. The operator selects a destination, activity or offering and relevant
   period, enters an operational objective, and may attach optional image
   evidence to the assessment.
2. ASP.NET Core authenticates, checks the required permission, validates the
   request, validates any optional image evidence, persists the objective and
   creates a workflow ID. Attachment bytes remain private and are exposed only
   through authorized API operations; an upload failure is never reported as
   a successful attachment.
3. The [Planning & Coordination Agent](agents/member-3-planning-coordination-agent.md)
   persists a structured plan with assigned agents, dependencies, tools and
   expected outputs.
4. The [Marine Conditions Intelligence Agent](agents/member-2-marine-conditions-intelligence-agent.md)
   returns sourced weather and marine context with time, freshness and missing
   data. The [Coastal Experience & Biodiversity Agent](agents/member-1-coastal-experience-biodiversity-agent.md)
   returns offering, schedule and optional biodiversity context.
5. The [Safety & Operations Agent](agents/member-4-safety-operations-agent.md)
   proposes a structured outcome and any operational action.
6. Application code independently validates schemas, safety profiles,
   freshness, availability, current state, allowed transitions, authorization
   and approval requirements. AI cannot override a blocked result.
7. A high-impact proposal pauses for an authorized reviewer to approve,
   reject or request revision. Rejection and revision record a decision but do
   not execute the proposed protected action.
8. For an eligible approval, ASP.NET Core rechecks applicable business rules,
   performs the permitted transactional state change and records audit history.
9. The initiating operator and other authorized viewers receive the same
   updated status through the public API in either client.

Representative proposals include CONTINUE, CONTINUE_WITH_CAUTION,
TEMPORARILY_SUSPEND, CANCEL_SESSION and INSUFFICIENT_DATA. Final enum values,
transition rules, expiry and concurrency behavior belong to the technical
contract. BLUEVERSE actions apply only to BLUEVERSE-managed operations.

## Tourist recommendation and itinerary

A Tourist submits coastal preferences and constraints—including a selected
date/time and duration—through React or Flutter. The planning agent
coordinates the marine and experience agents,
applies deterministic availability, operational and safety constraints, and
returns a recommendation. The tourist may create and later re-evaluate an
itinerary when relevant conditions change.

Normal tourist recommendations do not require staff approval. A final
accepted recommendation cannot reintroduce an activity classified as
unsuitable for the requested period. Missing required marine information is
exposed as uncertainty, never invented.

## Shared state and failure

Persist the workflow ID and type, initiator, objective, plan, status,
completed/current steps, structured outputs or auditable tool summaries,
validation, errors, bounded retries, approval decision, result and timestamps
as needed. Include correlated step/tool-call outcome and elapsed time where
needed for audit, recovery and performance evidence. Never store hidden model
reasoning, tokens or secrets.

The Member 3/4 business request, assessment, proposal and reviewer-decision
records may exist before G07 and must remain distinguishable from the later
Agentic AI plan, step and tool-execution state. If the private Agentic AI
dependency is absent or unavailable, persist and return the defined safe
business-workflow/dependency outcome; do not claim an agent completed or
fabricate its output. API liveness and database readiness remain separate
from Agentic AI availability.

Malformed model output, unsafe tools, stale or missing conditions, dependency
timeouts, unavailable biodiversity inference and approval rejection need
explicit outcomes. Exhausted recovery records SAFE_FAILURE. No protected side
effect may occur after safe failure.
