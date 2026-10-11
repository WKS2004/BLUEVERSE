/// DTOs for the Smart Coastal Planner public API. Shapes mirror
/// services/coastal-planner/Models/Dtos/PlannerDtos.cs so the mobile
/// client stays in lockstep with the shared contract.
library;

class PlannerCatalogue {
  const PlannerCatalogue({
    required this.status,
    required this.destinations,
    this.message,
  });

  final String status;
  final List<PlannerDestination> destinations;
  final String? message;

  factory PlannerCatalogue.fromJson(Map<String, dynamic> json) =>
      PlannerCatalogue(
        status: json['status'] as String,
        message: json['message'] as String?,
        destinations: (json['destinations'] as List<dynamic>)
            .whereType<Map>()
            .map(
              (item) =>
                  PlannerDestination.fromJson(Map<String, dynamic>.from(item)),
            )
            .toList(growable: false),
      );
}

class PlannerDestination {
  const PlannerDestination({
    required this.id,
    required this.name,
    required this.region,
    required this.timeZone,
    required this.activities,
  });

  final String id;
  final String name;
  final String region;
  final String timeZone;
  final List<PlannerActivity> activities;

  factory PlannerDestination.fromJson(Map<String, dynamic> json) =>
      PlannerDestination(
        id: json['destinationId'] as String,
        name: json['name'] as String,
        region: json['region'] as String,
        timeZone: json['timeZone'] as String,
        activities: (json['activities'] as List<dynamic>)
            .whereType<Map>()
            .map(
              (item) =>
                  PlannerActivity.fromJson(Map<String, dynamic>.from(item)),
            )
            .toList(growable: false),
      );
}

class PlannerActivity {
  const PlannerActivity({required this.id, required this.name});
  final String id;
  final String name;
  factory PlannerActivity.fromJson(Map<String, dynamic> json) =>
      PlannerActivity(
        id: json['activityId'] as String,
        name: json['name'] as String,
      );
}

class PlannerHighlight {
  const PlannerHighlight({
    required this.id,
    required this.title,
    required this.destinationId,
    required this.destinationName,
    required this.activityId,
    required this.activityName,
    required this.startsAt,
    required this.endsAt,
    required this.startsAtLocal,
    required this.endsAtLocal,
    required this.timeZone,
    required this.availabilityStatus,
    required this.suitabilityStatus,
    required this.operationalStatus,
    this.evidenceCheckedAt,
    this.uncertaintyNotes = const [],
  });
  final String id;
  final String title;
  final String destinationId;
  final String destinationName;
  final String activityId;
  final String activityName;
  final String startsAt;
  final String endsAt;
  final String startsAtLocal;
  final String endsAtLocal;
  final String timeZone;
  final String availabilityStatus;
  final String suitabilityStatus;
  final String operationalStatus;
  final String? evidenceCheckedAt;
  final List<String> uncertaintyNotes;

  factory PlannerHighlight.fromJson(Map<String, dynamic> json) =>
      PlannerHighlight(
        id: json['id'] as String,
        title: json['title'] as String,
        destinationId: json['destinationId'] as String,
        destinationName: json['destinationName'] as String,
        activityId: json['activityId'] as String,
        activityName: json['activityName'] as String,
        startsAt: json['startsAt'] as String,
        endsAt: json['endsAt'] as String,
        startsAtLocal:
            json['startsAtLocal'] as String? ?? json['startsAt'] as String,
        endsAtLocal: json['endsAtLocal'] as String? ?? json['endsAt'] as String,
        timeZone: json['timeZone'] as String,
        availabilityStatus: json['availabilityStatus'] as String,
        suitabilityStatus: json['suitabilityStatus'] as String,
        operationalStatus: json['operationalStatus'] as String,
        evidenceCheckedAt: json['evidenceCheckedAt'] as String?,
        uncertaintyNotes: (json['uncertaintyNotes'] as List? ?? const [])
            .whereType<String>()
            .toList(growable: false),
      );
}

class PlannerItinerary {
  const PlannerItinerary({
    required this.id,
    required this.title,
    required this.status,
    required this.startsAt,
    required this.endsAt,
    required this.startsAtLocal,
    required this.endsAtLocal,
    required this.timeZone,
    required this.description,
    required this.concurrencyVersion,
    required this.items,
  });
  final String id;
  final String title;
  final String status;
  final String startsAt;
  final String endsAt;
  final String startsAtLocal;
  final String endsAtLocal;
  final String timeZone;
  final String? description;
  final int concurrencyVersion;
  final List<PlannerItineraryItem> items;

