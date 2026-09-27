typedef JsonMap = Map<String, dynamic>;

String _string(JsonMap json, String key) {
  final value = json[key];
  if (value is! String || value.isEmpty) {
    throw FormatException('The coastal operations response is missing $key.');
  }
  return value;
}

String? _nullableString(JsonMap json, String key) =>
    json[key] is String ? json[key] as String : null;

int _integer(JsonMap json, String key) =>
    json[key] is int ? json[key] as int : 0;

bool _boolean(JsonMap json, String key) =>
    json[key] is bool ? json[key] as bool : false;

JsonMap _map(dynamic value, String name) {
  if (value is Map<String, dynamic>) return value;
  throw FormatException(
    'The coastal operations response has an invalid $name.',
  );
}

List<JsonMap> _maps(dynamic value, String name) {
  if (value is! List) {
    throw FormatException(
      'The coastal operations response has an invalid $name.',
    );
  }
  return value.map((item) => _map(item, name)).toList(growable: false);
}

class CoastalAssessment {
  const CoastalAssessment({
    required this.assessmentId,
    required this.workflowId,
    required this.targetType,
    required this.targetId,
    required this.sourceWorkflowId,
    required this.periodStartsAt,
    required this.periodEndsAt,
    required this.objective,
    required this.workflowStatus,
    required this.aiDependencyStatus,
    required this.aiDispatchOutcome,
    required this.aiDispatchRetryable,
    required this.componentDependencies,
    required this.version,
    required this.createdAt,
    required this.updatedAt,
  });

  final String assessmentId;
  final String workflowId;
  final String targetType;
  final String targetId;
  final String? sourceWorkflowId;
  final String periodStartsAt;
  final String periodEndsAt;
  final String objective;
  final String workflowStatus;
  final String aiDependencyStatus;
  final String aiDispatchOutcome;
  final bool aiDispatchRetryable;
  final List<CoastalDependency> componentDependencies;
  final int version;
  final String createdAt;
  final String updatedAt;

  factory CoastalAssessment.fromJson(JsonMap json) => CoastalAssessment(
    assessmentId: _string(json, 'assessmentId'),
    workflowId: _string(json, 'workflowId'),
    targetType: _string(json, 'targetType'),
    targetId: _string(json, 'targetId'),
    sourceWorkflowId: _nullableString(json, 'sourceWorkflowId'),
    periodStartsAt: _string(json, 'periodStartsAt'),
    periodEndsAt: _string(json, 'periodEndsAt'),
    objective: _string(json, 'objective'),
    workflowStatus: _string(json, 'workflowStatus'),
    aiDependencyStatus: _string(json, 'aiDependencyStatus'),
    aiDispatchOutcome: _string(json, 'aiDispatchOutcome'),
    aiDispatchRetryable: _boolean(json, 'aiDispatchRetryable'),
    componentDependencies: _maps(
      json['componentDependencies'] ?? const [],
      'component dependencies',
    ).map(CoastalDependency.fromJson).toList(growable: false),
    version: _integer(json, 'version'),
    createdAt: _string(json, 'createdAt'),
    updatedAt: _string(json, 'updatedAt'),
  );
}

class CoastalDependency {
  const CoastalDependency({
    required this.service,
    required this.status,
    required this.retryable,
    required this.checkedAt,
  });

  final String service;
  final String status;
  final bool retryable;
  final String? checkedAt;

  factory CoastalDependency.fromJson(JsonMap json) => CoastalDependency(
    service: _string(json, 'service'),
    status: _string(json, 'status'),
    retryable: _boolean(json, 'retryable'),
    checkedAt: _nullableString(json, 'checkedAt'),
  );
}

class CoastalEvidence {
  const CoastalEvidence({
    required this.evidenceId,
    required this.assessmentVersion,
    required this.mediaType,
    required this.byteLength,
    required this.inspectionStatus,
    required this.uploadedAt,
    required this.expiresAt,
  });

  final String evidenceId;
  final int assessmentVersion;
  final String mediaType;
  final int byteLength;
  final String inspectionStatus;
  final String uploadedAt;
  final String expiresAt;

  factory CoastalEvidence.fromJson(JsonMap json) => CoastalEvidence(
    evidenceId: _string(json, 'evidenceId'),
    assessmentVersion: _integer(json, 'assessmentVersion'),
    mediaType: _string(json, 'mediaType'),
    byteLength: _integer(json, 'byteLength'),
    inspectionStatus: _string(json, 'inspectionStatus'),
    uploadedAt: _string(json, 'uploadedAt'),
    expiresAt: _string(json, 'expiresAt'),
  );
}

