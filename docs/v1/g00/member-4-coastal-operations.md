# G00 Decisions — Coastal Operations, Advisories & Alerts

**Document status:** shared G00 decision record for Member 4. This record captures
the contracts required before Wanshaja Sooriyabandara's `features/coastal-operations`
implementation begins. It is recorded for shared review and ratification, not yet
accepted.

- **Component:** Coastal Operations, Advisories & Alerts (Member 4)
- **Assigned owner:** Wanshaja Sooriyabandara (`@WKS2004`)
- **Feature branch:** `features/coastal-operations`
- **Agentic AI branch (post-G07):** `agentic-ai/safety-operations`
- **Owner component contract:** `docs/v1/components/member-4-coastal-operations-advisories-alerts.md`
- **Paired Agentic contract:** `docs/v1/agents/member-4-safety-operations-agent.md`

## 1. Scope and agreed G00 references

This record is the Member 4 side of the shared `G00 — Shared contract freeze`
agreement described in `docs/v1/requirements-coverage-and-readiness.md#g00-exit-criteria`
and `docs/v1/member-branch-workflow.md`. It must be ratified by all four owners
before any component coding starts. Member branches may prepare private
`services/*` projects and pre-G07 integration seams only after this record is
accepted.

## 2. Service identity and delivery identity

| Item | Decision | Owner |
|---|---|---|
| Service subfolder | `services/coastal-operations` | WKS2004 |
| .NET project | `Blueverse.Services.CoastalOperations` | WKS2004 |
| Container name | `coastal-operations` | WKS2004 |
| Internal port | `8080` (HTTP) | WKS2004 |
| Private network | `service-net` | WKS2004 |
| Public route prefix | `/api/operations/...` | WKS2004 + API owner |
| PostgreSQL schema | `coastal_operations` | WKS2004 + shared DB owner |
| Migration ownership | EF Core migrations owned by `Blueverse.Services.CoastalOperations` | WKS2004 |
| API-to-service transport | `services/api` routes/to-forwards only; no Member 4 business logic or persistence | API owner |

Service-to-service transport keeps `X-Actor-Id`, `X-Actor-Roles`,
`X-Actor-Permissions` and `X-Correlation-Id` on internal calls, consistent with
the shared G00 convention already recorded for other member services.
`services/api` stays the sole public boundary; clients and agents never address
the container, internal port, internal routes or PostgreSQL directly.

## 3. Shared business identities and data authority

| Identifier | Entity | Scope and authority |
|---|---|---|
| `workflowId: guid` | Operational assessment / business workflow record | Business workflow identity, shared with Adithya Gunawardana (Member 3) where linked; authoritative for the assessment lifecycle and audit |
| Assessment ids | Assessment/proposal/version | Owner-owned; immutable after persisted unless a documented, permission-checked revision creates a new version |
| Alert ids | Advisory / alert record | Owner-owned; lifecycle `proposed`, `active`, `resolved`, `expired` |

Cross-component canonical IDs are frozen at G00 and must not be invented
independently in `features/coastal-operations`:

- Ushan Srinuka (Member 1): `destinationId: guid`, `activityId: guid`,
  `offeringId: guid`, `scheduleId: guid`, availability `AVAILABLE` /
  `UNAVAILABLE` / `UNKNOWN`, publication `DRAFT` / `PUBLISHED` / `ARCHIVED`.
- Sanuda Abeysinghe (Member 2): suitability `SUITABLE` / `CAUTION` /
  `UNSUITABLE` / `UNKNOWN`; deterministic profile/safety-profile IDs.
- Adithya Gunawardana (Member 3): workflow/request/status vocabulary,
  `workflowId: guid`, objective, plan and itinerary context.
- Wanshaja Sooriyabandara (Member 4): managed operational state,
  `OPEN`, `CAUTION`, `TEMPORARILY_SUSPENDED`, `CANCELLED`, `COMPLETED`, and
  proposal/decision, alert and audit records.

Data authority is not shared. Member 4 owns operational restrictions/state and
audit; it consumes, never rewrites, the other members' catalogue, marine,
suitability and workflow identity.

## 4. Public and private contracts

Exact public methods/paths, DTOs, validation, permission codes and error/status
behavior for the four required operations are recorded in the owning service
contract and endpoint catalog and are not frozen in prose here. The existing
`/api/health` keeps public API liveness; an operations dependency outage does not
make the API appear dead.

