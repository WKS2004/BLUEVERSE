---
contract_id: v1.component.coastal-planner
contract_type: business_component
release: v1
implementation_status: target_not_implemented
owner_label: member_3
requirements: "PROJECT_REQUIREMENTS.md sections 16, 17, 20-27, 40, 53"
non_crud_operation: itinerary_re_evaluation
minimum_meaningful_public_api_endpoints: 4
agent_contract: "../agents/member-3-planning-coordination-agent.md"
---

# Member 3 — Smart Coastal Planner & Itinerary Management

**Contract status:** v1 target specification. The current foundation does not
contain this complete v1 planner. **Ownership label:** Member 3, as named in
the frozen requirements; it does not identify a person or account.

## 1. Purpose and user outcome

This component helps a person plan coastal activities using their requested
place, time, duration, interests and relevant experience constraints. It
assembles current, eligible experiences into useful recommendations and lets
the user create, save, revisit and re-evaluate an itinerary. It is a **coastal
planner**, not a generic travel planner: hotels, airlines, restaurant
discovery, bookings and payment are outside this v1 contract.

The result should help a person understand what they could do, when and where,
why it fits their request, which availability and marine/safety evidence was
used, and what remains uncertain. Recommendations do not promise inventory,
conditions or real-world safety beyond the authoritative evidence and rules
available at evaluation time.

### 1.1 Cross-layer implementation overview

