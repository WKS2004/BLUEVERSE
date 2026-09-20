# BLUEVERSE Mobile

The mobile client is a Flutter/Dart starter project. The checked-in app is the
generated counter sample; field workflows, location/evidence capture and API
integration are deferred to the v1 implementation work.

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
be added only when that target is part of the delivery scope.

## API boundary

When API integration is added, the app must call the public ASP.NET Core
gateway using the host/LAN address appropriate for the device or emulator and
an endpoint registered in
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
`integration_test` workflows. The integration registry is empty of domain API
calls while this starter has no backend workflow; add the public API contract
and both client surfaces together when the first real screen is introduced.