Member 4 public capability set at minimum:

- assessment initiation, detail, queue/list, status and workflow progress;
- optional image-evidence upload and authorized reviewer retrieval of
  versioned metadata/content;
- reviewer approve, reject and request-revision decisions;
- resulting operational state, change history and advisory/alert
  create/read/update/resolve actions;
- the approval/decision call path (`POST /api/operations/assessments/{workflowId}/approve` is the referenced example; exact route/DTO is owner-chose at G00).

Private transport / internal handler contract (agreed at G00, implemented only
when the future Safety & Operations Agent exists):

| Internal route | Purpose | Status behavior |
|---|---|---|
| `GET /internal/agentic/health` | Bounded probe for the future agent | `NOT_CONNECTED` when unconfigured/unavailable; must not fail service liveness or database readiness |
| `POST /internal/agentic/safety-operations/dispatch` | Server-to-server proposal/context dispatch | Bounded response; explicit `UNAVAILABLE` when the dependency is not connected; never executes |

These are integration seams, not executable agent runtime. No agent tool, prompt
or AI-owned execution state is implemented by this component before G07.

## 5. Status vocabulary, workflow ID and correlation

- Business workflow status: `SUBMITTED`, `PROCESSING`, `COMPLETED`, `FAILED`.
- Agentic dependency status (`aiDependencyStatus`): `NOT_CONNECTED`,
  `UNAVAILABLE`, `AVAILABLE` (agreed pre-G07 semantics: never mixes with
  business status; liveness and database readiness never fail on AI status).
- Agent dispatch outcomes (`aiDispatchOutcome`): `NOT_REQUESTED`,
  `NOT_STARTED`, `SUCCEEDED`, `UNAVAILABLE`, `INVALID_RESULT`, with explicit
  `isRetryable: bool`.
- Correlation: every public response and persisted record carries
  `X-Correlation-Id` / `correlationId` on the shared workflow ID path.
- Timestamps: UTC ISO-8601 (RFC 3339) `yyyy-MM-ddTHH:mm:ssZ`; half-open periods.

## 6. Permission and actor propagation

- Named permission codes and resource scope: `operations.assessments.*`,
  `operations.assessments.manage`, `operations.review.*`,
  `operations.alerts.*`, `operations.images.*` are the agreed candidate
  family; final codes are recorded in the permission catalogue.
- The API's authenticated principal, role-to-permission resolution, resource
  scope and authorization are authoritative.
- Actor/operation context is propagated across the internal boundary, not
  resolved inside the private service.
- Separation-of-duties and initiator-reviewer rules remain explicit, decided
  design points at G00; do not silently assume an answer.

## 7. Pre-G07 AI dependency behavior

- Unconfigured private dependency reports `NOT_CONNECTED`; it does not create
  a completed-agent status and does not block ordinary non-AI behavior.
- Configured but unreachable or slow dependency reports `UNAVAILABLE` with an
  explicit retryability decision.
- Dispatched results are validated as untrusted; a blocked or invalid result
  returns a safe status and a safe next action, with no protected mutation.
- No LLM, agent runtime, tool loop or agent-owned database is implemented by
  this component before G07.

## 8. Persistence and audit scope

- Own records: operational assessments, proposals/decisions, operational
  state and history, alerts/advisories, execution history, optional image
  evidence metadata/permissions/version, audit fields.
- Test doubles for the future agent are allowed in tests; a fixture-backed
  production proposal generator is not.
- Real PostgreSQL evidence is required for provider-sensitive behavior;
  in-memory/contract doubles do not count as live integration.

## G00 review checklist

- [ ] Wanshaja Sooriyabandara (`@WKS2004`) records this as the Member 4 G00 input.
- [ ] Ushan Srinuka (`@Ushan-Srinuka`) confirms canonical target/availability/handoff contracts.
- [ ] Sanuda Abeysinghe (`@sanudaabey`) confirms condition/suitability/state contracts.
- [ ] Adithya Gunawardana (`@AdithyaGunawardana`) confirms workflow reference/status contracts.
- [ ] Maintainer records team G00 acceptance in
      `docs/v1/requirements-coverage-and-readiness.md` and
      `docs/v1/member-branch-workflow.md` once all four owners ratify.
