class DestinationDto {
  const DestinationDto({
    required this.id,
    required this.name,
    required this.slug,
    this.description,
    this.region,
    required this.latitude,
    required this.longitude,
    required this.status,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String name;
  final String slug;
  final String? description;
  final String? region;
  final double latitude;
  final double longitude;
  final String status;
  final DateTime createdAt;
  final DateTime updatedAt;

  factory DestinationDto.fromJson(Map<String, dynamic> json) {
    return DestinationDto(
      id: json['id'] as String? ?? '',
      name: json['name'] as String? ?? '',
      slug: json['slug'] as String? ?? '',
      description: json['description'] as String?,
      region: json['region'] as String?,
      latitude: (json['latitude'] as num?)?.toDouble() ?? 0.0,
      longitude: (json['longitude'] as num?)?.toDouble() ?? 0.0,
      status: json['status'] as String? ?? 'DRAFT',
      createdAt: json['createdAt'] != null
          ? DateTime.tryParse(json['createdAt'] as String) ?? DateTime.now()
          : DateTime.now(),
      updatedAt: json['updatedAt'] != null
          ? DateTime.tryParse(json['updatedAt'] as String) ?? DateTime.now()
          : DateTime.now(),
    );
  }

  Map<String, dynamic> toJson() => {
    'id': id,
    'name': name,
    'slug': slug,
    if (description != null) 'description': description,
    if (region != null) 'region': region,
    'latitude': latitude,
    'longitude': longitude,
    'status': status,
    'createdAt': createdAt.toIso8601String(),
    'updatedAt': updatedAt.toIso8601String(),
  };
}

class CreateDestinationRequest {
  const CreateDestinationRequest({
    required this.name,
    this.slug,
    this.description,
    this.region,
    required this.latitude,
    required this.longitude,
  });

  final String name;
  final String? slug;
  final String? description;
  final String? region;
  final double latitude;
  final double longitude;

  Map<String, dynamic> toJson() => {
    'name': name,
    if (slug != null) 'slug': slug,
    if (description != null) 'description': description,
    if (region != null) 'region': region,
    'latitude': latitude,
    'longitude': longitude,
  };
}

class UpdateDestinationRequest {
  const UpdateDestinationRequest({
    required this.name,
    this.slug,
    this.description,
    this.region,
    required this.latitude,
    required this.longitude,
  });

  final String name;
  final String? slug;
  final String? description;
  final String? region;
  final double latitude;
  final double longitude;

  Map<String, dynamic> toJson() => {
    'name': name,
    if (slug != null) 'slug': slug,
    if (description != null) 'description': description,
    if (region != null) 'region': region,
    'latitude': latitude,
    'longitude': longitude,
  };
}

class ActivityDto {
  const ActivityDto({
    required this.id,
    required this.code,
    required this.name,
    this.description,
    this.category,
    required this.status,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String code;
  final String name;
  final String? description;
  final String? category;
  final String status;
  final DateTime createdAt;
  final DateTime updatedAt;

  factory ActivityDto.fromJson(Map<String, dynamic> json) {
    return ActivityDto(
      id: json['id'] as String? ?? '',
      code: json['code'] as String? ?? '',
      name: json['name'] as String? ?? '',
      description: json['description'] as String?,
      category: json['category'] as String?,
      status: json['status'] as String? ?? 'DRAFT',
      createdAt: json['createdAt'] != null
          ? DateTime.tryParse(json['createdAt'] as String) ?? DateTime.now()
          : DateTime.now(),
      updatedAt: json['updatedAt'] != null
          ? DateTime.tryParse(json['updatedAt'] as String) ?? DateTime.now()
          : DateTime.now(),
    );
  }

  Map<String, dynamic> toJson() => {
    'id': id,
    'code': code,
    'name': name,
    if (description != null) 'description': description,
    if (category != null) 'category': category,
    'status': status,
    'createdAt': createdAt.toIso8601String(),
    'updatedAt': updatedAt.toIso8601String(),
  };
}

class CreateActivityRequest {
  const CreateActivityRequest({
    required this.code,
    required this.name,
    this.description,
    this.category,
  });

  final String code;
  final String name;
  final String? description;
  final String? category;

  Map<String, dynamic> toJson() => {
    'code': code,
    'name': name,
    if (description != null) 'description': description,
    if (category != null) 'category': category,
  };
}

class UpdateActivityRequest {
  const UpdateActivityRequest({
    required this.name,
    this.description,
    this.category,
  });

  final String name;
  final String? description;
  final String? category;

  Map<String, dynamic> toJson() => {
    'name': name,
    if (description != null) 'description': description,
    if (category != null) 'category': category,
  };
}

class OfferingDto {
  const OfferingDto({
    required this.id,
    required this.destinationId,
    required this.destinationName,
    required this.activityId,
    required this.activityName,
    required this.activityCode,
    required this.title,
    this.description,
    this.price,
    this.currency,
    this.durationMinutes,
    this.maxCapacity,
    required this.status,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String destinationId;
  final String destinationName;
  final String activityId;
  final String activityName;
  final String activityCode;
  final String title;
  final String? description;
  final double? price;
  final String? currency;
  final int? durationMinutes;
  final int? maxCapacity;
  final String status;
  final DateTime createdAt;
  final DateTime updatedAt;

  factory OfferingDto.fromJson(Map<String, dynamic> json) {
    return OfferingDto(
      id: json['id'] as String? ?? '',
      destinationId: json['destinationId'] as String? ?? '',
      destinationName: json['destinationName'] as String? ?? '',
      activityId: json['activityId'] as String? ?? '',
      activityName: json['activityName'] as String? ?? '',
      activityCode: json['activityCode'] as String? ?? '',
      title: json['title'] as String? ?? '',
      description: json['description'] as String?,
      price: (json['price'] as num?)?.toDouble(),
      currency: json['currency'] as String?,
      durationMinutes: json['durationMinutes'] as int?,
      maxCapacity: json['maxCapacity'] as int?,
      status: json['status'] as String? ?? 'DRAFT',
      createdAt: json['createdAt'] != null
          ? DateTime.tryParse(json['createdAt'] as String) ?? DateTime.now()
          : DateTime.now(),
      updatedAt: json['updatedAt'] != null
          ? DateTime.tryParse(json['updatedAt'] as String) ?? DateTime.now()
          : DateTime.now(),
    );
  }
}

class CreateOfferingRequest {
  const CreateOfferingRequest({
    required this.destinationId,
    required this.activityId,
    required this.title,
    this.description,
    this.price,
    this.currency,
    this.durationMinutes,
    this.maxCapacity,
  });

  final String destinationId;
  final String activityId;
  final String title;
  final String? description;
  final double? price;
  final String? currency;
  final int? durationMinutes;
  final int? maxCapacity;

  Map<String, dynamic> toJson() => {
    'destinationId': destinationId,
    'activityId': activityId,
    'title': title,
    if (description != null) 'description': description,
    if (price != null) 'price': price,
    if (currency != null) 'currency': currency,
    if (durationMinutes != null) 'durationMinutes': durationMinutes,
    if (maxCapacity != null) 'maxCapacity': maxCapacity,
  };
}

class UpdateOfferingRequest {
  const UpdateOfferingRequest({
    required this.title,
    this.description,
    this.price,
    this.currency,
    this.durationMinutes,
    this.maxCapacity,
  });

  final String title;
  final String? description;
  final double? price;
  final String? currency;
  final int? durationMinutes;
  final int? maxCapacity;

  Map<String, dynamic> toJson() => {
    'title': title,
    if (description != null) 'description': description,
    if (price != null) 'price': price,
    if (currency != null) 'currency': currency,
    if (durationMinutes != null) 'durationMinutes': durationMinutes,
    if (maxCapacity != null) 'maxCapacity': maxCapacity,
  };
}

class ScheduleDto {
  const ScheduleDto({
    required this.id,
    required this.offeringId,
    required this.startsAt,
    required this.endsAt,
    required this.timeZoneId,
    required this.isActive,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String offeringId;
  final DateTime startsAt;
  final DateTime endsAt;
  final String timeZoneId;
  final bool isActive;
  final DateTime createdAt;
  final DateTime updatedAt;

  factory ScheduleDto.fromJson(Map<String, dynamic> json) {
    return ScheduleDto(
      id: json['id'] as String? ?? '',
      offeringId: json['offeringId'] as String? ?? '',
      startsAt: json['startsAt'] != null
          ? DateTime.tryParse(json['startsAt'] as String) ?? DateTime.now()
          : DateTime.now(),
      endsAt: json['endsAt'] != null
          ? DateTime.tryParse(json['endsAt'] as String) ?? DateTime.now()
          : DateTime.now(),
      timeZoneId: json['timeZoneId'] as String? ?? 'Asia/Colombo',
      isActive: json['isActive'] as bool? ?? true,
      createdAt: json['createdAt'] != null
          ? DateTime.tryParse(json['createdAt'] as String) ?? DateTime.now()
          : DateTime.now(),
      updatedAt: json['updatedAt'] != null
          ? DateTime.tryParse(json['updatedAt'] as String) ?? DateTime.now()
          : DateTime.now(),
    );
  }
}

class CreateScheduleRequest {
  const CreateScheduleRequest({
    required this.startsAt,
    required this.endsAt,
    this.timeZoneId,
    this.isActive,
  });

  final DateTime startsAt;
  final DateTime endsAt;
  final String? timeZoneId;
  final bool? isActive;

  Map<String, dynamic> toJson() => {
    'startsAt': startsAt.toIso8601String(),
    'endsAt': endsAt.toIso8601String(),
    if (timeZoneId != null) 'timeZoneId': timeZoneId,
    if (isActive != null) 'isActive': isActive,
  };
}

class UpdateScheduleRequest {
  const UpdateScheduleRequest({
    required this.startsAt,
    required this.endsAt,
    this.timeZoneId,
    this.isActive,
  });

  final DateTime startsAt;
  final DateTime endsAt;
  final String? timeZoneId;
  final bool? isActive;

  Map<String, dynamic> toJson() => {
    'startsAt': startsAt.toIso8601String(),
    'endsAt': endsAt.toIso8601String(),
    if (timeZoneId != null) 'timeZoneId': timeZoneId,
    if (isActive != null) 'isActive': isActive,
  };
}

class PublicationEvaluationResponse {
  const PublicationEvaluationResponse({
    required this.targetId,
    required this.targetType,
    required this.currentStatus,
    required this.requestedStatus,
    required this.canTransition,
    required this.reasons,
  });

  final String targetId;
  final String targetType;
  final String currentStatus;
  final String requestedStatus;
  final bool canTransition;
  final List<String> reasons;

  factory PublicationEvaluationResponse.fromJson(Map<String, dynamic> json) {
    return PublicationEvaluationResponse(
      targetId: json['targetId'] as String? ?? '',
      targetType: json['targetType'] as String? ?? '',
      currentStatus: json['currentStatus'] as String? ?? '',
      requestedStatus: json['requestedStatus'] as String? ?? '',
      canTransition: json['canTransition'] as bool? ?? false,
      reasons: (json['reasons'] as List?)?.whereType<String>().toList() ?? const [],
    );
  }
}

class AvailabilityEvaluationRequest {
  const AvailabilityEvaluationRequest({
    required this.offeringId,
    required this.startsAt,
    required this.endsAt,
  });

  final String offeringId;
  final DateTime startsAt;
  final DateTime endsAt;

  Map<String, dynamic> toJson() => {
    'offeringId': offeringId,
    'startsAt': startsAt.toIso8601String(),
    'endsAt': endsAt.toIso8601String(),
  };
}

class OfferingSummaryDto {
  const OfferingSummaryDto({
    required this.offeringId,
    required this.offeringTitle,
    required this.destinationId,
    required this.destinationName,
    required this.destinationStatus,
    required this.activityId,
    required this.activityName,
    required this.activityStatus,
    required this.offeringStatus,
  });

  final String offeringId;
  final String offeringTitle;
  final String destinationId;
  final String destinationName;
  final String destinationStatus;
  final String activityId;
  final String activityName;
  final String activityStatus;
  final String offeringStatus;

  factory OfferingSummaryDto.fromJson(Map<String, dynamic> json) {
    return OfferingSummaryDto(
      offeringId: json['offeringId'] as String? ?? '',
      offeringTitle: json['offeringTitle'] as String? ?? '',
      destinationId: json['destinationId'] as String? ?? '',
      destinationName: json['destinationName'] as String? ?? '',
      destinationStatus: json['destinationStatus'] as String? ?? '',
      activityId: json['activityId'] as String? ?? '',
      activityName: json['activityName'] as String? ?? '',
      activityStatus: json['activityStatus'] as String? ?? '',
      offeringStatus: json['offeringStatus'] as String? ?? '',
    );
  }
}

class OperationalRestrictionContextDto {
  const OperationalRestrictionContextDto({
    required this.hasRestriction,
    this.restrictionType,
    this.severity,
    this.reason,
    this.effectiveUntil,
    required this.sourceStatus,
    required this.responded,
    this.targetEndpoint,
    required this.attemptsCount,
    required this.latencyMs,
    this.remoteStatus,
    this.message,
  });

  final bool hasRestriction;
  final String? restrictionType;
  final String? severity;
  final String? reason;
  final String? effectiveUntil;
  final String sourceStatus;
  final bool responded;
  final String? targetEndpoint;
  final int attemptsCount;
  final int latencyMs;
  final String? remoteStatus;
  final String? message;

  factory OperationalRestrictionContextDto.fromJson(Map<String, dynamic> json) {
    return OperationalRestrictionContextDto(
      hasRestriction: json['hasRestriction'] as bool? ?? false,
      restrictionType: json['restrictionType'] as String?,
      severity: json['severity'] as String?,
      reason: json['reason'] as String?,
      effectiveUntil: json['effectiveUntil'] as String?,
      sourceStatus: json['sourceStatus'] as String? ?? 'NOT_CONNECTED',
      responded: json['responded'] as bool? ?? false,
      targetEndpoint: json['targetEndpoint'] as String?,
      attemptsCount: json['attemptsCount'] as int? ?? 0,
      latencyMs: json['latencyMs'] as int? ?? 0,
      remoteStatus: json['remoteStatus'] as String?,
      message: json['message'] as String?,
    );
  }
}

class AvailabilityEvaluationResponse {
  const AvailabilityEvaluationResponse({
    required this.offeringId,
    required this.startsAt,
    required this.endsAt,
    required this.status,
    required this.reasonCodes,
    required this.offering,
    this.operationalRestriction,
    required this.evaluatedAt,
  });

  final String offeringId;
  final DateTime startsAt;
  final DateTime endsAt;
  final String status;
  final List<String> reasonCodes;
  final OfferingSummaryDto offering;
  final OperationalRestrictionContextDto? operationalRestriction;
  final DateTime evaluatedAt;

  factory AvailabilityEvaluationResponse.fromJson(Map<String, dynamic> json) {
    return AvailabilityEvaluationResponse(
      offeringId: json['offeringId'] as String? ?? '',
      startsAt: json['startsAt'] != null
          ? DateTime.tryParse(json['startsAt'] as String) ?? DateTime.now()
          : DateTime.now(),
      endsAt: json['endsAt'] != null
          ? DateTime.tryParse(json['endsAt'] as String) ?? DateTime.now()
          : DateTime.now(),
      status: json['status'] as String? ?? 'UNKNOWN',
      reasonCodes: (json['reasonCodes'] as List?)?.whereType<String>().toList() ?? const [],
      offering: OfferingSummaryDto.fromJson(
        json['offering'] is Map ? Map<String, dynamic>.from(json['offering']) : const {},
      ),
      operationalRestriction: json['operationalRestriction'] is Map
          ? OperationalRestrictionContextDto.fromJson(
              Map<String, dynamic>.from(json['operationalRestriction']),
            )
          : null,
      evaluatedAt: json['evaluatedAt'] != null
          ? DateTime.tryParse(json['evaluatedAt'] as String) ?? DateTime.now()
          : DateTime.now(),
    );
  }
}

class FocalSpeciesPredictionDto {
  const FocalSpeciesPredictionDto({
    required this.speciesName,
    required this.scientificName,
    required this.conservationStatus,
    required this.occurrenceProbability,
    this.habitatSuitability,
    this.primaryThreats,
  });

  final String speciesName;
  final String scientificName;
  final String conservationStatus;
  final double occurrenceProbability;
  final String? habitatSuitability;
  final String? primaryThreats;

  factory FocalSpeciesPredictionDto.fromJson(Map<String, dynamic> json) {
    return FocalSpeciesPredictionDto(
      speciesName: json['speciesName'] as String? ?? '',
      scientificName: json['scientificName'] as String? ?? '',
      conservationStatus: json['conservationStatus'] as String? ?? '',
      occurrenceProbability: (json['occurrenceProbability'] as num?)?.toDouble() ?? 0.0,
      habitatSuitability: json['habitatSuitability'] as String?,
      primaryThreats: json['primaryThreats'] as String?,
    );
  }
}

class BiodiversityContextResponseDto {
  const BiodiversityContextResponseDto({
    required this.destinationId,
    required this.destinationName,
    required this.latitude,
    required this.longitude,
    required this.status,
    required this.predictions,
    this.modelVersion,
    this.modelSource,
    this.uncertaintyNotes,
    this.evaluatedAt,
    required this.disclaimer,
    required this.responded,
    this.targetEndpoint,
    required this.attemptsCount,
    required this.latencyMs,
    this.remoteStatus,
    this.message,
  });

  final String destinationId;
  final String destinationName;
  final double latitude;
  final double longitude;
  final String status;
  final List<FocalSpeciesPredictionDto> predictions;
  final String? modelVersion;
  final String? modelSource;
  final String? uncertaintyNotes;
  final String? evaluatedAt;
  final String disclaimer;
  final bool responded;
  final String? targetEndpoint;
  final int attemptsCount;
  final int latencyMs;
  final String? remoteStatus;
  final String? message;

  factory BiodiversityContextResponseDto.fromJson(Map<String, dynamic> json) {
    return BiodiversityContextResponseDto(
      destinationId: json['destinationId'] as String? ?? '',
      destinationName: json['destinationName'] as String? ?? '',
      latitude: (json['latitude'] as num?)?.toDouble() ?? 0.0,
      longitude: (json['longitude'] as num?)?.toDouble() ?? 0.0,
      status: json['status'] as String? ?? 'not_connected',
      predictions: (json['predictions'] as List?)
              ?.whereType<Map>()
              .map((p) => FocalSpeciesPredictionDto.fromJson(Map<String, dynamic>.from(p)))
              .toList() ??
          const [],
      modelVersion: json['modelVersion'] as String?,
      modelSource: json['modelSource'] as String?,
      uncertaintyNotes: json['uncertaintyNotes'] as String?,
      evaluatedAt: json['evaluatedAt'] as String?,
      disclaimer: json['disclaimer'] as String? ?? '',
      responded: json['responded'] as bool? ?? false,
      targetEndpoint: json['targetEndpoint'] as String?,
      attemptsCount: json['attemptsCount'] as int? ?? 0,
      latencyMs: json['latencyMs'] as int? ?? 0,
      remoteStatus: json['remoteStatus'] as String?,
      message: json['message'] as String?,
    );
  }
}

class MarineConditionsContextDto {
  const MarineConditionsContextDto({
    required this.destinationId,
    required this.destinationName,
    required this.latitude,
    required this.longitude,
    required this.responded,
    this.targetEndpoint,
    required this.attemptsCount,
    required this.latencyMs,
    required this.remoteStatus,
    required this.safetyLevel,
    required this.waterCondition,
    this.waveHeightMeters,
    this.windSpeedKnots,
    this.tideStatus,
    this.advisoryMessage,
    required this.fallbackUsed,
    this.evaluatedAt,
    required this.disclaimer,
  });

  final String destinationId;
  final String destinationName;
  final double latitude;
  final double longitude;
  final bool responded;
  final String? targetEndpoint;
  final int attemptsCount;
  final int latencyMs;
  final String remoteStatus;
  final String safetyLevel;
  final String waterCondition;
  final double? waveHeightMeters;
  final double? windSpeedKnots;
  final String? tideStatus;
  final String? advisoryMessage;
  final bool fallbackUsed;
  final String? evaluatedAt;
  final String disclaimer;

  factory MarineConditionsContextDto.fromJson(Map<String, dynamic> json) {
    return MarineConditionsContextDto(
      destinationId: json['destinationId'] as String? ?? '',
      destinationName: json['destinationName'] as String? ?? '',
      latitude: (json['latitude'] as num?)?.toDouble() ?? 0.0,
      longitude: (json['longitude'] as num?)?.toDouble() ?? 0.0,
      responded: json['responded'] as bool? ?? false,
      targetEndpoint: json['targetEndpoint'] as String?,
      attemptsCount: json['attemptsCount'] as int? ?? 0,
      latencyMs: json['latencyMs'] as int? ?? 0,
      remoteStatus: json['remoteStatus'] as String? ?? 'NOT_CONNECTED',
      safetyLevel: json['safetyLevel'] as String? ?? 'Moderate',
      waterCondition: json['waterCondition'] as String? ?? 'Normal',
      waveHeightMeters: (json['waveHeightMeters'] as num?)?.toDouble(),
      windSpeedKnots: (json['windSpeedKnots'] as num?)?.toDouble(),
      tideStatus: json['tideStatus'] as String?,
      advisoryMessage: json['advisoryMessage'] as String?,
      fallbackUsed: json['fallbackUsed'] as bool? ?? false,
      evaluatedAt: json['evaluatedAt'] as String?,
      disclaimer: json['disclaimer'] as String? ?? '',
    );
  }
}

class ActiveAdvisoryItemDto {
  const ActiveAdvisoryItemDto({
    required this.advisoryId,
    required this.title,
    required this.severity,
    required this.description,
    required this.issuedAt,
    this.expiresAt,
  });

  final String advisoryId;
  final String title;
  final String severity;
  final String description;
  final String issuedAt;
  final String? expiresAt;

  factory ActiveAdvisoryItemDto.fromJson(Map<String, dynamic> json) {
    return ActiveAdvisoryItemDto(
      advisoryId: json['advisoryId'] as String? ?? '',
      title: json['title'] as String? ?? '',
      severity: json['severity'] as String? ?? 'Info',
      description: json['description'] as String? ?? '',
      issuedAt: json['issuedAt'] as String? ?? '',
      expiresAt: json['expiresAt'] as String?,
    );
  }
}

class OperationalAdvisoriesResponseDto {
  const OperationalAdvisoriesResponseDto({
    required this.destinationId,
    required this.destinationName,
    required this.responded,
    this.targetEndpoint,
    required this.attemptsCount,
    required this.latencyMs,
    required this.remoteStatus,
    required this.advisories,
    required this.fallbackUsed,
    required this.message,
  });

  final String destinationId;
  final String destinationName;
  final bool responded;
  final String? targetEndpoint;
  final int attemptsCount;
  final int latencyMs;
  final String remoteStatus;
  final List<ActiveAdvisoryItemDto> advisories;
  final bool fallbackUsed;
  final String message;

  factory OperationalAdvisoriesResponseDto.fromJson(Map<String, dynamic> json) {
    return OperationalAdvisoriesResponseDto(
      destinationId: json['destinationId'] as String? ?? '',
      destinationName: json['destinationName'] as String? ?? '',
      responded: json['responded'] as bool? ?? false,
      targetEndpoint: json['targetEndpoint'] as String?,
      attemptsCount: json['attemptsCount'] as int? ?? 0,
      latencyMs: json['latencyMs'] as int? ?? 0,
      remoteStatus: json['remoteStatus'] as String? ?? 'NOT_CONNECTED',
      advisories: (json['advisories'] as List?)
              ?.whereType<Map>()
              .map((a) => ActiveAdvisoryItemDto.fromJson(Map<String, dynamic>.from(a)))
              .toList() ??
          const [],
      fallbackUsed: json['fallbackUsed'] as bool? ?? false,
      message: json['message'] as String? ?? '',
    );
  }
}

class FavouriteDto {
  const FavouriteDto({
    required this.id,
    required this.userId,
    required this.targetType,
    required this.targetId,
    this.targetTitle,
    this.targetStatus,
    required this.createdAt,
  });

  final String id;
  final String userId;
  final String targetType;
  final String targetId;
  final String? targetTitle;
  final String? targetStatus;
  final DateTime createdAt;

  factory FavouriteDto.fromJson(Map<String, dynamic> json) {
    return FavouriteDto(
      id: json['id'] as String? ?? '',
      userId: json['userId'] as String? ?? '',
      targetType: json['targetType'] as String? ?? '',
      targetId: json['targetId'] as String? ?? '',
      targetTitle: json['targetTitle'] as String?,
      targetStatus: json['targetStatus'] as String?,
      createdAt: json['createdAt'] != null
          ? DateTime.tryParse(json['createdAt'] as String) ?? DateTime.now()
          : DateTime.now(),
    );
  }
}

class MapConfigDto {
  const MapConfigDto({
    required this.provider,
    required this.tileServiceType,
    required this.vectorTileUrl,
    required this.availableStyles,
    required this.defaultStyle,
    required this.attribution,
    required this.documentationUrl,
  });

  final String provider;
  final String tileServiceType;
  final String vectorTileUrl;
  final Map<String, String> availableStyles;
  final String defaultStyle;
  final String attribution;
  final String documentationUrl;

  factory MapConfigDto.fromJson(Map<String, dynamic> json) {
    return MapConfigDto(
      provider: json['provider'] as String? ?? 'OpenStreetMap / BLUEVERSE Tile Grid',
      tileServiceType: json['tileServiceType'] as String? ?? 'vector',
      vectorTileUrl: json['vectorTileUrl'] as String? ?? '',
      availableStyles: (json['availableStyles'] as Map?)?.map(
            (k, v) => MapEntry(k.toString(), v.toString()),
          ) ??
          const {},
      defaultStyle: json['defaultStyle'] as String? ?? 'coastal-bathymetry',
      attribution: json['attribution'] as String? ?? '© OpenStreetMap contributors',
      documentationUrl: json['documentationUrl'] as String? ?? '',
    );
  }
}

class MapSearchResultItemDto {
  const MapSearchResultItemDto({
    required this.displayName,
    required this.latitude,
    required this.longitude,
    this.type,
    this.category,
    this.region,
    this.country,
  });

  final String displayName;
  final double latitude;
  final double longitude;
  final String? type;
  final String? category;
  final String? region;
  final String? country;

  factory MapSearchResultItemDto.fromJson(Map<String, dynamic> json) {
    return MapSearchResultItemDto(
      displayName: json['displayName'] as String? ?? '',
      latitude: (json['latitude'] as num?)?.toDouble() ?? 0.0,
      longitude: (json['longitude'] as num?)?.toDouble() ?? 0.0,
      type: json['type'] as String?,
      category: json['category'] as String?,
      region: json['region'] as String?,
      country: json['country'] as String?,
    );
  }
}

class MapSearchResponseDto {
  const MapSearchResponseDto({
    required this.query,
    required this.results,
    required this.source,
    required this.fallback,
    required this.retrievedAt,
  });

  final String query;
  final List<MapSearchResultItemDto> results;
  final String source;
  final bool fallback;
  final String retrievedAt;

  factory MapSearchResponseDto.fromJson(Map<String, dynamic> json) {
    return MapSearchResponseDto(
      query: json['query'] as String? ?? '',
      results: (json['results'] as List?)
              ?.whereType<Map>()
              .map((r) => MapSearchResultItemDto.fromJson(Map<String, dynamic>.from(r)))
              .toList() ??
          const [],
      source: json['source'] as String? ?? '',
      fallback: json['fallback'] as bool? ?? false,
      retrievedAt: json['retrievedAt'] as String? ?? '',
    );
  }
}

class NearbyDestinationDto {
  const NearbyDestinationDto({
    required this.destinationId,
    required this.name,
    required this.slug,
    this.description,
    this.region,
    required this.latitude,
    required this.longitude,
    required this.distanceMeters,
    required this.activeOfferingsCount,
  });

  final String destinationId;
  final String name;
  final String slug;
  final String? description;
  final String? region;
  final double latitude;
  final double longitude;
  final double distanceMeters;
  final int activeOfferingsCount;

  factory NearbyDestinationDto.fromJson(Map<String, dynamic> json) {
    return NearbyDestinationDto(
      destinationId: json['destinationId'] as String? ?? '',
      name: json['name'] as String? ?? '',
      slug: json['slug'] as String? ?? '',
      description: json['description'] as String?,
      region: json['region'] as String?,
      latitude: (json['latitude'] as num?)?.toDouble() ?? 0.0,
      longitude: (json['longitude'] as num?)?.toDouble() ?? 0.0,
      distanceMeters: (json['distanceMeters'] as num?)?.toDouble() ?? 0.0,
      activeOfferingsCount: json['activeOfferingsCount'] as int? ?? 0,
    );
  }
}

class NearbyResponse {
  const NearbyResponse({
    required this.count,
    required this.results,
  });

  final int count;
  final List<NearbyDestinationDto> results;

  factory NearbyResponse.fromJson(Map<String, dynamic> json) {
    return NearbyResponse(
      count: json['count'] as int? ?? 0,
      results: (json['results'] as List?)
              ?.whereType<Map>()
              .map((d) => NearbyDestinationDto.fromJson(Map<String, dynamic>.from(d)))
              .toList() ??
          const [],
    );
  }
}

class MicroserviceDependencyReportDto {
  const MicroserviceDependencyReportDto({
    this.serviceKey,
    required this.serviceName,
    this.targetEndpoint,
    this.endpoint,
    required this.responded,
    required this.latencyMs,
    this.statusCode,
    this.httpStatusCode,
    this.attemptsCount,
    required this.status,
    this.fallbackStrategy,
    this.message,
    this.checkedAt,
  });

  final String? serviceKey;
  final String serviceName;
  final String? targetEndpoint;
  final String? endpoint;
  final bool responded;
  final int latencyMs;
  final int? statusCode;
  final int? httpStatusCode;
  final int? attemptsCount;
  final String status;
  final String? fallbackStrategy;
  final String? message;
  final String? checkedAt;

  factory MicroserviceDependencyReportDto.fromJson(Map<String, dynamic> json) {
    return MicroserviceDependencyReportDto(
      serviceKey: json['serviceKey'] as String?,
      serviceName: json['serviceName'] as String? ?? '',
      targetEndpoint: json['targetEndpoint'] as String?,
      endpoint: json['endpoint'] as String?,
      responded: json['responded'] as bool? ?? false,
      latencyMs: json['latencyMs'] as int? ?? 0,
      statusCode: json['statusCode'] as int?,
      httpStatusCode: json['httpStatusCode'] as int?,
      attemptsCount: json['attemptsCount'] as int?,
      status: json['status'] as String? ?? 'UNKNOWN',
      fallbackStrategy: json['fallbackStrategy'] as String?,
      message: json['message'] as String?,
      checkedAt: json['checkedAt'] as String?,
    );
  }
}

class DependenciesStatusResponseDto {
  const DependenciesStatusResponseDto({
    this.microservice,
    this.serviceName,
    this.version,
    this.overallStatus,
    this.evaluatedAt,
    this.timestamp,
    required this.dependencies,
    this.resilienceNote,
    this.allHealthy,
  });

  final String? microservice;
  final String? serviceName;
  final String? version;
  final String? overallStatus;
  final String? evaluatedAt;
  final String? timestamp;
  final List<MicroserviceDependencyReportDto> dependencies;
  final String? resilienceNote;
  final bool? allHealthy;

  factory DependenciesStatusResponseDto.fromJson(Map<String, dynamic> json) {
    return DependenciesStatusResponseDto(
      microservice: json['microservice'] as String?,
      serviceName: json['serviceName'] as String?,
      version: json['version'] as String?,
      overallStatus: json['overallStatus'] as String?,
      evaluatedAt: json['evaluatedAt'] as String?,
      timestamp: json['timestamp'] as String?,
      dependencies: (json['dependencies'] as List?)
              ?.whereType<Map>()
              .map((d) => MicroserviceDependencyReportDto.fromJson(Map<String, dynamic>.from(d)))
              .toList() ??
          const [],
      resilienceNote: json['resilienceNote'] as String?,
      allHealthy: json['allHealthy'] as bool?,
    );
  }
}

class AgentContextResponseDto {
  const AgentContextResponseDto({
    required this.agentName,
    required this.status,
    required this.detail,
    required this.plannedTools,
    required this.checkedAt,
  });

  final String agentName;
  final String status;
  final String detail;
  final List<String> plannedTools;
  final String checkedAt;

  factory AgentContextResponseDto.fromJson(Map<String, dynamic> json) {
    return AgentContextResponseDto(
      agentName: json['agentName'] as String? ?? '',
      status: json['status'] as String? ?? '',
      detail: json['detail'] as String? ?? '',
      plannedTools: (json['plannedTools'] as List?)?.whereType<String>().toList() ?? const [],
      checkedAt: json['checkedAt'] as String? ?? '',
    );
  }
}

class PagedResult<T> {
  const PagedResult({
    required this.total,
    required this.page,
    required this.pageSize,
    required this.items,
  });

  final int total;
  final int page;
  final int pageSize;
  final List<T> items;

  factory PagedResult.fromJson(
    Map<String, dynamic> json,
    T Function(Map<String, dynamic>) fromJsonT,
  ) {
    return PagedResult(
      total: json['total'] as int? ?? 0,
      page: json['page'] as int? ?? 1,
      pageSize: json['pageSize'] as int? ?? 0,
      items: (json['items'] as List?)
              ?.whereType<Map>()
              .map((item) => fromJsonT(Map<String, dynamic>.from(item)))
              .toList() ??
          const [],
    );
  }
}
