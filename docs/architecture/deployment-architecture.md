# Deployment Architecture

The deployment files describe the target topology. No cloud deployment is
currently evidenced in this checkout. The .NET 10 public API and internal Auth
sources are present at `services/api` and `services/auth`.

## Planned cloud direction

- React frontend: Vercel
- Flutter mobile client: platform-specific mobile distribution selected for the
  delivery target
- ASP.NET Core API: Render
- Auth service: Render
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
