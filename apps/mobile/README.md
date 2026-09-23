# BLUEVERSE Mobile

The mobile client is a Flutter/Dart application and a first-class product
surface for clients, staff and administrators. The checked-in app includes the
shared `/login` Auth session-management workflow: server-issued device
installation credentials, secure-storage-backed login and refresh,
current-device/everywhere logout and active-session display. Field workflows,
location/evidence capture and other domain features remain deferred to the v1
implementation work. It is not reserved for client-facing experiences; future
workflows should preserve the same role, permission and API contract on mobile
while using mobile strengths such as quick actions, location, camera and
notifications where they genuinely help.

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
means the phone/emulator itself, not the laptop running Docker. The app uses
`10.0.2.2` for an Android emulator. A physical device must receive the
laptop's current LAN address through Flutter's compile-time define; no
laptop-specific IP is checked into the app.

Set the address explicitly for both `flutter run` and `flutter build`:

```bash
# Android emulator
flutter run --dart-define=BLUEVERSE_API_BASE_URL=http://10.0.2.2:80

# Physical Android device: replace 192.168.1.42 with the laptop's IPv4 address
flutter run --dart-define=BLUEVERSE_API_BASE_URL=http://192.168.1.42:80
flutter build apk --debug --dart-define=BLUEVERSE_API_BASE_URL=http://192.168.1.42:80
```

The configured URL must use plain HTTP on port `80` for this local Docker
workflow. Start the stack with `docker compose up -d`, then verify the laptop
address from the device browser at `http://192.168.1.42/health`. Replace that
example with the current Wi-Fi IPv4 address from `ipconfig`. Debug/profile
Android builds permit this local HTTP connection; production transport should
use HTTPS.

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
It calls the public `/api/auth/...` gateway routes only; the mobile app never
calls the internal Auth service, PostgreSQL or another Docker hostname.
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
`integration_test` workflows. The registry currently contains the shared Auth
workflow and the starter home surface; add the public API contract and both
client surfaces together when the next real screen is introduced.
