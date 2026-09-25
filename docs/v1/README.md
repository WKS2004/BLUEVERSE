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
ASP.NET Core API, PostgreSQL-backed business state and server-owned
role-to-permission checks. Device input and responsive presentation may differ
without changing capability coverage. See
[cross-platform and permissions](cross-platform-and-permissions.md).

## Component and agent contract map

Each row links the complete business-component contract and the paired,
separate AI-agent contract. Read both for work that crosses the agent/API
boundary. Member numbers are ownership labels only; use the confirmed account
mapping for contribution records.

| Member label | Component contract | Core responsibility | Required business operation | Paired agent contract and responsibility |
|---|---|---|---|---|
| 1 (`v1.component.experience-biodiversity`) | [Coastal Experience & Biodiversity Discovery](components/member-1-coastal-experience-biodiversity-discovery.md) | Destinations, activities, offerings, schedules/availability, discovery, favourites and BLUEVERSE biodiversity integration | Publication and availability evaluation | [Coastal Experience & Biodiversity Agent](agents/member-1-coastal-experience-biodiversity-agent.md): sourced experience context and optional, uncertainty-aware biodiversity context |
| 2 (`v1.component.marine-safety`) | [Marine Conditions & Safety Intelligence](components/member-2-marine-conditions-safety-intelligence.md) | Backend-mediated weather/marine data, provenance/freshness, activity safety profiles and deterministic suitability | Activity/location/time suitability assessment | [Marine Conditions Intelligence Agent](agents/member-2-marine-conditions-intelligence-agent.md): sourced, time-aware marine context; no invented thresholds or authority |
| 3 (`v1.component.coastal-planner`) | [Smart Coastal Planner & Itinerary Management](components/member-3-smart-coastal-planner-itinerary-management.md) | Coastal recommendation requests, planning/delegation, assembly, itineraries and re-evaluation | Itinerary re-evaluation | [Planning & Coordination Agent](agents/member-3-planning-coordination-agent.md): structured workflow plan, specialist delegation, dependency tracking and assembly |
| 4 (`v1.component.coastal-operations`) | [Coastal Operations, Advisories & Alerts](components/member-4-coastal-operations-advisories-alerts.md) | Operational assessment/state, approval, alerts/advisories and execution history | Approve, reject or request revision, followed by controlled execution where eligible | [Safety & Operations Agent](agents/member-4-safety-operations-agent.md): structured recommendation/proposal; read-only and never the executor |

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

Each member contributes identifiable ASP.NET Core, PostgreSQL/EF Core, React,
Flutter, test, documentation, Git/GitHub and Agentic AI work. Each component
must contain meaningful relational data, at least four meaningful public API
endpoints and a business operation beyond CRUD. This index and the contracts
do not name unimplemented API routes or declare target components complete.

## Shared system behavior

- [V1 workflows](workflows.md) defines the assessed operational assessment and
  tourist recommendation path, including delegation, deterministic
  validation, approval, status return and safe failure.
- [Quality and delivery](quality-and-delivery.md) groups the v1 verification,
  integration, deployment and evidence requirements.
- [Requirements coverage and readiness](requirements-coverage-and-readiness.md)
  maps each component and agent to repository and assignment requirements,
  records the checked implementation status, and lists decisions and evidence
  required before v1 can be claimed complete.
- [Cross-platform and permissions](cross-platform-and-permissions.md) defines
  equal React/Flutter capability and server-side permission boundaries.
- [Agentic AI architecture](../agentic-ai/architecture.md) explains planning,
  delegation, controlled tools, deterministic validation and human approval.
- [UI integration](../development/ui-integration.md) defines the shared
  workflow-ID, route and public API contract.
- The [endpoint catalog](../api/endpoint-catalog.md) and
  [UI integration registry](../contracts/ui-integration.json) are the live
  inventories for implemented routes and screens; proposed capabilities in
  component contracts are not current endpoint declarations.

## Scope boundary

The [v0 guide](../v0/README.md) documents the shared foundation. v1 delivers
coastal tourism and operations, including
Open-Meteo and internal biodiversity-ML integration. Environmental-authority,
incident and pollution-response workflows belong to v2; fisheries and coastal
livelihood workflows belong to v3. Generic booking, payment, social-network,
emergency-dispatch and governmental beach-closure capabilities are not v1
requirements.
