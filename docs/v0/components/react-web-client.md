# React Web client

## Responsibility and current implementation

The React 19, TypeScript and Vite browser client lives in
[apps/web](../../../apps/web/README.md). It is an equal product surface for
every authorized role and workflow. The current route table declares the home,
sign-in, registration, profile, dashboard, Auth administration and branded
recovery pages. Their
shared workflow IDs and public API references are recorded in the
[UI integration registry](../../contracts/ui-integration.json).

Current source boundaries:

- The React Router setup and route declarations live in
  [routes.tsx](../../../apps/web/src/app/routes.tsx).
- Route-level screens live in
  [the pages directory](../../../apps/web/src/pages/HomePage.tsx).
- Shared header, footer and authentication composition live in
  [the layout components](../../../apps/web/src/components/layout/SiteHeader.tsx).
- Account navigation, Auth session state, public API requests and loading
  behavior live in their shared component and feature directories.
- Coastal photography and the Tailwind entry stylesheet live under
  [src/assets/coastal](../../../apps/web/src/assets/coastal/coast-hero-daylight.webp)
  and [src/styles/index.css](../../../apps/web/src/styles/index.css).

The implemented account experience includes registration, sign-in, saved-
account switching, session recovery, scoped sign-out, profile and password
management, session review/revocation and protected account deletion. The
permission-aware Auth administration screens manage users, roles and
permissions. A coastal overview dashboard presents current account details and
labels service areas that are not implemented as future work.

## Visual system and styling

React page and component styling uses Tailwind CSS utilities. Shared color,
font-stack and animation tokens plus base rules are declared in the Tailwind
entry stylesheet. Follow the canonical
[BLUEVERSE Design System](../../../DESIGN.md) for the shared
React/Flutter palette, typography, imagery, component character, motion and
accessibility. React implements those rules with Tailwind and reusable web
components; Flutter maps them to native widgets and `ThemeData`.

Reuse the shared header and footer. Keep authentication and account pages
within their existing page layouts. The sign-in and registration screens use
the same photo-and-form shell with their established desktop compositions; on
mobile, the coastal photo appears before the form. Page-specific layout can
adapt to content and viewport while keeping shared tokens and interaction
states consistent.

The web layout and shell are platform-specific; the shared visual system is
not. Follow the same
[cross-platform UI experience principles](../../project/ui-experience-principles.md)
in both clients. Do not reduce or assign a role's workflow actions based on
screen size or platform.

## Contract for continued implementation

Use the public API adapter and server-owned role-to-permission model for every
added workflow. Register a React route, a Flutter route and all public API
references under one shared workflow ID. A permitted management, tourism,
review or field action belongs in both clients.

Handle loading, empty, success, validation, denied, malformed-response,
timeout and dependency-failure states as applicable. Auth errors use the
public API's structured response. Do not infer permission from a hidden
control or call an internal service directly. Follow the
[UI integration guide](../../development/ui-integration.md) and the
[endpoint catalog](../../api/endpoint-catalog.md) when routes or API targets
change.

## Verification

From apps/web, use the lint, build and test commands in its README. For any
route or request-target change, run the UI integration validator and the
endpoint-catalog validator as described in the linked integration guide.
Verify the same authorized and denied business outcomes in Flutter.
