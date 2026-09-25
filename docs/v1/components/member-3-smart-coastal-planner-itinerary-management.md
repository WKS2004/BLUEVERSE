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

## 2. Ownership and boundaries

Member 3 owns:

- recommendation requests and the planning constraints needed to serve them;
- the persisted planning workflow for tourist recommendations and operational
  assessment orchestration;
- delegation to distinct marine, experience and (for operations) safety
  specialists;
- assembly of validated context into a final recommendation;
- user-owned itineraries and their ordered experience references; and
- itinerary re-evaluation when relevant conditions, availability or
  operational state have changed.

It does not own the source catalogue and availability (Member 1), environmental
retrieval and deterministic activity suitability (Member 2), operational
restrictions/approval/execution (Member 4), or the public authorization
boundary. The planning agent coordinates the process but does not replace a
source component's facts or deterministic rule.

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
| Planning workflow | A correlated, durable process with a shared workflow ID, structured plan, assigned specialists, step dependencies, status, validated results and errors. Detailed workflow state is defined in the shared [workflow contract](../workflows.md). |
| Candidate experience | A destination/activity/offering sourced from Member 1, combined with current schedule and availability, Member 2's condition/suitability result, and Member 4's applicable operational restrictions. |
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
4. Candidate eligibility is enforced deterministically. In particular, an
   activity classified as unsuitable for the requested circumstances cannot
   appear in the final accepted result for that same period, even if model
   text proposes it.
5. The recommendation communicates relevant evidence, timing, missing data
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
- retrieve permitted history/search/filter/pagination and useful aggregates;
  and
- return status and structured uncertainty through the shared workflow ID.

The API must never expose private orchestration, internal service hosts or
specialist agent tools directly. Add each user-facing React/Flutter workflow
to [`ui-integration.json`](../../contracts/ui-integration.json) with its
public API references.

## 8. React and Flutter experience parity

Both clients must provide the same authorized planning outcomes:

| Step | Required behavior in each client |
|---|---|
| Preferences | Enter the relevant place, date/time, duration, coastal activity/interests and relevant experience constraints. Explain optional fields and allow correction. |
| Request | Submit through the public API and display the same workflow ID/status model. Prevent confusing duplicate submission while relying on server checks. |
| Recommendation | Display coastal options, availability/schedule context, deterministic suitability, reasons and evidence timestamps, limitations and uncertainty. |
| Itinerary | Create/revisit, add/remove/reorder/update, save and inspect history under the same authorization rules. |
| Re-evaluation | Request a fresh assessment, compare affected items/evidence, and distinguish suggested changes from committed itinerary edits. |
| Monitoring | Inspect related workflow progress, completion, recoverable error or safe failure where permitted. |
| Feedback | Show validation, no eligible results, unavailable data, stale conditions, timeout, permission denial and retry/recovery guidance. |

Web and mobile may adapt input widgets and layout to their form factor. Their
business behavior, permission checks, data meaning and resulting itinerary
must be equivalent. The shared UI registry is the conformance inventory.

## 9. Cross-component handoffs

| Source/consumer | Handoff |
|---|---|
| Member 1 — Experience & Biodiversity | Candidate destinations, activities, offerings, schedule, availability, experience constraints and optional prediction context. |
| Member 2 — Marine Conditions & Safety | Sourced conditions, freshness/gaps and deterministic activity-specific suitability. |
| Member 4 — Coastal Operations | Current restrictions/status and, for the assessed operations path, the Safety & Operations proposal. |
| Planning & Coordination Agent | Durable plan, delegation, dependency tracking, structured assembly. It does not itself own business facts. |
| React and Flutter | Same result, status and workflow identity through public ASP.NET Core APIs. |

Each fact has one owner. Failures in one specialist must be reflected in the
plan/result; the planner must not synthesize missing authoritative facts.

## 10. Failure, privacy and resilience

Handle invalid or conflicting constraints, empty candidate sets, no available
offering, unavailable/stale marine information, restricted activities,
malformed specialist result, unavailable biodiversity, agent/tool timeout,
bounded retry exhaustion, failed persistence and re-evaluation against a
changed source state. Surface which inputs could not be verified. A workflow
that cannot produce a safe result completes with an explicit failure state;
it must not return a plausible fabricated itinerary.

Persist only the objective, necessary preferences/constraints, plan/status,
structured specialist results or auditable summaries, validation, permitted
errors/retry information and final result needed to operate and audit the
workflow. Never persist hidden chain-of-thought, credentials or unrelated
personal information. Per-user request and itinerary reads/writes are
authorized server-side.

## 11. Acceptance and evidence checklist

- valid, malformed, absent and competing preference inputs are validated;
- only published/usable, scheduled and available experiences are suggested;
- unavailable and operationally restricted offerings are excluded;
- deterministic `UNSUITABLE` cannot be bypassed during agent assembly or
  accepted-result serialization;
- missing/stale marine evidence is visible and is not described as a verified
  condition;
- ranking/recommendation output includes meaningful evidence and provenance;
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
and how recommendation failures are presented. Document any architecture or
public contract choice in the relevant ADR/API/database documentation. No
numeric safety policy is delegated to the planner or LLM.

## 13. Traceability

- Requirements: [sections 16, 17, 20–27, 40, 41 and 53](../../../PROJECT_REQUIREMENTS.md).
- Paired AI role: [Planning & Coordination Agent](../agents/member-3-planning-coordination-agent.md).
- Related contracts: [shared workflows](../workflows.md), [permissions and parity](../cross-platform-and-permissions.md), [quality and delivery](../quality-and-delivery.md), [requirements coverage and readiness](../requirements-coverage-and-readiness.md), [Agentic AI architecture](../../agentic-ai/architecture.md), [safety](../../agentic-ai/safety.md), [endpoint catalog](../../api/endpoint-catalog.md), [UI integration](../../contracts/ui-integration.json).
