---
name: blueverse-docker-gateway
description: Change or review BLUEVERSE Dockerfiles, Compose services, edge-nginx routing, container networks, health checks, DHI images, or local stack behavior. Do not use for deployments that do not affect repository Docker infrastructure.
---

# BLUEVERSE Docker and gateway

Read the root `AGENTS.md`, `.agents/routing.md`, and the Docker, architecture,
security, documentation, and validation rules. Inspect `compose.yaml`, the
affected Dockerfile/config, and matching workflow before editing.

- Preserve the selected DHI images and repository-root build context.
- Keep `edge-nginx` as the local entry point and public `/api/...` routing.
  Internal Auth, Agentic AI, service, and database boundaries stay isolated.
- Treat ASP.NET runtime images as shell-less; use exec-form startup and health
  behavior that does not assume unavailable utilities.
- Keep credentials outside images and committed configuration. Preserve
  network intent, startup dependencies, readiness semantics, and teardown.
- A new containerized service includes matching source, Dockerfile, Compose
  wiring, health/readiness, CI build discovery, tests, and documentation.

Validate Compose configuration before builds, then run affected image and
gateway health checks when Docker and DHI credentials are available. Report
unavailable services or credentials without substituting unrelated images.
