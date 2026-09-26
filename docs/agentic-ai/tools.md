# Agentic AI tools — v1 target

**Implementation status:** no executable Agentic AI tools are checked in.
The names below are candidate allowlisted operations from the v1
requirements, not API routes or callable production tools.

The [implementation blueprint](implementation-blueprint.md) defines the full
tool execution checklist, authoritative source map, model/retrieval boundary,
and pre-implementation decisions. Read it before introducing a new class of
tool such as document retrieval or RAG.

| Agent | Candidate read-only tools |
|---|---|
| Coastal Experience & Biodiversity | destination_lookup, activity_lookup, offering_lookup, schedule_lookup, biodiversity_prediction_lookup |
| Marine Conditions Intelligence | weather_forecast_lookup, marine_forecast_lookup, condition_snapshot_lookup |
| Safety & Operations | safety_profile_lookup, operational_status_lookup, active_alert_lookup, operational_constraint_lookup |
| Planning & Coordination | Delegates configured steps to specialist agents; it cannot create or grant itself tools. |

The candidate `biodiversity_prediction_lookup` belongs to the post-G07
Coastal Experience & Biodiversity Agent. Its backend handler may call only
the validated Adithya Gunawardana (Member 3) prediction contract; Adithya Gunawardana (Member 3) owns the ordinary v1
IT3091 service adapter on its `features/**` branch. The agent and tool do not
call the private IT3091 service directly. Keep prediction unavailability,
invalidity, provenance and uncertainty explicit. See
[ADR-0019](../adr/ADR-0019-biodiversity-inference-integration-ownership.md)
and the [Ushan Srinuka (Member 1) agent contract](../v1/agents/member-1-coastal-experience-biodiversity-agent.md).

For every tool actually introduced, document its purpose, authorized
agent(s), typed input, structured output, source or owning service,
validation, allowed side effects, timeout, bounded retry, failure response,
auditable summary and prompt-injection boundary. Keep access least
privileged. The agent cannot call arbitrary network targets, request
unnecessary secrets or use a tool outside its allowlist.

An auditable tool summary must correlate the workflow, step, agent and tool;
record the source/target category, start/end or elapsed duration, validated
outcome, error and retry count where relevant; and omit credentials, raw
unnecessary personal data and hidden model reasoning. The exact fields and
retention policy belong to the implementation schema. These timings support
failure diagnosis and the assignment's observability/performance evidence.

Lookup results and external responses are untrusted data. Validate tool
inputs before execution and outputs before they enter downstream planning
or business validation. No agent tool directly publishes, suspends,
cancels or modifies a protected BLUEVERSE record. The owning member service
owns authorized business execution after the public API's authentication and
permission integration, deterministic validation and required human
approval. For Wanshaja Sooriyabandara (Member 4) protected operations, that means the Wanshaja Sooriyabandara (Member 4) service.

See the [four agent contracts](../v1/README.md),
[safety controls](safety.md) and
[requirements sections 13, 15, 19 and 25](../../PROJECT_REQUIREMENTS.md).
