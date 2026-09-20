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
gateway through a registered `/api/...` endpoint in
[`docs/contracts/ui-integration.json`](../../docs/contracts/ui-integration.json).
It must not call the internal Auth, Agentic AI, PostgreSQL or other service
hostnames directly. Prefer literal relative `/api/...` paths; absolute hosts
must be allowlisted by the contract and dynamic request targets fail the
validator. A screen's route must also be registered under the same workflow ID
as its Flutter counterpart.

Before handing off any new, generated or updated UI, run from the repository
root:

```bash
python scripts/validation/validate_ui_integrations.py
```

Then run the web lint/build and the colocated React route/request/workflow
tests. The integration registry is empty of domain API calls while this
starter has no backend workflow; add the public API contract and both client
surfaces together when the first real screen is introduced.
