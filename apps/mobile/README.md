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

The experience-biodiversity workflow is available at `/experiences`,
`/experiences/favourites` and `/experiences/manage`, with destination and
offering detail routes. Discovery, availability, biodiversity context,
favourites and experience management use the registered public API. The
Coastal Map tab gets its MapLibre style configuration from the public API and
renders OpenFreeMap vector tiles with markers at published destination
coordinates. Map panning and zooming do not request device location; **Near
Me** requests the best available location accuracy only after the user taps it, sends it to the
nearby API request and does not save it in the app. Place search and experience
browsing remain available without location permission. Biodiversity values are
model predictions, not species observations. Schedules are interpreted in
`Asia/Colombo` and sent to the API as UTC instants.

### Rebuilt experience flows

- Discovery reads every available result page, preserves previously loaded
  sections during refresh failures, and shows errors alongside usable results.
  Search, activity and region filters remain separate from map place search.
- Discovery and management tabs keep their scroll position while switching.
  Tabs animate natively and the map keeps its native view alive. Tab selection
  survives navigation within the current app session, not a process restart.
- Map gestures own touches inside the map; swipe between tabs outside it.
  Coastline presets, place matches, destination chips and selected/nearby cards
  use the same public workflows as React. The map location button and Near Me
  request precise location only on tap, subject to device permission. Refreshing nearby results leaves
  the map mounted. Device location is not persisted.
- Destination and offering detail routes own their request state. Core content
  appears independently of optional marine, advisory, biodiversity and schedule
  requests. Failed context remains unavailable; it never indicates safe conditions.
- Management offers image heroes, pinned tabs, readable cards and consistent
  actions for all three sections. Publishing review applies only to its selected
  item and requested transition. Save buttons block repeated submissions; failed
  saves preserve fields and show the service error. Departure times wrap on phones.
- GET requests may try a fallback gateway after a connection failure. Writes
  are not automatically replayed after a timeout: refresh the affected section
  to check whether a change reached the service before submitting it again.
- Tests in `test/experience_rebuild_test.dart` cover paging, request races,
  account isolation, invalid responses, location denial, duplicate writes,
  retained tabs, failed forms and narrow layouts. Native map rendering and GPS
  still require an Android/iOS device; widget tests use an injected map platform.

## Commands

Run from `apps/mobile`:

```bash
flutter pub get
flutter doctor -v
flutter analyze
flutter test
flutter run
```

The Flutter project includes generated platform folders for Android, iOS,
Linux, macOS, Windows and web. Platform-specific release configuration should
be added only when that target is part of the delivery scope; it must not be
used to exclude a product role from the mobile client.

Android builds also need free space on the drive containing Gradle's user cache
and on the workspace drive. A healthy `flutter doctor` report does not check
whether engine extraction and APK packaging have enough disk space. Resolve
`There is not enough space on the disk` before retrying a failed build.

## Native session renewal

Experience requests share the native Auth client's credential provider. Before
dispatch, an access token within 30 seconds of expiry is renewed through the
public Auth refresh endpoint. Concurrent reads share one renewal; denied
permissions are not retried, and a changed account cannot be replaced by an
older renewal response. This keeps long-running map and management sessions
usable without restarting the app.

## Android build caches

