# ADR-0008: Agent Workflow State

**Status:** Proposed

## Context

The assignment explicitly requires persisted workflow state and execution evidence while prohibiting unnecessary sensitive or hidden reasoning storage.

## Decision

The [v1 workflow](../v1/workflows.md) now requires durable workflow ID and
type, initiator, objective, structured plan, step progress, structured
outputs or auditable tool summaries, validation, errors/retries, approval
status and decision, final result and timestamps as relevant. Persist only
what is needed to operate and audit the workflow; never persist hidden
model reasoning, secrets or unnecessary sensitive data.

## Partial AI-02 implementation — 2026-10-13

Planning Coordination uses a Planner-owned `agent_execution_states` table
keyed to an existing business workflow. The private agent writes through a
Planner service-key endpoint and has no database credentials. Stored state is
schema-versioned and bounded. This is a provisional component implementation
for review, not acceptance of a shared runtime state architecture or
retention policy.

## Consequences

Cross-agent state ownership, retention, restart/recovery, concurrency and
shared schema remain open. Planning & Coordination persists bounded
checkpoints in Planner-owned state; the Experience & Biodiversity specialist
is stateless and owns no execution-state store. Neither implementation
settles the shared workflow-state decision. Finalize that design with the
PostgreSQL/EF Core schema before accepting this ADR. This proposal is not
evidence of end-to-end workflow-state implementation.
