# ADR-0008: Agent Workflow State

**Status:** Proposed

## Context

The assignment explicitly requires persisted workflow state and execution evidence while prohibiting unnecessary sensitive or hidden reasoning storage.

## Decision

Persist only the Agentic AI workflow state and execution summaries required by the final design; never persist hidden model reasoning.

## Consequences

The schema must be finalized alongside the Agentic AI workflow design and database model.
