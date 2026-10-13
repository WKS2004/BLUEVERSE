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
python services/agents/marine-conditions/app.py
```

Endpoints:
- `GET /health` — Liveness & readiness check
- `POST /invocations` (or `POST /report`) — Accepts `MarineConditionsAgentInput` and returns `MarineConditionsReport`

## Running Tests

```bash
pytest services/agents/marine-conditions/tests -v
```

## Gemini configuration

The service uses `MARINE_CONDITIONS_GEMINI_API_KEY` and
`MARINE_CONDITIONS_GEMINI_MODEL` (default:
`gemini-3.6-flash`). It does not read the shared `GEMINI_API_KEY` or
`GEMINI_MODEL` variables. `MARINE_CONDITIONS_GEMINI_BASE_URL` optionally
overrides the Google API base URL. Any configured base URL must be the official
Google HTTPS `generativelanguage.googleapis.com/v1beta` endpoint.

Environment-based model calls are disabled by default. Set
`MARINE_CONDITIONS_GEMINI_ENABLED=true` only after the data-handling review
approves sending the agent's evidence to Gemini. Without that opt-in, the
service uses its deterministic synthesis path. Tests can still inject a
`GeminiClient` explicitly to exercise model response handling without enabling
runtime calls.

The live connection check sends a real request and is gated by the same opt-in:

```bash
MARINE_CONDITIONS_GEMINI_ENABLED=true python services/agents/marine-conditions/check_gemini.py
```
