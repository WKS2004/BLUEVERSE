---
name: blueverse-client-contract
description: Implement or review BLUEVERSE React and Flutter work that consumes the public API, especially shared contracts, authorization behavior, cross-platform workflows, and client tests. Do not use for isolated styling with no contract impact.
---

# BLUEVERSE client contract

Read the root `AGENTS.md`, `.agents/routing.md`, and the architecture, security,
testing and validation rules. Inspect the public contract and existing client
code before changing either client.

For v1 member work, follow `.agents/rules/v1-development.md` and the assigned
component contract. Keep both clients behind `services/api`, implement
equivalent authorized outcomes, and use member-specific modules with narrow
shared-file changes.

- React 19 + Vite and Flutter call only the public ASP.NET Core API/gateway.
  Never add direct Auth, Agentic AI, PostgreSQL or internal-service access.
- Style React Web pages and components with Tailwind CSS utilities. Keep
  shared color/type tokens and base rules in the Tailwind entry stylesheet;
  reserve standalone component CSS for effects that are awkward or unclear as
  utilities. Flutter continues to use native widgets and theming.
- Reuse the shared React site header and footer on site pages when they apply;
  keep navigation height and placement stable while scrolling.
- Make shared navigation reflect the current authentication state. Guests get
  sign-in and registration links; signed-in users get one username/avatar
  disclosure in the header with profile, dashboard, add-account, registration
  and per-account sign-out actions. Keep login-session controls in Profile,
  keep sign-out inside the disclosure, and make it keyboard accessible. The
  footer should remain a useful site footer, not a duplicate header action bar.
- Keep account identity changes coherent across the whole web application.
  Successful login and registration must update the shared Auth session before
  routing; account switching, login, registration and sign-out must persist the
  preferred account, use React Router for destination changes, then reload the
  document so every route restores the same active account. After a reload,
  restore that account before presenting protected pages.
- For responsive auth pages, keep the Login form to the left of its photo on
  desktop, preserve Registration's established desktop composition, and place
  the photo before the form on mobile for both. Put Profile/Dashboard navigation
  inside the account main section so its sticky desktop rail is bounded by the
  header and footer; retain the mobile navigation layout.
- Use cursor states to explain interactivity: enabled controls use a pointer,
  the active account row uses a default cursor, and only controls performing
  work use a wait cursor.
- Read signed-in identity and active sessions through the registered public
  Auth API. Show the API's session capacity clearly and guide users when it is
  full; keep the server authoritative for per-account and per-device limits,
  and present account-capacity conflicts with a plain recovery path. If Auth
  status is unavailable, keep shared chrome and marketing content calm and
  useful instead of exposing generic service errors or internal status labels.
- Use Tailwind interaction states that are visible on hover and keyboard focus,
  respect reduced-motion settings, and give primary actions comfortable rounded
  shapes without making the overall site feel like an admin console.
- Give React Web and Flutter Mobile equal authorized roles, business
  capabilities and workflow actions. Neither frontend has priority, design
  emphasis or ownership for a stakeholder group. Use the applicable
  requirements and owning component/workflow documents for scope.
- Prefer literal relative `/api/...` request paths. Absolute hosts must be
  allowlisted in `publicApi.allowedAbsoluteHosts`; unresolved dynamic request
  targets must fail validation rather than being inferred from configuration.
- Before implementing or reviewing a UI, read `docs/development/ui-integration.md`
  and update `docs/contracts/ui-integration.json` with one shared workflow
  ID, both relevant client routes and every public API endpoint reference.
  Clients connect through the workflow contract, not by calling each other.
- Also read `docs/api/endpoint-catalog.md`. **MUST** update its JSON source
  and generated Markdown view in the same change for every client route,
  literal `/api/...` request addition/update/removal or route rename; the UI
  registry is only the workflow-facing subset. Run
  `.agents/scripts/validate_endpoint_catalog.py` before the UI validator and
  its tests. A client route or request change is incomplete while either
  catalog is stale or validation fails. Ordinary client route changes do not
  edit `.agents` guidance.
- Keep server rules authoritative. Permission-aware UI improves the experience
  but never replaces backend authorization or the role-to-permission model.
- Preserve one API contract and authorization model across clients. Implement
  and test the same authorized workflow outcomes in both. Document layout or
  device-integration differences without removing business actions.
- Read `docs/project/ui-experience-principles.md` for every UI workflow. Keep
  the experience user-friendly, scope-aligned and realistic; adapt layout and
  interaction for web or mobile without making a technical or analytical
  dashboard the default.
- Route asynchronous client work through the shared BLUEVERSE loading screen
  in each client. Use specific, calm Auth transition messages for session
  restore, sign-in, registration, account switching, password changes and
  sign-out/session actions. Keep important Auth transitions visible long enough
  to be perceived, and preserve their context across intentional document
  reloads without storing account credentials. Keep that transition message
  through overlapping restore and initial-route data requests, and use a neutral
  coastal message for requests without a specific context. Avoid replacing the
  overlay with local competing spinners. If a load lasts at least two seconds,
  reveal the completed page with the coastal wave wash; honor reduced-motion
  preferences.
- Provide branded 404 and 500 recovery experiences for both clients, including
  unknown routes and unexpected rendering errors. Keep React error content in
  the shared site main section between the shared header and footer, and retain
  an unknown route such as `/signsin` in the address bar. Use extensionless
  routes for recovery pages; any Nginx static fallback URI must be internal and
  must not expose its asset filename. Redirect direct requests to physical
  fallback asset URLs to their extensionless React recovery routes. Nginx may
  serve browser fallbacks when the frontend is unavailable, but must not
  intercept `/api/` responses or replace structured API error bodies. Keep
  internal error details out of user-facing pages.
- Handle success, empty, validation, denied, dependency-failure, timeout/retry,
  cancellation and malformed-response states as applicable.
- `vercel-react-best-practices` is limited to browser/Vite-compatible guidance;
  ignore Next.js/server-only advice unless explicitly adopted.
- Official `flutter-*` skills are supplementary and must not bypass this API
  boundary or create a replacement sample application.
- Add deterministic React component/request/accessibility tests under
  `apps/web/src` (and optional `apps/web/e2e`) and Flutter unit/widget/API-
  boundary tests under `apps/mobile/test` or `apps/mobile/integration_test`,
  with stable IDs and CI discovery.

Validate the affected client locally, then verify contract, permission and
cross-platform behavior. Run
`scripts/validation/validate_ui_integrations.py` and its validator tests
before handoff. State unavailable platform tooling explicitly.
