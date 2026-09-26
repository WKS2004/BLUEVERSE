import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { useState } from 'react'
import {
  cleanup,
  closeWebTestServer,
  createElement,
  jsonResponse,
  loadWebModule,
  makeAuthUser,
  renderWithSessionProvider,
  resetTestBrowser,
  screen,
  userEvent,
  waitFor,
} from '../../../testSupport/reactTestHarness.js'

const [{ AuthSessionProvider }, { useAuthSession }] = await Promise.all([
  loadWebModule('/src/components/providers/AuthSessionProvider.tsx'),
  loadWebModule('/src/features/auth/authSession.ts'),
])
const originalFetch = globalThis.fetch

afterEach(() => {
  cleanup()
  resetTestBrowser()
  globalThis.fetch = originalFetch
})

after(async () => closeWebTestServer())

function Probe() {
  const session = useAuthSession()
  const [actionError, setActionError] = useState('')
  const run = (action) => { void action().catch((error) => setActionError(error.message)) }
  return createElement('section', null,
    createElement('output', { 'data-testid': 'session-status' }, session.status),
    createElement('output', { 'data-testid': 'session-user' }, session.user?.id ?? 'none'),
    createElement('output', { 'data-testid': 'session-error' }, session.error ?? ''),
    createElement('output', { 'data-testid': 'switching-account' }, session.switchingAccountId ?? ''),
    createElement('output', { 'data-testid': 'action-error', role: 'alert' }, actionError),
    createElement('output', { 'data-testid': 'accounts' }, JSON.stringify(session.accounts)),
    createElement('button', { onClick: () => run(() => session.switchAccount('user-2'), 'switch') }, 'Switch to second account'),
    createElement('button', { onClick: () => run(() => session.removeAccount('user-1')) }, 'Remove first account'),
    createElement('button', { onClick: () => run(() => session.removeAccount('user-2')) }, 'Remove second account'),
    createElement('button', { onClick: () => run(() => session.signOut()) }, 'Sign out'),
    createElement('button', { onClick: () => run(() => session.signOutEverywhere('verified-synthetic-password')) }, 'Sign out everywhere'),
    createElement('button', { onClick: () => run(() => session.updateUser('Updated Name')) }, 'Update profile name'),
    createElement('button', { onClick: () => session.forgetAccount('user-1') }, 'Forget first account'),
  )
}

function storedAccounts(accounts, preferredId) {
  window.localStorage.setItem('blueverse.signed-in-accounts:v1', JSON.stringify(accounts))
  if (preferredId) window.localStorage.setItem('blueverse.active-account-id:v1', preferredId)
}

function storedAccount(user) { return { id: user.id, fullName: user.fullName, email: user.email } }

function installFetch(routes) {
  const calls = []
  const queues = Object.fromEntries(Object.entries(routes).map(([key, value]) => [key, Array.isArray(value) ? [...value] : [value]]))
  globalThis.fetch = async (input, init = {}) => {
    const method = init.method ?? 'GET'
    const key = `${method} ${input}`
    calls.push({ input, init, method, key })
    const queue = queues[key]
    if (!queue?.length) throw new Error(`Unexpected public API request: ${key}`)
    const result = queue.length > 1 ? queue.shift() : queue[0]
    return typeof result === 'function' ? result(calls.at(-1)) : result
  }
  return calls
}

function renderProvider() { return renderWithSessionProvider(createElement(Probe), AuthSessionProvider) }

async function waitForStatus(status) {
  await waitFor(() => assert.equal(screen.getByTestId('session-status').textContent, status))
}

test('WEB-AUTH-SESSION-001 no active cookie session ends in signed-out state after expected 401 recovery (ui-integration: auth-signin)', async () => {
  const calls = installFetch({
    'GET /api/auth/me': [jsonResponse({ title: 'Unauthorized' }, 401), jsonResponse({ title: 'Unauthorized' }, 401)],
    'POST /api/auth/refresh': jsonResponse({ title: 'Unauthorized' }, 401),
  })
  renderProvider()

  await waitForStatus('signed-out')
  assert.equal(screen.getByTestId('session-user').textContent, 'none')
  assert.equal(screen.getByTestId('session-error').textContent, '')
  assert.deepEqual(calls.map((call) => call.key), ['GET /api/auth/me', 'POST /api/auth/refresh'])
  assert.equal(window.localStorage.getItem('blueverse.signed-in-accounts:v1'), null)
})

