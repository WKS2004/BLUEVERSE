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
gateway using the host/LAN address appropriate for the device or emulator. It
must not call internal Auth or Agentic AI services directly. The mobile client
and React client must use the same backend authorization model and contract.
