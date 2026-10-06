import 'dart:async';

import 'package:flutter/services.dart';
import 'package:geolocator/geolocator.dart';

class ExperienceCoordinates {
  const ExperienceCoordinates({
    required this.latitude,
    required this.longitude,
  });

  final double latitude;
  final double longitude;
}

class ExperienceLocationException implements Exception {
  const ExperienceLocationException(this.message);

  final String message;

  @override
  String toString() => message;
}

abstract interface class ExperienceLocationProvider {
  Future<ExperienceCoordinates> getApproximateCurrentLocation();
}

class GeolocatorExperienceLocationProvider
    implements ExperienceLocationProvider {
  const GeolocatorExperienceLocationProvider();

  @override
  Future<ExperienceCoordinates> getApproximateCurrentLocation() async {
    try {
      if (!await Geolocator.isLocationServiceEnabled()) {
        throw const ExperienceLocationException(
          'Location is turned off. Search for a coastal place instead.',
        );
      }

      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      }

      if (permission == LocationPermission.deniedForever) {
        throw const ExperienceLocationException(
          'Location permission is blocked. Enable it in your device settings, or search for a coastal place.',
        );
      }
      if (permission == LocationPermission.denied ||
          permission == LocationPermission.unableToDetermine) {
        throw const ExperienceLocationException(
          'Location permission was not granted. Search for a coastal place instead.',
        );
      }

      final position = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.low,
          timeLimit: Duration(seconds: 10),
        ),
      );
      if (!position.latitude.isFinite || !position.longitude.isFinite) {
        throw const ExperienceLocationException(
          'Your approximate location could not be read. Search for a coastal place instead.',
        );
      }

      return ExperienceCoordinates(
        latitude: position.latitude,
        longitude: position.longitude,
      );
    } on ExperienceLocationException {
      rethrow;
    } on LocationServiceDisabledException {
      throw const ExperienceLocationException(
        'Location is turned off. Search for a coastal place instead.',
      );
    } on PermissionDeniedException {
      throw const ExperienceLocationException(
        'Location permission was not granted. Search for a coastal place instead.',
      );
    } on TimeoutException {
      throw const ExperienceLocationException(
        'Getting your approximate location took too long. Search for a coastal place instead.',
      );
    } on MissingPluginException {
      throw const ExperienceLocationException(
        'Location is not available on this device. Search for a coastal place instead.',
      );
    } on PlatformException {
      throw const ExperienceLocationException(
        'Location is not available right now. Search for a coastal place instead.',
      );
    }
  }
}
