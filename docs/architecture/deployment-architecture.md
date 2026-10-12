# Deployment Architecture

The deployment files describe the target topology. No cloud deployment is
currently evidenced in this checkout. The .NET 10 public API, internal Auth
service and Member 2's marine-safety service are checked in; Compose includes
marine-safety on the private API and database networks. Hosted service
deployment and production PostgreSQL evidence remain open.

## Planned cloud direction

- React frontend: Vercel
- Flutter mobile client: platform-specific mobile distribution selected for the
  delivery target
- ASP.NET Core API: Render
- Auth service: Render
- v1 member services: one private .NET service per member component. The
  marine-safety service and local Compose integration are implemented; the
  other three service implementations and hosted topology remain open. Each
  service is reached through the public API boundary, not directly by either
  client.
- PostgreSQL: managed PostgreSQL on Render or an equivalent managed provider
- Redis: managed Redis-compatible service when introduced
- Agentic AI: internal runtime and deployment to be chosen for the defined
  four-agent v1 workflow; no executable service is checked in yet
- Biodiversity ML: private inference runtime and model startup to be
  documented when the separate IT3091 model is available

The local `edge-nginx` gateway is a development/container architecture
boundary. The production React frontend need not be routed through the local
gateway when Vercel is the selected web platform, and the Flutter client is
distributed through its mobile target channels. Both clients must offer the
same permitted role and workflow capabilities through the public API and
permission contract. Production configuration must keep Agentic AI and ML
inference private and provide a reproducible startup path for the assessed
workflow.

Marine-safety is checked in and runs in local Compose, but this does not
establish a cloud deployment or production readiness. Its local service ID,
private route, network and database ownership are recorded in the [Member 2
G00 decision record](../v1/g00/member-2-marine-safety-decisions.md). The
remaining component services and cross-component deployment choices are
still open. See [ADR-0020](../adr/ADR-0020-member-component-service-boundaries.md)
and the [service boundaries](service-boundaries.md).