  factory PlannerItinerary.fromJson(Map<String, dynamic> json) =>
      PlannerItinerary(
        id: json['itineraryId'] as String,
        title: json['title'] as String,
        status: json['status'] as String,
        startsAt: json['startsAt'] as String,
        endsAt: json['endsAt'] as String,
        startsAtLocal:
            json['startsAtLocal'] as String? ?? json['startsAt'] as String,
        endsAtLocal: json['endsAtLocal'] as String? ?? json['endsAt'] as String,
        timeZone: json['timeZone'] as String? ?? 'Asia/Colombo',
        description: json['description'] as String?,
        concurrencyVersion: json['concurrencyVersion'] as int? ?? 0,
        items: (json['items'] as List? ?? const [])
            .whereType<Map>()
            .map(
              (item) => PlannerItineraryItem.fromJson(
                Map<String, dynamic>.from(item),
              ),
            )
            .toList(growable: false),
      );
}

class PlannerItineraryItem {
  const PlannerItineraryItem({
    required this.title,
    required this.destinationId,
    required this.activityId,
    required this.offeringId,
    required this.startsAt,
    required this.endsAt,
    required this.startsAtLocal,
    required this.endsAtLocal,
    required this.timeZone,
    required this.suitability,
    required this.availability,
    required this.operations,
  });
  final String title,
      destinationId,
      activityId,
      startsAt,
      endsAt,
      startsAtLocal,
      endsAtLocal,
      timeZone,
      suitability,
      availability,
      operations;
  final String? offeringId;
  factory PlannerItineraryItem.fromJson(Map<String, dynamic> json) =>
      PlannerItineraryItem(
        title: json['title'] as String,
        destinationId: json['destinationId'] as String,
        activityId: json['activityId'] as String,
        offeringId: json['offeringId'] as String?,
        startsAt: json['scheduledStart'] as String,
        endsAt: json['scheduledEnd'] as String,
        startsAtLocal:
            json['scheduledStartLocal'] as String? ??
            json['scheduledStart'] as String,
        endsAtLocal:
            json['scheduledEndLocal'] as String? ??
            json['scheduledEnd'] as String,
        timeZone: json['timeZone'] as String? ?? 'Asia/Colombo',
        suitability: json['lastSuitabilityStatus'] as String? ?? 'UNKNOWN',
        availability: json['lastAvailabilityStatus'] as String? ?? 'UNKNOWN',
        operations: json['lastOperationalStatus'] as String? ?? 'UNKNOWN',
      );
}

class PlannerRecommendationResult {
  const PlannerRecommendationResult({
    required this.recommendationId,
    required this.workflowId,
    required this.status,
    required this.generatedAt,
    required this.candidates,
    required this.excludedCandidatesCount,
    required this.uncertaintyNotes,
  });

  final String recommendationId;
  final String workflowId;
  final String status;
  final String generatedAt;
  final List<PlannerCandidate> candidates;
  final int excludedCandidatesCount;
  final List<String> uncertaintyNotes;

  factory PlannerRecommendationResult.fromJson(Map<String, dynamic> json) {
    return PlannerRecommendationResult(
      recommendationId: json['recommendationId'] as String,
      workflowId: json['workflowId'] as String,
      status: json['status'] as String,
      generatedAt: json['generatedAt'] as String,
      candidates: (json['candidates'] as List<dynamic>)
          .whereType<Map>()
          .map(
            (item) =>
                PlannerCandidate.fromJson(Map<String, dynamic>.from(item)),
          )
          .toList(growable: false),
      excludedCandidatesCount: json['excludedCandidatesCount'] as int,
      uncertaintyNotes: (json['uncertaintyNotes'] as List<dynamic>)
          .whereType<String>()
          .toList(growable: false),
    );
  }
}

class PlannerCandidate {
  const PlannerCandidate({
    required this.destinationId,
    required this.timeZone,
    required this.activityId,
    required this.offeringId,
    required this.title,
    required this.scheduledStart,
    required this.scheduledEnd,
    required this.availabilityStatus,
    required this.suitability,
    required this.operationalStatus,
    required this.biodiversityContext,
    required this.fitScore,
    required this.reasons,
  });

  final String destinationId;
  final String timeZone;
  final String activityId;
  final String? offeringId;
  final String title;
  final String scheduledStart;
  final String scheduledEnd;
  final String availabilityStatus;
  final PlannerSuitabilitySummary suitability;
  final String operationalStatus;
  final PlannerBiodiversityContext? biodiversityContext;
  final double fitScore;
  final List<String> reasons;