test('WEB-AUTH-SESSION-002 legacy current-user session is upgraded into the browser account selector (ui-integration: auth-signin)', async () => {
  const user = makeAuthUser()
  const calls = installFetch({
    'GET /api/auth/me': jsonResponse(user),
    'POST /api/auth/refresh': jsonResponse(undefined, 204),
  })
  renderProvider()

  await waitForStatus('signed-in')
  assert.equal(screen.getByTestId('session-user').textContent, user.id)
  assert.deepEqual(JSON.parse(window.localStorage.getItem('blueverse.signed-in-accounts:v1')), [storedAccount(user)])
  assert.equal(window.localStorage.getItem('blueverse.active-account-id:v1'), user.id)
  assert.deepEqual(calls.map((call) => call.key), ['GET /api/auth/me', 'POST /api/auth/refresh'])
})

test('WEB-AUTH-SESSION-003 a remembered account is activated before its current user is restored (ui-integration: auth-signin)', async () => {
  const user = makeAuthUser()
  storedAccounts([storedAccount(user)], user.id)
  const calls = installFetch({
    'POST /api/auth/refresh': jsonResponse(undefined, 204),
    'GET /api/auth/me': jsonResponse(user),
  })
  renderProvider()

  await waitForStatus('signed-in')
  assert.equal(calls[0].key, 'POST /api/auth/refresh')
  assert.deepEqual(JSON.parse(calls[0].init.body), { useCookies: true, accountId: user.id })
  assert.equal(calls[1].key, 'GET /api/auth/me')
  assert.equal(screen.getByTestId('session-user').textContent, user.id)
})

test('WEB-AUTH-SESSION-004 expired remembered accounts are removed before trying the next saved account (ui-integration: auth-signin)', async () => {
  const expired = makeAuthUser({ id: 'expired-user', email: 'expired@example.test' })
  const active = makeAuthUser({ id: 'user-2', email: 'active@example.test', fullName: 'Active Visitor' })
  storedAccounts([storedAccount(expired), storedAccount(active)], expired.id)
  const calls = installFetch({
    'POST /api/auth/refresh': [jsonResponse({ title: 'Unauthorized' }, 401), jsonResponse(undefined, 204)],
    'GET /api/auth/me': jsonResponse(active),
  })
  renderProvider()

  await waitForStatus('signed-in')
  assert.equal(screen.getByTestId('session-user').textContent, active.id)
  assert.deepEqual(JSON.parse(window.localStorage.getItem('blueverse.signed-in-accounts:v1')), [storedAccount(active)])
  assert.equal(window.localStorage.getItem('blueverse.active-account-id:v1'), active.id)
  assert.deepEqual(calls.map((call) => call.key), ['POST /api/auth/refresh', 'POST /api/auth/refresh', 'GET /api/auth/me'])
})

test('WEB-AUTH-SESSION-005 service failure leaves the saved account available and marks identity unavailable (ui-integration: auth-signin)', async () => {
  const user = makeAuthUser()
  storedAccounts([storedAccount(user)], user.id)
  const calls = installFetch({ 'POST /api/auth/refresh': jsonResponse({ detail: 'Service is temporarily unavailable.' }, 503) })
  renderProvider()

  await waitForStatus('unavailable')
  assert.equal(screen.getByTestId('session-user').textContent, 'none')
  assert.equal(screen.getByTestId('session-error').textContent, 'Service is temporarily unavailable.')
  assert.deepEqual(JSON.parse(window.localStorage.getItem('blueverse.signed-in-accounts:v1')), [storedAccount(user)])
  assert.equal(calls.length, 1)
})

test('WEB-AUTH-SESSION-006 unauthorized current-user lookup refreshes cookies and retries once (ui-integration: auth-signin)', async () => {
  const user = makeAuthUser()
  storedAccounts([storedAccount(user)], user.id)
  const calls = installFetch({
    'POST /api/auth/refresh': [jsonResponse(undefined, 204), jsonResponse({ user })],
    'GET /api/auth/me': [jsonResponse({ title: 'Unauthorized' }, 401), jsonResponse(user)],
  })
  renderProvider()

  await waitForStatus('signed-in')
  assert.equal(screen.getByTestId('session-user').textContent, user.id)
  assert.deepEqual(calls.map((call) => call.key), [
    'POST /api/auth/refresh',
    'GET /api/auth/me',
    'POST /api/auth/refresh',
    'GET /api/auth/me',
  ])
})

test('WEB-AUTH-SESSION-007 account switch updates active identity and preference only after the selected session is verified (ui-integration: auth-signin)', async () => {
  const first = makeAuthUser()
  const second = makeAuthUser({ id: 'user-2', email: 'second@example.test', fullName: 'Second Visitor' })
  storedAccounts([storedAccount(first), storedAccount(second)], first.id)
  const calls = installFetch({
    'POST /api/auth/refresh': [jsonResponse(undefined, 204), jsonResponse(undefined, 204)],
    'GET /api/auth/me': [jsonResponse(first), jsonResponse(second)],
  })
  renderProvider()
  await waitForStatus('signed-in')

  await userEvent.setup().click(screen.getByRole('button', { name: 'Switch to second account' }))
  await waitFor(() => assert.equal(screen.getByTestId('session-user').textContent, second.id))
  assert.equal(window.localStorage.getItem('blueverse.active-account-id:v1'), second.id)
  assert.deepEqual(calls.map((call) => call.key), [
    'POST /api/auth/refresh', 'GET /api/auth/me',
    'POST /api/auth/refresh', 'GET /api/auth/me',
  ])
  assert.deepEqual(JSON.parse(calls[2].init.body), { useCookies: true, accountId: second.id })
})

