export type AuthUser = {
  id: string
  email: string
  fullName: string
  isActive: boolean
  createdAt: string
  roles: string[]
  permissions: string[]
}

export type AuthSession = {
  id: string
  deviceId: string
  createdAt: string
  lastSeenAt: string
  expiresAt: string
  rememberMe: boolean
  isCurrent: boolean
}

export type AuthResponse = {
  token: string | null
  expiresAt: string
  deviceId: string
  deviceKey: string | null
  refreshToken: string | null
  sessionExpiresAt: string
  rememberMe: boolean
  user: AuthUser
  roles: string[]
  permissions: string[]
}

export class AuthApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'AuthApiError'
    this.status = status
  }
}

async function parseResponse<T>(response: Response): Promise<T> {
  if (response.status === 204) {
    return undefined as T
  }

  const payload = (await response.json()) as Record<string, unknown>
  if (!response.ok) {
    const detail = typeof payload.detail === 'string' ? payload.detail : 'The request could not be completed.'
    throw new AuthApiError(response.status, detail)
  }

  return payload as T
}

export async function login(email: string, password: string, rememberMe: boolean): Promise<AuthResponse> {
  const response = await fetch('/api/auth/login', {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ email, password, rememberMe, useCookies: true }),
  })
  return parseResponse<AuthResponse>(response)
}

export async function refresh(): Promise<AuthResponse> {
  const response = await fetch('/api/auth/refresh', {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ useCookies: true }),
  })
  return parseResponse<AuthResponse>(response)
}

export async function getCurrentUser(): Promise<AuthUser> {
  const response = await fetch('/api/auth/me', {
    credentials: 'include',
    headers: { Accept: 'application/json' },
  })
  return parseResponse<AuthUser>(response)
}

export async function getSessions(): Promise<AuthSession[]> {
  const response = await fetch('/api/auth/sessions', {
    credentials: 'include',
    headers: { Accept: 'application/json' },
  })
  return parseResponse<AuthSession[]>(response)
}

export async function logoutCurrentDevice(): Promise<void> {
  const response = await fetch('/api/auth/logout', {
    method: 'POST',
    credentials: 'include',
  })
  return parseResponse<void>(response)
}

export async function logoutAllDevices(): Promise<void> {
  const response = await fetch('/api/auth/logout-all-devices', {
    method: 'POST',
    credentials: 'include',
  })
  return parseResponse<void>(response)
}
