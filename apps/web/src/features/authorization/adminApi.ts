import { withLoadingScreen } from '../loading/backendLoading.ts'

export type AdminRole = {
  id: string
  name: string
  description: string
  isSystemRole: boolean
  createdAt: string
  permissions: string[]
}

export type AdminPermission = {
  id: string
  code: string
  description: string
  createdAt: string
}

export type AdminUser = {
  id: string
  email: string
  fullName: string
  isActive: boolean
  createdAt: string
  roles: string[]
  permissions: string[]
}

export class AdminApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'AdminApiError'
    this.status = status
  }
}

function requestOptions(method = 'GET', body?: unknown): RequestInit {
  return {
    method,
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  }
}

async function request<T>(send: () => Promise<Response>): Promise<T> {
  return withLoadingScreen(async () => {
    const response = await send()

    if (response.status === 204) return undefined as T

    const content = await response.text()
    let payload: unknown
    if (content.trim()) {
      try { payload = JSON.parse(content) as unknown } catch { payload = undefined }
    }
    if (!response.ok) {
      const detail = typeof payload === 'object' && payload !== null && 'detail' in payload && typeof payload.detail === 'string'
        ? payload.detail
        : response.status === 403
          ? 'Your current roles do not allow this action.'
          : 'We couldn’t complete that administration request. Please try again.'
      throw new AdminApiError(response.status, detail)
    }
    if (payload === undefined) throw new AdminApiError(response.status, 'The server returned a response that could not be read.')
    return payload as T
  })
}

export const getAdminPermissions = () => request<AdminPermission[]>(() => fetch('/api/auth/permissions', requestOptions()))
export const getAdminRoles = () => request<AdminRole[]>(() => fetch('/api/auth/roles', requestOptions()))
export const createAdminRole = (name: string, description: string) =>
  request<AdminRole>(() => fetch('/api/auth/roles', requestOptions('POST', { name, description })))
export const updateAdminRole = (id: string, name: string, description: string) =>
  request<AdminRole>(() => fetch(`/api/auth/roles/${encodeURIComponent(id)}`, requestOptions('PUT', { name, description })))
export const setRolePermissions = (id: string, permissionCodes: string[]) =>
  request<AdminRole>(() => fetch(`/api/auth/roles/${encodeURIComponent(id)}/permissions`, requestOptions('POST', { permissionCodes })))
export const deleteAdminRole = (id: string) => request<void>(() => fetch(`/api/auth/roles/${encodeURIComponent(id)}`, requestOptions('DELETE')))

export const getAdminUsers = () => request<AdminUser[]>(() => fetch('/api/auth/users', requestOptions()))
export const createAdminUser = (input: { email: string; password: string; fullName: string; roleNames: string[] }) =>
  request<AdminUser>(() => fetch('/api/auth/users', requestOptions('POST', input)))
export const updateAdminUser = (id: string, input: { email: string; fullName: string; isActive: boolean; newPassword?: string }) =>
  request<AdminUser>(() => fetch(`/api/auth/users/${encodeURIComponent(id)}`, requestOptions('PUT', input)))
export const setUserRoles = (id: string, roleNames: string[]) =>
  request<AdminUser>(() => fetch(`/api/auth/users/${encodeURIComponent(id)}/roles`, requestOptions('POST', { roleNames })))
export const deleteAdminUser = (id: string) => request<void>(() => fetch(`/api/auth/users/${encodeURIComponent(id)}`, requestOptions('DELETE')))