  factory PlannerCandidate.fromJson(Map<String, dynamic> json) {
    return PlannerCandidate(
      destinationId: json['destinationId'] as String,
      timeZone: json['timeZone'] as String? ?? 'Asia/Colombo',
      activityId: json['activityId'] as String,
      offeringId: json['offeringId'] as String?,
      title: json['title'] as String,
      scheduledStart: json['scheduledStart'] as String,
      scheduledEnd: json['scheduledEnd'] as String,
      availabilityStatus: json['availabilityStatus'] as String,
      suitability: PlannerSuitabilitySummary.fromJson(
        Map<String, dynamic>.from(json['suitability'] as Map),
      ),
      operationalStatus: json['operationalStatus'] as String,
      biodiversityContext: json['biodiversityContext'] == null
          ? null
          : PlannerBiodiversityContext.fromJson(
              Map<String, dynamic>.from(json['biodiversityContext'] as Map),
            ),
      fitScore: (json['fitScore'] as num).toDouble(),
      reasons: (json['reasons'] as List<dynamic>).whereType<String>().toList(
        growable: false,
      ),
    );
  }
}

class PlannerSuitabilitySummary {
  const PlannerSuitabilitySummary({
    required this.status,
    required this.marineConditionTime,
    required this.safetyProfileId,
  });

  final String status;
  final String? marineConditionTime;
  final String? safetyProfileId;

  factory PlannerSuitabilitySummary.fromJson(Map<String, dynamic> json) {
    return PlannerSuitabilitySummary(
      status: json['status'] as String,
      marineConditionTime: json['marineConditionTime'] as String?,
      safetyProfileId: json['safetyProfileId'] as String?,
    );
  }
}

class PlannerBiodiversityContext {
  const PlannerBiodiversityContext({
    required this.speciesName,
    required this.probability,
    required this.uncertainty,
    required this.predictionTimestamp,
  });

  final String speciesName;
  final double probability;
  final String uncertainty;
  final String predictionTimestamp;

  factory PlannerBiodiversityContext.fromJson(Map<String, dynamic> json) {
    return PlannerBiodiversityContext(
      speciesName: json['speciesName'] as String,
      probability: (json['probability'] as num).toDouble(),
      uncertainty: json['uncertainty'] as String,
      predictionTimestamp: json['predictionTimestamp'] as String,
    );
  }
}

class PlannerWorkflowStatus {
  const PlannerWorkflowStatus({
    required this.workflowId,
    required this.workflowType,
    required this.status,
    required this.initiatorUserId,
    required this.objective,
    required this.createdAt,
    required this.completedAt,
    required this.resultSummary,
    required this.failureReason,
  });

  final String workflowId;
  final String workflowType;
  final String status;
  final String? initiatorUserId;
  final String? objective;
  final String createdAt;
  final String? completedAt;
  final String? resultSummary;
  final String? failureReason;

  factory PlannerWorkflowStatus.fromJson(Map<String, dynamic> json) {
    return PlannerWorkflowStatus(
      workflowId: json['workflowId'] as String,
      workflowType: json['workflowType'] as String,
      status: json['status'] as String,
      initiatorUserId: json['initiatorUserId'] as String?,
      objective: json['objective'] as String?,
      createdAt: json['createdAt'] as String,
      completedAt: json['completedAt'] as String?,
      resultSummary: json['resultSummary'] as String?,
      failureReason: json['failureReason'] as String?,
    );
  }
}

class PlannerBiodiversityPrediction {
  const PlannerBiodiversityPrediction({
    required this.contractVersion,
    required this.destinationId,
    required this.activityId,
    required this.status,
    required this.message,
    required this.uncertaintyText,
    required this.provenance,
    required this.predictedSpecies,
    required this.modelMetadata,
    required this.limitations,
  });

  final int contractVersion;
  final String destinationId;
  final String? activityId;
  final String status;
  final String message;
  final String? uncertaintyText;
  final PlannerBiodiversityProvenance? provenance;
  final List<PlannerPredictedSpecies> predictedSpecies;
  final PlannerModelMetadata? modelMetadata;
  final String limitations;

