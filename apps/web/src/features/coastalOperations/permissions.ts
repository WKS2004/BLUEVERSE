import type { AuthUser } from '../auth/auth'

export const coastalOperationsPermissions = {
  assessmentCreate: 'operations.assessment.create',
  assessmentRead: 'operations.assessment.read',
  assessmentQueueRead: 'operations.assessment.queue.read',
  assessmentUpdate: 'operations.assessment.update',
  assessmentDelete: 'operations.assessment.delete',
  assessmentSubmit: 'operations.assessment.submit',
  assessmentDecide: 'operations.assessment.decide',
  evidenceUpload: 'operations.evidence.upload',
  evidenceRead: 'operations.evidence.read',
  targetStatusRead: 'operations.target.status.read',
  targetHistoryRead: 'operations.target.history.read',
  alertRead: 'operations.alert.read',
  alertManage: 'operations.alert.manage',
  alertDecide: 'operations.alert.decide',
  alertCreate: 'operations.alert.create',
  alertUpdate: 'operations.alert.update',
  alertDelete: 'operations.alert.delete',
  alertPublish: 'operations.alert.publish',
  alertResolve: 'operations.alert.resolve',
  auditRead: 'operations.audit.read',
} as const

const allPermissionCodes = Object.values(coastalOperationsPermissions)

export function coastalNavigationLinks(user: Pick<AuthUser, 'permissions'> | null) {
  const grants = new Set(user?.permissions.map((permission) => permission.toLowerCase()))
  const logs = grants.has(coastalOperationsPermissions.auditRead) && (grants.has(coastalOperationsPermissions.assessmentRead) || grants.has(coastalOperationsPermissions.assessmentQueueRead) || grants.has(coastalOperationsPermissions.alertRead) || allPermissionCodes.some((code) => code.startsWith('operations.alert.') && grants.has(code)))
  return [
    ...(allPermissionCodes.some((code) => code.startsWith('operations.assessment.') && grants.has(code)) ? [{ label: 'Assessments', href: '/operations/assessments' }] : []),
    ...(allPermissionCodes.some((code) => code.startsWith('operations.alert.') && grants.has(code)) ? [{ label: 'Alerts', href: '/operations/alerts' }] : []),
    ...(logs ? [{ label: 'Logs', href: '/operations/logs' }] : []),
  ]
}

export function hasCoastalOperationsAccess(user: Pick<AuthUser, 'permissions'> | null): boolean {
  if (!user) return false
  const grants = new Set(user.permissions.map((permission) => permission.toLowerCase()))
  return allPermissionCodes.some((permission) => grants.has(permission))
}
