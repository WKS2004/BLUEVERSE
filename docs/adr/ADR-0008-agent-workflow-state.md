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

## Consequences

The four-agent responsibilities and assessed flow are defined, but the
table design, ownership, retention and restart/recovery strategy remain
open. Finalize those with the PostgreSQL/EF Core schema before accepting
this ADR. This proposed record is not evidence of an implemented schema.
