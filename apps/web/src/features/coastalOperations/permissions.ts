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
} as const

const allPermissionCodes = Object.values(coastalOperationsPermissions)

export function hasCoastalOperationsAccess(user: Pick<AuthUser, 'permissions'> | null): boolean {
  if (!user) return false
  const grants = new Set(user.permissions.map((permission) => permission.toLowerCase()))
  return allPermissionCodes.some((permission) => grants.has(permission))
}
