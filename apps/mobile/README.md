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

`lib/main.dart` registers the global `/planner` introduction, protected
`/planner/plan` creation and `/planner/saved` history pages, recommendation
results and trip details, as well as `/signin`, `/signup`, `/profile`,
`/dashboard`, `/admin`, `/admin/permissions`, `/admin/roles`, `/admin/users`,
`/404` and `/500`. The launch screen is `/` and restores the saved account
before showing onboarding. The onboarding carousel stays focused on the
BLUEVERSE introduction and account choices. The global planner entry is
available from sign-in, registration and account menus. Protected pages return
guests to their intended page after successful sign-in.

The planner uses dedicated bundled coastal imagery and native Material 3
heroes. Plan creation and history share permission-aware tabs and directional
transitions that honor reduced-motion settings. History provides status and
conditions filters, search, page sizing and paging. Destination-local times
are resolved with the IANA time-zone database and daylight-saving gaps or
overlaps must be corrected before a recommendation request is sent.

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

On Windows, run from the repository root through the cache wrapper. It sets
`GRADLE_USER_HOME`, `PUB_CACHE`, temporary files and the Flutter build
directory under `D:\WKS\FlutterBuildCache`:

```powershell
.\scripts\Windows\Invoke-FlutterCached.ps1 -FlutterArgs @('pub','get')
.\scripts\Windows\Invoke-FlutterCached.ps1 -FlutterArgs @('analyze','--no-pub')
.\scripts\Windows\Invoke-FlutterCached.ps1 -FlutterArgs @('test','--no-pub')
.\scripts\Windows\Invoke-FlutterCached.ps1 -FlutterArgs @('run','--dart-define=BLUEVERSE_API_BASE_URL=http://10.0.2.2:80')
```

For an Android debug build, use `-FlutterArgs @('build','apk','--debug','--dart-define=BLUEVERSE_API_BASE_URL=http://10.0.2.2:80')`. The wrapper preserves an existing cache and refuses to merge two build directories. On other platforms, use the standard Flutter commands with `PUB_CACHE` and `GRADLE_USER_HOME` configured for the chosen cache locations.

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
address or the USB reverse address below. The explicit compile-time value
overrides automatic emulator/USB fallback discovery.

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
address, use the attached USB device with an ADB reverse tunnel. Enable USB
debugging, accept the phone's authorization prompt and put Android platform-tools
(`adb`) on PATH. On Windows, replace the device ID and run from the repository root:

```powershell
adb devices
$mobileDevice = 'REPLACE_WITH_DEVICE_ID'
adb -s $mobileDevice reverse tcp:80 tcp:80
.\scripts\Windows\Invoke-FlutterCached.ps1 -FlutterArgs @('run','-d',$mobileDevice,'--dart-define=BLUEVERSE_API_BASE_URL=http://127.0.0.1:80')
```

To install a debug APK while preserving existing app data, use:

```powershell
.\scripts\Windows\Invoke-FlutterCached.ps1 -FlutterArgs @('build','apk','--debug','--no-pub','--dart-define=BLUEVERSE_API_BASE_URL=http://127.0.0.1:80')
adb -s $mobileDevice install -r 'D:\WKS\FlutterBuildCache\projects\blueverse-adi\build\app\outputs\flutter-apk\app-debug.apk'
```

Keep USB connected for gateway access. Remove the tunnel with
`adb -s $mobileDevice reverse --remove tcp:80` when finished. Without an explicit
base URL, Android tries the emulator address followed by the USB reverse
address; the explicit USB setting avoids probing the emulator address on a
physical phone. A Wi-Fi build instead needs the laptop's current LAN address.

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
