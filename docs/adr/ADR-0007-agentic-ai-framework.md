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

## Consequences

The eventual decision must cover planning, delegation, allowlisted tools,
persisted state, deterministic validation, authorized approval, failure
recovery and evaluation. Record options and the selected runtime before
marking this ADR accepted.
