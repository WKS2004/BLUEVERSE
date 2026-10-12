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
  permission management. The React client also implements the marine
  conditions, history and safety-profile workflows. The coastal overview
  labels service areas other than marine-safety as future work. The [v0
component guide](../../docs/v0/components/react-web-client.md) records the
source boundaries and extension contract. Added domain workflows must
preserve the same business capabilities as Flutter; layout and input methods
may adapt to browser use without assigning a stakeholder group to this client.

## Source layout and navigation

- `src/app/` owns application setup, the React Router route table and route
  scroll behavior. `BrowserRouter` wraps the app and `routes.tsx` declares the
  `/`, `/signin`, `/signup`, `/profile`, `/dashboard`, `/admin`,
  `/admin/permissions`, `/admin/roles`, `/admin/users`, `/marine/conditions`,
  `/marine/history`, `/marine/safety-profiles`, `/404` and `/500` routes.
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
`/api/...` endpoints in the [shared UI integration contract](../../docs/contracts/ui-integration.json). Workflows use the public Auth routes for account registration, sign-in, refresh, profile and account security, session management, and permission-aware user/role/permission administration. Marine workflows use the public `/api/marine/...` routes; the service checks Auth-backed identity and current role permissions. The client must not call internal Auth, marine-safety, Agentic AI, PostgreSQL or other service hostnames directly. Browser authentication uses protected HttpOnly
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
The current suite passes 201 cases (2026-10-06), including 44 marine-safety
API-boundary and component cases. These tests are not a
substitute for deployed-gateway or real-browser end-to-end checks. The
registry contains the shared home, Auth registration and session-management,
profile management, coastal overview and marine conditions/safety workflows.
New workflows must
continue to register both client surfaces and their public API references
together.

## Coastal Planner feature branch

React now provides `/planner`, persistent recommendation details,
`/planner/saved` and owner itinerary management. Destination/activity choices
come from the public catalogue projection by name. Saved trips support notes,
schedule edits, stable stop reordering/removal with undo, condition reviews
and durable history. Permissions use Auth-issued claims; private service hosts
never appear in client requests. See
[planner setup and integration limits](../../docs/development/coastal-planner.md).
Live operations/IT3091 peers and cross-component compatibility remain pending;
Flutter planner workflows are implemented on the integration branch.

2026-10-05 verification: lint and production build passed; all 201 React tests
passed with `node --experimental-strip-types --test --test-concurrency=2`.
The bounded worker count was needed during concurrent Docker builds after an
unconstrained run exhausted local memory. No tests were skipped. Browser checks
of the local Docker gateway verified the missing-catalogue recovery and saved
trip empty state at desktop and phone widths; live recommendations still need
compatible owner services.


## Coastal Planner React experience

- `/planner` introduces the planner to guests and signed-in users, explains
  the three-step process and offers creation actions and optional live ideas.
- `/planner/plan` is the separate authenticated builder. Guests sign in and
  return to this page; authorized users enter directly. Suggestions remain
  linked to the caller's snapshot, with explicit selection and draft save/append.
- `/planner/saved` is personal plan history, with Draft, Confirmed and Past
  views, search on the displayed page and pagination.
- `/planner/recommendations/:recommendationId` revisits suggestions;
  `/planner/itineraries/:itineraryId` displays the itinerary timeline and
  preserves draft editing, confirmation and condition-review history.

Shared top menus expose The planner from home, sign-in, account, administration
and recovery pages. The public `/planner` introduction has no account sidebar
and omits the redundant top self-link. Personal planner pages also omit the
standalone top planner link. Create a plan and Plan history appear in the
shared account sidebar, alongside Profile, Dashboard and authorized Administration
links; this sidebar becomes an in-page navigation panel at narrow widths. The
current planner action is highlighted. The sidebar's Coastal planner entry opens
`/planner/plan` directly. Creation and history load together on demand, so
switching their tabs has no intermediate loading screen. Their catalogue/history
reads show status within the page; other planner work uses the shared loading
screen. A 320ms directional fade-and-slide introduces either page, with motion
disabled when reduced motion is requested. Live catalogue availability remains a data
dependency; the public introduction stays useful during an outage. This
navigation redesign is React-specific; Flutter retains its native presentation
and registered planner workflows.

