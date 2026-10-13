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

The AI service has private liveness, Planning Coordination readiness and
dispatch routes under agent-specific namespaces. It has no public port. Each
specialist adapter targets a distinct `/internal/agentic/agents/{agent-id}/dispatch`
route, so future agent services can use separate route namespaces. Experience,
Marine and Safety endpoints remain target paths; those services are not yet
implemented. Coastal Planner persists checkpoints in
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

## Gemini integration and provider configuration

- Gemini is the default provider and uses Google's Interactions REST API with
  `x-goog-api-key`, schema-constrained JSON output, `store:false`, an 8-second
  timeout and deterministic plan validation. The API key stays in an
  environment variable and never enters a URL or log.
- Configure `AGENTIC_MODEL_NAME` and `AGENTIC_MODEL_API_KEY`. The Gemini API
  base defaults to Google AI Studio's `v1beta` endpoint and may be overridden
  by `AGENTIC_GEMINI_API_BASE_URL` for compatible test servers.
- `AGENTIC_MODEL_PROVIDER=openai-compatible` retains the earlier adapter;
  configure `AGENTIC_MODEL_BASE_URL`, model and key for that option.
- Provider selection is Adithya Gunawardana's requested implementation
  preference, not shared AI-00/provider approval or a data-processing review.
  Free-tier access, eligible models and quotas vary by Google project and
  change over time; users must check their AI Studio project limits.
- Google's billing documentation says free-tier inputs and outputs may be used
  to improve Google products. Keep the feature disabled until project data
  handling is reviewed; do not send personal or sensitive data through a
  free-tier key.
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
