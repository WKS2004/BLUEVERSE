# ADR-0004: Permission-Based Authorization

**Status:** Accepted

## Context

BLUEVERSE is intended to support evolving stakeholder groups. Hard-coded role-name checks would couple business logic to today's roles.

## Decision

Use user → role(s) → permission(s) authorization and check permissions at protected operations.

## Consequences

Permission definitions and role assignment require careful administration and testing.