test('WEB-AUTH-SESSION-008 account mismatch fails closed and restores the previously active account (ui-integration: auth-signin)', async () => {
  const first = makeAuthUser()
  const second = makeAuthUser({ id: 'user-2', fullName: 'Second Visitor' })
  storedAccounts([storedAccount(first), storedAccount(second)], first.id)
  const calls = installFetch({
    'POST /api/auth/refresh': [jsonResponse(undefined, 204), jsonResponse(undefined, 204), jsonResponse(undefined, 204)],
    'GET /api/auth/me': [jsonResponse(first), jsonResponse(first)],
  })
  renderProvider()
  await waitForStatus('signed-in')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Switch to second account' }))

  assert.equal((await screen.findByTestId('action-error')).textContent, 'That account is no longer available in this browser.')
  assert.equal(screen.getByTestId('session-user').textContent, first.id)
  assert.deepEqual(calls.map((call) => call.key), [
    'POST /api/auth/refresh', 'GET /api/auth/me',
    'POST /api/auth/refresh', 'GET /api/auth/me',
    'POST /api/auth/refresh',
  ])
})

test('WEB-AUTH-SESSION-009 removing the active account restores the next valid saved account (ui-integration: auth-signin)', async () => {
  const first = makeAuthUser()
  const second = makeAuthUser({ id: 'user-2', fullName: 'Second Visitor', email: 'second@example.test' })
  storedAccounts([storedAccount(first), storedAccount(second)], first.id)
  const calls = installFetch({
    'POST /api/auth/refresh': [jsonResponse(undefined, 204), jsonResponse(undefined, 204)],
    'GET /api/auth/me': [jsonResponse(first), jsonResponse(second)],
    'POST /api/auth/logout-account': jsonResponse(undefined, 204),
  })
  renderProvider()
  await waitForStatus('signed-in')

  await userEvent.setup().click(screen.getByRole('button', { name: 'Remove first account' }))
  await waitFor(() => assert.equal(screen.getByTestId('session-user').textContent, second.id))
  assert.equal(window.localStorage.getItem('blueverse.active-account-id:v1'), second.id)
  assert.deepEqual(JSON.parse(window.localStorage.getItem('blueverse.signed-in-accounts:v1')), [storedAccount(second)])
  const removal = calls.find((call) => call.key === 'POST /api/auth/logout-account')
  assert.deepEqual(JSON.parse(removal.init.body), { userId: first.id })
})

test('WEB-AUTH-SESSION-010 removing a non-active account leaves the active session and preference intact (ui-integration: auth-signin)', async () => {
  const first = makeAuthUser()
  const second = makeAuthUser({ id: 'user-2', fullName: 'Second Visitor' })
  storedAccounts([storedAccount(first), storedAccount(second)], first.id)
  const calls = installFetch({
    'POST /api/auth/refresh': jsonResponse(undefined, 204),
    'GET /api/auth/me': jsonResponse(first),
    'POST /api/auth/logout-account': jsonResponse(undefined, 204),
  })
  renderProvider()
  await waitForStatus('signed-in')

  await userEvent.setup().click(screen.getByRole('button', { name: 'Remove second account' }))
  await waitFor(() => assert.deepEqual(JSON.parse(screen.getByTestId('accounts').textContent), [storedAccount(first)]))
  assert.equal(screen.getByTestId('session-user').textContent, first.id)
  assert.equal(window.localStorage.getItem('blueverse.active-account-id:v1'), first.id)
  assert.equal(calls.filter((call) => call.key === 'GET /api/auth/me').length, 1)
})

test('WEB-AUTH-SESSION-011 sign-out removes the current account and moves to signed-out when no account remains (ui-integration: auth-signin)', async () => {
  const user = makeAuthUser()
  storedAccounts([storedAccount(user)], user.id)
  const calls = installFetch({
    'POST /api/auth/refresh': jsonResponse(undefined, 204),
    'GET /api/auth/me': jsonResponse(user),
    'POST /api/auth/logout-account': jsonResponse(undefined, 204),
  })
  renderProvider()
  await waitForStatus('signed-in')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Sign out' }))

  await waitForStatus('signed-out')
  assert.equal(screen.getByTestId('session-user').textContent, 'none')
  assert.deepEqual(JSON.parse(screen.getByTestId('accounts').textContent), [])
  assert.equal(window.localStorage.getItem('blueverse.active-account-id:v1'), null)
  assert.equal(calls.at(-1).key, 'POST /api/auth/logout-account')
})

