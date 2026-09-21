import assert from 'node:assert/strict'
import { afterEach, test } from 'node:test'

import {
  AuthApiError,
  getCurrentUser,
  login,
  logoutAllDevices,
} from './auth.ts'

const originalFetch = globalThis.fetch

afterEach(() => {
  globalThis.fetch = originalFetch
})

function response(body, status = 200) {
  return new globalThis.Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
  })
}

test('WEB-AUTH-001 login sends the public cookie transport and remember-me contract', async () => {
  const expected = {
    token: null,
    expiresAt: '2026-09-22T00:00:00Z',
    deviceId: 'server-device-id',
    deviceKey: 'server-device-key',
    refreshToken: null,
    sessionExpiresAt: '2026-10-21T00:00:00Z',
    rememberMe: true,
    user: {
      id: 'user-1',
      email: 'user@example.com',
      fullName: 'Example User',
      isActive: true,
      createdAt: '2026-01-01T00:00:00Z',
      roles: ['User'],
      permissions: ['profile.read'],
    },
    roles: ['User'],
    permissions: ['profile.read'],
  }
  let request

  globalThis.fetch = async (input, init) => {
    request = { input, init }
    return response(expected)
  }

  const actual = await login('user@example.com', 'correct-password', true)

  assert.deepEqual(actual, expected)
  assert.equal(request.input, '/api/auth/login')
  assert.equal(request.init.method, 'POST')
  assert.equal(request.init.credentials, 'include')
  assert.deepEqual(request.init.headers, {
    'Content-Type': 'application/json',
    Accept: 'application/json',
  })
  assert.deepEqual(JSON.parse(request.init.body), {
    email: 'user@example.com',
    password: 'correct-password',
    rememberMe: true,
    useCookies: true,
  })
})

test('WEB-AUTH-002 unauthorized RFC 7807 responses retain status and detail', async () => {
  globalThis.fetch = async () => response({
    type: 'https://tools.ietf.org/html/rfc7807',
    title: 'Unauthorized',
    status: 401,
    detail: 'Invalid credentials.',
  }, 401)

  await assert.rejects(
    () => getCurrentUser(),
    (error) => {
      assert.ok(error instanceof AuthApiError)
      assert.equal(error.status, 401)
      assert.equal(error.message, 'Invalid credentials.')
      return true
    },
  )
})

test('WEB-AUTH-003 logout everywhere uses the public route and accepts 204', async () => {
  let request
  globalThis.fetch = async (input, init) => {
    request = { input, init }
    return response(undefined, 204)
  }

  await logoutAllDevices()

  assert.equal(request.input, '/api/auth/logout-all-devices')
  assert.equal(request.init.method, 'POST')
  assert.equal(request.init.credentials, 'include')
})
