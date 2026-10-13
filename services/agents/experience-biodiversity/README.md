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
never implies open capacity. Every tool call forwards the caller's Bearer
token, and agent report POSTs reject requests without one.

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
The optional `GEMINI_API_KEY` enables Google AI Studio section selection.
Without it the service returns deterministic context. The model receives only
fixed section names and normalized status enums. Its output cannot author
report text or choose tools. The service has a healthcheck, no host-published
port and a 20-second API consumer deadline; the provider timeout is capped at
8 seconds.

## Running Tests

```bash
python -B -m pytest services/agents/experience-biodiversity/tests -v
```
