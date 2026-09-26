import type { AuthUser } from '../auth/auth'

export type PermissionSubject = Pick<AuthUser, 'permissions'>

const legacyAliases: Record<string, string> = {
  'auth.user.create': 'auth.user.manage',
  'auth.user.update': 'auth.user.manage',
  'auth.user.delete': 'auth.user.manage',
  'auth.role.create': 'auth.role.manage',
  'auth.role.update': 'auth.role.manage',
  'auth.role.delete': 'auth.role.manage',
}

export function hasAllPermissions(user: PermissionSubject | null, required: readonly string[]): boolean {
  if (!user) return false
  const granted = new Set(user.permissions.map((permission) => permission.toLowerCase()))
  return required.every((permission) => {
    const normalized = permission.toLowerCase()
    return granted.has(normalized) || Boolean(legacyAliases[normalized] && granted.has(legacyAliases[normalized]))
  })
}

export function hasAnyPermission(user: PermissionSubject | null, candidates: readonly string[]): boolean {
  return candidates.some((permission) => hasAllPermissions(user, [permission]))
}
