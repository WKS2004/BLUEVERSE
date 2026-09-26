# v1 requirements coverage and development readiness

This is the handoff checklist for the four member components and their four
distinct Agentic AI roles. It tracks the 25 September 2026 baseline audit and
the 26 September map, device-capability and biodiversity-integration
ownership amendments; it is not evidence
that the v1 services exist. Start with the
[v1 index](README.md) and the owning component and agent contracts, then use
this page to resolve cross-component decisions and collect implementation
evidence.

## Final start-readiness decision — 2026-09-26

**Decision: GO for G00; conditional GO for member implementation; NO-GO for
claiming v1 complete.** The repository has enough approved scope, component
contracts, ownership boundaries, cross-component relationships, branch rules
and validation guidance to begin the shared G00 contract-freeze work now. The
four members may begin their complete component implementations concurrently
only after the G00 exit criteria below are agreed and recorded. This is a
shared start gate, not a member-by-member implementation order.

The current source contains the public API and Auth services, but no v1 domain
service. The endpoint catalog currently records 33 public endpoints and 22
frontend routes, all for the foundation; it records no Agentic AI endpoint.
Those are honest baseline counts, not missing documentation. Actual component
services, feature routes, provider integrations, device workflows and the
Agentic AI runtime remain implementation work.

| Area | Final assessment | Evidence and next gate |
|---|---|---|
| v0 foundation and shared contracts | Ready as the starting baseline | Existing API/Auth, React/Flutter, Docker/Compose, role-to-permission model, endpoint catalog, UI registry and agent resources are present. Static catalog, UI-integration and agent-resource validators passed on this audit. This does not assert that application builds, tests or live infrastructure were rerun. |
| v1 requirements and member plans | Ready as implementation specifications | Four complete member contracts, four paired-agent contracts, work-area plans, relationship map, workflow, device, integration, quality and branch guides link the scope. These are target contracts, not implementation evidence. |
| Four member services and workflows | Not implemented; expected before kickoff | `services/` currently contains only `api` and `auth`. Each new private component service, its persistence, public API integration, React and Flutter workflow, provider adapters and tests must be built on its assigned feature branch. |
| G00 shared contract freeze | Pending; required before component coding | Assign named member owners and settle the shared contracts listed below. Record decisions in their owning contracts and mark G00 accepted in the [branch tracker](member-branch-workflow.md#component-branch-status). |
| External integrations and device behavior | Interfaces can be planned at G00; live decisions are owner gates | Agree cross-member data contracts and adapter boundaries before parallel work. Select and verify the map provider, Open-Meteo data policy, IT3091 wire/auth contract and Member 4 image storage policy before accepting each live integration. |
| Executable Agentic AI | Correctly deferred; prohibited before G07 | Member branches prepare only their business workflow and typed, bounded, unavailable-aware AI integration seams. Start executable agents, tools, orchestration, model calls and AI-owned state on `agentic-ai/**` only after all four components pass G07. |
| Full assessment and release | Not ready | The backend test workflow's path filters do not satisfy the guideline's every-push/every-pull-request-to-`main` requirement. The known Flutter widget-test reference to removed `MyHomePage`, duplicate Auth test IDs, hosted mobile HTTPS configuration and release evidence also remain open; see [foundation gaps](../project/foundation-gap-analysis.md). |

### G00 exit criteria

Before any member starts component coding, record agreement on all of the
following in the linked contracts or a reviewable G00 decision record:

1. **Named owners and branches:** map each Member 1–4 label to a confirmed
   teammate, assign the four `features/<component>` branches and PR owners,
   and identify the maintainer for sequential merges and shared-file
   reconciliation. Member labels alone are not a person assignment.
2. **Shared business identities and data authority:** freeze canonical IDs,
   workflow identity, source-of-truth ownership, producer/consumer payloads,
   status meanings and cross-component references shown in the
   [relationship map](component-relationships.md).
3. **Public and private contracts:** agree the first public operation set,
   route/method and DTO ownership, validation, permission codes/resource
   scope, error/status behavior, and the private API-to-service transport and
   actor/permission propagation. Keep the existing API/Auth business flows
   unchanged; clients still call only the public API.
4. **Persistence and time:** decide how the shared PostgreSQL instance is
   divided among service-owned schemas, migrations and database credentials;
   document keys, constraints, migration ownership and concurrency approach.
   Agree the shared timestamp, time-zone, period and availability semantics
   required by the component relationships.
5. **Service and delivery identity:** assign each service folder, project,
   container and internal route/port; agree private network membership,
   environment configuration/secrets, service startup and health/readiness
   behavior, test-project discovery and CI/Compose integration. Do not invent
   colliding identifiers independently across branches.
6. **Shared client and registry edits:** agree distinct workflow IDs, React
   and Flutter route ownership, permission references and endpoint/UI catalog
   edits. Keep component modules member-specific and resolve shared route,
   navigation, catalog and Compose conflicts sequentially during PR merges.
7. **Provider and evidence seams:** agree the minimum adapter request/result
   schemas and safe unavailable/fallback behavior for map discovery,
   Open-Meteo, IT3091 inference and private image evidence. Concrete provider,
   licensing, quota, storage and retention decisions may be finalized by the
   owning member before that integration is accepted; test doubles do not
   count as live integration.
8. **Pre-G07 AI dependency behavior:** freeze typed dispatch/status meanings
   for not-connected, unavailable and available states, bounded probe and
   dispatch behavior, and retryability. Keep API liveness and database
   readiness separate. No member branch implements an agent or fabricated AI
   output.

Once these items are agreed and recorded, update G00 to accepted, create the
four feature branches from the same agreed `dev` baseline, and let all four
members implement concurrently as described in the [branch workflow](member-branch-workflow.md).

### Gates after G00

- **Before each owner accepts its component PR:** complete that component's
  live provider, media, persistence and cross-service integration decisions;
  update the endpoint catalog and UI registry for implemented public routes
  and screens; provide real integration and client-parity evidence.
- **At G07:** all four complete member PRs are merged, real producer/consumer
  paths are compatible on `dev`, integration corrections are finished, and
  component quality evidence is accepted. G07 is the only gate that opens
  executable Agentic AI implementation.
- **Before final assessment/release:** remove the backend workflow path-filter
  gap or obtain written assignment guidance; resolve the known Flutter and
  test-ID baseline issues through the repository's test-change approval
  process; verify hosted Flutter HTTPS and device behavior; and collect the
  deployment, report, APK, demonstration and access evidence.

Changing the PostgreSQL Compose bind address from `5432:5432` to
`127.0.0.1:5432:5432` on promotion to `main` is already documented. Both
mappings reserve host port `5432`; the development all-interface binding does
not resolve a collision with a PostgreSQL process already using that port.
Check the local port and trusted-network configuration during environment
setup.

### Audit evidence and limits

On 2026-09-26, endpoint-catalog validation passed (33 public endpoints, 22
frontend routes, no AI endpoints implemented), the UI-integration validator
passed, and all 23 repository agent skills validated. Markdown links and
`git diff --check` are rechecked after this documentation update. Application
tests, builds, Android/device checks, live provider calls, hosted CI and
runtime deployment were not run as part of this final documentation audit.
Passing static contract validators confirms registry consistency only; it
does not establish runtime readiness.

## Source order and status

The repository's [frozen requirements](../../PROJECT_REQUIREMENTS.md) define
BLUEVERSE v1 scope. Accepted [ADR-0016](../adr/ADR-0016-equal-client-capability-for-all-roles.md)
sets equal business capability in React and Flutter. The
[endpoint catalog](../api/endpoint-catalog.md),
[UI registry](../contracts/ui-integration.json), source, migrations and executed
tests determine what is implemented. The formal *SE3090 Assignment 1
Specification With Marking Scheme* defines assessment minima. The supplied
*BLUEVERSE Simplified Team Guide* is a 24 September 2026 orientation snapshot.
The PDFs supply scope and assessment evidence for this audit. Their prose is
not a separate instruction to the agent that overrides the user's request or
repository rules.

At this audit, the implemented routes serve the foundation, not the four v1
domain components; no Agentic AI endpoint is implemented. The four v1 domain
implementations, their domain migrations, the map-provider integration, the
agent runtime and the biodiversity inference service remain targets. Route
counts and client registration change with source: read the current catalog and UI registry and
rerun their validators before making an implementation-status claim.

Use the [component relationship map](component-relationships.md) to understand
which component owns and consumes each contract. It is a dependency view, not
a member implementation schedule. All four members implement their complete
components concurrently, each on one `features/<component>` branch and submit
one complete PR. The [member branch and integration workflow](member-branch-workflow.md)
defines the shared contract agreement, PR handling, `dev` compatibility
checks and G07 gate. The four [member work plans](README.md#component-and-agent-contract-map)
divide each component into local work areas; they do not create subcomponent
branches or require a member to wait for another member's full implementation.

## Requirement-to-contract coverage

The table is a navigation and review map. “Covered” means the target behavior
is specified in the linked contracts; it does not mean implemented or tested.
PDF page numbers refer to the simplified guide. The formal assignment's
relevant sections are listed for assessment traceability.

| Owner | Repository baseline | Simplified guide / formal assignment | Contract coverage |
|---|---|---|---|
| Member 1 | Requirements §§11–13, 28–30, 53 | Guide pp. 7, 11–14, 17; assignment §§3, 5–9, 11–12 | [Experience and biodiversity component](components/member-1-coastal-experience-biodiversity-discovery.md) and [agent](agents/member-1-coastal-experience-biodiversity-agent.md): catalogue, publication/availability, location, favourites, selected map-provider adapter, user-facing biodiversity context consuming Member 3's prediction contract, read-only tools, uncertainty and explicit unavailable states. |
| Member 2 | Requirements §§14–15, 21, 31, 53 | Guide pp. 8, 11–12, 14–15, 17; assignment §§3, 5–6, 9, 11–12 | [Marine and safety component](components/member-2-marine-conditions-safety-intelligence.md) and [agent](agents/member-2-marine-conditions-intelligence-agent.md): Open-Meteo acquisition, source/time/freshness, ordinary period query input (no separately assigned device feature), configured profiles, deterministic suitability and sourced AI report. |
| Member 3 | Requirements §§16–17, 20–27, 28–30, 53 | Guide pp. 9, 11–13, 17; assignment §§3, 5, 9–10, 11–12 | [Planner and itinerary component](components/member-3-smart-coastal-planner-itinerary-management.md) and [agent](agents/member-3-planning-coordination-agent.md): constraints, recommendations, itinerary changes/re-evaluation, backend-mediated IT3091 inference adapter, structured plan, delegation, safe assembly and cross-client date/time selection. |
| Member 4 | Requirements §§18–19, 21–27, 53 | Guide pp. 10–13, 15, 17; assignment §§3, 5, 9–10, 12 | [Operations component](components/member-4-coastal-operations-advisories-alerts.md) and [agent](agents/member-4-safety-operations-agent.md): assessments, optional private image evidence, managed state, proposals, reviewer decisions, alerts, read-only recommendation, revalidation, execution and audit. |
| Shared system | Requirements §§4–11, 20–27, 32–48, 54–55; accepted ADR-0016 | Guide pp. 2–5, 11–18, 21; assignment §§1–14, 17 | [Workflows](workflows.md), [client parity and permissions](cross-platform-and-permissions.md), [device capabilities](device-capabilities.md), [quality and delivery](quality-and-delivery.md), [Agentic AI architecture](../agentic-ai/architecture.md), [implementation blueprint](../agentic-ai/implementation-blueprint.md), [tools](../agentic-ai/tools.md), [safety](../agentic-ai/safety.md) and [evaluation](../agentic-ai/evaluation.md) cover common API, device, media, data, security, cross-client, test and assessed-flow rules. |

The formal assignment's web/mobile “primarily” wording describes suggested
usage. For BLUEVERSE, [ADR-0016](../adr/ADR-0016-equal-client-capability-for-all-roles.md)
and the requirements require every permitted role and business action in
both clients. Platform-specific layout, navigation and device input provide
the different user experiences. Flutter GPS and a React location input are
the concrete v1 example.

The selected device interactions are target requirements, not current
implementation claims: Member 1 owns one-time GPS/location discovery, Member 3
owns date/time selection for itinerary inputs, and Member 4 owns optional
image evidence on operations assessments. Member 2's time period remains a
normal marine-query field and is not treated as a separate device feature. See
the complete
[device-capability contract](device-capabilities.md). The v1 client routes and
service APIs needed to use them are still to be implemented with their owner
branches.

## Agentic development coverage

The four agent role contracts describe each member's contribution. The shared
runtime and controls also need explicit owners in the implementation plan;
none is complete merely because an agent description exists.

| Development element | Repository and assignment source | Owning contract and required evidence |
|---|---|---|
| Model, provider, retrieval and runtime selection | Requirements §§20–27, 40, 47; assignment pp. 6, 8–9, 12–13 | The [implementation blueprint](../agentic-ai/implementation-blueprint.md) distinguishes required model capabilities from unselected vendor/framework choices, sets structured tool retrieval as the current baseline, explains why RAG/vector storage is not required without an approved document corpus, and lists the ADR and deployment evidence to resolve before implementation. |
| Distinct roles and orchestration | Requirements §§20, 22–23, 53, 55; assignment §9 | The [architecture](../agentic-ai/architecture.md), [planner agent](agents/member-3-planning-coordination-agent.md) and three specialist agent contracts require four distinguishable, invoked roles, a structured plan, dependency-aware delegation and validated typed handoffs. |
| Durable workflow state | Requirements §§22, 24, 27, 55; assignment §9.1 | [Workflows](workflows.md) and [ADR-0008](../adr/ADR-0008-agent-workflow-state.md) require a shared ID, persisted plan/step/result status, bounded resume and the same public status in both clients. Design the state schema before calling this implemented. |
| Tool policy and observability | Requirements §§25–26, 40; assignment §9.1 | [Tools](../agentic-ai/tools.md) and each agent contract require allowlists, typed/validated arguments and outputs, least privilege, timeout/retry limits, correlated call outcomes and measured timings. Actual calls and audit records are the evidence. |
| Deterministic decision layer | Requirements §§21–23, 40, 55; assignment §§9–10 | [Safety](../agentic-ai/safety.md), [marine suitability](components/member-2-marine-conditions-safety-intelligence.md) and [operations](components/member-4-coastal-operations-advisories-alerts.md) require application-owned schema, freshness, availability, safety-profile, transition and permission checks. Test that model prose cannot override a blocked result. |
| Approval and protected execution | Requirements §§18–19, 22, 26–27, 41; assignment §§9–10 | The [operations component](components/member-4-coastal-operations-advisories-alerts.md), [Safety & Operations agent](agents/member-4-safety-operations-agent.md) and [canonical flow](workflows.md) require proposal, pause, authorized approve/reject/revise, revalidation, transactional execution and audit. Agent tools never perform the protected change. |
| Recovery and injection resistance | Requirements §§25–27, 40, 55; assignment §§9–10 | [Safety](../agentic-ai/safety.md) and [evaluation](../agentic-ai/evaluation.md) require invalid/malicious input, unavailable dependencies, finite retries, failure state and proof of no forbidden side effect. |
| Member feature access and dependency availability | Requirements §§20–27, 40, 53, 55; assignment §9 | The [member integration boundary](agentic-ai-integration-boundary.md) requires each `features/**` component to prepare its authorized public workflow contract, typed private Agentic AI adapter, server-side availability check and explicit not-connected/unavailable behavior before G07. This is integration scaffolding, not agent/runtime implementation. |
| Third-party map API | Assignment §11; location-aware discovery in the v1 requirements and team guide pp. 3, 7 | Member 1's private component service owns the BLUEVERSE map-provider adapter and discovery contract; `services/api` exposes it through the public boundary. Provider choice, precise feature scope, rendering compatibility, terms/attribution, quotas, key handling, caching and failure policy remain explicit implementation decisions. |
| ML and external-data boundaries | Requirements §§28–31; assignment §11 | [Member 3's planner component](components/member-3-smart-coastal-planner-itinerary-management.md) owns the BLUEVERSE IT3091 inference-service consumer adapter; IT3091 supplies the private model/service. [Member 1's experience component](components/member-1-coastal-experience-biodiversity-discovery.md) owns the user-facing biodiversity context surface and consumes the validated Member 3 contract. The paired Member 1 agent may request prediction context only through that backend contract. Member 2 separately owns Open-Meteo, and Member 1 owns the map adapter. These remain distinct from Agentic AI, with provenance, minimal location data, uncertainty and explicit unavailability. |
| Evaluation and release evidence | Requirements §§39–42, 55; assignment §§9, 12–13, 16–17 | [Evaluation](../agentic-ai/evaluation.md) and [quality and delivery](quality-and-delivery.md) require the executed four-agent golden case, deterministic negative/recovery cases, full assertions, actual timing/performance measurements, CI and reviewer-visible results. |

## Decisions the contracts cannot infer

These are real design decisions left open by the requirements and both PDFs.
Do not turn an example name, threshold, route or state into an implemented
contract by copying it from prose. Record the decision in the owning API,
database, service or ADR document, then update both clients, tests and these
contracts in the same work.

| Decision area | Owner and required output before the feature is called ready |
|---|---|
| Named ownership | The four “Member” labels have no confirmed mapping to named people. Record that mapping through the team process before attributing component work; the [account map](../project/ai-team-members.md) identifies AI-log accounts, not component ownership. |
| Service/API ownership and integration | Each owner creates one internal .NET microservice in its own `services/<component-service>/` subfolder. The service owns its public-operation behavior and domain logic. `services/api` receives only public route/authentication/permission integration and private service routing/forwarding; Auth is reused. Keep all component services private and expose their operations only under public `/api/...` paths. See [ADR-0020](../adr/ADR-0020-member-component-service-boundaries.md). |
| Public APIs and permissions | Each owner defines at least four meaningful public operations, exact methods/paths, DTOs, validation/error responses, permission codes and resource scope. The member service implements them; the existing API integrates/routes them without owning member business logic. Register only implemented public and private routes in the [endpoint catalog](../api/endpoint-catalog.md); public paths stay under `/api/...` without a path-version segment. The public API also needs workflow initiation, status, execution summaries and reviewer decisions where applicable. |
| Database and audit | Each service owner defines normalized entities, keys, relationships, constraints, indexes, suitable PostgreSQL types, EF Core migrations, audit fields, useful seed data and transaction/concurrency rules. Complete the [schema/ER documentation](../database/schema.md) and obtain real PostgreSQL evidence for provider-specific behavior. Agree shared PostgreSQL and component schema ownership at G00. |
| Member 1 catalogue, maps and biodiversity presentation | Finalize catalogue states/transitions, schedule time-zone and availability rules, nearby-query semantics, favourite targets/ownership, and how sourced Member 3 biodiversity results are presented in destination/activity contexts. Select a map provider and supported functions; verify its terms support ASP.NET-mediated access and the chosen display approach; define credential restrictions, attribution, quota/rate behavior, cache/retention and fallback. Member 1 consumes the Member 3 public contract and does not implement the ML adapter. |
| Member 2 environmental policy | Select only needed Open-Meteo variables; define unit/time normalization, source/freshness limits, activity profile criteria and defensible threshold provenance, profile changes, missing-data/`UNKNOWN` behavior and provider retry/rate handling. The LLM does not choose these values. |
| Member 3 planner and ML integration | Define recommendation eligibility/ranking and missing-evidence behavior, itinerary ownership/ordering/duplicates, re-evaluation triggers and comparison/confirmation semantics, plus workflow-status and result-version contracts. Resolve the private IT3091 request/result schema, authentication, timeouts, finite retries, location minimization, provenance/freshness, caching/retention and explicit unavailable/invalid outcomes. A deterministic `UNSUITABLE` result must remain excluded after AI assembly; a biodiversity prediction is contextual and never an automatic safety/operations decision. |
| Member 4 operations and approval | Define operational and proposal/decision state machines, permitted transitions, high-impact policy, alert severity/lifecycle, reviewer permission and separation of duties, proposal version/expiry, revision, stale/duplicate/concurrent decision behavior, revalidation, transaction and audit. No agent may execute a protected change. |
| Member 4 image evidence | Before implementation, select accepted image formats and limits, private storage provider/configuration, content inspection/sanitization, attachment/version lifecycle, reviewer access, deletion/retention and failure behavior. Keep storage private behind the public API and Member 4 service; raw media is not a Safety & Operations Agent input. Follow [ADR-0018](../adr/ADR-0018-assessment-evidence-storage-boundary.md). |
| Agent runtime and tools | Resolve proposed [ADR-0007](../adr/ADR-0007-agentic-ai-framework.md) and [ADR-0008](../adr/ADR-0008-agent-workflow-state.md). Version typed input/output schemas, agent/step/tool allowlists, state storage and retention, correlation and timing summaries, timeouts, finite retries, resume/idempotency and safe failure. Define measurable golden-case and negative-case release gates in the [evaluation contract](../agentic-ai/evaluation.md). |
| Model, provider and retrieval | The [Agentic AI implementation blueprint](../agentic-ai/implementation-blueprint.md) records the model capability contract, institution/no-cost and privacy review, version/fallback policy, structured-tool retrieval baseline, and the conditions and controls required before adding RAG, embeddings or vector storage. Select and document these before the AI runtime is accepted; do not imply a provider or RAG stack is already chosen. |
| Backend Agentic dependency health | Finalize at G00 the configured private health/dispatch contract, `not connected`/unavailable outcomes, bounded timeouts/retryability, status persistence and public workflow representation. Preserve `GET /api/health` as liveness and keep database readiness separate. Exact routes and schemas remain implementation decisions; see the [integration boundary](agentic-ai-integration-boundary.md). |
| Both clients and deployment | For every new UI, register the same workflow ID, React route, Flutter route and public API references in the [UI registry](../contracts/ui-integration.json). Cover every permitted/denied role action in both clients. Resolve the hosted HTTPS configuration gap in the current [Flutter gateway configuration](../../apps/mobile/lib/data/services/api_gateway_config.dart) before mobile release evidence. |
| Shared reporting and evidence | Choose real application data and useful analytics views for both clients; do not use decorative counters. Keep stable requirement-linked test IDs, backend/real-PostgreSQL/client/AI/integration cases, CI results (including backend tests on every push and pull request to `main`), performance measurements, deployment URLs/startup and each owner's Git/PR evidence. See [quality and delivery](quality-and-delivery.md). |

## Component relationships and shared integration gate

The [component relationship map](component-relationships.md) is the complete
visual and tabular description of producer/consumer contracts. Its key
relationships are:

- **Member 1 ↔ Member 4:** Member 1 owns canonical experience identities,
  schedule and availability evidence; Member 4 owns managed operational state
  and restrictions consumed by Member 1 when it computes effective
  availability. Agree both schemas at G00 and develop in parallel.
- **Members 1, 2 and 4 → Member 3:** Member 3's deterministic
  recommendations consume Member 1 publication/availability, Member 2
  condition/suitability, and Member 4 operational restrictions. Implement
  against the agreed contracts and contract-level test doubles while all four
  branches are in progress.
- **Members 1, 2 and 3 → Member 4:** Member 4's assessment and review workflow
  consumes experience evidence, environmental evidence/suitability and
  Member 3's business workflow identity. It does not consume Agentic AI
  execution state as its business source of truth.
- **Member 2's external source:** backend-mediated Open-Meteo acquisition and
  normalization can develop alongside the other member components. Activity
  suitability uses the shared Member 1 activity taxonomy contract.
- **Member 1's map API:** Member 1 owns the map-provider adapter and location-
  discovery consumer contract in its private component service; the provider
  is not the canonical destination store, and both clients reach it through
  the public API. The vendor and
  capability scope must be selected and documented before a live integration
  is accepted.
- **Member 3's ML API:** Member 3 owns BLUEVERSE's private consumer adapter
  and public prediction contract for IT3091 biodiversity inference. Member 1
  owns the user-facing experience and consumes that validated contract. The
  IT3091 workstream supplies the model and inference service; it is not an
  Agentic AI agent. See [ADR-0019](../adr/ADR-0019-biodiversity-inference-integration-ownership.md).

G00 means the team has agreed the IDs, data ownership, public/private
boundaries, schemas, permission/error behavior, time semantics and shared
workflow identity needed for parallel implementation. It does not mean a
provider component has been implemented or merged. Each member then completes
all work areas on one branch and submits one component PR. The maintainer
merges the four PRs without a prescribed member order and resolves merge
conflicts. Afterward, the team verifies the real producer/consumer paths on
`dev` and fixes compatibility issues there. G07 is accepted only after all
four components are present, their integration corrections are complete, and
the component, parity, test and documentation evidence is recorded. See the
[branch and integration workflow](member-branch-workflow.md) for the status
tracker and detailed handoffs.

Before G07, member components may implement their public workflow APIs,
typed private Agentic AI access adapters, availability checks and safe
not-connected/unavailable behavior. Those are component-owned integration
boundaries. Actual agents, tools, model calls, orchestration and AI-owned
execution state are deferred to `agentic-ai/**` branches until all four
member components pass G07. The suggested work after that gate is in the
[branch workflow](member-branch-workflow.md#agentic-ai-work-after-g07).

Each member work-area plan groups the complete component scope and identifies
contract dependencies. Its numbered headings are not a required implementation
schedule: each owner implements the full component on one feature branch;
internal work areas may overlap where local technical dependencies allow, and
all four member branches can proceed concurrently after G00. The
[component relationship map](component-relationships.md) is the dependency
reference and does not assign a member-by-member start order. Submit one
complete feature PR per member to `dev`; the maintainer resolves merge
conflicts, then the team checks compatibility and fixes integration defects
on `dev` before G07. Shared endpoint/UI registry changes are reconciled and
regenerated sequentially during the PR merges. Actual Agentic AI
implementation then uses `agentic-ai/**` branches only after G07; member
branches prepare the API, adapter and dependency-unavailable behavior
beforehand.

## Evidence each owner must retain

Each member should be able to point from their own component and agent
contracts to an ASP.NET Core service/endpoint, an EF Core migration and
PostgreSQL data, React and Flutter workflows, tests with requirement-linked
case IDs, agent input/output/tool/evaluation evidence, relevant ADR or API
documentation, and traceable Git/PR work. The team must be able to show the
complete Flutter-initiated, React-reviewed assessment and the equivalent
authorized actions in both clients. Passing prose, a stub route, a generated
test that was not run, or a plausible AI sentence is not implementation
evidence. Every student's assessed individual AI reflection is written by
that student.
