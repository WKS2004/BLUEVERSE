/// Marine domain models shared by the Flutter client.
///
/// These are thin translations of the backend DTOs in
/// [Blueverse.MarineSafety.Dtos] and [Blueverse.MarineSafety.Models]. The
/// Flutter client never applies the safety rules; the backend returns the
/// evaluated result and this layer simply preserves and renders it.
///
/// Field names intentionally mirror the backend payload so a change in the
/// contract is visible in exactly one place.

import 'dart:convert';

/// A persisted condition snapshot as returned by the backend.
class ConditionSnapshotDto {
  const ConditionSnapshotDto({
    required this.id,
    required this.latitude,
    required this.longitude,
    required this.forecastTime,
    required this.retrievedAt,
    required this.windSpeed,
    required this.waveHeight,
    required this.swellHeight,
    required this.rain,
    required this.weatherCode,
    required this.source,
    required this.freshnessStatus,
    required this.missingFields,
  });

  final String id;
  final double latitude;
  final double longitude;
  final DateTime forecastTime;
  final DateTime retrievedAt;
  final double? windSpeed;
  final double? waveHeight;
  final double? swellHeight;
  final double? rain;
  final int? weatherCode;
  final String source;
  final String freshnessStatus;
  final List<String> missingFields;

  factory ConditionSnapshotDto.fromJson(Map<String, dynamic> json) {
    return ConditionSnapshotDto(
      id: _requiredString(json, 'id'),
      latitude: _requiredDouble(json, 'latitude'),
      longitude: _requiredDouble(json, 'longitude'),
      forecastTime: _requiredDateTime(json, 'forecastTime'),
      retrievedAt: _requiredDateTime(json, 'retrievedAt'),
      windSpeed: _optionalDouble(json, 'windSpeed'),
      waveHeight: _optionalDouble(json, 'waveHeight'),
      swellHeight: _optionalDouble(json, 'swellHeight'),
      rain: _optionalDouble(json, 'rain'),
      weatherCode: _optionalInt(json, 'weatherCode'),
      source: _requiredString(json, 'source'),
      freshnessStatus: _requiredString(json, 'freshnessStatus'),
      missingFields: _stringList(json['missingFields']),
    );
  }

  /// Mirrors the backend record. All map values are `String` so the JSON
  /// serializer can encode them without a run-time type error.
  Map<String, dynamic> toJson() => <String, dynamic>{
        'id': id,
        'latitude': latitude.toStringAsFixed(5),
        'longitude': longitude.toStringAsFixed(5),
        'forecastTime': forecastTime.toIso8601String(),
        'retrievedAt': retrievedAt.toIso8601String(),
        'windSpeed': windSpeed?.toStringAsFixed(2),
        'waveHeight': waveHeight?.toStringAsFixed(2),
        'swellHeight': swellHeight?.toStringAsFixed(2),
        'rain': rain?.toStringAsFixed(2),
        'weatherCode': weatherCode?.toString(),
        'source': source,
        'freshnessStatus': freshnessStatus,
        'missingFields': missingFields.join(','),
      };

  /// True when the backend classified the evidence as unavailable, stale or
  /// incomplete. Unknown is a valid server outcome and must not be treated as
  /// safe.
  bool get isUnavailable =>
      freshnessStatus == 'UNAVAILABLE' ||
      freshnessStatus == 'STALE' ||
      missingFields.isNotEmpty;
}

/// One request / result pair as returned by [evaluateSuitability].
class SuitabilityResultDto {
  const SuitabilityResultDto({
    required this.status,
    required this.activityId,
    required this.activityName,
    required this.location,
    required this.requestedTime,
    required this.evaluatedAt,
    required this.conditions,
    required this.source,
    required this.retrievedAt,
    required this.freshness,
    required this.missingFields,
    required this.violations,
    required this.cautionFactors,
    required this.assessmentId,
    required this.snapshotId,
  });

