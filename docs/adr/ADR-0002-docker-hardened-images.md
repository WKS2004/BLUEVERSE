# ADR-0002: Docker Hardened Images

**Status:** Accepted

## Context

The project explicitly adopts a hardened-container-first approach instead of building with generic images and hardening later.

## Decision

Use the selected Docker Hardened Images from the beginning of the containerized implementation.

## Consequences

Image availability and minimal-image behavior must be tested. Runtime images may lack common shell utilities.
