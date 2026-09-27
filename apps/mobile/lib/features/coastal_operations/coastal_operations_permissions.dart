abstract final class CoastalOperationsPermissions {
  static const assessmentCreate = 'operations.assessment.create';
  static const assessmentRead = 'operations.assessment.read';
  static const assessmentQueueRead = 'operations.assessment.queue.read';
  static const assessmentDecide = 'operations.assessment.decide';
  static const evidenceUpload = 'operations.evidence.upload';
  static const evidenceRead = 'operations.evidence.read';
  static const targetStatusRead = 'operations.target.status.read';
  static const targetHistoryRead = 'operations.target.history.read';
  static const alertRead = 'operations.alert.read';
  static const alertManage = 'operations.alert.manage';
  static const alertDecide = 'operations.alert.decide';

  static const all = {
    assessmentCreate,
    assessmentRead,
    assessmentQueueRead,
    assessmentDecide,
    evidenceUpload,
    evidenceRead,
    targetStatusRead,
    targetHistoryRead,
    alertRead,
    alertManage,
    alertDecide,
  };

  static bool hasAccess(Iterable<String> permissions) {
    final grants = permissions.map((permission) => permission.toLowerCase());
    return grants.any(all.contains);
  }
}
