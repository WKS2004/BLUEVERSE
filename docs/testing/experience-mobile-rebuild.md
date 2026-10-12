# Flutter experience rebuild — 2026-10-10

Scope: Member 1 experience-biodiversity on the current feature branch. This
record covers implementation checks, not shared G00 or G07 acceptance.
The initial checks below are historical; the resumed Android verification
section records the newer build and physical-device evidence.

## Regression coverage

Existing MOB-EXP-001–013 assertions and fixtures were updated with user approval.
They retain API, permission, unavailable-context, wishlist and availability
checks. The old fixture returned the same published records for every status;
it now models separate status lists. Tests scroll the lazy discovery list and
use the current human-readable labels. Map widget tests inject the platform
boundary rather than changing the global target platform while widgets remain
mounted. This avoids tooltip/platform teardown failures; it does not simulate
native map rendering or GPS.

New cases in `apps/mobile/test/experience_rebuild_test.dart`:

| Cases | Behavior |
|---|---|
| MOB-EXP-014–015 | All pages, independent partial failures, latest search wins, unfiltered map destinations |
| MOB-EXP-016–018 | Core details before optional peers/schedules, stale detail responses, disposal |
| MOB-EXP-019–020 | Duplicate management writes, permission rejection, account-switch private-data isolation |
| MOB-EXP-021–022 | Cancelled and duplicate location requests, denied permission, manual discovery remains usable |
| MOB-EXP-023–025 | Invalid pagination/probabilities, GET failover, no timed-out write replay, scoped credentials |
| MOB-EXP-026–027 | Narrow management cards with enlarged text, retained discovery tab without automatic GPS |
| MOB-EXP-028 | Failed save preserves input, disables duplicate saves and blocks dismissal while saving |
| MOB-EXP-029–031 | Complete/bounded map coordinates, combined region/keyword filters, narrow departure controls |
| MOB-EXP-032 | Pushed detail isolation and safe wishlist refresh after the navigation frame |

## Initial rebuild checks

- `flutter analyze --no-pub`: no issues.
- Android `assembleDebug` passed with the corrected Kotlin setting, using
  `gradlew.bat --offline --no-daemon --console=plain
  "-Dorg.gradle.jvmargs=-Xmx2G -XX:MaxMetaspaceSize=1G"
  "-Ptarget=lib/main.dart" "-Ptarget-platform=android-arm64" assembleDebug`
  and the existing local Gradle cache. The resulting
  `apps/mobile/build/app/outputs/apk/debug/app-debug.apk` includes all three
  management hero assets. A final rebuild after the route-teardown fix also
  passed (34 seconds), and its Dart kernel is newer than the changed source.
  Earlier online attempts were stopped during slow
  dependency downloads; the first cached attempt exposed the cross-drive
  Kotlin incremental-cache defect. The offline build passed after disabling it.
- `flutter test --no-pub`: 133 tests passed, including Auth, administration,
  onboarding, gateway, experience API and experience widgets.
- `python scripts/validation/validate_ui_integrations.py`: passed.
- `python .agents/scripts/validate_endpoint_catalog.py`: passed; no route changes.
- Validator unit suite: 35 tests passed. On this Windows sandbox, temporary
  fixture directories were directed to a workspace temp folder after the
  default sandbox temp location denied file creation; expectations were unchanged.

Live checks against the local public gateway returned HTTP 200 for map
configuration, destinations, activities and offerings (5 destinations and
3 offerings in the current seed data). These host checks do not prove that a
physical phone can reach the gateway over Wi-Fi or USB.

## Initial device acceptance status

No Android/iOS device was connected during the initial rebuild. Verify real map style
and tile loading, attribution expansion, marker selection, pan/pinch gestures,
location permission denial/recovery and USB/LAN connectivity on the phone.
Switching tabs outside the map should remain possible while gestures inside
it stay with the map. Keep a selected destination open during a nearby refresh
and verify that the map stays mounted. Tab selection is retained only for the
current app session; a process restart does not restore it.

The current SDK/plugins still emit legacy Kotlin/AGP deprecation warnings;
these are a future upgrade concern and did not fail the debug build.

