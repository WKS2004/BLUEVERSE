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
    this.title = '',
    this.timeZoneId,
    this.periodStartsLocal,
    this.periodEndsLocal,
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

  final String title;
  final String? timeZoneId;
  final String? periodStartsLocal;
  final String? periodEndsLocal;
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

  CoastalAssessment copyWith({int? version}) => CoastalAssessment(
    title: title,
    timeZoneId: timeZoneId,
    periodStartsLocal: periodStartsLocal,
    periodEndsLocal: periodEndsLocal,
    assessmentId: assessmentId,
    workflowId: workflowId,
    targetType: targetType,
    targetId: targetId,
    sourceWorkflowId: sourceWorkflowId,
    periodStartsAt: periodStartsAt,
    periodEndsAt: periodEndsAt,
    objective: objective,
    workflowStatus: workflowStatus,
    aiDependencyStatus: aiDependencyStatus,
    aiDispatchOutcome: aiDispatchOutcome,
    aiDispatchRetryable: aiDispatchRetryable,
    componentDependencies: componentDependencies,
    version: version ?? this.version,
    createdAt: createdAt,
    updatedAt: updatedAt,
  );

  factory CoastalAssessment.fromJson(JsonMap json) => CoastalAssessment(
    title: _nullableString(json, 'title') ?? _string(json, 'objective'),
    timeZoneId: _nullableString(json, 'timeZoneId'),
    periodStartsLocal: _nullableString(json, 'periodStartsLocal'),
    periodEndsLocal: _nullableString(json, 'periodEndsLocal'),
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
    this.updatedAt = '',
    this.createdAt = '',
    this.timeZoneId,
    this.validFromLocal,
    this.validUntilLocal,
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

  final String? timeZoneId;
  final String? validFromLocal;
  final String? validUntilLocal;
  final String createdAt;
  final String updatedAt;
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
    createdAt: _nullableString(json, 'createdAt') ?? '',
    updatedAt:
        _nullableString(json, 'updatedAt') ??
        _nullableString(json, 'createdAt') ??
        '',
    timeZoneId: _nullableString(json, 'timeZoneId'),
    validFromLocal: _nullableString(json, 'validFromLocal'),
    validUntilLocal: _nullableString(json, 'validUntilLocal'),
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

class CoastalAuditItem {
  const CoastalAuditItem({
    this.actorName,
    this.actorRoles = const [],
    this.summary,
    this.recordTitle,
    this.changes = const [],
    required this.auditId,
    required this.action,
    required this.actorId,
    required this.correlationId,
    required this.createdAt,
  });
  final String? actorName, summary, recordTitle;
  final List<String> actorRoles;
  final List<CoastalAuditChange> changes;
  final String auditId;
  final String action;
  final String actorId;
  final String correlationId;
  final String createdAt;
  factory CoastalAuditItem.fromJson(JsonMap json) => CoastalAuditItem(
    actorName: _nullableString(json, 'actorName'),
    summary: _nullableString(json, 'summary'),
    recordTitle: _nullableString(json, 'recordTitle'),
    actorRoles: json['actorRoles'] is List
        ? (json['actorRoles'] as List).whereType<String>().toList()
        : const [],
    changes: json['changes'] is List
        ? _maps(
            json['changes'],
            'activity changes',
          ).map(CoastalAuditChange.fromJson).toList()
        : const [],
    auditId: _string(json, 'auditId'),
    action: _string(json, 'action'),
    actorId: _string(json, 'actorId'),
    correlationId: _string(json, 'correlationId'),
    createdAt: _string(json, 'createdAt'),
  );
}

class CoastalAuditChange {
  const CoastalAuditChange({required this.field, this.before, this.after});
  final String field;
  final String? before, after;
  factory CoastalAuditChange.fromJson(JsonMap json) => CoastalAuditChange(
    field: _string(json, 'field'),
    before: _nullableString(json, 'before'),
    after: _nullableString(json, 'after'),
  );
}

class CoastalNamedReference {
  const CoastalNamedReference({
    required this.id,
    required this.title,
    this.targetType,
    this.targetId,
  });
  final String id;
  final String title;
  final String? targetType;
  final String? targetId;
  factory CoastalNamedReference.fromJson(JsonMap json) => CoastalNamedReference(
    id: _string(json, 'id'),
    title: _string(json, 'title'),
    targetType: _nullableString(json, 'targetType'),
    targetId: _nullableString(json, 'targetId'),
  );
}

class CoastalTimeZoneChoice {
  const CoastalTimeZoneChoice({
    this.currentOffsetMinutes,
    required this.id,
    required this.country,
    required this.location,
    required this.rulesAvailable,
  });
  final int? currentOffsetMinutes;
  final String id;
  final String country;
  final String location;
  final bool rulesAvailable;
  factory CoastalTimeZoneChoice.fromJson(JsonMap json) => CoastalTimeZoneChoice(
    currentOffsetMinutes: json['currentOffsetMinutes'] is int
        ? json['currentOffsetMinutes'] as int
        : null,
    id: _string(json, 'id'),
    country: _string(json, 'country'),
    location: _string(json, 'location'),
    rulesAvailable: _boolean(json, 'rulesAvailable'),
  );
}

class CoastalReferenceOptions {
  const CoastalReferenceOptions({required this.status, required this.items});
  final String status;
  final List<CoastalNamedReference> items;
  factory CoastalReferenceOptions.fromJson(JsonMap json) =>
      CoastalReferenceOptions(
        status: _string(json, 'status'),
        items: _maps(
          json['items'],
          'reference choices',
        ).map(CoastalNamedReference.fromJson).toList(),
      );
}

class CoastalFormOptions {
  const CoastalFormOptions({
    required this.timeZones,
    required this.targets,
    required this.plans,
    required this.assessments,
  });
  final List<CoastalTimeZoneChoice> timeZones;
  final CoastalReferenceOptions targets;
  final CoastalReferenceOptions plans;
  final List<CoastalNamedReference> assessments;
  factory CoastalFormOptions.fromJson(JsonMap json) => CoastalFormOptions(
    timeZones: _maps(
      json['timeZones'],
      'time zones',
    ).map(CoastalTimeZoneChoice.fromJson).toList(),
    targets: CoastalReferenceOptions.fromJson(_map(json['targets'], 'targets')),
    plans: CoastalReferenceOptions.fromJson(_map(json['plans'], 'plans')),
    assessments: _maps(
      json['assessments'],
      'assessment choices',
    ).map(CoastalNamedReference.fromJson).toList(),
  );
}
