# Planning & Coordination implementation

**Status:** partial AI-02 implementation on `agentic-ai/planning-coordination`.
G07 acceptance was recorded at Adithya Gunawardana's direction on 2026-10-13.
That approval does not accept AI-00 decisions or establish runtime evidence.

## Runtime boundary

Coastal Planner remains the business authority. It can dispatch a minimized
recommendation context to the private Planning & Coordination service when
`AgenticAi:Enabled` is true. The result is execution context only. It cannot
change candidate eligibility, availability, marine suitability, safety,
operations status, user authorization or itinerary state.

The AI service has private liveness, readiness and dispatch routes. It has no
public port. Coastal Planner persists checkpoints in
`coastal_planner.agent_execution_states` through
`PUT /internal/agentic/executions/{workflowId}`. That route requires
`X-Blueverse-Service-Key`, an existing Planner workflow and a versioned state
whose workflow ID matches the route. A second dispatch with a different
correlation ID is rejected. Agentic AI has no database credentials.

## State and failure behavior

| State | Meaning |
|---|---|
| `PLANNING` | Initial state saved before requesting a plan. |
| `DELEGATING` | Plan passed deterministic validation; specialist steps are being run. |
| `BLOCKED` | Required specialist dependency is missing, unavailable or invalid. No final result is assembled. |
| `COMPLETED` | Required specialist reports passed envelope, identity and provenance checks. This does not certify business eligibility or safety. |
| `UNAVAILABLE` | Model, persistence or required service dependency is unavailable. |
| `SAFE_FAILURE` | Input or model plan failed deterministic validation, or execution could not safely continue. |

Model calls are limited to 8 seconds, specialist calls to 5 seconds and
Planner checkpoint writes to 1 second; Planner cancels dispatch after 35
seconds. No retries are automatic. This avoids duplicate model or specialist calls
while duplicate-dispatch behavior and recovery are still open in AI-00. A
bounded request timeout applies at each model and specialist adapter. A
failure never fabricates a report. No hidden reasoning, credentials or
unnecessary user data are stored.

## Provisional integration assumptions

- Model adapter speaks OpenAI-compatible chat completions and JSON mode. It
  runs only when base URL, model name and API key are supplied at runtime.
- Only an institution-provided or no-cost model may be configured after data
  handling review. No provider or model is approved by this implementation.
- Specialist role URLs and report names follow current v1 agent contracts.
  Those agent services and exact report schemas are not yet implemented.
- Reports require matching workflow, step and agent identities, expected
  report type, `SUCCEEDED`, source references, uncertainty notes and a
  timezone-aware production timestamp. Report bodies remain bounded JSON;
  role-specific field validation awaits specialist-owner schemas.
- The runtime stores checkpoints in Planner to keep PostgreSQL access inside
  the Planner service. Physical ownership remains subject to ADR-0008 review.

These assumptions require review under ADR-0007/ADR-0008 and AI-00. The
implementation is disabled by default with `AGENTIC_AI_ENABLED=false`.

## Local validation

Run deterministic contract tests:

```powershell
python -m unittest discover -s services/ai/planning-coordination/tests -v
```

Run endpoint catalog validation:

```powershell
python .agents/scripts/validate_endpoint_catalog.py --write-markdown
python .agents/scripts/validate_endpoint_catalog.py
```

The `agentic-ai` Compose profile contains three specialist image shells.
Dispatch therefore returns explicit unavailable/blocked state until those
owners implement compatible services. Live model, PostgreSQL migration and
Compose evidence require configured dependencies and Docker.