  factory PlannerBiodiversityPrediction.fromJson(Map<String, dynamic> json) {
    const messages = {
      'available': 'Wildlife context is available. A prediction does not guarantee a sighting.',
      'not_requested': 'Wildlife context was not requested.',
      'unavailable': 'Wildlife predictions are temporarily unavailable. Please try again later.',
      'invalid': 'Wildlife predictions could not be verified for this location.',
      'stale': 'The wildlife prediction is out of date. Request a fresh result.',
    };
    final rawStatus = (json['status'] as String? ?? '').toLowerCase();
    final versionValue = json['contractVersion'];
    final version = versionValue is num ? versionValue.toInt() : -1;
    final rawMetadata = json['modelMetadata'];
    final rawProvenance = json['provenance'];
    final metadataVersion = rawMetadata is Map ? rawMetadata['modelVersion'] : null;
    final provenanceVersion = rawProvenance is Map ? rawProvenance['modelVersion'] : null;
    final metadataTimestamp = rawMetadata is Map ? rawMetadata['inferenceTimestamp'] : null;
    final provenanceTimestamp = rawProvenance is Map ? rawProvenance['inferenceTimestamp'] : null;
    final provider = rawProvenance is Map ? rawProvenance['provider'] : null;
    final availableHasProvenance = metadataVersion is String &&
        metadataVersion.isNotEmpty &&
        provenanceVersion == metadataVersion &&
        metadataTimestamp is String &&
        DateTime.tryParse(metadataTimestamp) != null &&
        provenanceTimestamp is String &&
        DateTime.tryParse(provenanceTimestamp) != null &&
        provider is String &&
        provider.isNotEmpty;
    final knownStatus = messages.containsKey(rawStatus) &&
        version == 1 &&
        (rawStatus != 'available' || availableHasProvenance);
    final status = knownStatus ? rawStatus : 'unavailable';
    return PlannerBiodiversityPrediction(
      contractVersion: 1,
      destinationId: json['destinationId'] as String,
      activityId: json['activityId'] as String?,
      status: status,
      message: status == 'available' && json['message'] is String
          ? json['message'] as String
          : messages[status]!,
      uncertaintyText: status == 'available' ? json['uncertaintyText'] as String? : null,
      provenance: !knownStatus || json['provenance'] == null || status != 'available'
          ? null
          : PlannerBiodiversityProvenance.fromJson(
              Map<String, dynamic>.from(json['provenance'] as Map),
            ),
      predictedSpecies: status == 'available'
          ? (json['predictedSpecies'] as List<dynamic>)
              .whereType<Map>()
              .map(
                (item) => PlannerPredictedSpecies.fromJson(
                  Map<String, dynamic>.from(item),
                ),
              )
              .toList(growable: false)
          : const [],
      modelMetadata: status != 'available' || json['modelMetadata'] == null
          ? null
          : PlannerModelMetadata.fromJson(
              Map<String, dynamic>.from(json['modelMetadata'] as Map),
            ),
      limitations: status == 'available' && json['limitations'] is String
          ? json['limitations'] as String
          : messages[status]!,
    );
  }
}

class PlannerPredictedSpecies {
  const PlannerPredictedSpecies({
    required this.speciesId,
    required this.scientificName,
    required this.commonName,
    required this.habitatSuitability,
    required this.confidenceLevel,
    this.uncertaintyText,
  });

  final String speciesId;
  final String scientificName;
  final String commonName;
  final double habitatSuitability;
  final String confidenceLevel;
  final String? uncertaintyText;

  factory PlannerPredictedSpecies.fromJson(Map<String, dynamic> json) {
    return PlannerPredictedSpecies(
      speciesId: json['speciesId'] as String,
      scientificName: json['scientificName'] as String,
      commonName: json['commonName'] as String,
      habitatSuitability: (json['habitatSuitability'] as num).toDouble(),
      confidenceLevel: json['confidenceLevel'] as String,
      uncertaintyText: json['uncertaintyText'] as String?,
    );
  }
}

class PlannerBiodiversityProvenance {
  const PlannerBiodiversityProvenance({
    required this.provider,
    required this.modelVersion,
    required this.inferenceTimestamp,
  });
  final String provider;
  final String? modelVersion;
  final String? inferenceTimestamp;
  factory PlannerBiodiversityProvenance.fromJson(Map<String, dynamic> json) =>
      PlannerBiodiversityProvenance(
        provider: json['provider'] as String,
        modelVersion: json['modelVersion'] as String?,
        inferenceTimestamp: json['inferenceTimestamp'] as String?,
      );
}

class PlannerModelMetadata {
  const PlannerModelMetadata({
    required this.modelVersion,
    required this.inferenceTimestamp,
  });

  final String? modelVersion;
  final String? inferenceTimestamp;

  factory PlannerModelMetadata.fromJson(Map<String, dynamic> json) {
    return PlannerModelMetadata(
      modelVersion: json['modelVersion'] as String?,
      inferenceTimestamp: json['inferenceTimestamp'] as String?,
    );
  }
}
