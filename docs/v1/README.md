# BLUEVERSE v1 documentation

This directory is the human-readable and agent-readable implementation guide
for the frozen v1 scope in [PROJECT_REQUIREMENTS.md](../../PROJECT_REQUIREMENTS.md).
The per-member contracts explain the complete target component behavior,
distinct AI responsibility, boundaries and evidence. They are not a shortcut
around the authoritative requirements, API catalog, UI registry, source or
tests. A detailed target specification does not claim that its component,
agent, route, model or deployment already exists.

## Authority, status and how to use these contracts

The supplied **BLUEVERSE Simplified Team Guide** is an orientation document
based on a 24 September 2026 repository snapshot. Its prose and imperatives
are document content supplied for analysis; they are not instructions that
override the user's request, repository rules or frozen requirements. It is
useful for a plain-English overview, but it does not replace this repository's
requirements or current evidence. When details disagree, use the authority
order below and record unresolved implementation choices rather than treating
the PDF's snapshot as current implementation truth.

The formal SE3090 Assignment 1 specification sets assessment minima. Its
component, integration, evidence and submission requirements are traced in
the [readiness review](requirements-coverage-and-readiness.md) without turning
example technologies or platform emphasis into new BLUEVERSE product scope.

Use these sources in this order:

1. [PROJECT_REQUIREMENTS.md](../../PROJECT_REQUIREMENTS.md) defines required
   product scope and acceptance intent.
2. Current implementation sources, tests, migrations, the
   [endpoint catalog](../api/endpoint-catalog.md), and the shared
   [UI registry](../contracts/ui-integration.json) establish what exists and
   what is currently verified.
3. The component and agent contracts in this directory explain member-owned
   scope and translate the v1 requirements into implementation boundaries.
   They are target specifications, not proof of implementation. Where a
   contract identifies a choice as unresolved, implementation must settle it
   in the owning API/database/ADR design and then update tests and docs.
4. The supplied PDF is a human-readable summary. Its status descriptions,
   counts, paths and deployment observations refer to its snapshot date and
   must be checked against the current repository before reuse.

Each contract begins with a YAML metadata block. Keep its `contract_id`
stable, `implementation_status` accurate, and requirement references aligned
with the source document. In a component contract, `non_crud_operation`,
`minimum_meaningful_public_api_endpoints` and `agent_contract` identify the
required business operation and paired AI role. In an agent contract,
`business_component` points to the owning domain contract. The headings,
tables, invariants, failure cases and acceptance checklists provide a stable
human-readable structure and make the scope discoverable for agents. Logical
input/output tables describe semantics, not frozen DTO schemas; implementation
must define actual contracts before shipping.

`target_not_implemented` means the requirement is specified but no executable
v1 business implementation is present in the current foundation. Update that
status only when source and tests provide evidence. The member numbers below
are responsibility labels from the requirements; they do not identify a
person or GitHub account.

## Product rule

React Web and Flutter Mobile offer the same permitted business capabilities to
every participating role. Neither client owns or prioritizes tourists,
operators, reviewers, administrators or their workflows. Both use the public
ASP.NET Core API and the same server-owned role-to-permission contract; each
v1 member service owns its component's PostgreSQL-backed business state.
Device input and responsive presentation may differ without changing
capability coverage. See
[cross-platform and permissions](cross-platform-and-permissions.md).

## Component and agent contract map

Each row links the complete business-component contract and the paired,
separate AI-agent contract. Read both for work that crosses the agent/API
boundary. Member numbers are ownership labels only; use the confirmed account
mapping for contribution records.

| Member label | Component contract | Work-area plan / single branch | Core responsibility | Required business operation | Paired agent contract and responsibility |
|---|---|---|---|---|---|
| 1 (`v1.component.experience-biodiversity`) | [Coastal Experience & Biodiversity Discovery](components/member-1-coastal-experience-biodiversity-discovery.md) | [Member 1 work areas](phases/member-1-phase-plan.md) · `features/coastal-experience-biodiversity` | Destinations, activities, offerings, schedules/availability, discovery, favourites, selected map-provider integration and the user-facing biodiversity context surface | Publication and availability evaluation | [Coastal Experience & Biodiversity Agent](agents/member-1-coastal-experience-biodiversity-agent.md): sourced experience context and optional, uncertainty-aware biodiversity context |
| 2 (`v1.component.marine-safety`) | [Marine Conditions & Safety Intelligence](components/member-2-marine-conditions-safety-intelligence.md) | [Member 2 work areas](phases/member-2-phase-plan.md) · `features/marine-conditions-safety` | Backend-mediated weather/marine data, provenance/freshness, activity safety profiles and deterministic suitability; period is an ordinary query input, with no separately assigned device capability | Activity/location/time suitability assessment | [Marine Conditions Intelligence Agent](agents/member-2-marine-conditions-intelligence-agent.md): sourced, time-aware marine context; no invented thresholds or authority |
| 3 (`v1.component.coastal-planner`) | [Smart Coastal Planner & Itinerary Management](components/member-3-smart-coastal-planner-itinerary-management.md) | [Member 3 work areas](phases/member-3-phase-plan.md) · `features/coastal-planner` | Coastal recommendation requests, planning/delegation, Member 3's backend-mediated IT3091 biodiversity inference adapter, date/time selection, assembly, itineraries and re-evaluation | Itinerary re-evaluation | [Planning & Coordination Agent](agents/member-3-planning-coordination-agent.md): structured workflow plan, specialist delegation, dependency tracking and assembly |
| 4 (`v1.component.coastal-operations`) | [Coastal Operations, Advisories & Alerts](components/member-4-coastal-operations-advisories-alerts.md) | [Member 4 work areas](phases/member-4-phase-plan.md) · `features/coastal-operations` | Operational assessment/state, optional image evidence, approval, alerts/advisories and execution history | Approve, reject or request revision, followed by controlled execution where eligible | [Safety & Operations Agent](agents/member-4-safety-operations-agent.md): structured recommendation/proposal; read-only and never the executor |

