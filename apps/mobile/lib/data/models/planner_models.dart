/// DTOs for the Smart Coastal Planner public API. Shapes mirror
/// services/coastal-planner/Models/Dtos/PlannerDtos.cs so the mobile
/// client stays in lockstep with the shared contract.
library;

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
          .map((item) => PlannerCandidate.fromJson(Map<String, dynamic>.from(item)))
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
              Map<String, dynamic>.from(
                json['biodiversityContext'] as Map,
              ),
            ),
      fitScore: (json['fitScore'] as num).toDouble(),
      reasons: (json['reasons'] as List<dynamic>)
          .whereType<String>()
          .toList(growable: false),
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
    required this.destinationId,
    required this.activityId,
    required this.status,
    required this.predictedSpecies,
    required this.modelMetadata,
    required this.limitations,
  });

  final String destinationId;
  final String? activityId;
  final String status;
  final List<PlannerPredictedSpecies> predictedSpecies;
  final PlannerModelMetadata? modelMetadata;
  final String limitations;

  factory PlannerBiodiversityPrediction.fromJson(Map<String, dynamic> json) {
    return PlannerBiodiversityPrediction(
      destinationId: json['destinationId'] as String,
      activityId: json['activityId'] as String?,
      status: json['status'] as String,
      predictedSpecies: (json['predictedSpecies'] as List<dynamic>)
          .whereType<Map>()
          .map(
            (item) =>
                PlannerPredictedSpecies.fromJson(Map<String, dynamic>.from(item)),
          )
          .toList(growable: false),
      modelMetadata: json['modelMetadata'] == null
          ? null
          : PlannerModelMetadata.fromJson(
              Map<String, dynamic>.from(json['modelMetadata'] as Map),
            ),
      limitations: json['limitations'] as String,
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
  });

  final String speciesId;
  final String scientificName;
  final String commonName;
  final double habitatSuitability;
  final String confidenceLevel;

  factory PlannerPredictedSpecies.fromJson(Map<String, dynamic> json) {
    return PlannerPredictedSpecies(
      speciesId: json['speciesId'] as String,
      scientificName: json['scientificName'] as String,
      commonName: json['commonName'] as String,
      habitatSuitability: (json['habitatSuitability'] as num).toDouble(),
      confidenceLevel: json['confidenceLevel'] as String,
    );
  }
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
