import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import {
  activateAccount,
  AuthApiError,
  changePassword,
  deleteCurrentUser,
  getCurrentUser,
  getSessions,
  logoutCurrentDevice,
  refresh,
  register,
  removeAccountFromDevice,
  updateCurrentUser,
} from '../auth.ts'
import { closeWebTestServer, jsonResponse } from '../../../testSupport/reactTestHarness.js'

const originalFetch = globalThis.fetch

afterEach(() => {
  globalThis.fetch = originalFetch
})

after(async () => closeWebTestServer())

function captureFetch(response = jsonResponse({ accepted: true })) {
  let captured
  globalThis.fetch = async (input, init = {}) => {
    captured = { input, init }
    return response
  }
  return () => captured
}

test('WEB-AUTH-REQ-006 registration uses the public cookie endpoint and sends only its declared fields (ui-integration: auth-registration)', async () => {
  const getRequest = captureFetch(jsonResponse({ user: { id: 'new-user' } }))

  await register('  Coastal Visitor  ', 'visitor@example.test', 'synthetic-password', true)

  const { input, init } = getRequest()
  assert.equal(input, '/api/auth/register')
  assert.equal(init.method, 'POST')
  assert.equal(init.credentials, 'include')
  assert.deepEqual(init.headers, { 'Content-Type': 'application/json', Accept: 'application/json' })
  assert.deepEqual(JSON.parse(init.body), {
    fullName: '  Coastal Visitor  ',
    email: 'visitor@example.test',
    password: 'synthetic-password',
    rememberMe: true,
    useCookies: true,
  })
})

test('WEB-AUTH-REQ-007 registration hides account-existence detail from the browser (ui-integration: auth-registration)', async () => {
  globalThis.fetch = async () => jsonResponse({ title: 'Bad Request', detail: 'An account already exists for this email.' }, 400)

  await assert.rejects(() => register('Visitor', 'visitor@example.test', 'synthetic-password', false), (error) => {
    assert.ok(error instanceof AuthApiError)
    assert.equal(error.status, 400)
    assert.equal(error.message, 'We could not create an account with those details. If you already have an account, sign in instead.')
    assert.doesNotMatch(error.message, /already exists/i)
    return true
  })
})

test('WEB-AUTH-REQ-008 refresh omits an absent account id and keeps browser cookies in the request (ui-integration: auth-signin)', async () => {
  const getRequest = captureFetch(jsonResponse({ user: { id: 'restored-user' } }))

  await refresh()

  const { input, init } = getRequest()
  assert.equal(input, '/api/auth/refresh')
  assert.equal(init.method, 'POST')
  assert.equal(init.credentials, 'include')
  assert.deepEqual(JSON.parse(init.body), { useCookies: true })
})

test('WEB-AUTH-REQ-009 refresh selects only the requested browser account (ui-integration: auth-signin)', async () => {
  const getRequest = captureFetch(jsonResponse({ user: { id: 'account-b' } }))

  await refresh('account-b')

  const { input, init } = getRequest()
  assert.equal(input, '/api/auth/refresh')
  assert.deepEqual(JSON.parse(init.body), { useCookies: true, accountId: 'account-b' })
})

test('WEB-AUTH-REQ-010 account activation accepts an empty success and sends the selected account id (ui-integration: auth-signin)', async () => {
  const getRequest = captureFetch(jsonResponse(undefined, 204))

  await activateAccount('account-b')

  const { input, init } = getRequest()
  assert.equal(input, '/api/auth/refresh')
  assert.equal(init.method, 'POST')
  assert.equal(init.credentials, 'include')
  assert.deepEqual(JSON.parse(init.body), { useCookies: true, accountId: 'account-b' })
})

test('WEB-AUTH-REQ-011 account activation rejects a response for a different account (ui-integration: auth-signin)', async () => {
  captureFetch(jsonResponse({ user: { id: 'account-a' } }))

  await assert.rejects(() => activateAccount('account-b'), (error) => {
    assert.ok(error instanceof AuthApiError)
    assert.equal(error.status, 401)
    assert.equal(error.message, 'That account is no longer available in this browser.')
    return true
  })
})

test('WEB-AUTH-REQ-012 current-user lookup is a cookie-authenticated public GET (ui-integration: auth-profile-management)', async () => {
  const user = { id: 'user-1', email: 'visitor@example.test' }
  const getRequest = captureFetch(jsonResponse(user))

  assert.deepEqual(await getCurrentUser(), user)

  const { input, init } = getRequest()
  assert.equal(input, '/api/auth/me')
  assert.equal(init.method, undefined)
  assert.equal(init.credentials, 'include')
  assert.deepEqual(init.headers, { Accept: 'application/json' })
  assert.equal(init.body, undefined)
})

test('WEB-AUTH-REQ-013 profile update sends only the editable full name (ui-integration: auth-profile-management)', async () => {
  const user = { id: 'user-1', fullName: 'New Coastal Name' }
  const getRequest = captureFetch(jsonResponse(user))

  assert.deepEqual(await updateCurrentUser('New Coastal Name'), user)

  const { input, init } = getRequest()
  assert.equal(input, '/api/auth/me')
  assert.equal(init.method, 'PUT')
  assert.equal(init.credentials, 'include')
  assert.deepEqual(JSON.parse(init.body), { fullName: 'New Coastal Name' })
})