  final String status;
  final String activityId;
  final String activityName;
  final LocationDto location;
  final DateTime requestedTime;
  final DateTime evaluatedAt;
  final ConditionsDto? conditions;
  final String? source;
  final DateTime? retrievedAt;
  final String? freshness;
  final List<String> missingFields;
  final List<String> violations;
  final List<String> cautionFactors;
  final String assessmentId;
  final String snapshotId;

  factory SuitabilityResultDto.fromJson(Map<String, dynamic> json) {
    return SuitabilityResultDto(
      status: _requiredString(json, 'status'),
      activityId: _requiredString(json, 'activityId'),
      activityName: _requiredString(json, 'activityName'),
      location: LocationDto.fromJson(_requiredMap(json, 'location')),
      requestedTime: _requiredDateTime(json, 'requestedTime'),
      evaluatedAt: _requiredDateTime(json, 'evaluatedAt'),
      conditions: json['conditions'] == null
          ? null
          : ConditionsDto.fromJson(Map<String, dynamic>.from(json['conditions'])),
      source: json['source'] as String?,
      retrievedAt: json['retrievedAt'] != null
          ? DateTime.parse(json['retrievedAt'] as String)
          : null,
      freshness: json['freshness'] as String?,
      missingFields: _stringList(json['missingFields']),
      violations: _stringList(json['violations']),
      cautionFactors: _stringList(json['cautionFactors']),
      assessmentId: _requiredString(json, 'assessmentId'),
      snapshotId: _requiredString(json, 'snapshotId'),
    );
  }

  /// Mirrors the backend record. All map values are `String` so the JSON
  /// serializer can encode them without a run-time type error.
  Map<String, dynamic> toJson() => <String, dynamic>{
        'status': status,
        'activityId': activityId,
        'activityName': activityName,
        'location': location.toJson(),
        'requestedTime': requestedTime.toIso8601String(),
        'evaluatedAt': evaluatedAt.toIso8601String(),
        'source': source,
        'retrievedAt': retrievedAt?.toIso8601String(),
        'freshness': freshness,
        'missingFields': missingFields.join(','),
        'violations': violations.join(','),
        'cautionFactors': cautionFactors.join(','),
        'assessmentId': assessmentId,
        'snapshotId': snapshotId,
      };

  /// The officially classified outcome. It is set by the backend safety
  /// profile rules, never derived in Flutter.
  bool get isSuitable => status == 'SUITABLE';
  bool get isCaution => status == 'CAUTION';
  bool get isUnsuitable => status == 'UNSUITABLE';
  bool get isUnknown => status == 'UNKNOWN';
}

/// A location in WGS84 decimal degrees.
class LocationDto {
  const LocationDto({required this.latitude, required this.longitude});

  final double latitude;
  final double longitude;

  factory LocationDto.fromJson(Map<String, dynamic> json) => LocationDto(
        latitude: _requiredDouble(json, 'latitude'),
        longitude: _requiredDouble(json, 'longitude'),
      );

  Map<String, dynamic> toJson() => <String, dynamic>{
        'latitude': latitude.toStringAsFixed(5),
        'longitude': longitude.toStringAsFixed(5),
      };
}

/// Weather and marine variables returned by the suitability evaluator.
class ConditionsDto {
  const ConditionsDto({
    required this.windSpeed,
    required this.waveHeight,
    required this.swellHeight,
    required this.rain,
    required this.weatherCode,
  });

  final double? windSpeed;
  final double? waveHeight;
  final double? swellHeight;
  final double? rain;
  final int? weatherCode;

  factory ConditionsDto.fromJson(Map<String, dynamic> json) => ConditionsDto(
        windSpeed: _optionalDouble(json, 'windSpeed'),
        waveHeight: _optionalDouble(json, 'waveHeight'),
        swellHeight: _optionalDouble(json, 'swellHeight'),
        rain: _optionalDouble(json, 'rain'),
        weatherCode: _optionalInt(json, 'weatherCode'),
      );