class CoastalDecision {
  const CoastalDecision({
    required this.decisionId,
    required this.decision,
    required this.workflowStatus,
    required this.decidedAt,
    required this.explanation,
  });

  final String decisionId;
  final String decision;
  final String workflowStatus;
  final String decidedAt;
  final String? explanation;

  factory CoastalDecision.fromJson(JsonMap json) => CoastalDecision(
    decisionId: _string(json, 'decisionId'),
    decision: _string(json, 'decision'),
    workflowStatus: _string(json, 'workflowStatus'),
    decidedAt: _string(json, 'decidedAt'),
    explanation: _nullableString(json, 'explanation'),
  );
}

class CoastalAssessmentDetail {
  const CoastalAssessmentDetail({
    required this.assessment,
    required this.decisions,
    required this.evidence,
  });

  final CoastalAssessment assessment;
  final List<CoastalDecision> decisions;
  final List<CoastalEvidence> evidence;

  factory CoastalAssessmentDetail.fromJson(JsonMap json) =>
      CoastalAssessmentDetail(
        assessment: CoastalAssessment.fromJson(
          _map(json['assessment'], 'assessment'),
        ),
        decisions: _maps(
          json['decisions'] ?? const [],
          'decisions',
        ).map(CoastalDecision.fromJson).toList(growable: false),
        evidence: _maps(
          json['evidence'] ?? const [],
          'evidence',
        ).map(CoastalEvidence.fromJson).toList(growable: false),
      );
}

class CoastalAlert {
  const CoastalAlert({
    required this.alertId,
    required this.targetType,
    required this.targetId,
    required this.assessmentId,
    required this.title,
    required this.description,
    required this.severity,
    required this.visibility,
    required this.lifecycle,
    required this.validFrom,
    required this.validUntil,
    required this.version,
  });

  final String alertId;
  final String targetType;
  final String targetId;
  final String? assessmentId;
  final String title;
  final String description;
  final String severity;
  final String visibility;
  final String lifecycle;
  final String validFrom;
  final String validUntil;
  final int version;

  factory CoastalAlert.fromJson(JsonMap json) => CoastalAlert(
    alertId: _string(json, 'alertId'),
    targetType: _string(json, 'targetType'),
    targetId: _string(json, 'targetId'),
    assessmentId: _nullableString(json, 'assessmentId'),
    title: _string(json, 'title'),
    description: _string(json, 'description'),
    severity: _string(json, 'severity'),
    visibility: _string(json, 'visibility'),
    lifecycle: _string(json, 'lifecycle'),
    validFrom: _string(json, 'validFrom'),
    validUntil: _string(json, 'validUntil'),
    version: _integer(json, 'version'),
  );
}

class CoastalPage<T> {
  const CoastalPage({required this.items, required this.nextCursor});

  final List<T> items;
  final String? nextCursor;

  factory CoastalPage.fromJson(JsonMap json, T Function(JsonMap) parseItem) =>
      CoastalPage(
        items: _maps(
          json['items'],
          'items',
        ).map(parseItem).toList(growable: false),
        nextCursor: _nullableString(json, 'nextCursor'),
      );
}

class CoastalOperationalStatus {
  const CoastalOperationalStatus({
    required this.targetType,
    required this.targetId,
    required this.operationalState,
    required this.stateVersion,
    required this.updatedAt,
  });

  final String targetType;
  final String targetId;
  final String operationalState;
  final int stateVersion;
  final String updatedAt;

  factory CoastalOperationalStatus.fromJson(JsonMap json) =>
      CoastalOperationalStatus(
        targetType: _string(json, 'targetType'),
        targetId: _string(json, 'targetId'),
        operationalState: _string(json, 'operationalState'),
        stateVersion: _integer(json, 'stateVersion'),
        updatedAt: _string(json, 'updatedAt'),
      );
}

class CoastalHistoryItem {
  const CoastalHistoryItem({
    required this.historyId,
    required this.previousState,
    required this.newState,
    required this.createdAt,
  });

  final String historyId;
  final String previousState;
  final String newState;
  final String createdAt;

  factory CoastalHistoryItem.fromJson(JsonMap json) => CoastalHistoryItem(
    historyId: _string(json, 'historyId'),
    previousState: _string(json, 'previousState'),
    newState: _string(json, 'newState'),
    createdAt: _string(json, 'createdAt'),
  );
}
