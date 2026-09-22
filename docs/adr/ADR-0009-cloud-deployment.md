# ADR-0009: Cloud Deployment

**Status:** Proposed

## Context

This matches the current deployment direction while keeping the application architecture independent of free-tier limitations.

## Decision

Use Vercel for the React Web frontend, the selected mobile distribution
channels for Flutter Mobile, and Render for Dockerized backend services, with
managed PostgreSQL/Redis-compatible services where appropriate. Both clients
use the same public API and permission contract.

## Consequences

Cloud-specific configuration must remain separate from core business logic and local Docker architecture.