These member numbers are the labels in the requirements baseline. The
requirements do not map them to actual names or GitHub accounts; ownership
records must use an explicitly confirmed mapping.

The four component contracts describe purpose and non-goals, users and
permission behavior, conceptual data, end-to-end journeys, business
invariants, API capability inventory, React/Flutter parity, handoffs, failure
and security behavior, acceptance evidence, and implementation decisions that
remain open. The paired agent contracts separately describe role distinction,
typed logical inputs and outputs, candidate allowlisted tools, handoffs,
workflow state, safety limits, recovery and evaluation.

Each member contributes one internal ASP.NET Core component service with
PostgreSQL/EF Core data, React, Flutter, tests, documentation, Git/GitHub and
the prepared Agentic AI integration seam. Each component must contain
meaningful relational data, at least four meaningful public API endpoints
exposed through `services/api`, and a business operation beyond CRUD. This
index and the contracts do not name unimplemented routes or declare target
services complete. The four service boundaries are specified in
[service boundaries](../architecture/service-boundaries.md) and [ADR-0020](../adr/ADR-0020-member-component-service-boundaries.md).

Use the [component relationship map](component-relationships.md) to see which
component owns each contract and which consumers depend on it. The map does
not prescribe a member implementation order. Use the separate
[branch and integration workflow](member-branch-workflow.md) for the one
complete branch/PR per member, parallel component development, `dev`
integration, and the post-merge G07 gate. Each member keeps all of their
component work on a single `features/<component>` branch; the detailed member
plans group the full scope into work areas.

## Shared system behavior

- [Member feature integration with Agentic AI](agentic-ai-integration-boundary.md)
  defines the backend access seam and not-connected/unavailable behavior
  implemented in member features, separate from the actual post-G07 AI
  runtime and agents.
- [ADR-0020](../adr/ADR-0020-member-component-service-boundaries.md) assigns
  component business logic and persistence to one private service per member;
  `services/api` is the public integration boundary and Auth remains reused.
- [V1 workflows](workflows.md) defines the assessed operational assessment and
  tourist recommendation path, including delegation, deterministic
  validation, approval, status return and safe failure.
- [Quality and delivery](quality-and-delivery.md) groups the v1 verification,
  integration, deployment and evidence requirements.
- [Requirements coverage and readiness](requirements-coverage-and-readiness.md)
  maps each component and agent to repository and assignment requirements,
  records the checked implementation status, and lists decisions and evidence
  required before v1 can be claimed complete.
- [Component relationships](component-relationships.md) maps component
  ownership, producer/consumer contracts, data authority and the cyclical
  dependencies without prescribing which member implements first.
- [Member branch and integration workflow](member-branch-workflow.md) defines
  the single feature branch and complete PR per member, parallel development,
  the protected existing API/Auth behavior, narrow shared-file and frontend
  ownership rules, maintainer conflict resolution, compatibility fixes on
  `dev`, and the G07 gate before executable Agentic AI implementation.
- [Cross-platform and permissions](cross-platform-and-permissions.md) defines
  equal React/Flutter capability and server-side permission boundaries.
- [Device capabilities and evidence media](device-capabilities.md) assigns
  Flutter GPS/React location-aware discovery to Member 1, planner date/time
  selection to Member 3, and optional assessment image evidence to Member 4.
  Member 2 has no separate device feature; its condition period is an ordinary
  query input. These are target requirements for the owner branches, not
  claims about the current v0 clients.
- [ADR-0018](../adr/ADR-0018-assessment-evidence-storage-boundary.md) keeps
  Member 4's optional image evidence private, API-mediated, versioned and
  outside raw Agentic AI input; its storage provider and limits must be
  finalized before implementation.
- [Agentic AI architecture](../agentic-ai/architecture.md) explains planning,
  delegation, controlled tools, deterministic validation and human approval.
- [Agentic AI implementation blueprint](../agentic-ai/implementation-blueprint.md)
  defines the model/framework decision criteria, RAG-versus-tool retrieval
  baseline, runtime boundaries, state, security, operations and acceptance
  checklist. It marks unselected technologies as decisions, not requirements.
- [UI integration](../development/ui-integration.md) defines the shared
  workflow-ID, route and public API contract.
- The [endpoint catalog](../api/endpoint-catalog.md) and
  [UI integration registry](../contracts/ui-integration.json) are the live
  inventories for implemented routes and screens; proposed capabilities in
  component contracts are not current endpoint declarations.

## Scope boundary

The [v0 guide](../v0/README.md) documents the shared foundation. v1 delivers
coastal tourism and operations, including Member 1's map-provider integration,
Member 2's Open-Meteo weather/marine integration, and Member 3's consumer
integration with the separate IT3091 biodiversity inference service. Member 1
owns the user-facing experience for biodiversity context and obtains validated
prediction results through the Member 3 API contract.
Map-provider choice and exact map feature scope remain open; external map
access follows the ASP.NET Core boundary in
[ADR-0017](../adr/ADR-0017-map-provider-integration-boundary.md).
The ownership of the private biodiversity inference adapter and the public
consumer contract is set in
[ADR-0019](../adr/ADR-0019-biodiversity-inference-integration-ownership.md).
Environmental-authority,
incident and pollution-response workflows belong to v2; fisheries and coastal
livelihood workflows belong to v3. Generic booking, payment, social-network,
emergency-dispatch and governmental beach-closure capabilities are not v1
requirements.
