# Flutter client

## Responsibility and source

`apps/mobile` is the Flutter/Dart client and an equal product surface for every
authorized role and workflow. `lib/main.dart` registers `/`, `/signin`,
`/signup`, `/profile`, `/dashboard`, the administration routes and the branded
recovery routes. The same frontend paths and public API references are
registered by workflow in the [shared UI registry](../../contracts/ui-integration.json).

The UI, view model, repository, API service, models, credential store and
gateway resolution follow the existing UI/logic/data separation. Authentication
uses the public ASP.NET Core API and platform secure storage for server-issued
installation credentials. The client does not call internal Auth, Agentic AI,
PostgreSQL or other Docker service hostnames.

## Launch and onboarding

At launch, `AuthViewModel.restore()` restores the saved session before the home
route chooses its experience. A restored account opens `/dashboard` directly.
When there is no active account, `/` shows the full-screen coastal image slider
in `lib/ui/onboarding_screen.dart`. It supports swipe gestures and visible left
and right arrows; Back appears after the first slide, Next appears before the
final slide, and Skip on introduction slides jumps directly to the final
Sign in/Create account choices. Successful fresh sign-in opens Dashboard.
Signing out of the final active device account returns to the carousel. Saved
account switching remains available through the existing Auth session flow.

The carousel reuses coastal images in `assets/coastal/onboarding/`, declared in
`pubspec.yaml`. Text sits over a Coast Ink gradient for contrast, while page
indicators, controls, headings and buttons follow the shared design system.

The shared account experience also provides profile editing, password changes,
session review and revocation, and protected account deletion. Permission-aware
screens support user, role and permission administration. The same authorized
actions are available in React through the matching workflow IDs; layout and
touch interaction adapt to mobile.

## Shared visual system

React Web and Flutter use the same canonical
[BLUEVERSE Design System](../../../DESIGN.md): the same eleven
color roles, Manrope/Sora type hierarchy, coastal photography, rounded control
language, spacing rhythm and accessibility expectations. Flutter maps those
tokens to native Material widgets in
[`blueverse_theme.dart`](../../../apps/mobile/lib/ui/blueverse_theme.dart).
This mapping keeps the visual language shared without copying web layouts or
Tailwind classes. Neither client currently bundles the named font files, so
platform font fallback is expected.

## Public API and local gateway

The gateway configuration accepts an explicit
`BLUEVERSE_API_BASE_URL` at build time. For a standard local Android emulator,
pass `http://10.0.2.2:80`; a physical device uses the development host's LAN
address. This explicit value overrides the checked-in environment-specific
Android fallback, which differs from the standard emulator host. Do not rely
on that fallback for a device run. The client targets the public gateway
rather than a Docker service hostname.

The current resolver accepts local plain HTTP on port `80` only and does not
follow a changed `BLUEVERSE_HTTP_PORT` value. A deployment HTTPS endpoint is
not supported by the checked-in mobile configuration yet; resolve that
transport and release configuration before claiming a deployed mobile client
is ready. See the
[mobile setup guide](../../../apps/mobile/README.md) for the local device
workflow.

The Auth adapter stores the server-issued device ID, device proof key,
short-lived access token and rotating refresh token in platform secure storage.
Every request and Flutter route must remain registered under the same workflow
ID as its React counterpart, including public API references in
`docs/contracts/ui-integration.json`.

## Contract for continued implementation

Use the registered public API contract and server-owned permissions. Every
authorized workflow and role must have the same business actions and outcomes
in React. Mobile layout or device input may differ without reducing capability
coverage. Handle denied storage/network access, malformed responses, refresh
failure, loading and empty states explicitly.

## Verification

Run `flutter analyze`, the relevant unit/widget/integration suites, the
[UI validator](../../development/ui-integration.md) and route catalog
validation for changed requests. Use mock HTTP and an in-memory
`AuthCredentialStore` for deterministic request tests; exercise a device when
behavior depends on platform storage or network routing. See
[`apps/mobile/README.md`](../../../apps/mobile/README.md) for local run setup.
