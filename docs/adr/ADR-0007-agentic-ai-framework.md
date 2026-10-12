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

## Container image preparation — 2026-10-13

At the user's direction, the repository has four opt-in, per-role Docker
images based on `dhi.io/python:3.13-alpine`, wired to the private Docker
network. Three remain runtime shells. Planning Coordination has a standard
library HTTP runtime and a provider-neutral OpenAI-compatible adapter. This
is a provisional implementation choice for the assigned component, not
team-wide framework approval. The service is disabled by default. No
model/provider is selected or approved; configuration must meet the
assignment's institutional/no-cost requirement and pass data-handling review.
Other specialist services remain unimplemented.

## Consequences

The eventual decision must cover planning, delegation, allowlisted tools,
persisted state, deterministic validation, authorized approval, failure
recovery and evaluation. Record options and the selected runtime before
marking this ADR accepted.
