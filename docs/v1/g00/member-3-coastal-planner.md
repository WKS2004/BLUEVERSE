# G00 Decisions — Smart Coastal Planner & Itinerary Management

**Document status:** Member 3 proposal for shared G00 review. This record
captures the Smart Coastal Planner & Itinerary Management inputs for the G00
Shared Contract Freeze. It is not team-accepted until all four owners agree.

- **Component:** Smart Coastal Planner & Itinerary Management (Member 3)
- **Assigned owner:** Adithya Gunawardana (`@AdithyaGunawardana`)
- **Feature branch:** `features/coastal-planner`
- **Agentic AI branch (post-G07):** `agentic-ai/planning-coordination`
- **Owner component contract:** [`docs/v1/components/member-3-smart-coastal-planner-itinerary-management.md`](../components/member-3-smart-coastal-planner-itinerary-management.md)
- **Paired Agentic contract:** [`docs/v1/agents/member-3-planning-coordination-agent.md`](../agents/member-3-planning-coordination-agent.md)

**2026-10-05 consumer implementation note:** The Member 3 branch now implements
the planner catalogue projection and durable review-history reads, destination
time zones, explicit search outcomes, snapshot-validated saves and a bounded,
non-executing private AI availability seam. The exact source payload additions
and routes are documented in [the planner guide](../../development/coastal-planner.md)
and the endpoint catalogue. They require owner confirmation; this note does
not change the pending team acceptance status or authorize post-G07 execution.
React planner screens are implemented; Flutter routes remain planned in the
shared UI registry.

---

## 1. Scope and G00 objectives

