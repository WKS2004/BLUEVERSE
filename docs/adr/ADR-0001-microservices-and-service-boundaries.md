# ADR-0001: Microservices and Service Boundaries

**Status:** Accepted

## Context

BLUEVERSE is planned as an integrated ecosystem with multiple future capabilities. The assignment requires a coherent system, while the project roadmap introduces additional intelligence and stakeholder capabilities.

## Decision

Use a service-oriented/microservice architecture with separate edge gateway, public API, Auth service and data services.

## Consequences

Clear boundaries improve independent evolution and deployment, but introduce networking, configuration and operational complexity.
