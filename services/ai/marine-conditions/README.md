# Marine Conditions Intelligence Agent (Member 2 — Sanuda Abeysinghe)

- **Role:** Marine Conditions Intelligence Agent
- **Contract ID:** `v1.agent.marine-conditions`
- **Owner:** Sanuda Abeysinghe (`@sanudaabey`), Member 2
- **Branch:** `agentic-ai/marine-conditions`
- **Requirements Trace:** `PROJECT_REQUIREMENTS.md` sections 15, 20–27, 31, 40

## Overview

The Marine Conditions Intelligence Agent is a specialist and evidence summarizer that collects and interprets weather and marine context for a validated coastal objective, coordinates, activity, and time period.

### Core Boundaries

1. **Evidence Summarizer:** Produces a structured `MarineConditionsReport` preserving provider provenance, units, observation/forecast timestamps, freshness, missing fields, and objective relevance.
2. **Strict Non-Authority:**
   - Does not invent numbers or physical thresholds.
   - Does not decide or alter activity suitability (`SUITABLE`, `CAUTION`, `UNSUITABLE`, `UNKNOWN`).
   - Does not authorize operational continuation, suspension, cancellation, or alert issuance.
   - Includes non-negotiable navigation disclaimer on every report.
3. **Allowlisted Read Tools:**
   - `weather_forecast_lookup`: Retrieves weather factors (`wind_speed_10m`, `precipitation`, `weather_code`, `temperature_2m`).
   - `marine_forecast_lookup`: Retrieves marine factors (`wave_height`, `swell_wave_height`, `wave_direction`, `wave_period`).
   - `condition_snapshot_lookup`: Retrieves historical or cached condition snapshots.

## Running the Service

```bash
python -m services.ai.marine-conditions.app
```

Endpoints:
- `GET /health` — Liveness & readiness check
- `POST /invocations` (or `POST /report`) — Accepts `MarineConditionsAgentInput` and returns `MarineConditionsReport`

## Running Tests

```bash
pytest services/ai/marine-conditions/tests -v
```