test('WEB-AUTH-REQ-014 session listing is a cookie-authenticated public GET (ui-integration: auth-profile-management)', async () => {
  const sessions = [{ id: 'session-1', isCurrent: true }]
  const getRequest = captureFetch(jsonResponse(sessions))

  assert.deepEqual(await getSessions(), sessions)

  const { input, init } = getRequest()
  assert.equal(input, '/api/auth/sessions')
  assert.equal(init.credentials, 'include')
  assert.deepEqual(init.headers, { Accept: 'application/json' })
})

test('WEB-AUTH-REQ-015 session revocation encodes the path identifier and defaults verification to empty (ui-integration: auth-profile-management)', async () => {
  const getRequest = captureFetch(jsonResponse(undefined, 204))

  await (await import('../auth.ts')).revokeSession('session/with space')

  const { input, init } = getRequest()
  assert.equal(input, '/api/auth/sessions/session%2Fwith%20space')
  assert.equal(init.method, 'DELETE')
  assert.equal(init.credentials, 'include')
  assert.deepEqual(JSON.parse(init.body), { currentPassword: '' })
})

test('WEB-AUTH-REQ-016 removing a device account sends its user id to the public logout route (ui-integration: auth-signin)', async () => {
  const getRequest = captureFetch(jsonResponse(undefined, 204))

  await removeAccountFromDevice('account-b')

  const { input, init } = getRequest()
  assert.equal(input, '/api/auth/logout-account')
  assert.equal(init.method, 'POST')
  assert.equal(init.credentials, 'include')
  assert.deepEqual(JSON.parse(init.body), { userId: 'account-b' })
})

test('WEB-AUTH-REQ-017 password changes send the current and new values only to the public password route (ui-integration: auth-profile-management)', async () => {
  const getRequest = captureFetch(jsonResponse(undefined, 204))

  await changePassword('old-synthetic-password', 'new-synthetic-password')

  const { input, init } = getRequest()
  assert.equal(input, '/api/auth/change-password')
  assert.equal(init.method, 'POST')
  assert.equal(init.credentials, 'include')
  assert.deepEqual(JSON.parse(init.body), {
    currentPassword: 'old-synthetic-password',
    newPassword: 'new-synthetic-password',
  })
})

test('WEB-AUTH-REQ-018 current-device logout sends no credential material in the request body (ui-integration: auth-signin)', async () => {
  const getRequest = captureFetch(jsonResponse(undefined, 204))

  await logoutCurrentDevice()

  const { input, init } = getRequest()
  assert.equal(input, '/api/auth/logout')
  assert.equal(init.method, 'POST')
  assert.equal(init.credentials, 'include')
  assert.equal(init.body, undefined)
})

test('WEB-AUTH-REQ-019 account deletion is an authenticated DELETE with no browser-supplied account body (ui-integration: auth-profile-management)', async () => {
  const getRequest = captureFetch(jsonResponse(undefined, 204))

  await deleteCurrentUser()

  const { input, init } = getRequest()
  assert.equal(input, '/api/auth/me')
  assert.equal(init.method, 'DELETE')
  assert.equal(init.credentials, 'include')
  assert.deepEqual(init.headers, { Accept: 'application/json' })
  assert.equal(init.body, undefined)
})

test('WEB-AUTH-REQ-020 successful empty JSON responses are rejected while intentional 204 responses remain valid (ui-integration: Auth API response contract)', async () => {
  globalThis.fetch = async () => jsonResponse(undefined, 200)
  await assert.rejects(() => getCurrentUser(), (error) => {
    assert.ok(error instanceof AuthApiError)
    assert.equal(error.status, 200)
    assert.equal(error.message, 'The server returned an empty response.')
    return true
  })

  globalThis.fetch = async () => jsonResponse(undefined, 204)
  await assert.doesNotReject(() => logoutCurrentDevice())
})

test('WEB-AUTH-REQ-021 malformed successful JSON is rejected with a safe message (ui-integration: Auth API response contract)', async () => {
  globalThis.fetch = async () => new Response('{not-json', { status: 200 })

  await assert.rejects(() => getCurrentUser(), (error) => {
    assert.ok(error instanceof AuthApiError)
    assert.equal(error.status, 200)
    assert.equal(error.message, 'The server returned a response that could not be read.')
    assert.doesNotMatch(error.message, /not-json/)
    return true
  })
})

test('WEB-AUTH-REQ-022 malformed error JSON uses a generic message without exposing the response payload (ui-integration: Auth API response contract)', async () => {
  globalThis.fetch = async () => new Response('{not-json', { status: 503 })

  await assert.rejects(() => getCurrentUser(), (error) => {
    assert.ok(error instanceof AuthApiError)
    assert.equal(error.status, 503)
    assert.equal(error.message, 'We couldn’t complete that just now. Please try again.')
    assert.doesNotMatch(error.message, /not-json/)
    return true
  })
})

test('WEB-AUTH-REQ-023 device capacity errors map to the stated five-account recovery path (ui-integration: auth-registration)', async () => {
  globalThis.fetch = async () => jsonResponse({ title: 'Device Account Limit Reached', status: 409 }, 409)

  await assert.rejects(() => register('Visitor', 'visitor@example.test', 'synthetic-password', false), (error) => {
    assert.ok(error instanceof AuthApiError)
    assert.equal(error.status, 409)
    assert.match(error.message, /maximum of five active accounts/)
    assert.match(error.message, /Sign out of an account on this browser/)
    return true
  })
})

test('WEB-AUTH-REQ-024 network failures remain retryable errors rather than being converted to success (ui-integration: Auth API dependency failure)', async () => {
  const failure = new TypeError('offline fixture')
  globalThis.fetch = async () => { throw failure }

  await assert.rejects(() => getCurrentUser(), (error) => error === failure)
})
