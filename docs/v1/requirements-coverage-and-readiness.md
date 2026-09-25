# v1 requirements coverage and development readiness

This is the handoff checklist for the four member components and their four
distinct Agentic AI roles. It records the 25 September 2026 document audit;
it is not evidence that the v1 services exist. Start with the
[v1 index](README.md) and the owning component and agent contracts, then use
this page to resolve cross-component decisions and collect implementation
evidence.

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
implementations, their domain migrations, the agent runtime and the
biodiversity inference service remain targets. Route counts and client
registration change with source: read the current catalog and UI registry and
rerun their validators before making an implementation-status claim.

## Requirement-to-contract coverage

The table is a navigation and review map. “Covered” means the target behavior
is specified in the linked contracts; it does not mean implemented or tested.
PDF page numbers refer to the simplified guide. The formal assignment's
relevant sections are listed for assessment traceability.

| Owner | Repository baseline | Simplified guide / formal assignment | Contract coverage |
|---|---|---|---|
| Member 1 | Requirements §§12–13, 28–30, 53 | Guide pp. 7, 11–14, 17; assignment §§3, 5–9, 11–12 | [Experience and biodiversity component](components/member-1-coastal-experience-biodiversity-discovery.md) and [agent](agents/member-1-coastal-experience-biodiversity-agent.md): catalogue, publication/availability, location, favourites, model-backed context, read-only tools, uncertainty and unavailable inference. |
| Member 2 | Requirements §§14–15, 21, 31, 53 | Guide pp. 8, 11–12, 14–15, 17; assignment §§3, 5–6, 9, 11–12 | [Marine and safety component](components/member-2-marine-conditions-safety-intelligence.md) and [agent](agents/member-2-marine-conditions-intelligence-agent.md): Open-Meteo acquisition, source/time/freshness, configured profiles, deterministic suitability and sourced AI report. |
| Member 3 | Requirements §§16–17, 20–27, 53 | Guide pp. 9, 11–13, 17; assignment §§3, 5, 9–10, 12 | [Planner and itinerary component](components/member-3-smart-coastal-planner-itinerary-management.md) and [agent](agents/member-3-planning-coordination-agent.md): constraints, recommendations, itinerary changes/re-evaluation, structured plan, delegation and safe assembly. |
| Member 4 | Requirements §§18–19, 21–27, 53 | Guide pp. 10–13, 15, 17; assignment §§3, 5, 9–10, 12 | [Operations component](components/member-4-coastal-operations-advisories-alerts.md) and [agent](agents/member-4-safety-operations-agent.md): assessments, managed state, proposals, reviewer decisions, alerts, read-only recommendation, revalidation, execution and audit. |
| Shared system | Requirements §§4–11, 20–27, 32–48, 54–55; accepted ADR-0016 | Guide pp. 2–5, 11–18, 21; assignment §§1–14, 17 | [Workflows](workflows.md), [client parity and permissions](cross-platform-and-permissions.md), [quality and delivery](quality-and-delivery.md), [Agentic AI architecture](../agentic-ai/architecture.md), [tools](../agentic-ai/tools.md), [safety](../agentic-ai/safety.md) and [evaluation](../agentic-ai/evaluation.md) cover the common API, data, security, cross-client, test and assessed-flow rules. |

The formal assignment's web/mobile “primarily” wording describes suggested
usage. For BLUEVERSE, [ADR-0016](../adr/ADR-0016-equal-client-capability-for-all-roles.md)
and the requirements require every permitted role and business action in
both clients. Platform-specific layout, navigation and device input provide
the different user experiences. Flutter GPS and a React location input are
the concrete v1 example.

## Agentic development coverage

The four agent role contracts describe each member's contribution. The shared
runtime and controls also need explicit owners in the implementation plan;
none is complete merely because an agent description exists.