This document records the Member 3 contracts required to satisfy the
[G00 exit criteria](../requirements-coverage-and-readiness.md#g00-exit-criteria):

1. Shared identities, canonical identifiers and cross-component handoffs.
2. Candidate public API routes, DTOs, authorization permissions and error semantics.
3. Private service identity, Docker networking, gateway forwarding and health semantics.
4. Database schema boundaries, persistence invariants, concurrency and time conventions.
5. External ML adapter seam (IT3091 biodiversity inference) and pre-G07 Agentic AI boundaries.
6. Proposed client workflows and React/Flutter experience parity.

---

## 2. Shared identities and cross-component handoffs

### 2.1 Member 3 owned entities and identifiers

All business identifiers are canonical UUIDs (RFC 4122 v4):

| Identifier | Entity | Scope and authority |
|---|---|---|
| `workflowId: guid` | Planning workflow | Business request/workflow identity for tracking recommendation requests, status, results and audit trails. Shared across components. |
| `recommendationId: guid` | Recommendation session / result | Ephemeral or persisted recommendation candidate assembly output. |
| `itineraryId: guid` | Saved itinerary | User-owned collection of planned coastal activities with dates, order and notes. |
| `itemId: guid` | Itinerary item | Line item referencing a destination/activity/offering in an itinerary. |
| `predictionId: guid` | Biodiversity prediction snapshot | Validated ML inference result snapshot from IT3091. |

### 2.2 Inbound dependencies consumed by Member 3

Member 3 consumes authoritative facts from peer components; it never creates competing records:

- **From Ushan Srinuka (Member 1 — Experience Catalogue):**
  - Canonical `destinationId: guid`, `activityId: guid`, `offeringId: guid`, `scheduleId: guid`.
  - Effective availability: `AVAILABLE`, `UNAVAILABLE`, `UNKNOWN`.
  - Catalogue publication state: `DRAFT`, `PUBLISHED`, `ARCHIVED`. Only `PUBLISHED` and `AVAILABLE` offerings can be suggested or scheduled.
  - Invariant: A missing or stale Member 1 status yields `UNKNOWN`; the planner never assumes availability.
- **From Sanuda Abeysinghe (Member 2 — Marine Conditions & Safety Intelligence):**
  - Deterministic suitability assessment: `SUITABLE`, `CAUTION`, `UNSUITABLE`, `UNKNOWN`.
  - Marine safety profile ID/version, condition timestamp, freshness and missing factors.
  - Invariant: An activity classified as `UNSUITABLE` for a requested period is deterministically excluded from recommendations and flagged in re-evaluations. The planner never weakens thresholds or overrides `UNSUITABLE`.
- **From Wanshaja Sooriyabandara (Member 4 — Coastal Operations, Advisories & Alerts):**
  - Authoritative operational state: `OPEN`, `CAUTION`, `TEMPORARILY_SUSPENDED`, `CANCELLED`, `COMPLETED`.
  - Active operational alerts and restrictions.
  - Invariant: Operations under `TEMPORARILY_SUSPENDED` or `CANCELLED` are excluded from candidate assembly.

### 2.3 Outbound dependencies produced by Member 3

- **To Ushan Srinuka (Member 1 — Experience & Biodiversity Discovery):**
  - Validated biodiversity prediction context via public endpoint `GET /api/planner/biodiversity/predictions`.
  - Contract provides focal species, location, prediction timestamp, model/version, probability/habitat suitability, uncertainty and limitations.
  - Sourced from private IT3091 adapter; Member 1 uses this for user-facing experience presentation.
- **To Wanshaja Sooriyabandara (Member 4 — Coastal Operations):**
  - Business workflow reference (`workflowId: guid`, objective, initiator, target references, period) linked to operational assessments and incident reviews.

---

## 3. Candidate public API routes, DTOs, and error semantics

All public routes use relative `/api/...` paths without version segments. Clients communicate only via the public API.

### 3.1 Candidate endpoints

| HTTP Method and Path | Purpose | Required Permission |
|---|---|---|
| `POST /api/planner/recommendations` | Submit coastal preferences/constraints and generate recommendation candidates | `planner.recommendations.create` |
| `GET /api/planner/recommendations/{recommendationId:guid}` | Retrieve recommendation candidate details, suitability, and evidence | `planner.recommendations.read` |
| `GET /api/planner/workflows/{workflowId:guid}` | Get status and lifecycle of a planning workflow | `planner.workflows.read` |
| `POST /api/planner/itineraries` | Create a user-owned saved itinerary | `planner.itineraries.manage` |
| `GET /api/planner/itineraries` | List the caller's saved itineraries (paginated) | `planner.itineraries.manage` |
| `GET /api/planner/itineraries/{itineraryId:guid}` | Retrieve a specific itinerary with ordered items and latest status | `planner.itineraries.manage` |
| `PUT /api/planner/itineraries/{itineraryId:guid}` | Update itinerary details, reorder items, or adjust dates | `planner.itineraries.manage` |
| `DELETE /api/planner/itineraries/{itineraryId:guid}` | Delete a caller-owned itinerary | `planner.itineraries.manage` |
| `POST /api/planner/itineraries/{itineraryId:guid}/re-evaluations` | **Non-CRUD operation**: Evaluate existing itinerary against current marine suitability, availability, and operational restrictions | `planner.itineraries.manage` |
| `GET /api/planner/biodiversity/predictions` | Query validated biodiversity prediction context for a canonical destination/location | `planner.biodiversity.read` |

### 3.2 Candidate DTO contracts

#### 1. Recommendation Request & Result
```json
// POST /api/planner/recommendations
{
  "targetDestinationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "startsAt": "2026-10-01T08:00:00Z",
  "endsAt": "2026-10-01T18:00:00Z",
  "durationHours": 8,
  "preferredActivityIds": ["4fa85f64-5717-4562-b3fc-2c963f66afa7"],
  "experienceLevel": "INTERMEDIATE",
  "includeBiodiversityContext": true
}
```

```json
// Response: RecommendationResultDto
{
  "recommendationId": "5fa85f64-5717-4562-b3fc-2c963f66afa8",
  "workflowId": "6fa85f64-5717-4562-b3fc-2c963f66afa9",
  "status": "COMPLETED",
  "generatedAt": "2026-09-27T11:00:00Z",
  "candidates": [
    {
      "destinationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "activityId": "4fa85f64-5717-4562-b3fc-2c963f66afa7",
      "offeringId": "7fa85f64-5717-4562-b3fc-2c963f66afb0",
      "title": "Guided Snorkeling Tour",
      "scheduledStart": "2026-10-01T09:00:00Z",
      "scheduledEnd": "2026-10-01T11:30:00Z",
      "availabilityStatus": "AVAILABLE",
      "suitability": {
        "status": "SUITABLE",
        "marineConditionTime": "2026-10-01T09:00:00Z",
        "safetyProfileId": "8fa85f64-5717-4562-b3fc-2c963f66afb1"
      },
      "operationalStatus": "OPEN",
      "biodiversityContext": {
        "speciesName": "Chelonia mydas (Green Sea Turtle)",
        "probability": 0.82,
        "uncertainty": "LOW",
        "predictionTimestamp": "2026-09-27T10:30:00Z"
      },
      "fitScore": 0.95,
      "reasons": ["Optimal wave and tide conditions", "Offered during scheduled opening hours"]
    }
  ],
  "excludedCandidatesCount": 2,
  "uncertaintyNotes": []
}
```

#### 2. Itinerary Re-evaluation (Non-CRUD)
```json
// POST /api/planner/itineraries/{itineraryId}/re-evaluations
{
  "reEvaluationMode": "FULL_ASSESSMENT"
}
```

```json
// Response: ItineraryReEvaluationResultDto
{
  "itineraryId": "9fa85f64-5717-4562-b3fc-2c963f66afb2",
  "evaluatedAt": "2026-09-27T11:15:00Z",
  "hasChanges": true,
  "summary": "1 item now has safety cautions due to rising swell conditions.",
  "items": [
    {
      "itemId": "afa85f64-5717-4562-b3fc-2c963f66afb3",
      "offeringId": "7fa85f64-5717-4562-b3fc-2c963f66afb0",
      "currentAvailability": "AVAILABLE",
      "currentSuitability": "CAUTION",
      "currentOperationalStatus": "OPEN",
      "advisoryMessage": "Wave height expected to exceed 1.5m during activity window.",
      "suggestedAction": "REVIEW_OR_RESCHEDULE"
    }
  ]
}
```

#### 3. Biodiversity Prediction Contract (Consumed by Member 1)
```json
// GET /api/planner/biodiversity/predictions?destinationId={guid}&activityId={guid}
// Response: BiodiversityPredictionDto
{
  "destinationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "activityId": "4fa85f64-5717-4562-b3fc-2c963f66afa7",
  "status": "AVAILABLE",
  "predictedSpecies": [
    {
      "speciesId": "bfa85f64-5717-4562-b3fc-2c963f66afb4",
      "scientificName": "Chelonia mydas",
      "commonName": "Green Sea Turtle",
      "habitatSuitability": 0.84,
      "confidenceLevel": "HIGH"
    }
  ],
  "modelMetadata": {
    "modelVersion": "it3091-v1.2",
    "inferenceTimestamp": "2026-09-27T10:00:00Z"
  },
  "limitations": "Contextual prediction only. Not a guarantee of wildlife sighting or site safety."
}
```

### 3.3 Error semantics

Use standard RFC 7807 `ProblemDetails`:
- `400 Bad Request`: Malformed payload, invalid UUIDs, or past date ranges.
- `401 Unauthorized`: Missing or invalid bearer JWT.
- `403 Forbidden`: Caller lacks required permission code or attempts to access another user's itinerary.
- `404 Not Found`: Referenced itinerary, recommendation, or workflow not found.
- `409 Conflict`: Optimistic concurrency violation when updating an itinerary with a stale version.
- `422 Unprocessable Entity`: Planning constraints cannot be reconciled (e.g. `endsAt` before `startsAt`).
- `503 Service Unavailable`: Upstream dependency unavailable during synchronous requirement, returned with explicit reason code.

---

## 4. Service identity, networking, and delivery

### 4.1 Service attributes

- **Service Directory:** `services/coastal-planner/`
- **.NET Project:** `Blueverse.Services.CoastalPlanner` (ASP.NET Core 10, C# 13, pinned by `global.json`)
- **Docker Container Name:** `coastal-planner`
- **Internal Port:** `8080` (HTTP)
- **Container Network:** `service-net` (private internal network)
- **Public API Gateway:** `services/api` forwards `/api/planner/{**catch-all}` to `http://coastal-planner:8080/api/planner/{**catch-all}`.
- **Service-to-Service Context:** Forwarded requests carry authenticated actor context via internal headers (`X-Actor-Id`, `X-Actor-Roles`, `X-Actor-Permissions`, `X-Correlation-Id`).

### 4.2 Health and readiness semantics

- `GET /health/live`: Unauthenticated process liveness probe. Returns `200 OK` when the ASP.NET Core process is running.
- `GET /health/ready`: Checks PostgreSQL database connectivity and migration state.
- **Rule:** Agentic AI runtime or IT3091 ML service availability must NOT affect process liveness or readiness probes. Non-AI workflows remain fully functional without external ML dependencies.

---

## 5. Persistence, concurrency, and time conventions

### 5.1 Persistence boundaries

- PostgreSQL schema: `coastal_planner` schema within the BLUEVERSE PostgreSQL instance.
- Migrations: Owned exclusively by `Blueverse.Services.CoastalPlanner` via EF Core migrations.
- Data Ownership:
  - Planning requests and workflows.
  - User itineraries and ordered items.
  - Cached/snapshot recommendation results and re-evaluation histories.
  - IT3091 prediction cache (with explicit TTL/freshness constraints).

### 5.2 Concurrency and ownership

- Itineraries are caller-scoped: users can view and mutate only itineraries where `OwnerUserId == actorId` (unless an admin role has oversight permissions).
- Optimistic concurrency control via `ConcurrencyVersion` (or PostgreSQL `xmin` token) on the `Itinerary` entity. Conflicting concurrent updates return `409 Conflict`.

### 5.3 Time conventions

- All internal and API timestamps use UTC ISO-8601 (RFC 3339) instants (`yyyy-MM-ddTHH:mm:ssZ`).
- Date intervals are half-open: `[startsAt, endsAt)` (inclusive start, exclusive end).
- Local display on clients uses an IANA time zone identifier (e.g. `Asia/Colombo`).
- Date-only planning parameters (e.g. travel dates) use `YYYY-MM-DD` strings distinct from instant timestamps.

---

## 6. External ML integration (IT3091) and Pre-G07 Agentic AI seams

### 6.1 IT3091 Biodiversity ML adapter

- **Ownership:** Implemented solely by `services/coastal-planner/` as a server-side HTTP adapter.
- **Privacy & Minimization:** Requests send only coarse coordinate bounding boxes or canonical `destinationId` and optional season/month. No personal identifiers or precise tracking coordinates are shared.
- **Validation:** Server deterministically validates schema, value ranges (probabilities in `[0.0, 1.0]`), timestamps, and model version metadata before exposing results.
- **Resilience:** HTTP client configured with 3-second timeout and at most 1 retry. Outages return explicit status `UNAVAILABLE` or `NOT_REQUESTED`. No fallback to dummy/fabricated data.

### 6.2 Pre-G07 Agentic AI seam

- **Access Seam:** `services/coastal-planner/` exposes private internal endpoints on `service-net` for the future planning agent:
  - `GET /internal/agentic/health`: Bounded probe (max 2 seconds timeout) returning current dependency state.
  - `POST /internal/agentic/planning-coordination/dispatch`: Server-to-server dispatch endpoint (max 15 seconds timeout, 0 auto-retries).
- **Dependency Statuses (`aiDependencyStatus`):**
  - `NOT_CONNECTED`: No AI container/runtime configured. Default baseline for pre-G07 feature work.
  - `UNAVAILABLE`: Configured runtime unreachable or timeout exceeded.
  - `AVAILABLE`: Probe succeeded.
- **Dispatch Outcomes (`aiDispatchOutcome`):**
  - `NOT_REQUESTED`, `NOT_STARTED`, `SUCCEEDED`, `UNAVAILABLE`, `INVALID_RESULT`.
  - Accompanied by explicit `isRetryable: bool` flag.
- **Payload Contract:** `PlanningCoordinationDispatchRequest` uses `contractVersion: 1` containing `workflowId: guid`, `recommendationId: guid`, `destinationId: guid`, `timeRange: { startsAt, endsAt }`, `preferredActivityIds: guid[]`, and `constraints`.
- **Invariants:**
  - Business workflow status (`SUBMITTED`, `PROCESSING`, `COMPLETED`, `FAILED`) remains independent of `aiDependencyStatus`.
  - Process liveness (`/health/live`) and DB readiness (`/health/ready`) must NEVER fail due to AI status.
  - The deterministic recommendation and candidate assembly path runs fully when AI is `NOT_CONNECTED` or `UNAVAILABLE`.
  - No LLM prompt templates, agent runtimes, tool orchestration loops, or agent-owned databases are implemented prior to G07.

---

## 7. Proposed UI workflows, device capabilities, and client parity

React Web and Flutter Mobile must achieve 100% capability parity:

### 7.1 Shared UI workflows

| Workflow ID | React Route | Flutter Route | User Capability |
|---|---|---|---|
| `planner-recommendations` | `/planner` | `/planner` | Enter constraints, view recommendations, inspect suitability & biodiversity context |
| `planner-itinerary-list` | `/planner/saved` | `/planner/saved` | View, manage, and delete saved user itineraries |
| `planner-itinerary-detail` | `/planner/itineraries/:itineraryId` | `/planner/itineraries/:itineraryId` | View itinerary schedule, edit item ordering, update notes |
| `planner-re-evaluation` | `/planner/itineraries/:itineraryId/re-evaluate` | `/planner/itineraries/:itineraryId/re-evaluate` | Trigger re-evaluation and inspect updated suitability/availability advisories |

### 7.2 Device capability contract (Date/Time selection)

Per [v1 device-capability contract](../device-capabilities.md):
- **Flutter:** Uses accessible native date and time picker controls (`showDatePicker`, `showTimePicker`).
- **React:** Uses semantic HTML5 date/time inputs (`<input type="date">`, `<input type="time">`) with labels and full keyboard navigation.
- **Timezone & Horizon:** Both clients display local time with destination IANA time zone (`Asia/Colombo`). Date-only strings (`YYYY-MM-DD`) remain distinct from UTC ISO-8601 instants. Server validates supported planning horizon (up to 30 days ahead) and chronologic order (`startsAt < endsAt`). Client pickers cannot make unavailable or unsuitable activities eligible.
- **Scope Limit:** Date/time selection is strictly user input for planning requests; no push/local notifications, background alarms, or calendar integration are permitted.

---

## 8. Decisions deliberately deferred

1. IT3091 production credentials, network endpoint URL, and hosting topology (settled before live integration gate).
2. Advanced AI agent orchestration, multi-agent tool loops, and prompt configurations (deferred to G07 and `agentic-ai/planning-coordination` branch).
3. Long-term historical itinerary retention and cold storage policies.

---

## G00 review checklist

- [x] Adithya Gunawardana proposes this document as Member 3 input for G00 freeze.
- [ ] Ushan Srinuka confirms candidate catalogue and availability contracts and biodiversity consumer requirements.
- [ ] Sanuda Abeysinghe confirms candidate activity suitability and marine condition evidence contracts.
- [ ] Wanshaja Sooriyabandara confirms operational restriction handoffs and workflow reference tracking.
- [ ] Maintainer records team G00 acceptance in readiness and branch tracking documentation once all four owners ratify.
