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
route. Experience and Marine have private role-specific HTTP APIs, but their
current request/response contracts do not yet match Planning's typed specialist
envelope. Safety Operations now has its own private dispatch endpoint and
typed proposal-only contract. End-to-end peer compatibility remains pending.
Coastal Planner persists checkpoints in
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

- Gemini is the default provider and uses Google's `generateContent` REST API
  with `x-goog-api-key`, JSON response schema, an 8-second timeout, hidden
  thought-part filtering and deterministic local plan validation. The key
  stays in the agent-specific environment variable and never enters a URL or
  log. The provider request does not include a `store:false` field; provider
  retention and data handling remain an AI-00 decision.
- Configure `PLANNING_COORDINATION_GEMINI_ENABLED=true`,
  `PLANNING_COORDINATION_GEMINI_MODEL` and
  `PLANNING_COORDINATION_GEMINI_API_KEY`. The adapter pins the Google AI
  Studio HTTPS `v1beta` origin; the model path is escaped before request.
- The optional OpenAI-compatible adapter uses only
  `PLANNING_COORDINATION_OPENAI_COMPATIBLE_BASE_URL`,
  `PLANNING_COORDINATION_OPENAI_COMPATIBLE_MODEL` and
  `PLANNING_COORDINATION_OPENAI_COMPATIBLE_API_KEY`.
- Provider selection is Adithya Gunawardana's requested implementation
  preference, not shared AI-00/provider approval or a data-processing review.
  Free-tier access, eligible models and quotas vary by Google project and
  change over time; users must check their AI Studio project limits.
- Google's billing documentation says free-tier inputs and outputs may be used
  to improve Google products. Keep the feature disabled until project data
  handling is reviewed; do not send personal or sensitive data through a
  free-tier key.
- Specialist role URLs and report names follow current v1 agent contracts.
  Partial Experience, Marine and Safety runtimes are present under
  `services/agents`, but the Experience and Marine response shapes do not yet
  match Planning's versioned specialist envelope. Cross-agent report schemas
  still need owner alignment and integration validation.
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
python -m unittest discover -s services/agents/planning-coordination/tests -v
```

Run endpoint catalog validation:

```powershell
python .agents/scripts/validate_endpoint_catalog.py --write-markdown
python .agents/scripts/validate_endpoint_catalog.py
```

The `agentic-ai` Compose profile contains the agent services and Gemini egress
proxy. Agent-specific Gemini calls and Planner dispatch remain disabled by
default. The direct E&B/Marine APIs still need adapters for Planning's
versioned specialist envelope before the golden workflow is compatible. Live
provider tests require each agent's distinct key, explicit enablement and the
shared data-handling review; no committed example contains secrets.