| Development element | Repository and assignment source | Owning contract and required evidence |
|---|---|---|
| Distinct roles and orchestration | Requirements §§20, 22–23, 53, 55; assignment §9 | The [architecture](../agentic-ai/architecture.md), [planner agent](agents/member-3-planning-coordination-agent.md) and three specialist agent contracts require four distinguishable, invoked roles, a structured plan, dependency-aware delegation and validated typed handoffs. |
| Durable workflow state | Requirements §§22, 24, 27, 55; assignment §9.1 | [Workflows](workflows.md) and [ADR-0008](../adr/ADR-0008-agent-workflow-state.md) require a shared ID, persisted plan/step/result status, bounded resume and the same public status in both clients. Design the state schema before calling this implemented. |
| Tool policy and observability | Requirements §§25–26, 40; assignment §9.1 | [Tools](../agentic-ai/tools.md) and each agent contract require allowlists, typed/validated arguments and outputs, least privilege, timeout/retry limits, correlated call outcomes and measured timings. Actual calls and audit records are the evidence. |
| Deterministic decision layer | Requirements §§21–23, 40, 55; assignment §§9–10 | [Safety](../agentic-ai/safety.md), [marine suitability](components/member-2-marine-conditions-safety-intelligence.md) and [operations](components/member-4-coastal-operations-advisories-alerts.md) require application-owned schema, freshness, availability, safety-profile, transition and permission checks. Test that model prose cannot override a blocked result. |
| Approval and protected execution | Requirements §§18–19, 22, 26–27, 41; assignment §§9–10 | The [operations component](components/member-4-coastal-operations-advisories-alerts.md), [Safety & Operations agent](agents/member-4-safety-operations-agent.md) and [canonical flow](workflows.md) require proposal, pause, authorized approve/reject/revise, revalidation, transactional execution and audit. Agent tools never perform the protected change. |
| Recovery and injection resistance | Requirements §§25–27, 40, 55; assignment §§9–10 | [Safety](../agentic-ai/safety.md) and [evaluation](../agentic-ai/evaluation.md) require invalid/malicious input, unavailable dependencies, finite retries, failure state and proof of no forbidden side effect. |
| ML and external-data boundaries | Requirements §§28–31; assignment §11 | The [experience component](components/member-1-coastal-experience-biodiversity-discovery.md), [marine component](components/member-2-marine-conditions-safety-intelligence.md) and paired agents keep biodiversity inference and Open-Meteo behind the API, with provenance, minimal shared data, uncertainty and explicit unavailability. |
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
| Public APIs and permissions | Each owner defines at least four meaningful public operations, exact methods/paths, DTOs, validation/error responses, permission codes and resource scope. Register only implemented routes in the [endpoint catalog](../api/endpoint-catalog.md); new routes stay under `/api/...` without a path-version segment. The API also needs workflow initiation, status, execution summaries and reviewer decisions where applicable. |
| Database and audit | Each owner defines normalized entities, keys, relationships, constraints, indexes, suitable PostgreSQL types, EF Core migrations, audit fields, useful seed data and transaction/concurrency rules. Complete the [schema/ER documentation](../database/schema.md) and obtain real PostgreSQL evidence for provider-specific behavior. |
| Member 1 publication and inference | Finalize catalogue states/transitions, schedule time-zone and availability rules, nearby-query semantics, favourite targets/ownership, and the internal biodiversity request/result, version, uncertainty and unavailable response. The actual trained-model path must work when available. |
| Member 2 environmental policy | Select only needed Open-Meteo variables; define unit/time normalization, source/freshness limits, activity profile criteria and defensible threshold provenance, profile changes, missing-data/`UNKNOWN` behavior and provider retry/rate handling. The LLM does not choose these values. |
| Member 3 planner behavior | Define recommendation eligibility/ranking and missing-evidence behavior, itinerary ownership/ordering/duplicates, re-evaluation triggers and comparison/confirmation semantics, plus workflow-status and result-version contracts. A deterministic `UNSUITABLE` result must remain excluded after AI assembly. |
| Member 4 operations and approval | Define operational and proposal/decision state machines, permitted transitions, high-impact policy, alert severity/lifecycle, reviewer permission and separation of duties, proposal version/expiry, revision, stale/duplicate/concurrent decision behavior, revalidation, transaction and audit. No agent may execute a protected change. |
| Agent runtime and tools | Resolve proposed [ADR-0007](../adr/ADR-0007-agentic-ai-framework.md) and [ADR-0008](../adr/ADR-0008-agent-workflow-state.md). Version typed input/output schemas, agent/step/tool allowlists, state storage and retention, correlation and timing summaries, timeouts, finite retries, resume/idempotency and safe failure. Define measurable golden-case and negative-case release gates in the [evaluation contract](../agentic-ai/evaluation.md). |
| Both clients and deployment | For every new UI, register the same workflow ID, React route, Flutter route and public API references in the [UI registry](../contracts/ui-integration.json). Cover every permitted/denied role action in both clients. Resolve the hosted HTTPS configuration gap in the current [Flutter gateway configuration](../../apps/mobile/lib/data/services/api_gateway_config.dart) before mobile release evidence. |
| Shared reporting and evidence | Choose real application data and useful analytics views for both clients; do not use decorative counters. Keep stable requirement-linked test IDs, backend/real-PostgreSQL/client/AI/integration cases, CI results (including backend tests on every push and pull request to `main`), performance measurements, deployment URLs/startup and each owner's Git/PR evidence. See [quality and delivery](quality-and-delivery.md). |

## Integrated delivery sequence

The four ownership areas can progress in parallel, but the assessed workflow
depends on the following handoffs:

1. Establish the role-to-permission matrix, domain identifiers, data owners,
   public API contracts and decisions above. Material architecture choices
   receive accepted ADRs before they are claimed as final.
2. Implement each domain model, migration, application service, public API
   operations and requirement-based tests. Keep source, OpenAPI, endpoint
   catalog and database documentation aligned.
3. Implement corresponding React and Flutter experiences for every permitted
   role/action, register both routes and API references under one workflow ID,
   and check normal, denied, empty, stale and dependency-failure paths.
4. Add backend-mediated Open-Meteo and internal biodiversity inference with
   validated responses, source metadata, minimal data sharing and explicit
   unavailability behavior.
5. Implement four distinct agents, typed specialist handoffs, controlled
   tools, durable state, deterministic validation, reviewer approval and
   transactional execution. Keep tool and approval evidence reviewable.
6. Run the tourist and canonical operational flows end to end, including
   rejection/revision, timeout/retry, prompt injection, safe failure,
   concurrency and status return. Record actual evaluation and performance
   results; document deployment and demonstrate the system without external
   AI assistance during the viva.

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
