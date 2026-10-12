# Coastal Operations mobile experience — 2026-10-11

The Flutter Coastal Operations screens use the same authorized workflows as
React through the existing ASP.NET Core public API. This rebuild changes
presentation and navigation; it preserves the domain models, request service,
permission grants and named routes in the
[UI integration contract](../contracts/ui-integration.json).

## Workspace experience

- Assessments, Alerts and Logs use a native pinned section navigation strip
  above scrolling content. The strip scrolls horizontally on narrow phones and
  marks the current section for accessibility.
- Each collection has a clear introduction and a light hero card with coastal
  photography above readable copy. Existing bundled assessment and advisory
  photographs remain scope-specific; Logs uses its category's photograph.
  Counts describe records on the current page, not collection totals.
- Content enters with a 420 ms directional fade and slide in both directions
  between Assessments/Alerts and Assessment logs/Alert logs. Shared chrome and
  tabs stay outside the animation. Search and refresh do not replay it.
  Platform reduced-motion settings disable entry movement and fading.
- Native pull-to-refresh, 500 ms live search, lifecycle filters, advanced ID
  search, server cursor pagination and page sizes 5/10/25/50/100 remain.
  Search places its filter/reset controls below the text field to leave room
  for entry on compact phones. Failed reads retain existing records and allow
  recovery; stale responses remain guarded by account/request generation.
- White record cards put wrapping status metadata above the title, then the
  coastal summary, dates, reference and authorized actions. Legacy records
  whose objective supplies the title do not repeat the same text.

## Forms, details and activity

Full-page create/edit workspaces retain Back and shared navigation. Forms group
the draft overview, coastal links, advisory audience/urgency, dates/time zone,
and review purpose/message. Named selections, native date/time pickers,
server-resolved time-zone rules, optimistic versions and idempotency behavior
remain. Generic save failures retain the user's entries and offer retry.
Unavailable catalogues allow unlinked drafts; publication still requires a
valid linked target according to the server contract.

Detail views retain grant-aware draft editing, cancellation/withdrawal,
assessment publication, advisory publication/resolution, scoped evidence,
context availability, operational status/history and recorded decisions.
Authored published content and all details opened from Logs remain read-only.
Assessment evidence upload/removal remains draft-only. Automated proposals
remain disconnected before G07; the UI does not fabricate recommendations or
approval/apply actions for absent proposals.

Activity uses expandable native cards with local readable times and exact
before/after values. Ordinary activity retains recorded names and roles;
Logs activity retains correlation references and its existing identity
display policy. Missing legacy snapshots remain explicit.

## Verification

`coastal_operations_rebuild_test.dart` checks phone/tablet sizes, doubled text,
read-only narrow details, pinned navigation, full-page form validation, both
transition directions, route category persistence, reduced motion and denied
Logs access. It also verifies that unexpected save failures retain entered
values and permit a successful retry. Existing workflow and API tests continue to cover draft save,
evidence, activity, permission isolation, stale reads and cursor controls.

Approved edits to existing tests add only scroll/frame waits for controls below
the taller content; their assertions remain unchanged. Optional preview
environment variables in the new test helper produce local image captures
without adding machine-specific configuration to the application.

Device-specific camera/gallery integration and real-server behavior require
separate device testing; deterministic widget/API tests use synthetic fixtures.

### 2026-10-11 Android recheck

The complete final mobile suite passed all 144 tests, `flutter analyze` found
no issues, and UI integration and endpoint catalogue validation passed. Pub
resolved the current lockfile offline without changing it. Gradle built the
final ARM64 debug APK offline using `D:\WKS\FlutterBuildCache\gradle`; package
dependencies resolve through `D:\WKS\FlutterBuildCache\pub`. These existing
caches are usable for the current physical-device build. The x86-64 debug
engine JAR is still absent, so this check does not verify an offline universal
APK or an x86-64 emulator build. The legacy `C:\Users\MSI\.gradle` directory
and other checked C: Gradle locations were already absent; no cache deletion
was necessary.

The APK was installed on the connected ARM64 Android 16 phone. The user signed
in locally, and Assessments, Alerts, Assessment logs and Alert logs opened
against the running public gateway through `adb reverse tcp:80 tcp:80` with
an explicit `http://127.0.0.1:80` build setting. Both navigation directions,
pinned tabs while scrolling, live empty states and both create forms were
checked. No Flutter or Android crash errors appeared in the app's process log.
A small workspace description was corrected to say coastal records so it
also applies to Alerts. The final APK was reinstalled, and a cold launch
restored the signed-in account successfully.

The live account currently has no Coastal Operations records. No records were
created, changed or published during this device check; those workflow checks
use the automated fixtures. Camera/gallery and populated record workflows
remain separate device checks. The forms correctly report unavailable coastal
catalogue integration, which still prevents publication without a valid link.