Public peer-service outages remain explicit unavailable states. The client
cannot make an absent private member service available. Production HTTPS/port
configuration and provider acceptance remain the documented follow-up work.

## Resumed Android verification — 2026-10-10

### Cache and build environment

- `flutter doctor -v`: Android SDK/toolchain healthy, licenses accepted, Redmi
  connected and authorized; Flutter 3.47.2 / Dart 3.13.2.
- Removed only the explicitly requested `C:\Users\MSI\.gradle` directory after
  validating its absolute location and checking for active cache users/reparse
  points. The directory remains absent after the subsequent builds.
- Reused `D:\WKS\FlutterBuildCache\gradle` and `pub`; the new PowerShell
  initializer directs Gradle, Pub, TEMP and TMP to this root for its terminal
  and child processes. Existing contents are preserved. Absolute-path checks,
  file-collision rejection, environment isolation and temporary-file creation
  were verified. It does not change Windows TEMP for unrelated applications.
- `flutter pub get` succeeded without changing the lockfile. All 86 external
  package file references resolved to D:, with none under C:.
- Debug builds succeeded through Flutter's normal APK command. The final
  session-renewal build completed in 19.5 seconds and was installed successfully
  on the Redmi. The installed ARM64 APK includes all three hero images and
  the MapLibre native library. The gateway was supplied at compile time as
  `http://10.111.186.188:80`, as requested; no laptop IP was hardcoded.

### Defects discovered during real-device checks

1. MapLibre's transitive Android SDK contributed precise location permission
   despite the app requesting approximate location. Manifest merge removal
   markers now remove fine/background location. The final merged manifest
   retains coarse location and Internet only for these capabilities; the
   installed app requested approximate location successfully. The Android app
   label was also corrected to BLUEVERSE.
2. After the native access token expired, draft/archived management requests
   returned HTTP 403 while published reads still worked. The shared native
   Auth credential provider now renews an access token within 30 seconds of
   expiry before dispatching experience/Auth requests. Concurrent loads share
   one rotating-token refresh. Real permission denials are not replayed;
   refresh rejection blocks the business request, and an account change or
   mismatched refresh identity cannot overwrite the selected account.
   Reinstalled management loaded its full status lists without the warning.
   After the updated app stayed open beyond the 15-minute access-token lifetime,
   opening management triggered the public Auth refresh and all draft/archived
   reads without 401/403 responses. The same Android process remained running;
   this verifies recovery without restarting or signing in again.

### Automated evidence

- `flutter analyze --no-pub`: no issues.
- `flutter test --no-pub --reporter expanded`: **141 tests passed**.
- MOB-EXP-033 protects the Android manifest permission removals and app label;
  merged-manifest/installed-package checks supply the native evidence.
- MOB-EXP-034–040 cover concurrent renewal, complete request headers/payloads,
  genuine permission denial, revoked renewal, account-switch races, mismatched
  identity, malformed JWT handling and legacy credential isolation.
- Endpoint catalog and UI integration validators passed. No routes or endpoint
  declarations were added; workflow references include the existing Auth
  renewal boundary. Validator unit suite: 35 passed during this verification.

### Physical Redmi evidence

| Check | Observed result |
|---|---|
| Gateway and session | LAN gateway health returned HTTP 200; user signed in on the phone; replacement installs retained/restored the account. |
| Real map | OpenFreeMap tiles, labels and destination markers rendered through native MapLibre. |
| Map gestures and tabs | Horizontal movement inside the map panned it without changing the tab. Explore → Map retained the camera; the captured map region was identical before/after. |
| Place search | Hikkaduwa returned five real place results. Selecting a result focused the map and loaded nearby Hikkaduwa/Mirissa cards. |
| Marker selection | Tapping Nilaveli's native marker displayed Nilaveli & Pigeon Island and its nearby result at 0.0 km. |
| Destination detail | Inspecting that marker opened the correct destination and linked offering while optional peer context showed safe unavailable copy. |
| Location denial | Declining permission showed recovery copy; manual map/search remained usable. |
| Approximate GPS | Coarse permission granted; current-location point appeared and centered the map. The device's area returned the valid empty result within 50 km of the five seeded destinations. Exact device coordinates were not recorded. |
| Management | Destinations, Activities and Offerings & Schedules rendered real records and their actions; the departure manager loaded its active schedule. No publishing-status warning after correction. |
| Runtime | Updated app process log contained no matching Dart unhandled exception, RenderFlex overflow or Android fatal exception during these checks. |

