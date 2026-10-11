import 'dart:async';

import 'package:flutter/foundation.dart';
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
  Future<ExperienceCoordinates> getCurrentLocation();
}

class GeolocatorExperienceLocationProvider
    implements ExperienceLocationProvider {
  const GeolocatorExperienceLocationProvider();

  @override
  Future<ExperienceCoordinates> getCurrentLocation() async {
    try {
      if (!await Geolocator.isLocationServiceEnabled()) {
        throw const ExperienceLocationException(
          'Location is turned off. Search for a coastal place instead.',
        );
      }

      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      } else if (defaultTargetPlatform == TargetPlatform.android &&
          permission != LocationPermission.deniedForever &&
          permission != LocationPermission.unableToDetermine &&
          await Geolocator.getLocationAccuracy() ==
              LocationAccuracyStatus.reduced) {
        // Existing installs may have only coarse access. Offer Android's
        // precise permission choice on this explicit location action.
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
          accuracy: LocationAccuracy.best,
          timeLimit: Duration(seconds: 30),
        ),
      );
      if (!position.latitude.isFinite ||
          !position.longitude.isFinite ||
          position.latitude < -90 ||
          position.latitude > 90 ||
          position.longitude < -180 ||
          position.longitude > 180) {
        throw const ExperienceLocationException(
          'Your current location could not be read. Search for a coastal place instead.',
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
        'Getting your current location took too long. Search for a coastal place instead.',
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
