# Coastal Experience & Biodiversity Agent (Member 1 — Ushan Srinuka)

Executable specialist Agentic AI service for the Coastal Experience & Biodiversity domain (`services/agents/experience-biodiversity`), governed by:

- [`docs/v1/agents/member-1-coastal-experience-biodiversity-agent.md`](../../../docs/v1/agents/member-1-coastal-experience-biodiversity-agent.md)
- [`docs/v1/g00/member-1-experience-biodiversity.md`](../../../docs/v1/g00/member-1-experience-biodiversity.md)
- [`docs/adr/0019-coastal-planner-biodiversity-prediction-integration.md`](../../../docs/adr/0019-coastal-planner-biodiversity-prediction-integration.md)

## Role & Safety Boundary

- **Read-Only Specialist Context Provider**: Assembles evidence-grounded `ExperienceBiodiversityContextReport` payloads for destinations, activities, offerings, schedules, and optional biodiversity predictions.
- **No Direct Client Exposure**: Runs exclusively on the internal `blueverse_internal` network; React and Flutter clients never call this service directly.
- **Strict Separation of Statuses**: Preserves distinct `publication_status` (`PUBLISHED`, `DRAFT`, `ARCHIVED`), `schedule_status` (`SCHEDULED`, `UNSCHEDULED`, `NOT_EVALUATED`), `availability_status` (`AVAILABLE`, `UNAVAILABLE`, `UNKNOWN`, `NOT_EVALUATED`), and `operational_restriction` context.
- **Non-Hallucinated Biodiversity Enrichment (ADR-0019)**: Validates `destinationId` match, model provenance (`modelVersion`, `provider`), and `habitatSuitability` ($[0.0, 1.0]$) before surfacing species predictions. Unavailable, stale, or malformed predictions return `predictions=[]` with explicit uncertainty notes.

## Allowlisted Tools

1. `destination_lookup` — `GET /api/experiences/destinations/{id}`
2. `activity_lookup` — `GET /api/experiences/activities/{id}`
3. `offering_lookup` — `GET /api/experiences/offerings/{id}`
4. `schedule_lookup` — `GET /api/experiences/offerings/{id}/schedules`
5. `biodiversity_prediction_lookup` — `GET /api/planner/biodiversity/predictions?destinationId={id}`

Schedule DTOs do not contain seat availability. The agent reports schedule
coverage separately and uses the experience service's interval availability
endpoint before marking a requested period `AVAILABLE`; a date-only request
never implies open capacity. Tourist report requests continue to use the
caller Bearer token and existing detail tools. For callerless operational
assessment dispatch, the private service may instead authenticate with
`X-Blueverse-Service-Key`; that path uses only
`GET /api/experiences/catalogue`, which returns published, minimized context,
and does not call the planner biodiversity endpoint or forward a user token.
The service key is checked against `AGENTIC_SERVICE_KEY` and is never stored in
workflow state or report data.

## Internal HTTP Endpoints

- `GET /health` — Container health probe
- `GET /api/experiences/agent/health` — Namespaced container health probe
- `GET /api/experiences/agent/info` — Agent identity, owner metadata, model configuration, and tool allowlist
- `POST /api/experiences/agent/context-reports` — Validates `ExperienceAgentInput` and returns `ExperienceBiodiversityContextReport`
- `POST /api/experiences/agent/generate` — Compatibility alias for context report generation

The `/api/experiences/agent/...` prefix is required to avoid route collisions
with the other member agents.

## Local Compose runtime

Start the ordinary `dev` stack, then run
`docker compose --profile agentic-ai up -d agent-experience-biodiversity`.
Gemini section selection is opt-in. Set
`EXPERIENCE_BIODIVERSITY_GEMINI_ENABLED=true` and configure this agent's
`EXPERIENCE_BIODIVERSITY_GEMINI_API_KEY`. It does not read a shared Gemini key.
Configure the same internal `AGENTIC_SERVICE_KEY` used by Planning Coordination
for callerless operational assessment dispatch.
The model is selected with `EXPERIENCE_BIODIVERSITY_GEMINI_MODEL`; provider
calls use Google's `generateContent` endpoint and send the key in the
`x-goog-api-key` header.
Without it the service returns deterministic context. The model receives only
fixed section names and normalized status enums. Its output cannot author
report text or choose tools. The service has a healthcheck, no host-published
port and a 20-second API consumer deadline; the provider timeout is capped at
8 seconds.

## Running Tests

```bash
python -B -m pytest services/agents/experience-biodiversity/tests -v
```
