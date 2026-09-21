# Deployment Architecture

The deployment files describe the target topology. No cloud deployment is
currently evidenced in this checkout. The .NET 10 public API and internal Auth
sources are present at `services/api` and `services/auth`.

## Planned cloud direction

- React frontend: Vercel
- ASP.NET Core API: Render
- Auth service: Render
- PostgreSQL: managed PostgreSQL on Render or an equivalent managed provider
- Redis: managed Redis-compatible service when introduced
- Agentic AI: deployment chosen after workflow/component design

The local `edge-nginx` gateway is a development/container architecture boundary. The production frontend need not be routed through the local gateway when Vercel is the selected frontend platform.
