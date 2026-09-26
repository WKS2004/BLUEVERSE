# BLUEVERSE v1 shared workflows

This page describes target workflows from
[PROJECT_REQUIREMENTS.md](../../PROJECT_REQUIREMENTS.md), sections 20–27 and
41. No v1 Agentic AI service or Agentic AI endpoint is implemented in the
current foundation. The public API and owning member-service state remain
authoritative.
Before G07, each member implements its private .NET component service, its
public business-workflow API integration through `services/api`, and the
private Agentic AI access seam, including a structured not-connected or
unavailable outcome. They do not implement agents, orchestration or
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
2. `services/api` authenticates the caller, applies the existing permission
   contract and routes the operation to Wanshaja Sooriyabandara's private service. The Member
   4 service validates the domain request and optional image evidence,
   persists the objective and creates the business workflow ID. Attachment
   bytes remain private and are exposed only through authorized public API
   operations; an upload failure is never reported as a successful
   attachment.
3. The [Planning & Coordination Agent](agents/member-3-planning-coordination-agent.md)
   creates a structured execution plan with assigned agents, dependencies,
   tools and expected outputs. The private Agentic AI runtime persists the
   logical execution state after G07 using its accepted state boundary.
4. The [Marine Conditions Intelligence Agent](agents/member-2-marine-conditions-intelligence-agent.md)
   returns sourced weather and marine context with time, freshness and missing
   data. The [Coastal Experience & Biodiversity Agent](agents/member-1-coastal-experience-biodiversity-agent.md)
   returns offering, schedule and optional biodiversity context.
   When that context needs a prediction, the Ushan Srinuka (Member 1) agent obtains it through
   Adithya Gunawardana's validated public/typed contract; only Adithya Gunawardana's private
   backend adapter calls IT3091.
5. The [Safety & Operations Agent](agents/member-4-safety-operations-agent.md)
   proposes a structured outcome and any operational action.
6. Application code independently validates schemas, safety profiles,
   freshness, availability, current state, allowed transitions, authorization
   and approval requirements. AI cannot override a blocked result.
7. A high-impact proposal pauses for an authorized reviewer to approve,
   reject or request revision. Rejection and revision record a decision but do
   not execute the proposed protected action.
8. For an eligible approval, the Wanshaja Sooriyabandara (Member 4) service rechecks current business
   rules and state, performs the permitted transactional change and records
   audit history after the public API has authenticated and authorized the
   operation.
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

The owning member service persists its business workflow ID/type, initiator,
objective, business status, component state, approval decision, result and
necessary timestamps. After G07, the Agentic AI runtime owns the logical plan,
agent-step/tool execution and recovery state; its physical storage boundary
and call path are finalized through ADR-0008 and the accepted service
contracts. Correlate both records using the agreed workflow/correlation IDs.
Include structured outcomes, bounded retries and elapsed time where required
for audit, recovery and performance evidence. Never store hidden model
reasoning, tokens or secrets.

The Adithya Gunawardana and Wanshaja Sooriyabandara business request, assessment, proposal and reviewer-decision
records may exist before G07 and must remain distinguishable from the later
Agentic AI plan, step and tool-execution state. If the private Agentic AI
dependency is absent or unavailable, persist and return the defined safe
business-workflow/dependency outcome; do not claim an agent completed or
fabricate its output. API liveness, member-service health, database readiness
and Agentic AI availability remain distinct states.

Malformed model output, unsafe tools, stale or missing conditions, dependency
timeouts, unavailable biodiversity inference and approval rejection need
explicit outcomes. Exhausted recovery records SAFE_FAILURE. No protected side
effect may occur after safe failure.