Create a plan and Plan history share permission-aware route tabs and a wide
image hero card, following the supplied Coastal Operations layout. History has
one unified panel with status filters, live per-page search, reset, condition-check
refinement, a 10/20/25/50 page-size selector and pagination. Empty results use a
text state and one authorized creation action below the controls; the hero is the
only image. Counts describe the current page because the API does not return a
global total. Full returned pages enable Next even when local filters hide rows.

Dedicated imagery for discovery, guided experiences, trip preparation and saved
memories is in [`src/assets/planner`](src/assets/planner/README.md). These are
illustrative AI-generated scenes, separate from live recommendation evidence.

## Marine Safety web workflows

The implementation lives in `src/features/marine/` (typed public API adapter
and live published activity catalogue) and `src/pages/marine/` (workflow pages,
navigation and result explanations). The shared workflow IDs are
`marine-conditions`, `marine-condition-history` and
`marine-safety-profile-management`.

Sign in with `marine.profile.read` to use the three registered routes:

Open **Account → Marine & safety** on desktop or mobile, the **Marine & safety**
account-sidebar group, or Dashboard's marine quick link. The header shows home
section links only on Home; Administration retains account actions without
those marketing links. A role name alone never enables the Marine shortcut.

- `/marine/conditions`: enter signed decimal coordinates and an optional UTC
  time, read condition evidence independently, or compare a published activity
  against its reviewed limits. The Web UI explains SUITABLE, CAUTION, UNSUITABLE
  and UNKNOWN without recalculating the server result. Missing readings stay
  explicit; sources, units and forecast/retrieval times remain visible.
- `/marine/history`: filter saved conditions and assessments, inspect captured
  criteria and citations, and refresh independently available records. Failed
  data is labelled unavailable; a successful empty result stays distinct. If a
  refresh fails, retained records are identified as the last-loaded data.
- `/marine/safety-profiles`: inspect versions and effective periods. With the
  additional `marine.profile.manage` grant, create or edit cited drafts, review
  another manager's draft, or confirm deactivation while preserving history.
  The editor groups each factor with its evidence; limits use at most two
  decimal places and caution must be lower than the corresponding maximum.

All pages share responsive Tailwind styling, the established header/footer and
accessible Marine workflow navigation. The sidebar and page tabs share the labels
**Coastal conditions**, **Coastal evidence** (saved readings and assessments),
and **Activity safety limits**. Labels are defined once in
`src/features/marine/marineWorkflowLabels.ts` to keep both menus consistent.
Catalogue outages preserve condition
lookup, stored records and deactivation; only data-dependent actions are
disabled. Saved-profile requests have their own retry action. See the shared
[availability contract](../../docs/api/marine-dependency-availability.md).

Presentation differs from Flutter while authorized actions, permissions and
public API contracts remain equivalent. The browser does not call internal
services, perform Activity CRUD, invent profile thresholds or execute agents.


### Marine page heroes

Each Marine page has its own responsive editorial hero: ocean swell for
conditions, a coastal field notebook for saved evidence, and kayaking preparation
for activity safety limits. `src/pages/marine/MarinePageHero.tsx` pairs workflow
copy with three newly generated local WebP images. Text stays on a solid paper
panel; every screen width stacks the photo above the copy. Images have intrinsic dimensions,
priority loading, decorative empty alt text and no safety/data status claims.
The persistent workspace places a shared heading and introduction first, then
the existing pill navigation, then the selected page's hero and forms. The tabs
stick below the site header while scrolling. Switching tabs animates only the
content panel over 260 ms; reduced-motion preferences disable this animation.
Initial reads during Marine tab switches show inline status instead of the
full-screen loader. First entry, explicit retries/refreshes, saves and Auth
retain their normal loading feedback. Switching from deep in a page brings the
workflow start into view; switching near the top preserves the scroll position.

[Asset inventory and generation prompts](src/assets/coastal/marine/README.md)
record the image provenance. The imagery illustrates the theme; actual provider
and stored evidence continues to come from the public API.