| Layer | Component responsibility and implementation contract |
|---|---|
| **Universal product idea** | Turn a person's coastal place/time/interests/constraints into eligible, explainable coastal recommendations and user-owned itineraries. The planner assembles evidence from its source owners; it does not become the catalogue, marine-data provider or operations authority. A deterministic recommendation and itinerary path remains usable before the future AI runtime is connected. |
| **React Web** | Provide the same authorized request, result, workflow-status, itinerary lifecycle and re-evaluation outcomes as Flutter. Use React 19/TypeScript/Vite/React Router, reusable pages/components, Tailwind utilities and existing request/state separation. Show evidence time, availability, suitability, uncertainty and re-evaluation changes; do not locally rank a blocked item back into an eligible result. See the [React component contract](../../v0/components/react-web-client.md), [UI integration guide](../../development/ui-integration.md), and [React state ADR](../../adr/ADR-0005-react-state-management.md). |
| **Flutter Mobile** | Provide the same public workflow with native Dart/Material screens, the UI/logic/data separation, repository/API service and view-model pattern. Inputs and itinerary editing may be adapted to mobile interaction, but permissions, candidate eligibility, status, saved itinerary state and business outcome match React. See the [Flutter component contract](../../v0/components/flutter-client.md), [UI integration guide](../../development/ui-integration.md), and [Flutter state ADR](../../adr/ADR-0006-flutter-state-management.md). |
| **ASP.NET Core and data** | The public API owns request authorization/validation, deterministic candidate assembly, business workflow identity/status/result and user-owned itinerary persistence. It also owns Member 3's private server-side adapter to the separate IT3091 biodiversity inference service and the validated public prediction-result capability consumed by Member 1. EF Core/PostgreSQL persist business request and itinerary state under the approved schema; prediction caching/persistence is not assumed and must be decided from freshness, privacy and retention needs. Member 1/2/4 source results remain authoritative; the planner stores references or evidence snapshots only as the accepted retention contract requires. |
| **ML service integration** | Member 3 sends minimal validated location/species/context to the private IT3091 service, validates response schema, numeric ranges, timestamps and provenance, and returns genuine predictions or a clear unavailable/invalid result. IT3091 supplies the trained model and inference service; Member 3 does not train, host, or claim ownership of it. Member 1's experience screens consume the Member 3 public contract. Biodiversity is optional contextual enrichment, never a safety or operational authority, and this adapter is ordinary backend ML/API integration rather than Agentic AI. |
| **Other provider integration** | Member 3 does not own the map provider or Open-Meteo. It consumes Member 1's canonical catalogue/location/availability and Member 2's backend-mediated Open-Meteo conditions/suitability, plus Member 4 restrictions. Only the typed Member 3 server adapter may call IT3091. Neither client, planner UI, future model nor agent tool may call the map provider, Open-Meteo, IT3091, Auth or internal hosts directly. Map-assisted discovery remains in Member 1; planning receives validated Member 1 data. |
| **Paired Agentic AI role** | The future Planning & Coordination Agent creates a structured plan, delegates to the distinct Member 1 and Member 2 specialists, tracks dependencies and assembles their validated outputs; it participates in Member 4 assessment when that workflow requests it. Before G07, Member 3 implements its ordinary deterministic business behavior, public workflow contract, typed private adapter and safe not-connected/unavailable state only. Actual orchestration/model calls and AI execution state wait for `agentic-ai/**` after G07. |
| **Component relationships** | Member 3 consumes Member 1 publication/schedule/availability, Member 2 condition freshness and suitability, and Member 4 current restrictions. It owns the adapter that obtains optional biodiversity predictions from IT3091 and exposes validated context to Member 1; Member 1 owns experience-facing presentation. Its business workflow ID/objective/status/result references are consumed by Member 4 for traceable assessment. It never replaces source ownership or weakens `UNSUITABLE`, `UNKNOWN`, stale or restricted results. See the [producer/consumer relationship map](../component-relationships.md#producer-consumer-and-authority-map). |

Both clients expose the same permitted outcomes through ASP.NET Core, the
shared role-to-permission model and UI integration registry. This overview is
the layer map; the later sections specify recommendation semantics, itinerary
state, failure cases and acceptance evidence.

The shared [Agentic AI implementation blueprint](../../agentic-ai/implementation-blueprint.md)
defines common model, tool, retrieval, security, recovery and evaluation
requirements for this component's paired agent.

## 2. Ownership and boundaries

Member 3 owns:

- recommendation requests and the planning constraints needed to serve them;
- the persisted business request/workflow identity, status and result for
  tourist recommendations and operational-assessment requests;
- deterministic assembly of eligible candidates and validated context into a
  useful recommendation;
- user-owned itineraries and their ordered experience references;
- itinerary re-evaluation when relevant conditions, availability or
  operational state have changed;
- BLUEVERSE's backend-mediated IT3091 biodiversity inference adapter and
  validated public prediction-result contract, but not the separately
  supplied IT3091 model or inference service.

It does not own the source catalogue and availability or experience-facing
biodiversity presentation (Member 1), environmental retrieval and deterministic
activity suitability (Member 2), operational
restrictions/approval/execution (Member 4), or the public authorization
boundary. The Planning & Coordination Agent's plan generation, specialist
delegation, tool execution and orchestration are a separate post-G07 delivery;
the agent coordinates the process but does not replace a source component's
facts or deterministic rule.

On `features/coastal-planner`, prepare the public request/status/result API
and a typed private adapter for the future planner, including the backend
availability check and a safe not-connected/unavailable result. The ordinary
business workflow remains usable for deterministic recommendations and
itineraries when the private Agentic AI service is absent. Keep business
request state distinct from future agent plan/step state. Follow the shared
[member integration boundary](../agentic-ai-integration-boundary.md).

## 3. Users, permissions and data minimization

Tourists create requests and manage their own saved itineraries where
permitted. Authorized operational users may initiate, track, inspect or
search workflows according to their permissions. Server-side role-to-
permission policy is authoritative. Do not infer access from a client route or
a hard-coded role. Exact capabilities by permission, sharing semantics, and
aggregate access are implementation decisions.

Collect only information needed for the specific objective. Relevant
preference fields may include a coastal region or destination, date/time,
available duration, preferred activities, interests, experience level where
relevant and optional constraints. Avoid gathering unrelated sensitive
profile data or treating inferred preferences as explicit consent.

## 4. Domain concepts

| Concept | Meaning |
|---|---|
| Recommendation request | A validated objective with only the preference and time/location constraints needed to produce a coastal recommendation. |
| Business workflow | A durable Member 3 request/assessment process with shared workflow ID, initiator, business status, result version, timestamps and links to relevant records. This exists independently of an AI run and is defined in the shared [workflow contract](../workflows.md). |
| Agentic execution state | A post-G07 record of the planner's structured plan, assigned agents, dependencies, tool calls, outputs and recovery state. It is not implemented by the member feature branch; see the [Agentic AI integration boundary](../agentic-ai-integration-boundary.md). |
| Candidate experience | A destination/activity/offering sourced from Member 1, combined with current schedule and availability, Member 2's condition/suitability result, and Member 4's applicable operational restrictions. |
| Biodiversity prediction context | Optional genuine ML output obtained through Member 3's private IT3091 adapter from the separate IT3091 workstream. A validated result may carry focal species, requested canonical location/area, model and version, prediction time, probability or habitat-suitability interpretation, uncertainty and limitations. It is supplementary context, not evidence of observed presence or safety. |
| Recommendation | A set of suggested experiences or activities, with reasons, evidence, suitability/availability context and explicit uncertainty. Exact ranking and presentation schema are implementation choices. |
| Itinerary | A caller-owned or otherwise explicitly authorized ordered collection of coastal experience references for one or more dates/times. Exact ownership, sharing and item identity rules require a technical decision. |
| Itinerary item | A reference to an eligible destination/activity/offering plus an itinerary position and any documented user-edited schedule/context. It must not be a copied source of truth for availability or operational status. |
| Re-evaluation result | A fresh comparison of an existing itinerary with current condition, availability and operational evidence; it identifies affected items, changes and uncertainty. |

The exact normalized data model, item duplication policy, ordering behavior,
concurrency token and retention period are database/API design decisions.

## 5. Recommendation workflow

### 5.1 Request validation and planning

1. React or Flutter submits the user's coastal objective and relevant
   constraints to ASP.NET Core.
2. The API authenticates the caller, checks permission, validates identifiers
   and requested period, minimizes/persists only necessary input, and creates
   the shared workflow record and ID.
3. The Planning & Coordination Agent produces a structured plan: required
   experience and marine information, specialist assignment, dependencies,
   allowlisted tools, expected output types, and assembly step.
4. Each delegated result is validated before it can be consumed. The planner
   cannot add tools or bypass a required dependency.

### 5.2 Candidate construction and deterministic constraints

1. Candidate experiences come from valid destinations and activities with
   published/usable state, an appropriate offering, schedule and availability.
2. Relevant marine evidence and deterministic activity suitability are
   obtained from Member 2. A missing required condition is represented as
   uncertainty; the planner does not fabricate a value.
3. Applicable restrictions are read from Member 4's authoritative operational
   state.
4. When the objective has a clear biodiversity-context need, the backend may
   request an optional prediction through Member 3's IT3091 adapter. Use only
   validated location/species/context input; preserve source, model/version,
   prediction time, uncertainty and limitations. An unavailable, stale,
   invalid or unrequested result stays explicitly distinct. Do not require
   this optional context for eligibility and do not use it to rank safety or
   override any component's deterministic decision.
5. Candidate eligibility is enforced deterministically. In particular, an
   activity classified as unsuitable for the requested circumstances cannot
   appear in the final accepted result for that same period, even if model
   text proposes it.
6. The recommendation communicates relevant evidence, timing, missing data
   and limitations. The user can distinguish current evidence from a stored
   historical result.

Normal tourist recommendations do not require staff approval, but all
deterministic safety, availability and operational constraints still apply.

### 5.3 Itinerary management

The user can create an itinerary from appropriate experiences, inspect it,
add/remove/reorder/update items, save it and return to it later. Every write
must re-check permission, item existence, current eligibility where required,
ownership, ordering and concurrency rules. A saved itinerary is not a promise
that conditions or availability will remain unchanged.

### 5.4 Re-evaluation

The required non-CRUD business operation evaluates an existing itinerary
against changed marine conditions, experience availability and operational
state. It should identify which evidence changed, which items remain valid,
which need attention, what alternative or action is suggested, and what is
unknown. It must not silently leave a now-unsuitable item looking current, or
silently rewrite the user's itinerary. Whether the result is a snapshot,
version, or updated plan is an implementation choice; explicit user intent
and the original itinerary must remain understandable.

The triggering policy (manual only, user prompt, or other permitted trigger),
how changed evidence is detected, how a prior assessment is linked, and what
the user confirms before edits are applied must be documented before
implementation.

## 6. Business invariants

1. Only valid coastal destinations and currently usable/published activities
   are candidate sources.
2. An offering must be available and appropriately scheduled for the requested
   period; catalog publication alone does not prove availability.
3. Member 4's operational state must be respected. The planner cannot create
   its own contradictory restriction state.
4. Member 2's deterministic suitability result constrains final output. An
   `UNSUITABLE` candidate cannot be reintroduced by prompt, reranking, retry or
   result assembly.
5. Missing/stale required marine conditions remain explicit and must not be
   described as verified safe conditions.
6. Biodiversity enriches discovery context only; it is not a safety signal
   unless a separate deterministic business rule explicitly defines that use.
7. Itinerary order and item membership are persisted consistently and scoped
   to an authorized principal or documented sharing scope.
8. Source status and suitability are fetched/revalidated as required; a
   copied status value in an itinerary never overrides the owner component.
9. Authorization, candidate eligibility and edits are enforced by the server
   on every request, including retries and re-evaluation.

## 7. Public API capability contract

At least four meaningful public API endpoints and one operation beyond CRUD
are required. This list names capabilities, not live routes. The exact public
paths, methods, request/response DTOs, permissions, result status and
asynchronous behavior must be implemented and entered in the
[endpoint catalog](../../api/endpoint-catalog.md). Routes are under `/api/...`
without a version path segment.

The public capability set must support:

- create/inspect recommendation requests and results;
- retrieve related planning workflows, progress and failure/result details
  permitted to the caller;
- create, retrieve, update and manage saved itineraries and their ordered
  items;
- re-evaluate a stored itinerary against current source-component context;
- retrieve optional biodiversity prediction context through Member 3's
  validated public capability for Member 1's destination/activity experience
  surfaces or applicable planner context. Member 3 owns its exact route and
  DTO and adds them to the endpoint catalog only when implemented;
- retrieve permitted history/search/filter/pagination and useful aggregates;
  and
- return status and structured uncertainty through the shared workflow ID.

The API must never expose private orchestration, internal service hosts or
specialist agent tools directly. If the future planner is not connected or
unavailable, return the accepted safe business-workflow status; do not claim
that an AI plan completed. Add each user-facing React/Flutter workflow
to [`ui-integration.json`](../../contracts/ui-integration.json) with its
public API references.

## 8. React and Flutter experience parity

Both clients must provide the same authorized planning outcomes:

| Step | Required behavior in each client |
|---|---|
| Preferences | Enter the relevant place, date/time, duration, coastal activity/interests and relevant experience constraints. Use accessible native date/time picker controls in Flutter and semantic date/time inputs in React. Explain optional fields and allow correction. |
| Request | Submit through the public API and display the same workflow ID/status model. Prevent confusing duplicate submission while relying on server checks. |
| Recommendation | Display coastal options, availability/schedule context, deterministic suitability, reasons and evidence timestamps, limitations and uncertainty. |
| Biodiversity context | Where the workflow requests this optional context, show a genuine validated result and its provenance/uncertainty, or the explicit unavailable/invalid state. It never acts as a safety signal or replaces Member 1's experience detail presentation contract. |
| Itinerary | Create/revisit, add/remove/reorder/update, save and inspect history under the same authorization rules. |
| Re-evaluation | Request a fresh assessment, compare affected items/evidence, and distinguish suggested changes from committed itinerary edits. |
| Monitoring | Inspect related workflow progress, completion, recoverable error or safe failure where permitted. |
| Feedback | Show validation, no eligible results, unavailable data, stale conditions, timeout, permission denial and retry/recovery guidance. |

Web and mobile may adapt input widgets and layout to their form factor. Their
business behavior, permission checks, data meaning and resulting itinerary
must be equivalent. The shared UI registry is the conformance inventory.
Keep date-only values distinct from instants. The member must agree the
destination/user time-zone representation, daylight-saving gap/overlap
behavior, supported planning horizon and range validation before freezing the
API contract. The backend is authoritative; client pickers improve input but
cannot make an unavailable or unsuitable offering eligible. See the shared
[device-capability contract](../device-capabilities.md).

## 9. Cross-component handoffs

| Source/consumer | Handoff |
|---|---|
| Member 1 — Experience & Biodiversity | Candidate destinations, activities, offerings, schedule, availability, experience constraints and optional prediction context. |
| Member 2 — Marine Conditions & Safety | Sourced conditions, freshness/gaps and deterministic activity-specific suitability. |
| Member 4 — Coastal Operations | Current restrictions/status and, for the assessed operations path, the Safety & Operations proposal. |
| IT3091 biodiversity inference workstream | Member 3 sends a validated, privacy-minimal private request and receives a genuine prediction or dependency/schema failure. Member 3 exposes only its validated public result capability; Member 1 consumes that result for the experience-facing screen. |
| Planning & Coordination Agent | Durable plan, delegation, dependency tracking, structured assembly. It does not itself own business facts. |
| React and Flutter | Same result, status and workflow identity through public ASP.NET Core APIs. |

Each fact has one owner. Failures in one specialist must be reflected in the
plan/result; the planner must not synthesize missing authoritative facts.

## 10. Failure, privacy and resilience

Handle invalid or conflicting constraints, empty candidate sets, no available
offering, unavailable/stale marine information, restricted activities,
malformed specialist result, unavailable biodiversity, agent/tool timeout,
bounded retry exhaustion, failed persistence and re-evaluation against a
changed source state. The IT3091 adapter must bound connection/read timeouts
and any retry, validate the returned schema, numeric probability/range fields,
timestamps, model/version metadata and result-to-query association, and map
dependency or invalid-response conditions to an explicit safe status. Do not
return a prior cached prediction as current unless its age and reuse policy
are explicitly accepted; do not convert missing/invalid values to zero or
invent a substitute. Surface which inputs could not be verified. A workflow
that cannot produce a safe result completes with an explicit failure state;
it must not return a plausible fabricated itinerary.

Persist only the objective, necessary preferences/constraints, plan/status,
structured specialist results or auditable summaries, validation, permitted
errors/retry information and final result needed to operate and audit the
workflow. Never persist hidden chain-of-thought, credentials or unrelated
personal information. Send the least precise location and smallest set of
species/context fields supported by the model contract. Treat the IT3091
response as untrusted external-service data until deterministic validation
passes. Keep credentials and private host details on the backend. Per-user
request and itinerary reads/writes are authorized server-side.

## 11. Acceptance and evidence checklist

- valid, malformed, absent and competing preference inputs are validated;
- only published/usable, scheduled and available experiences are suggested;
- unavailable and operationally restricted offerings are excluded;
- deterministic `UNSUITABLE` cannot be bypassed during agent assembly or
  accepted-result serialization;
- missing/stale marine evidence is visible and is not described as a verified
  condition;
- ranking/recommendation output includes meaningful evidence and provenance;
- the IT3091 adapter returns a genuine service prediction when available and
  validates model/version, request association, result ranges, timestamps,
  provenance, uncertainty and limitations before exposing it;
- timeout, connection failure, absent model, malformed/out-of-range output,
  stale result and bounded retry exhaustion produce the specified explicit
  unavailable/invalid status without fabricated values or leaked credentials;
- Member 1 can consume the validated Member 3 public result contract, while
  neither client nor any agent calls the private IT3091 service directly;
- biodiversity is optional context and cannot change deterministic candidate
  eligibility, safety suitability, operational restriction or approval;
- itinerary item add/remove/reorder/update is persisted with documented
  ownership, duplicate and concurrency semantics;
- re-evaluation identifies changed conditions, availability and operational
  states without silently mutating user intent;
- workflow ID, progress, errors, retry and safe-failure behavior are durable
  and consistent for authorized viewers;
- one user's itinerary/request is not accessible to another without explicit
  sharing permission;
- React and Flutter can complete all equivalent authorized journeys; and
- catalog/registry, endpoint contracts, database, API, agents, tests and docs
  describe the same behavior.

The owner must retain attributable implementation and verification evidence
for API, PostgreSQL/EF Core, React, Flutter, tests, documentation and Git as
required by the repository's individual contribution rules.

## 12. Decisions to finalize during implementation

Specify final preference schema and data-retention boundaries; candidate
ranking and tie-breaking; the exact input/evidence that makes a condition
required; workflow status vocabulary; synchronous/asynchronous API behavior;
itinerary ownership/sharing; item uniqueness, ordering and concurrency; how
re-evaluation detects and stores changes; result snapshot/retention policy;
how recommendation failures are presented; and the IT3091 private request and
response schema, authentication, location precision, supported output fields,
freshness/timeout/retry/error mapping, health semantics, cache/retention policy
and public Member 1 consumer API capability. Record the provider contract and
privacy/failure decisions in the implementation/API/ADR documentation before
the adapter is accepted. No numeric safety policy is delegated to the planner
or LLM.

## 13. Traceability

- Requirements: [sections 16, 17, 20–27, 40, 41 and 53](../../../PROJECT_REQUIREMENTS.md).
- Paired AI role: [Planning & Coordination Agent](../agents/member-3-planning-coordination-agent.md).
- Component work areas on one member branch: [Member 3 phase plan](../phases/member-3-phase-plan.md); producer/consumer relationships: [component relationship map](../component-relationships.md); PR and G07 process: [member branch workflow](../member-branch-workflow.md).
- Device input: [v1 device-capability contract](../device-capabilities.md) defines equivalent date/time selection for planning on React and Flutter.
- ML integration ownership: [ADR-0019](../../adr/ADR-0019-biodiversity-inference-integration-ownership.md) assigns the BLUEVERSE IT3091 adapter to Member 3 and the experience-facing consumer to Member 1.
- Related contracts: [shared workflows](../workflows.md), [member Agentic AI integration boundary](../agentic-ai-integration-boundary.md), [permissions and parity](../cross-platform-and-permissions.md), [quality and delivery](../quality-and-delivery.md), [requirements coverage and readiness](../requirements-coverage-and-readiness.md), [Agentic AI architecture](../../agentic-ai/architecture.md), [safety](../../agentic-ai/safety.md), [endpoint catalog](../../api/endpoint-catalog.md), [UI integration](../../contracts/ui-integration.json).
