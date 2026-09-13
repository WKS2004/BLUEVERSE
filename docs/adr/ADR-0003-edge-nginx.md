# ADR-0003: Edge Nginx

**Status:** Accepted

## Context

The local architecture needs one entry point for API and web routing and a place for baseline security headers.

## Decision

Use edge-nginx as the local Docker entry point and reverse proxy.

## Consequences

Production routing may differ when Vercel/Render are used; local gateway assumptions must not leak into client business logic.
