# ADR-0007: Agentic AI Framework

**Status:** Proposed

## Context

The v1 business components, four distinct agent responsibilities and
canonical assessed workflow are now defined in
[the v1 guide](../v1/README.md). The assignment permits multiple frameworks
or custom orchestration. The framework choice remains open and must support
the fixed workflow rather than redefine it.

## Decision

Select and justify a framework or custom orchestration approach before
implementing the four-agent workflow. This proposed ADR does not claim a
framework has been selected or that an executable service exists.

## Member 1 runtime implementation — 2026-10-13

The team owner reported G07 acceptance and approval to begin agent work. The
Member 1 branch now implements a stateless, standard-library Python HTTP
specialist with read-only typed tools. Optional Google AI Studio use is limited
to selecting validated report sections; deterministic code renders the
summary. The runtime has no durable agent state and does not orchestrate other
agents. The Compose profile contains this runtime plus three future role
shells.

This scoped implementation does not choose the framework, persistence model,
orchestration topology or shared contracts for the complete four-agent
workflow. Keep this ADR Proposed until those broader decisions are reviewed.

## Consequences

The eventual decision must cover planning, delegation, allowlisted tools,
persisted state, deterministic validation, authorized approval, failure
recovery and evaluation. Record options and the selected runtime before
marking this ADR accepted.