  Map<String, dynamic> toJson() => <String, dynamic>{
        'windSpeed': windSpeed?.toStringAsFixed(2),
        'waveHeight': waveHeight?.toStringAsFixed(2),
        'swellHeight': swellHeight?.toStringAsFixed(2),
        'rain': rain?.toStringAsFixed(2),
        'weatherCode': weatherCode?.toString(),
      };
}

/// A configured deterministic safety profile for one activity.
class SafetyProfileDto {
  const SafetyProfileDto({
    required this.id,
    required this.activityId,
    required this.activityName,
    required this.maxWindSpeed,
    required this.maxWaveHeight,
    required this.maxSwellHeight,
    required this.cautionWindSpeed,
    required this.cautionWaveHeight,
    required this.cautionSwellHeight,
    required this.isActive,
    required this.version,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String activityId;
  final String activityName;
  final double maxWindSpeed;
  final double maxWaveHeight;
  final double maxSwellHeight;
  final double? cautionWindSpeed;
  final double? cautionWaveHeight;
  final double? cautionSwellHeight;
  final bool isActive;
  final int version;
  final DateTime createdAt;
  final DateTime updatedAt;

  factory SafetyProfileDto.fromJson(Map<String, dynamic> json) =>
      SafetyProfileDto(
        id: _requiredString(json, 'id'),
        activityId: _requiredString(json, 'activityId'),
        activityName: _requiredString(json, 'activityName'),
        maxWindSpeed: _requiredDouble(json, 'maxWindSpeed'),
        maxWaveHeight: _requiredDouble(json, 'maxWaveHeight'),
        maxSwellHeight: _requiredDouble(json, 'maxSwellHeight'),
        cautionWindSpeed: _optionalDouble(json, 'cautionWindSpeed'),
        cautionWaveHeight: _optionalDouble(json, 'cautionWaveHeight'),
        cautionSwellHeight: _optionalDouble(json, 'cautionSwellHeight'),
        isActive: _requiredBool(json, 'isActive'),
        version: _requiredInt(json, 'version'),
        createdAt: _requiredDateTime(json, 'createdAt'),
        updatedAt: _requiredDateTime(json, 'updatedAt'),
      );

  /// Mirrors the backend record. All map values are `String` so the JSON
  /// serializer can encode them without a run-time type error.
  Map<String, dynamic> toJson() => <String, dynamic>{
        'id': id,
        'activityId': activityId,
        'activityName': activityName,
        'maxWindSpeed': maxWindSpeed.toStringAsFixed(2),
        'maxWaveHeight': maxWaveHeight.toStringAsFixed(2),
        'maxSwellHeight': maxSwellHeight.toStringAsFixed(2),
        'cautionWindSpeed': cautionWindSpeed?.toStringAsFixed(2),
        'cautionWaveHeight': cautionWaveHeight?.toStringAsFixed(2),
        'cautionSwellHeight': cautionSwellHeight?.toStringAsFixed(2),
        'isActive': isActive ? 'true' : 'false',
        'version': version.toString(),
        'createdAt': createdAt.toIso8601String(),
        'updatedAt': updatedAt.toIso8601String(),
      };

  /// The server actively manages only one active row per activity; this
  /// status is authoritative.
  bool get isCurrent => isActive;
}

/// Draft for a new or edited safety profile. Mapped to the backend
/// [CreateSafetyProfileDto] / [UpdateSafetyProfileDto] payload.
class SafetyProfileDraft {
  SafetyProfileDraft({
    required this.maxWindSpeed,
    required this.maxWaveHeight,
    required this.maxSwellHeight,
    this.cautionWindSpeed,
    this.cautionWaveHeight,
    this.cautionSwellHeight,
  });

  final double maxWindSpeed;
  final double maxWaveHeight;
  final double maxSwellHeight;
  final double? cautionWindSpeed;
  final double? cautionWaveHeight;
  final double? cautionSwellHeight;

  factory SafetyProfileDraft.fromJson(Map<String, dynamic> json) =>
      SafetyProfileDraft(
        maxWindSpeed: _requiredDouble(json, 'maxWindSpeed'),
        maxWaveHeight: _requiredDouble(json, 'maxWaveHeight'),
        maxSwellHeight: _requiredDouble(json, 'maxSwellHeight'),
        cautionWindSpeed: _optionalDouble(json, 'cautionWindSpeed'),
        cautionWaveHeight: _optionalDouble(json, 'cautionWaveHeight'),
        cautionSwellHeight: _optionalDouble(json, 'cautionSwellHeight'),
      );

  Map<String, dynamic> toJson() => <String, dynamic>{
        'maxWindSpeed': maxWindSpeed.toStringAsFixed(2),
        'maxWaveHeight': maxWaveHeight.toStringAsFixed(2),
        'maxSwellHeight': maxSwellHeight.toStringAsFixed(2),
        'cautionWindSpeed': cautionWindSpeed?.toStringAsFixed(2),
        'cautionWaveHeight': cautionWaveHeight?.toStringAsFixed(2),
        'cautionSwellHeight': cautionSwellHeight?.toStringAsFixed(2),
      };

  /// Build the payload for [SafetyApiClient.createProfile].
  Map<String, dynamic> toCreatePayload({required String activityId}) =>
      <String, dynamic>{
        'activityId': activityId,
        'maxWindSpeed': maxWindSpeed,
        'maxWaveHeight': maxWaveHeight,
        'maxSwellHeight': maxSwellHeight,
        if (cautionWindSpeed != null)
          'cautionWindSpeed': cautionWindSpeed,
        if (cautionWaveHeight != null)
          'cautionWaveHeight': cautionWaveHeight,
        if (cautionSwellHeight != null)
          'cautionSwellHeight': cautionSwellHeight,
      };

  /// Build the payload for [SafetyApiClient.updateProfile].
  Map<String, dynamic> toUpdatePayload() => <String, dynamic>{
        'maxWindSpeed': maxWindSpeed,
        'maxWaveHeight': maxWaveHeight,
        'maxSwellHeight': maxSwellHeight,
        'isActive': true,
        if (cautionWindSpeed != null)
          'cautionWindSpeed': cautionWindSpeed,
        if (cautionWaveHeight != null)
          'cautionWaveHeight': cautionWaveHeight,
        if (cautionSwellHeight != null)
          'cautionSwellHeight': cautionSwellHeight,
      };
}

String _requiredString(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is! String || value.isEmpty) {
    throw FormatException('Missing or invalid $key.');
  }
  return value;
}

double _requiredDouble(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is! num) {
    throw FormatException('Missing or invalid $key.');
  }
  return value.toDouble();
}

int _requiredInt(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is! num || !value.isFinite) {
    throw FormatException('Missing or invalid $key.');
  }
  return value.toInt();
}

DateTime _requiredDateTime(Map<String, dynamic> json, String key) {
  try {
    return DateTime.parse(_requiredString(json, key));
  } catch (error) {
    throw FormatException('Missing or invalid $key.');
  }
}

double? _optionalDouble(Map<String, dynamic> json, String key) {
  if (json[key] == null) return null;
  if (json[key] is! num) {
    throw FormatException('Invalid $key.');
  }
  return (json[key] as num).toDouble();
}

int? _optionalInt(Map<String, dynamic> json, String key) {
  if (json[key] == null) return null;
  if (json[key] is! num || !json[key].isFinite) {
    throw FormatException('Invalid $key.');
  }
  return (json[key] as num).toInt();
}

bool _requiredBool(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is! bool) {
    throw FormatException('Missing or invalid $key.');
  }
  return value;
}

List<String> _stringList(Object? value) {
  if (value is! List) return const [];
  return value.whereType<String>().toList(growable: false);
}

Map<String, dynamic> _requiredMap(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is! Map) {
    throw FormatException('Missing or invalid $key.');
  }
  return Map<String, dynamic>.from(value);
}