The project disables Kotlin incremental compilation to avoid Kotlin cache
failures when the checkout and Pub cache are on different Windows drives.
Gradle task caching remains available; Kotlin recompilation may take longer.
See [Kotlin compilation and caches](https://kotlinlang.org/docs/gradle-compilation-and-caches.html).
Keep enough free space on the drives containing Gradle, Pub and the temporary
compiler output. On Windows, configure all three before running Flutter. From
the repository root in **each new PowerShell terminal**:

```powershell
. .\scripts\Windows\powershell\initialize-flutter-cache.ps1 -CacheRoot D:\WKS\FlutterBuildCache
Set-Location apps\mobile
flutter pub get
flutter analyze --no-pub
flutter test --no-pub
```

The helper reuses `gradle` and `pub` inside the chosen directory and sets both
`TEMP` and `TMP` to its `temp` directory for this terminal and child processes.
Gradle wrapper downloads, dependency transforms, Pub packages and temporary
compiler output use the selected cache drive. The Flutter SDK's own cache stays
inside its SDK directory. After changing `PUB_CACHE`, run `flutter pub get`;
retain `pubspec.lock` and reuse compatible cached dependencies.

## Android location permission

Coastal discovery requests location only after a user taps **Near Me** or the
map's current-location button. The app declares both fine and coarse foreground
permissions, requests `LocationAccuracy.best` with a 30-second limit, and offers
the precise permission choice to Android installs that previously had only
coarse access. Select **Precise** in the Android permission dialog; if needed,
enable **Use precise location** under the app's Location permission in settings.
The OS and available GPS signals determine the actual accuracy; selecting
approximate access remains a supported fallback. Background permission stays
removed from the merged APK using Android's
[manifest merge rules](https://developer.android.com/build/manage-manifests#node_markers),
and guarded by `MOB-EXP-033`. The request behavior is covered by MOB-EXP-041–048.
Location stays in memory and is used for the public
nearby request; place search remains available after a permission denial.
See Geolocator's [accuracy settings](https://pub.dev/documentation/geolocator/latest/geolocator/LocationAccuracy.html).

For an ARM64 Android phone, avoid downloading other architectures during the
first local validation build:

```powershell
flutter build apk --debug --target-platform=android-arm64 --dart-define=BLUEVERSE_API_BASE_URL=http://127.0.0.1:80
adb reverse tcp:80 tcp:80
```

That gateway setting requires the USB reverse tunnel. Use the emulator/LAN
settings below when the target is an emulator or a phone connected over Wi-Fi.

## API boundary

The Auth workflow calls the public ASP.NET Core gateway using the host address
appropriate for the device or emulator and port `80`. Android `localhost`
means the phone/emulator itself, not the laptop running Docker. For a standard
Android emulator, explicitly set `BLUEVERSE_API_BASE_URL` to
`http://10.0.2.2:80`; for a physical device, use the laptop's current LAN
address. Without an explicit compile-time value, Android tries the standard
emulator host `10.0.2.2:80`, then the USB reverse tunnel at `127.0.0.1:80`.
An explicit value overrides both defaults; no laptop LAN address is hardcoded.

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
flutter run --dart-define=BLUEVERSE_API_BASE_URL=http://127.0.0.1:80
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

## Marine Safety input and recovery behavior

### Open the Marine workspace

Signed-in launch opens the standard Dashboard. Accounts with
`marine.profile.read` can select **Marine & safety** on Dashboard or in the
account menu. The three registered routes also open their matching initial tab:
`/marine/conditions`, `/marine/history` and `/marine/safety-profiles`.
The native shell has one app bar, pinned horizontally scrollable pill navigation
and a vertically scrolling hero/form area. Labels match the Web app:
**Coastal conditions**, **Coastal evidence**, **Activity safety limits**.
The 260 ms panel transition is disabled by reduced-motion preferences.

Marine requests obtain the selected account's bearer header through the existing
Auth secure-storage adapter for every request; they do not copy tokens into
widgets or cache another account's credentials. Changes to account identity or
grants rebuild or remove protected content. The app owns and closes its Marine
HTTP client. Dashboard, Profile, Administration and account switching remain
reachable from the workspace.

The three approved Marine photographs are bundled under
`assets/coastal/marine/`; each hero stacks the photo above guidance and retains
readable copy if image decoding fails. Photographs illustrate the workflow and
do not represent live conditions or proof of safety. See the
[asset provenance](assets/coastal/marine/README.md).

Condition results show wind, waves, swell and rain with units, timestamps,
sources and missing evidence. Plain-language explanations preserve all four
server classifications, including UNKNOWN with no condition payload. UTC input
rejects impossible dates and normalizes explicit offsets. Pulling to refresh
the condition page performs a read and does not create another assessment.

Saved readings and assessments load independently. One unavailable dataset
does not hide the other; a failed refresh retains labelled last-loaded data and
offers retry. Profile reads have explicit retry, an unavailable response is
distinct from an empty list, and missing caution values show **Not configured**.
Tab bootstrap reads use inline feedback; explicit condition/history/profile
reads and production writes use shared loading feedback. Auth keeps its own
transition messages. Draft/version creation, independent review and confirmed
deactivation remain the same authorized actions as Web.

The mobile suite includes synthetic API and widget evidence for these paths;
physical-device and live gateway/provider integration remain separate checks.

The 2026-10-10 recheck also covers the complete native profile workflow: invalid
draft input makes no request; cited drafts and new versions stay inactive until
a different manager reviews them; cancelling deactivation makes no DELETE.
Closing the protected page cancels a pending dialog action, and replies from
older profile reads cannot replace newer refreshed data. Unknown Marine paths
are unmatched; profile details are shown within `/marine/safety-profiles`.
All Marine tests now use the default `*_test.dart` discovery convention.

An unavailable activity producer is shown as "Activity data unavailable" with
retry. Stored records and independent condition lookup remain usable; only
profile draft/edit/review and evaluation require available published activity
data. Deactivation remains available to authorized managers. The HTTP error
adapter retains the structured availability metadata; a successful empty
catalogue stays distinct from a dependency failure. See the shared
[availability contract](../../docs/api/marine-dependency-availability.md).

React and Flutter expose all three Marine pages with `marine.profile.read`;
profile mutation controls additionally require `marine.profile.manage`. Enter
latitude/longitude as signed decimal WGS84 coordinates. Nonblank invalid filters
are rejected before querying. Maximum and optional caution limits accept at most
two decimal places, preserving the exact reviewed values.

Condition reuse requires matching rounded coordinates, an applicable forecast
from requested time minus 30 minutes through plus one hour, and retrieval within
the preceding 15 minutes. Future retrieval timestamps cannot be reused.
Acquisition/evaluation rejects UTC request times outside
`0001-01-01T01:00:00Z`–`9999-12-31T22:59:59.9999999Z` with 400 before provider
access; provider forecast availability still has its own limits.

Flutter allows 45 seconds per request. An ambiguous timeout or connection failure
after dispatching a write reports that its outcome is unconfirmed and makes no
automatic second attempt. Check assessment history or the profile before retrying;
a timeout does not cancel server work. Read requests may try another configured
gateway candidate. Server-enforced mutation idempotency is not implemented.
