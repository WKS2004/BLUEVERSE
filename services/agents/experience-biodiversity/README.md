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

## Internal HTTP Endpoints

- `GET /health` — Container health probe
- `GET /api/agent/info` — Agent identity, owner metadata, and tool allowlist
- `POST /api/agent/generate` — Validates `ExperienceAgentInput` and returns `ExperienceBiodiversityContextReport`

## Running Tests

```bash
python -B -m pytest services/agents/experience-biodiversity/tests -v
```
