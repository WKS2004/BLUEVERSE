# BLUEVERSE Web

The web client is a React 19 + TypeScript application built with Vite. The
checked-in UI is still the generated starter screen; domain workflows and API
integration are deferred to the v1 implementation work.

## Commands

Run from `apps/web`:

```bash
npm install
npm run dev
npm run lint
npm run build
npm run preview
```

The production Docker build runs from the repository root and uses
`infrastructure/docker/frontend/Dockerfile`. That image serves the Vite build
with Nginx and exposes `GET /health`.

## API boundary

When API integration is added, this client must call the public ASP.NET Core
gateway through `/api/...`. It must not call the internal Auth or Agentic AI
services directly.
