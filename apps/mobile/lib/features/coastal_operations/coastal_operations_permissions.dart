abstract final class CoastalOperationsPermissions {
  static const assessmentCreate = 'operations.assessment.create';
  static const assessmentRead = 'operations.assessment.read';
  static const assessmentQueueRead = 'operations.assessment.queue.read';
  static const assessmentUpdate = 'operations.assessment.update';
  static const assessmentDelete = 'operations.assessment.delete';
  static const assessmentSubmit = 'operations.assessment.submit';
  static const assessmentDecide = 'operations.assessment.decide';
  static const evidenceUpload = 'operations.evidence.upload';
  static const evidenceRead = 'operations.evidence.read';
  static const targetStatusRead = 'operations.target.status.read';
  static const targetHistoryRead = 'operations.target.history.read';
  static const alertRead = 'operations.alert.read';
  static const alertManage = 'operations.alert.manage';
  static const alertDecide = 'operations.alert.decide';
  static const alertCreate = 'operations.alert.create';
  static const alertUpdate = 'operations.alert.update';
  static const alertDelete = 'operations.alert.delete';
  static const alertPublish = 'operations.alert.publish';
  static const alertResolve = 'operations.alert.resolve';
  static const auditRead = 'operations.audit.read';

  static const all = {
    assessmentCreate,
    assessmentRead,
    assessmentQueueRead,
    assessmentUpdate,
    assessmentDelete,
    assessmentSubmit,
    assessmentDecide,
    evidenceUpload,
    evidenceRead,
    targetStatusRead,
    targetHistoryRead,
    alertRead,
    alertManage,
    alertDecide,
    alertCreate,
    alertUpdate,
    alertDelete,
    alertPublish,
    alertResolve,
    auditRead,
  };

  static bool hasAccess(Iterable<String> permissions) {
    final grants = permissions.map((permission) => permission.toLowerCase());
    return grants.any(all.contains);
  }

  static bool hasAssessmentAccess(Iterable<String> permissions) =>
      permissions.any(
        (p) =>
            p.toLowerCase().startsWith('operations.assessment.') &&
            all.contains(p.toLowerCase()),
      );
  static bool hasAlertAccess(Iterable<String> permissions) => permissions.any(
    (p) =>
        p.toLowerCase().startsWith('operations.alert.') &&
        all.contains(p.toLowerCase()),
  );
  static bool canManageAlerts(Iterable<String> permissions) => permissions.any(
    (p) => {
      alertManage,
      alertDecide,
      alertCreate,
      alertUpdate,
      alertDelete,
      alertPublish,
      alertResolve,
    }.contains(p.toLowerCase()),
  );
  static bool canReadLogs(Iterable<String> permissions) {
    final grants = permissions.map((p) => p.toLowerCase()).toSet();
    return grants.contains(auditRead) &&
        (grants.contains(assessmentRead) ||
            grants.contains(assessmentQueueRead) ||
            grants.contains(alertRead) ||
            canManageAlerts(grants));
  }
}
