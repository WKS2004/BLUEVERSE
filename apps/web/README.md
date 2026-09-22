# BLUEVERSE Web

The web client is a React 19 + TypeScript application built with Vite. The
checked-in UI includes the shared `/login` Auth session-management workflow:
cookie-based login, refresh recovery, current-device/everywhere logout and
active-session display. Domain workflows beyond Auth remain deferred to the
v1 implementation work.

## Commands

Run from `apps/web`:

```bash
npm install
npm run dev
npm run lint
npm run build
npm run test:ci -- src/auth.request.test.js
npm run preview
```

The production Docker build runs from the repository root and uses
`infrastructure/docker/frontend/Dockerfile`. That image serves the Vite build
with Nginx and exposes `GET /health`.

## API boundary

The Auth client calls the public ASP.NET Core gateway through the registered
`/api/...` endpoints in
[`docs/contracts/ui-integration.json`](../../docs/contracts/ui-integration.json).
The current shared workflow uses `/api/auth/login`, `/api/auth/refresh`,
`/api/auth/me`, `/api/auth/sessions`, `/api/auth/logout` and
`/api/auth/logout-all-devices`. It must not call the internal Auth, Agentic AI,
PostgreSQL or other service hostnames directly. Browser authentication uses
protected HttpOnly cookies; raw access and refresh tokens are not exposed to
the browser UI. Prefer literal relative `/api/...` paths; absolute hosts must
be allowlisted by the contract and dynamic request targets fail the validator.
A screen's route must also be registered under the same workflow ID as its
Flutter counterpart.

Before handing off any new, generated or updated UI, run from the repository
root:

```bash
python scripts/validation/validate_ui_integrations.py
```

Then run the web lint/build and the colocated request-boundary tests. The
current Auth suite uses Node 24's built-in test runner so CI does not need a
second browser or component-test dependency; component and browser workflow
coverage can be added when those surfaces are introduced. The registry
currently contains the implemented Auth workflow and the starter home surface;
add the public API contract and both client surfaces together when the next
product workflow is introduced.