test('WEB-AUTH-SESSION-012 sign-out everywhere verifies password and restores another account when available (ui-integration: auth-profile-management)', async () => {
  const first = makeAuthUser()
  const second = makeAuthUser({ id: 'user-2', fullName: 'Second Visitor' })
  storedAccounts([storedAccount(first), storedAccount(second)], first.id)
  const calls = installFetch({
    'POST /api/auth/refresh': [jsonResponse(undefined, 204), jsonResponse(undefined, 204)],
    'GET /api/auth/me': [jsonResponse(first), jsonResponse(second)],
    'POST /api/auth/logout-all-devices': jsonResponse(undefined, 204),
  })
  renderProvider()
  await waitForStatus('signed-in')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Sign out everywhere' }))

  await waitFor(() => assert.equal(screen.getByTestId('session-user').textContent, second.id))
  const logout = calls.find((call) => call.key === 'POST /api/auth/logout-all-devices')
  assert.deepEqual(JSON.parse(logout.init.body), { currentPassword: 'verified-synthetic-password' })
  assert.equal(window.localStorage.getItem('blueverse.active-account-id:v1'), second.id)
})

test('WEB-AUTH-SESSION-013 profile update refreshes both session state and the remembered account label (ui-integration: auth-profile-management)', async () => {
  const user = makeAuthUser()
  const updated = { ...user, fullName: 'Updated Name' }
  storedAccounts([storedAccount(user)], user.id)
  const calls = installFetch({
    'POST /api/auth/refresh': jsonResponse(undefined, 204),
    'GET /api/auth/me': jsonResponse(user),
    'PUT /api/auth/me': jsonResponse(updated),
  })
  renderProvider()
  await waitForStatus('signed-in')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Update profile name' }))

  await waitFor(() => assert.equal(screen.getByTestId('session-user').textContent, user.id))
  assert.deepEqual(JSON.parse(window.localStorage.getItem('blueverse.signed-in-accounts:v1')), [storedAccount(updated)])
  assert.equal(calls.at(-1).key, 'PUT /api/auth/me')
  assert.deepEqual(JSON.parse(calls.at(-1).init.body), { fullName: 'Updated Name' })
})

test('WEB-AUTH-SESSION-014 manually forgetting the active account clears its identity and selects the remaining account (ui-integration: auth-signin)', async () => {
  const first = makeAuthUser()
  const second = makeAuthUser({ id: 'user-2', fullName: 'Second Visitor' })
  storedAccounts([storedAccount(first), storedAccount(second)], first.id)
  installFetch({
    'POST /api/auth/refresh': jsonResponse(undefined, 204),
    'GET /api/auth/me': jsonResponse(first),
  })
  renderProvider()
  await waitForStatus('signed-in')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Forget first account' }))

  await waitForStatus('signed-out')
  assert.equal(screen.getByTestId('session-user').textContent, 'none')
  assert.equal(window.localStorage.getItem('blueverse.active-account-id:v1'), second.id)
  assert.deepEqual(JSON.parse(window.localStorage.getItem('blueverse.signed-in-accounts:v1')), [storedAccount(second)])
})

test('WEB-AUTH-SESSION-015 repeated switch requests are ignored while the chosen account activation is in progress (ui-integration: auth-signin)', async () => {
  const first = makeAuthUser()
  const second = makeAuthUser({ id: 'user-2', fullName: 'Second Visitor' })
  storedAccounts([storedAccount(first), storedAccount(second)], first.id)
  let finishActivation
  const pendingActivation = new Promise((resolve) => { finishActivation = resolve })
  const calls = installFetch({
    'POST /api/auth/refresh': [jsonResponse(undefined, 204), () => pendingActivation],
    'GET /api/auth/me': [jsonResponse(first), jsonResponse(second)],
  })
  renderProvider()
  await waitForStatus('signed-in')

  const interaction = userEvent.setup()
  await interaction.click(screen.getByRole('button', { name: 'Switch to second account' }))
  await waitFor(() => assert.equal(screen.getByTestId('switching-account').textContent, second.id))
  await interaction.click(screen.getByRole('button', { name: 'Switch to second account' }))
  assert.equal(calls.filter((call) => call.key === 'POST /api/auth/refresh').length, 2)
  finishActivation(jsonResponse(undefined, 204))
  await waitFor(() => assert.equal(screen.getByTestId('session-user').textContent, second.id))
})
