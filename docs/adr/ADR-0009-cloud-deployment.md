# ADR-0009: Cloud Deployment

**Status:** Proposed

## Context

This matches the current deployment direction while keeping the application architecture independent of free-tier limitations.

## Decision

Use Vercel for the React frontend and Render for Dockerized backend services, with managed PostgreSQL/Redis-compatible services where appropriate.

## Consequences

Cloud-specific configuration must remain separate from core business logic and local Docker architecture.
