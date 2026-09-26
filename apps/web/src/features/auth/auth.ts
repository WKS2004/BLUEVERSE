import {
  AUTH_LOADING_CONTEXTS,
  COASTAL_LOADING_CONTEXT,
  withLoadingScreen,
} from '../loading/backendLoading.ts'

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

  const responseText = await response.text()
  let payload: unknown
  if (responseText.trim()) {
    try {
      payload = JSON.parse(responseText) as unknown
    } catch {
      if (response.ok) {
        throw new AuthApiError(response.status, 'The server returned a response that could not be read.')
      }
    }
  }

  if (!response.ok) {
    const title = typeof payload === 'object' && payload !== null && !Array.isArray(payload) && 'title' in payload && typeof payload.title === 'string'
      ? payload.title
      : null
    const responseDetail = typeof payload === 'object' && payload !== null && !Array.isArray(payload) && 'detail' in payload && typeof payload.detail === 'string'
      ? payload.detail
      : null
    const detail = title === 'Device Account Limit Reached'
      ? 'This browser already has the maximum of five active accounts. Sign out of an account on this browser, then try again.'
      : responseDetail ?? 'We couldn’t complete that just now. Please try again.'
    throw new AuthApiError(response.status, detail)
  }

  if (payload === undefined) {
    throw new AuthApiError(response.status, 'The server returned an empty response.')
  }
  return payload as T
}

function request<T>(
  send: () => Promise<Response>,
  context = COASTAL_LOADING_CONTEXT,
): Promise<T> {
  return withLoadingScreen(async () => parseResponse<T>(await send()), context)
}

export async function login(email: string, password: string, rememberMe: boolean): Promise<AuthResponse> {
  return request<AuthResponse>(() => fetch('/api/auth/login', {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ email, password, rememberMe, useCookies: true }),
  }), AUTH_LOADING_CONTEXTS.signIn)
}

export async function register(fullName: string, email: string, password: string, rememberMe: boolean): Promise<AuthResponse> {
  try {
    return await request<AuthResponse>(() => fetch('/api/auth/register', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ fullName, email, password, rememberMe, useCookies: true }),
    }), AUTH_LOADING_CONTEXTS.registration)
  } catch (error) {
    if (error instanceof AuthApiError && error.status === 400 && /\balready exists\b/i.test(error.message)) {
      throw new AuthApiError(400, 'We could not create an account with those details. If you already have an account, sign in instead.')
    }
    throw error
  }
}

export async function refresh(accountId?: string): Promise<AuthResponse> {
  return request<AuthResponse>(() => fetch('/api/auth/refresh', {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ useCookies: true, ...(accountId ? { accountId } : {}) }),
  }), AUTH_LOADING_CONTEXTS.sessionRestore)
}

export async function activateAccount(accountId: string): Promise<void> {
  return withLoadingScreen(async () => {
    const response = await fetch('/api/auth/refresh', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ useCookies: true, accountId }),
    })
    if (response.status === 204) return
    const refreshed = await parseResponse<AuthResponse>(response)
    if (refreshed.user.id !== accountId) {
      throw new AuthApiError(401, 'That account is no longer available in this browser.')
    }
  }, AUTH_LOADING_CONTEXTS.switchAccount)
}

export async function getCurrentUser(): Promise<AuthUser> {
  return request<AuthUser>(() => fetch('/api/auth/me', {
    credentials: 'include',
    headers: { Accept: 'application/json' },
  }), AUTH_LOADING_CONTEXTS.sessionRestore)
}

export async function updateCurrentUser(fullName: string): Promise<AuthUser> {
  return request<AuthUser>(() => fetch('/api/auth/me', {
    method: 'PUT',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ fullName }),
  }), AUTH_LOADING_CONTEXTS.profile)
}

export async function getSessions(): Promise<AuthSession[]> {
  return request<AuthSession[]>(() => fetch('/api/auth/sessions', {
    credentials: 'include',
    headers: { Accept: 'application/json' },
  }), AUTH_LOADING_CONTEXTS.sessions)
}

export async function revokeSession(sessionId: string, currentPassword = ''): Promise<void> {
  return request<void>(() => fetch(`/api/auth/sessions/${encodeURIComponent(sessionId)}`, {
    method: 'DELETE',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ currentPassword }),
  }), AUTH_LOADING_CONTEXTS.endSession)
}

export async function removeAccountFromDevice(userId: string): Promise<void> {
  return request<void>(() => fetch('/api/auth/logout-account', {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ userId }),
  }), AUTH_LOADING_CONTEXTS.signOut)
}

export async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  return request<void>(() => fetch('/api/auth/change-password', {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ currentPassword, newPassword }),
  }), AUTH_LOADING_CONTEXTS.password)
}

export async function logoutCurrentDevice(): Promise<void> {
  return request<void>(() => fetch('/api/auth/logout', {
    method: 'POST',
    credentials: 'include',
  }), AUTH_LOADING_CONTEXTS.signOut)
}

export async function logoutAllDevices(currentPassword: string): Promise<void> {
  return request<void>(() => fetch('/api/auth/logout-all-devices', {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ currentPassword }),
  }), AUTH_LOADING_CONTEXTS.signOut)
}

export async function deleteCurrentUser(): Promise<void> {
  return request<void>(() => fetch('/api/auth/me', {
    method: 'DELETE',
    credentials: 'include',
    headers: { Accept: 'application/json' },
  }), AUTH_LOADING_CONTEXTS.deleteAccount)
}
