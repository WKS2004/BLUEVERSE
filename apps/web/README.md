# BLUEVERSE Web

React Web styling uses Tailwind CSS utilities with shared BLUEVERSE color and
type tokens defined in `src/styles/index.css`. The repository-root
[DESIGN.md](../../DESIGN.md) is the canonical visual guide for React Web and
Flutter Mobile. Its
[React Web implementation section](../../DESIGN.md)
documents how the shared palette and typography hierarchy map to Tailwind.
Follow it when adding or revising web surfaces, and reuse the shared site
header and footer. Tailwind is integrated with the Vite plugin.

The web client is a React 19 + TypeScript application built with Vite. It is
an equal product surface for every authorized role and workflow. Its
implemented workflows provide cookie-based account registration and sign-in,
refresh recovery, saved-account switching, current-device and all-device
logout, profile editing, password changes, session review/revocation and
account deletion. Permission-aware administration covers user, role and
permission management. The coastal overview dashboard shows current account
information and labels unavailable service areas as future work. The [v0
component guide](../../docs/v0/components/react-web-client.md) records the
source boundaries and extension contract. Added domain workflows must
preserve the same business capabilities as Flutter; layout and input methods
may adapt to browser use without assigning a stakeholder group to this client.

## Source layout and navigation

- `src/app/` owns application setup, the React Router route table and route
  scroll behavior. `BrowserRouter` wraps the app and `routes.tsx` declares the
  `/`, `/signin`, `/signup`, `/profile`, `/dashboard`, `/admin`,
  `/admin/permissions`, `/admin/roles`, `/admin/users`, `/404` and `/500`
  routes.
- `src/components/feedback/BackendLoadingScreen.tsx` shows the shared loading
  screen for backend work, with contextual account transitions and a wave
  reveal after loads exceed two seconds. Successful account transitions carry
  their loading context across the deliberate document refresh. Unknown paths
  keep their requested URL and show `GlobalErrorPage.tsx` inside the shared
  header, main and footer shell; `/404` and `/500` remain extensionless recovery
  routes. Direct access to the physical fallback asset URLs is canonicalized to
  those extensionless React routes.
- `src/pages/` contains route-level pages.
- `src/components/layout/` contains the shared site header, footer and auth
  page layout. `src/components/account/` contains account-area navigation;
  `src/components/providers/` contains the auth session provider.
- `src/features/auth/` contains Auth API calls, browser account storage,
  session context and safe post-auth navigation helpers. Its `__tests__/`
  folder covers public request contracts, saved-account storage, session
  restoration and post-auth navigation.
- `src/app/__tests__/`, `src/components/**/__tests__/`, and
  `src/pages/**/__tests__/` cover route, shared-shell, page and administration
  behavior. `src/testSupport/reactTestHarness.js` provides the JSDOM and
  React Testing Library setup used by the component suites.
- `src/assets/coastal/` contains coastal photography; `src/styles/` contains
  the Tailwind entry stylesheet and shared design tokens.
- `src/styles/index.css` declares the web Tailwind tokens and base rules.
- The repository-root [`DESIGN.md`](../../DESIGN.md) records the shared design
  system and the React-specific styling implementation.

Use React Router `Link`, `useNavigate` and `useLocation` for in-app navigation.
Avoid `window.location` navigation and ordinary anchors for app routes so the
URL, history, and signed-in return destination stay in the SPA.

## Commands

Run from `apps/web`:

```bash
npm install
npm run dev
npm run lint
npm run build
npm run test:ci
npm run preview
```

The production Docker build runs from the repository root and uses
`infrastructure/docker/frontend/Dockerfile`. That image serves the Vite build
with Nginx and exposes `GET /health`.

## API boundary

The client calls the public ASP.NET Core gateway through the registered
`/api/...` endpoints in the [shared UI integration contract](../../docs/contracts/ui-integration.json). Workflows use the public Auth routes for account registration, sign-in, refresh, profile and account security, session management, and permission-aware user/role/permission administration. They must not call the internal Auth, Agentic AI, PostgreSQL or other service hostnames directly. Browser authentication uses protected HttpOnly
cookies; raw access and refresh tokens are not exposed to the browser UI. Prefer
literal relative `/api/...` paths; absolute hosts must be allowlisted by the
contract and dynamic request targets fail the validator. A screen's route must
also be registered under the same workflow ID as its Flutter counterpart.

Before handing off any new, generated or updated UI, run from the repository
root:

```bash
python scripts/validation/validate_ui_integrations.py
```

Then run the web lint/build and `npm run test:ci` from `apps/web`. The suite
uses Node 24's built-in test runner; request contracts mock `fetch`, while DOM
tests use JSDOM, React Testing Library and `user-event` with Vite SSR module
loading. The tests cover the implemented Auth, profile, administration,
navigation, recovery and shared-shell behaviors without a live API or browser.
The current suite passes 157 cases (2026-09-25). These tests are not a
substitute for deployed-gateway or real-browser end-to-end checks. The
registry contains the shared home, Auth registration and session-management,
profile management and coastal overview workflows. New workflows must
continue to register both client surfaces and their public API references
together.
