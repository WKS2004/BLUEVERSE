import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:geolocator/geolocator.dart';
import 'package:mobile/data/services/experience_location_service.dart';

class _LocationPlatform extends GeolocatorPlatform {
  bool enabled = true;
  LocationPermission permission = LocationPermission.whileInUse;
  LocationPermission requestedPermission = LocationPermission.whileInUse;
  LocationAccuracyStatus precision = LocationAccuracyStatus.precise;
  int permissionRequests = 0;
  int positionRequests = 0;
  LocationSettings? settings;
  Object? positionError;
  double latitude = 6.14;
  double longitude = 80.10;

  @override
  Future<bool> isLocationServiceEnabled() async => enabled;
  @override
  Future<LocationPermission> checkPermission() async => permission;
  @override
  Future<LocationAccuracyStatus> getLocationAccuracy() async => precision;
  @override
  Future<LocationPermission> requestPermission() async {
    permissionRequests++;
    return requestedPermission;
  }

  @override
  Future<Position> getCurrentPosition({
    LocationSettings? locationSettings,
  }) async {
    positionRequests++;
    settings = locationSettings;
    if (positionError != null) throw positionError!;
    return Position(
      latitude: latitude,
      longitude: longitude,
      timestamp: DateTime.utc(2026),
      accuracy: 3,
      altitude: 0,
      altitudeAccuracy: 0,
      heading: 0,
      headingAccuracy: 0,
      speed: 0,
      speedAccuracy: 0,
    );
  }
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late GeolocatorPlatform original;
  late _LocationPlatform platform;
  const provider = GeolocatorExperienceLocationProvider();
  setUp(() {
    original = GeolocatorPlatform.instance;
    GeolocatorPlatform.instance = platform = _LocationPlatform();
    debugDefaultTargetPlatformOverride = TargetPlatform.android;
  });
  tearDown(() {
    GeolocatorPlatform.instance = original;
    debugDefaultTargetPlatformOverride = null;
  });

  // experience-discovery: revised precise foreground location requirement.
  test(
    'MOB-EXP-041 location action requests best accuracy with bounded wait',
    () async {
      final position = await provider.getCurrentLocation();
      expect(position.latitude, 6.14);
      expect(position.longitude, 80.10);
      expect(platform.settings!.accuracy, LocationAccuracy.best);
      expect(platform.settings!.timeLimit, const Duration(seconds: 30));
      expect(platform.positionRequests, 1);
      expect(platform.permissionRequests, 0);
    },
  );
  test(
    'MOB-EXP-042 upgraded coarse installs offer the precise permission choice',
    () async {
      platform.precision = LocationAccuracyStatus.reduced;
      final position = await provider.getCurrentLocation();
      expect(platform.permissionRequests, 1);
      expect(platform.positionRequests, 1);
      expect(position.longitude, 80.10);
      expect(platform.settings!.accuracy, LocationAccuracy.best);
    },
  );
  test(
    'MOB-EXP-043 initial permission denial can recover on user approval',
    () async {
      platform.permission = LocationPermission.denied;
      expect((await provider.getCurrentLocation()).latitude, 6.14);
      expect(platform.permissionRequests, 1);
      expect(platform.positionRequests, 1);
    },
  );
  test('MOB-EXP-044 rejected permission never fetches a position', () async {
    platform.permission = LocationPermission.denied;
    platform.requestedPermission = LocationPermission.denied;
    await expectLater(
      provider.getCurrentLocation(),
      throwsA(
        isA<ExperienceLocationException>().having(
          (e) => e.message,
          'recovery',
          contains('Search for a coastal place'),
        ),
      ),
    );
    expect(platform.permissionRequests, 1);
    expect(platform.positionRequests, 0);
  });
  test('MOB-EXP-045 blocked permission requires settings rather than another prompt', () async {
    platform.permission = LocationPermission.deniedForever;
    await expectLater(
      provider.getCurrentLocation(),
      throwsA(
        isA<ExperienceLocationException>().having(
          (e) => e.message,
          'settings',
          contains('device settings'),
        ),
      ),
    );
    expect(platform.permissionRequests, 0);
    expect(platform.positionRequests, 0);
  });
  test(
    'MOB-EXP-046 disabled location leaves manual search as recovery',
    () async {
      platform.enabled = false;
      await expectLater(
        provider.getCurrentLocation(),
        throwsA(
          isA<ExperienceLocationException>().having(
            (e) => e.message,
            'disabled',
            contains('turned off'),
          ),
        ),
      );
      expect(platform.positionRequests, 0);
      expect(platform.permissionRequests, 0);
    },
  );
  test('MOB-EXP-047 location timeout gives a bounded recovery error', () async {
    platform.positionError = TimeoutException('Synthetic timeout');
    await expectLater(
      provider.getCurrentLocation(),
      throwsA(
        isA<ExperienceLocationException>().having(
          (e) => e.message,
          'timeout',
          contains('took too long'),
        ),
      ),
    );
    expect(platform.positionRequests, 1);
  });
  test(
    'MOB-EXP-048 malformed and out-of-range device fixes are rejected',
    () async {
      for (final coordinates in [
        (double.nan, 80.0),
        (91.0, 80.0),
        (6.0, double.infinity),
        (6.0, -181.0),
      ]) {
        platform.latitude = coordinates.$1;
        platform.longitude = coordinates.$2;
        await expectLater(
          provider.getCurrentLocation(),
          throwsA(isA<ExperienceLocationException>()),
        );
      }
      expect(platform.positionRequests, 4);
    },
  );
}