### Remaining acceptance scope

This verifies the Android debug build on one Redmi. Native pinch gestures,
iOS, release signing/production HTTPS and destructive/live authoring writes
were not exercised. Automated fixtures cover authoring, permissions and
failure behavior; they are not evidence of live production CRUD acceptance.
Provider acceptance and G00/G07 remain separate gates. Current plugin Kotlin
migration warnings are non-fatal and remain a future compatibility follow-up.

## Revised precise-location and tab verification — 2026-10-10

Ushan revised the requirement from approximate-only access to precise
foreground location. The earlier coarse-only evidence above is historical.
MOB-EXP-033 was updated under the existing approval for affected Flutter tests
to retain fine/coarse foreground permissions and continue excluding background
tracking. The service now requests `LocationAccuracy.best`, waits at most 30
seconds, and offers Android's precision choice to coarse-only upgraded installs.
Device/OS permission and signal quality still determine achieved accuracy;
denied access retains manual discovery. Method and UI copy now refer to current
location rather than incorrectly assuming an approximate fix. The iOS usage
description was updated; iOS execution has not been verified.

Management explicitly uses `TabAlignment.start` and zero strip padding to remove
Material 3's extra leading offset. Scrollable labels and animated tab selection
remain available on narrow screens.

- MOB-EXP-041–048 verify best accuracy, bounded wait, permission upgrade,
  initial denial recovery, denial/blocking, disabled services, timeouts and
  malformed/out-of-range fixes through a fake native location boundary.
- MOB-EXP-049 checks the rendered first tab position and switching at 320,
  400 and 1200 pixels.
- Full Flutter suite: **152 tests passed**; analysis: no issues.
- Android debug build: passed in 37 seconds. Final merged manifest contains
  fine/coarse location and Internet; background location is absent.
- Updated APK installed on the Redmi without clearing its account data.
- Both contract validators and all 35 validator regression tests passed.
- Build caches and temporary files remained under D:; C: `.gradle` stays absent.

### Final Redmi checks — 2026-10-10

After the owner unlocked the phone, the installed update passed these checks:

- Android displayed the approximate-to-precise upgrade dialog after tapping
  the map location button. Accepting it granted fine and coarse permission;
  the map centered on the current-location point. Background permission is
  absent. Achieved GPS accuracy still depends on the device and signal.
- The current-location nearby request completed with the valid empty result
  for the five seeded destinations within 50 km. Exact device coordinates
  were not recorded in repository evidence.
- Management's first tab starts without the extra leading offset; Destinations
  (5), Activities (4), and Offerings (3) loaded their records and distinct heroes.
- Hikkaduwa search returned five live results; selecting the first focused
  the map on that place. Horizontal map movement panned without changing tabs.
  Switching Explore → Coastal Map preserved the panned camera: the captured
  map region had identical pixels before and after returning.
- After a laptop restart, the owner reauthorized USB debugging and confirmed
  two-finger pinch zoom works without switching tabs. This is owner-observed
  gesture evidence; the automated ADB check used single-finger movement.
- Post-restart installed permissions retained fine/coarse grants. The running
  app's log contained zero matching Dart unhandled exceptions, Flutter errors,
  RenderFlex overflows or Android fatal exceptions during the captured window.
- C: `.gradle` remained absent. The terminal initializer directed Gradle, Pub,
  TEMP and TMP to `D:\WKS\FlutterBuildCache`; no new build was needed after
  the restart because the verified APK was already installed.

The Android debug/device verification is complete for this scope. iOS, signed
release/production HTTPS, live authoring writes, provider acceptance and shared
G00/G07 acceptance remain outside the checks performed here.
