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
completing the four-agent workflow. Private component runtimes now exist for
Experience & Biodiversity, Marine Conditions, and Planning & Coordination,
but this proposed ADR does not claim that a team-wide framework has been
selected.

## Container image preparation — 2026-10-13

At the user's direction, the repository has four opt-in, per-role Docker
images based on `dhi.io/python:3.13-alpine`, wired to the private Docker
network. Experience & Biodiversity, Marine Conditions, and Planning &
Coordination have private runtimes; Safety Operations remains a shell.
Planning & Coordination uses a standard-library HTTP runtime and a
provider adapter. These are provisional component implementations, not
team-wide framework approval. Planner dispatch is disabled by default.
Gemini is configured by default for current runtimes, but model/provider
approval, data handling and AI-00 decisions remain open.

## Container image preparation — 2026-10-13

At the user's direction, the repository now has four opt-in, per-role Docker
image shells based on `dhi.io/python:3.13-alpine`, wired to the private Docker
network. They contain no agent code, dependencies, listener, health endpoint,
model configuration or tools, and their default command only prints the
Python version. This is packaging preparation for the later Agentic AI work;
it does not accept the runtime framework, model/provider, or executable
process topology. Those decisions remain for AI-00 after G07.

## Consequences

The eventual decision must cover planning, delegation, allowlisted tools,
persisted state, deterministic validation, authorized approval, failure
recovery and evaluation. Record options and the selected runtime before
marking this ADR accepted.
