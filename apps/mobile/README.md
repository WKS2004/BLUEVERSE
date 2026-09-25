# BLUEVERSE Mobile

The mobile client is a Flutter/Dart application and an equal product surface
for every authorized role and workflow. Its implemented workflows provide
coastal onboarding, account registration and sign-in, saved-account switching,
refresh, current-device and all-device logout, profile editing, password
changes, session review/revocation, account deletion, permission-aware user,
role and permission administration, and a coastal overview dashboard that
labels future service areas. Auth credentials use server-issued installation
identifiers and platform secure storage. The
[v0 component guide](../../docs/v0/components/flutter-client.md) records
the source boundaries and extension contract. Added domain workflows must
preserve the same business capabilities as React; layout and device input
may vary without assigning a stakeholder group to this client.

`lib/main.dart` registers `/signin`, `/signup`, `/profile`, `/dashboard`,
`/admin`, `/admin/permissions`, `/admin/roles`, `/admin/users`, `/404` and
`/500`; the launch screen is `/` and restores the saved account before showing
onboarding.

Flutter applies the same palette, typography hierarchy, imagery, spacing,
component character and interaction states documented in the shared
[BLUEVERSE Design System](../../DESIGN.md). The native mapping is
in [`blueverse_theme.dart`](lib/ui/blueverse_theme.dart); it uses Material 3
widgets and does not depend on Tailwind. See the Flutter Mobile
implementation section in the repository-root
[`DESIGN.md`](../../DESIGN.md) for app-specific guidance. The shared
[UI experience principles](../../docs/project/ui-experience-principles.md)
govern both clients.

On launch, the app restores the saved session first. A signed-in user opens
Dashboard directly. A signed-out user sees the full-screen coastal onboarding
carousel in [`onboarding_screen.dart`](lib/ui/onboarding_screen.dart), with
swipe and side-arrow navigation, Back after the first slide, Next before the
final slide, and Skip to the final Sign in/Create account choices. Signing out
of the last active device account returns to the carousel. Its photos are
bundled under `assets/coastal/onboarding/` and reuse the current BLUEVERSE
coastal photography.

## Commands

Run from `apps/mobile`:

```bash
flutter pub get
flutter analyze
flutter test
flutter run
```

The Flutter project includes generated platform folders for Android, iOS,
Linux, macOS, Windows and web. Platform-specific release configuration should
be added only when that target is part of the delivery scope; it must not be
used to exclude a product role from the mobile client.

## API boundary

The Auth workflow calls the public ASP.NET Core gateway using the host address
appropriate for the device or emulator and port `80`. Android `localhost`
means the phone/emulator itself, not the laptop running Docker. For a standard
Android emulator, explicitly set `BLUEVERSE_API_BASE_URL` to
`http://10.0.2.2:80`; for a physical device, use the laptop's current LAN
address. The explicit compile-time value overrides the checked-in
environment-specific Android fallback, which differs from the standard
emulator host. Do not rely on that fallback for a device run.

Set the address explicitly for both `flutter run` and `flutter build`:

```bash
# Android emulator
flutter run --dart-define=BLUEVERSE_API_BASE_URL=http://10.0.2.2:80

# Physical Android device: replace 192.168.1.42 with the laptop's IPv4 address
flutter run --dart-define=BLUEVERSE_API_BASE_URL=http://192.168.1.42:80
flutter build apk --debug --dart-define=BLUEVERSE_API_BASE_URL=http://192.168.1.42:80
```

The current mobile URL resolver accepts plain HTTP on port `80` for this local
Docker workflow. Start the stack with `docker compose up -d`, then verify the
laptop address from the device browser at `http://192.168.1.42/health`.
Replace that example with the current Wi-Fi IPv4 address from `ipconfig`.
Debug/profile Android builds permit this local HTTP connection. The resolver
accepts only HTTP on port `80`; it does not follow a changed
`BLUEVERSE_HTTP_PORT` value. It also does not accept HTTPS, so deployment-ready
mobile transport remains unimplemented and must be resolved before a
production build.

Some managed or campus Wi-Fi networks isolate clients even when both devices
show addresses in the same subnet. If the phone cannot reach the laptop's LAN
address, use the attached USB device with an ADB reverse tunnel:

```bash
adb reverse tcp:80 tcp:80
flutter run
```

The client tries `127.0.0.1:80` only as this final USB-reverse fallback when no
explicit `BLUEVERSE_API_BASE_URL` was embedded. It does not make Android
`localhost` the normal network target.

The Auth adapter stores the server-issued device ID, device proof key,
short-lived access token and rotating refresh token in platform secure storage.
It calls the registered public `/api/auth/...` gateway routes for Auth,
profile, session and administration operations; the mobile app never calls the
internal Auth service, PostgreSQL or another Docker hostname.
Package tests inject an in-memory credential store and mock HTTP client to
check native login, refresh, session recovery, logout and failure behavior
without storing real credentials or requiring a live gateway.

Every request remains an endpoint registered in
[`docs/contracts/ui-integration.json`](../../docs/contracts/ui-integration.json).
It must not call internal Auth, Agentic AI, PostgreSQL or other service
hostnames directly. Prefer literal relative `/api/...` paths when the gateway
is same-origin; otherwise the absolute host must be allowlisted by the
contract. Dynamic request targets fail validation. A screen's Flutter route
must be registered under the same workflow ID as its React counterpart, even
when the platform-specific path or presentation differs.

Before handing off any new, generated or updated UI, run from the repository
root:

```bash
python scripts/validation/validate_ui_integrations.py
```

Then run `flutter analyze`, the unit/widget tests and applicable
`integration_test` workflows. The registry contains the shared home, Auth
session-management and registration, profile-management and coastal overview
workflows. Add the public API contract and both client surfaces together for
each future workflow.
