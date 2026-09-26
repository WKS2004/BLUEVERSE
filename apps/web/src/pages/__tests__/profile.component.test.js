import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import {
  cleanup,
  closeWebTestServer,
  createElement,
  fireEvent,
  jsonResponse,
  loadWebModule,
  makeAuthSessionValue,
  makeAuthUser,
  renderInApp,
  resetTestBrowser,
  screen,
  userEvent,
  waitFor,
} from '../../testSupport/reactTestHarness.js'

const [{ default: ProfilePage }, { AuthApiError }] = await Promise.all([
  loadWebModule('/src/pages/ProfilePage.tsx'),
  loadWebModule('/src/features/auth/auth.ts'),
])
const originalFetch = globalThis.fetch

afterEach(() => {
  cleanup()
  resetTestBrowser()
  globalThis.fetch = originalFetch
})

after(async () => closeWebTestServer())

function session(id, isCurrent = false, rememberMe = false) {
  return {
    id,
    deviceId: `device-${id}`,
    createdAt: '2026-09-01T00:00:00.000Z',
    lastSeenAt: '2026-09-24T12:00:00.000Z',
    expiresAt: '2026-10-24T12:00:00.000Z',
    rememberMe,
    isCurrent,
  }
}

function installFetch(routes) {
  const calls = []
  globalThis.fetch = async (input, init = {}) => {
    const method = init.method ?? 'GET'
    const call = { input, init, method }
    calls.push(call)
    const response = routes[`${method} ${input}`]
    if (response instanceof Function) return response(call)
    if (!response) throw new Error(`Unexpected public API request: ${method} ${input}`)
    return response
  }
  return calls
}

function signedInProfile({ user = makeAuthUser(), auth = {}, sessions = [] } = {}) {
  const fetchRoutes = { 'GET /api/auth/sessions': jsonResponse(sessions) }
  const calls = installFetch(fetchRoutes)
  renderInApp(createElement(ProfilePage), {
    path: '/profile',
    auth: makeAuthSessionValue({ user, status: 'signed-in', ...auth }),
  })
  return calls
}

test('WEB-UI-PROFILE-001 signed-out profile links back to the profile and explains when authentication is unavailable (ui-integration: auth-profile-management)', () => {
  renderInApp(createElement(ProfilePage), { path: '/profile?tab=security', auth: makeAuthSessionValue({ status: 'unavailable' }) })

  assert.ok(screen.getByRole('heading', { name: 'Sign in to view your profile.' }))
  assert.ok(screen.getByText('We couldn’t load your profile just now. Sign in again or try again in a little while.'))
  const profileSection = screen.getByRole('heading', { name: 'Sign in to view your profile.' }).closest('section')
  assert.equal(profileSection.querySelector('a').getAttribute('href'), '/signin?returnTo=%2Fprofile')
  const headerLinks = screen.getAllByRole('link', { name: 'Sign in' }).filter((link) => !profileSection.contains(link))
  assert.ok(headerLinks.every((link) => link.getAttribute('href') === '/signin?returnTo=%2Fprofile%3Ftab%3Dsecurity'))
})

test('WEB-UI-PROFILE-002 profile shows server-owned identity, roles and editable name only (ui-integration: auth-profile-management)', async () => {
  const user = makeAuthUser({ fullName: 'Coastal Visitor', roles: ['Visitor', 'Field Reviewer'] })
  const calls = signedInProfile({ user })

  assert.ok(screen.getByRole('heading', { name: 'Coastal Visitor' }))
  assert.equal(document.getElementById('profile-full-name').defaultValue, 'Coastal Visitor')
  assert.equal(document.getElementById('profile-email').value, user.email)
  assert.equal(document.getElementById('profile-email').readOnly, true)
  assert.deepEqual(Array.from(screen.getByRole('list', { name: 'Your assigned roles' }).querySelectorAll('li')).map((item) => item.textContent), user.roles)
  await screen.findByText('No active login sessions to show.')
  assert.deepEqual(calls.map(({ method, input }) => `${method} ${input}`), ['GET /api/auth/sessions'])
})

test('WEB-UI-PROFILE-003 profile updates trim the name and report the completed change (ui-integration: auth-profile-management)', async () => {
  const user = makeAuthUser()
  const calls = []
  signedInProfile({ user, auth: { updateUser: async (fullName) => { calls.push(fullName); return { ...user, fullName } } } })
  await screen.findByText('No active login sessions to show.')

  await userEvent.setup().clear(document.getElementById('profile-full-name'))
  await userEvent.setup().type(document.getElementById('profile-full-name'), '  Updated Coastal Name  ')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Save changes' }))

  assert.equal((await screen.findByText('Your profile has been updated.')).textContent, 'Your profile has been updated.')
  assert.deepEqual(calls, ['Updated Coastal Name'])
})

test('WEB-UI-PROFILE-004 profile rejects empty and over-100-character names before calling the update action (ui-integration: auth-profile-management)', async () => {
  const user = makeAuthUser()
  let calls = 0
  signedInProfile({ user, auth: { updateUser: async () => { calls += 1; return user } } })
  await screen.findByText('No active login sessions to show.')
  const form = document.getElementById('profile-full-name').closest('form')

  document.getElementById('profile-full-name').value = ' '.repeat(101)
  fireEvent.submit(form)
  assert.equal((await screen.findByRole('alert')).textContent, 'Enter a name between 1 and 100 characters.')
  assert.equal(calls, 0)

  cleanup()
  installFetch({ 'GET /api/auth/sessions': jsonResponse([]) })
  renderInApp(createElement(ProfilePage), { path: '/profile', auth: makeAuthSessionValue({ user, status: 'signed-in', updateUser: async () => { calls += 1; return user } }) })
  await screen.findByText('No active login sessions to show.')
  document.getElementById('profile-full-name').value = ''
  fireEvent.submit(document.getElementById('profile-full-name').closest('form'))
  assert.equal(calls, 0)
  assert.equal((await screen.findByRole('alert')).textContent, 'Enter a name between 1 and 100 characters.')
})

test('WEB-UI-PROFILE-005 profile update dependency errors are safe and leave the form retryable (ui-integration: auth-profile-management)', async () => {
  const user = makeAuthUser()
  signedInProfile({ user, auth: { updateUser: async () => { throw new AuthApiError(503, 'private server message') } } })
  await screen.findByText('No active login sessions to show.')

  await userEvent.setup().click(screen.getByRole('button', { name: 'Save changes' }))

  assert.equal((await screen.findByRole('alert')).textContent, 'We couldn’t save your changes just now. Please try again.')
  assert.equal(screen.getByRole('button', { name: 'Save changes' }).disabled, false)
  assert.doesNotMatch(document.body.textContent, /private server message/)
})

test('WEB-UI-PROFILE-006 session-load failure shows a calm message and does not expose backend detail (ui-integration: auth-profile-management)', async () => {
  installFetch({ 'GET /api/auth/sessions': jsonResponse({ detail: 'database host unavailable' }, 503) })
  renderInApp(createElement(ProfilePage), { path: '/profile', auth: makeAuthSessionValue({ user: makeAuthUser(), status: 'signed-in' }) })

  assert.ok(await screen.findByText('Your signed-in devices could not be loaded. Try again in a moment.'))
  assert.doesNotMatch(document.body.textContent, /database host unavailable/)
})

test('WEB-UI-PROFILE-007 five active sessions show the server-owned capacity guidance (ui-integration: auth-profile-management)', async () => {
  signedInProfile({ sessions: Array.from({ length: 5 }, (_, index) => session(`session-${index}`, index === 0, true)) })

  await screen.findByText('This account has reached its active-session limit. End an older session before signing in on another device.')
  assert.equal(document.querySelectorAll('#sessions li').length, 5)
  assert.ok(screen.getByText('5 active'))
})

test('WEB-UI-PROFILE-008 revoking another device requires password verification and updates the list on success (ui-integration: auth-profile-management)', async () => {
  const sessions = [session('session-current', true), session('session-remote')]
  const calls = installFetch({
    'GET /api/auth/sessions': jsonResponse(sessions),
    'DELETE /api/auth/sessions/session-remote': jsonResponse(undefined, 204),
  })
  renderInApp(createElement(ProfilePage), { path: '/profile', auth: makeAuthSessionValue({ user: makeAuthUser(), status: 'signed-in' }) })
  await screen.findByText('Signed-in device')

  await userEvent.setup().click(screen.getByRole('button', { name: 'End session' }))
  assert.ok(screen.getByRole('dialog', { name: 'End this device session?' }))
  assert.equal(calls.length, 1)
  await userEvent.setup().type(document.getElementById('verify-current-password'), 'verified-synthetic-password')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Verify and continue' }))

  await waitFor(() => assert.equal(document.querySelectorAll('#sessions li').length, 1))
  const revokeCall = calls.find((call) => call.method === 'DELETE')
  assert.equal(revokeCall.input, '/api/auth/sessions/session-remote')
  assert.deepEqual(JSON.parse(revokeCall.init.body), { currentPassword: 'verified-synthetic-password' })
  assert.equal(screen.queryByRole('dialog'), null)
})

test('WEB-UI-PROFILE-009 cancellation leaves a remote session unchanged and sends no revocation (ui-integration: auth-profile-management)', async () => {
  const sessions = [session('session-current', true), session('session-remote')]
  const calls = installFetch({ 'GET /api/auth/sessions': jsonResponse(sessions) })
  renderInApp(createElement(ProfilePage), { path: '/profile', auth: makeAuthSessionValue({ user: makeAuthUser(), status: 'signed-in' }) })
  await screen.findByText('Signed-in device')
  await userEvent.setup().click(screen.getByRole('button', { name: 'End session' }))
  await userEvent.setup().click(screen.getByRole('button', { name: 'Cancel' }))

  assert.equal(screen.queryByRole('dialog'), null)
  assert.equal(document.querySelectorAll('#sessions li').length, 2)
  assert.equal(calls.length, 1)
})

test('WEB-UI-PROFILE-010 current-session sign-out uses the account-removal action without password prompting (ui-integration: auth-profile-management)', async () => {
  const user = makeAuthUser()
  const calls = []
  installFetch({
    'GET /api/auth/sessions': jsonResponse([session('session-current', true)]),
    'POST /api/auth/logout-account': (call) => { calls.push(call); return jsonResponse(undefined, 204) },
  })
  renderInApp(createElement(ProfilePage), { path: '/profile', auth: makeAuthSessionValue({ user, status: 'signed-in', removeAccount: async (id) => calls.push(id) }) })
  await screen.findByText('This browser')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Sign out here' }))

  await waitFor(() => assert.equal(calls.length, 1))
  assert.equal(calls[0], user.id)
  assert.match(window.sessionStorage.getItem('blueverse.pending-auth-transition'), /^signOut\|\d+$/)
})

test('WEB-UI-PROFILE-011 password form rejects mismatched new passwords without calling the API (ui-integration: auth-profile-management)', async () => {
  const calls = installFetch({ 'GET /api/auth/sessions': jsonResponse([]) })
  renderInApp(createElement(ProfilePage), { path: '/profile', auth: makeAuthSessionValue({ user: makeAuthUser(), status: 'signed-in' }) })
  await screen.findByText('No active login sessions to show.')
  const interaction = userEvent.setup()
  await interaction.type(document.getElementById('current-password'), 'current-synthetic-password')
  await interaction.type(document.getElementById('new-password'), 'new-synthetic-password')
  await interaction.type(document.getElementById('confirm-new-password'), 'different-synthetic-password')
  await interaction.click(screen.getByRole('button', { name: 'Update password' }))

  assert.equal((await screen.findByRole('alert')).textContent, 'Your new passwords do not match yet.')
  assert.equal(calls.length, 1)
})

test('WEB-UI-PROFILE-012 password change sends both values, forgets the account and preserves sign-out context (ui-integration: auth-profile-management)', async () => {
  const user = makeAuthUser()
  const calls = installFetch({
    'GET /api/auth/sessions': jsonResponse([]),
    'POST /api/auth/change-password': (call) => { callsSeen.push(call); return jsonResponse(undefined, 204) },
  })
  const callsSeen = []
  const forgotten = []
  renderInApp(createElement(ProfilePage), { path: '/profile', auth: makeAuthSessionValue({ user, status: 'signed-in', forgetAccount: (id) => forgotten.push(id) }) })
  await screen.findByText('No active login sessions to show.')
  const interaction = userEvent.setup()
  await interaction.type(document.getElementById('current-password'), 'current-synthetic-password')
  await interaction.type(document.getElementById('new-password'), 'new-synthetic-password')
  await interaction.type(document.getElementById('confirm-new-password'), 'new-synthetic-password')
  await interaction.click(screen.getByRole('button', { name: 'Update password' }))

  await waitFor(() => assert.equal(callsSeen.length, 1))
  assert.deepEqual(JSON.parse(callsSeen[0].init.body), { currentPassword: 'current-synthetic-password', newPassword: 'new-synthetic-password' })
  assert.deepEqual(forgotten, [user.id])
  assert.equal(window.sessionStorage.getItem('blueverse.auth-transition-notice'), 'Your password has changed. The account you were using was signed out for security.')
  assert.match(window.sessionStorage.getItem('blueverse.pending-auth-transition'), /^password\|\d+$/)
  assert.equal(calls.length, 2)
})

test('WEB-UI-PROFILE-013 account-wide sign-out requires current-password confirmation (ui-integration: auth-profile-management)', async () => {
  const user = makeAuthUser()
  const calls = []
  signedInProfile({ user, auth: { signOutEverywhere: async (password) => calls.push(password) } })
  await screen.findByText('No active login sessions to show.')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Sign out this account everywhere' }))
  assert.ok(screen.getByRole('dialog', { name: 'Sign out this account everywhere?' }))

  await userEvent.setup().type(document.getElementById('verify-current-password'), 'verified-synthetic-password')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Verify and continue' }))

  await waitFor(() => assert.deepEqual(calls, ['verified-synthetic-password']))
  assert.match(window.sessionStorage.getItem('blueverse.pending-auth-transition'), /^signOut\|\d+$/)
})

test('WEB-UI-PROFILE-014 account deletion requires a final confirmation and remains cancellable (ui-integration: auth-profile-management)', async () => {
  const user = makeAuthUser()
  const calls = installFetch({ 'GET /api/auth/sessions': jsonResponse([]) })
  const forgotten = []
  renderInApp(createElement(ProfilePage), { path: '/profile', auth: makeAuthSessionValue({ user, status: 'signed-in', forgetAccount: (id) => forgotten.push(id) }) })
  await screen.findByText('No active login sessions to show.')

  await userEvent.setup().click(screen.getByRole('button', { name: 'Delete account' }))
  assert.ok(screen.getByRole('alertdialog', { name: 'Delete this account permanently?' }))
  await userEvent.setup().click(screen.getByRole('button', { name: 'Cancel' }))
  assert.equal(screen.queryByRole('alertdialog'), null)
  assert.equal(calls.length, 1)
  assert.deepEqual(forgotten, [])
})

test('WEB-UI-PROFILE-015 confirmed account deletion calls the public endpoint and forgets the account (ui-integration: auth-profile-management)', async () => {
  const user = makeAuthUser()
  const calls = installFetch({
    'GET /api/auth/sessions': jsonResponse([]),
    'DELETE /api/auth/me': (call) => { callsSeen.push(call); return jsonResponse(undefined, 204) },
  })
  const callsSeen = []
  const forgotten = []
  renderInApp(createElement(ProfilePage), { path: '/profile', auth: makeAuthSessionValue({ user, status: 'signed-in', forgetAccount: (id) => forgotten.push(id) }) })
  await screen.findByText('No active login sessions to show.')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Delete account' }))
  await userEvent.setup().click(screen.getByRole('button', { name: 'Yes, delete account' }))

  await waitFor(() => assert.deepEqual(forgotten, [user.id]))
  assert.equal(callsSeen[0].input, '/api/auth/me')
  assert.equal(callsSeen[0].method, 'DELETE')
  assert.match(window.sessionStorage.getItem('blueverse.pending-auth-transition'), /^deleteAccount\|\d+$/)
  assert.equal(calls.length, 2)
})

test('WEB-UI-PROFILE-016 account deletion dependency failure stays in the dialog with the server-safe message (ui-integration: auth-profile-management)', async () => {
  const user = makeAuthUser()
  installFetch({
    'GET /api/auth/sessions': jsonResponse([]),
    'DELETE /api/auth/me': jsonResponse({ detail: 'The account is protected by an active system role.' }, 409),
  })
  renderInApp(createElement(ProfilePage), { path: '/profile', auth: makeAuthSessionValue({ user, status: 'signed-in' }) })
  await screen.findByText('No active login sessions to show.')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Delete account' }))
  await userEvent.setup().click(screen.getByRole('button', { name: 'Yes, delete account' }))

  assert.ok(await screen.findByText('The account is protected by an active system role.'))
  assert.ok(screen.getByRole('alertdialog'))
})

test('WEB-UI-PROFILE-017 names at both documented length boundaries can be saved', async () => {
  const user = makeAuthUser()
  const updates = []
  signedInProfile({ user, auth: { updateUser: async (fullName) => { updates.push(fullName); return { ...user, fullName } } } })
  await screen.findByText('No active login sessions to show.')

  const nameInput = document.getElementById('profile-full-name')
  const interaction = userEvent.setup()
  await interaction.clear(nameInput)
  await interaction.type(nameInput, 'A')
  await interaction.click(screen.getByRole('button', { name: 'Save changes' }))
  await screen.findByText('Your profile has been updated.')

  await interaction.clear(nameInput)
  await interaction.type(nameInput, 'N'.repeat(100))
  await interaction.click(screen.getByRole('button', { name: 'Save changes' }))
  await waitFor(() => assert.deepEqual(updates, ['A', 'N'.repeat(100)]))
})

test('WEB-UI-PROFILE-018 new passwords shorter than eight characters are rejected before the API call', async () => {
  const calls = installFetch({ 'GET /api/auth/sessions': jsonResponse([]) })
  renderInApp(createElement(ProfilePage), { path: '/profile', auth: makeAuthSessionValue({ user: makeAuthUser(), status: 'signed-in' }) })
  await screen.findByText('No active login sessions to show.')
  const interaction = userEvent.setup()
  await interaction.type(document.getElementById('current-password'), 'current-synthetic-password')
  await interaction.type(document.getElementById('new-password'), '1234567')
  await interaction.type(document.getElementById('confirm-new-password'), '1234567')
  fireEvent.submit(document.getElementById('new-password').closest('form'))

  assert.equal((await screen.findByRole('alert')).textContent, 'Use at least 8 characters for your new password.')
  assert.deepEqual(calls.map((call) => `${call.method} ${call.input}`), ['GET /api/auth/sessions'])
})

test('WEB-UI-PROFILE-019 current-password validation from the public API is shown without clearing retryable input', async () => {
  const calls = installFetch({
    'GET /api/auth/sessions': jsonResponse([]),
    'POST /api/auth/change-password': jsonResponse({ detail: 'The current password is incorrect.' }, 400),
  })
  renderInApp(createElement(ProfilePage), { path: '/profile', auth: makeAuthSessionValue({ user: makeAuthUser(), status: 'signed-in' }) })
  await screen.findByText('No active login sessions to show.')
  const interaction = userEvent.setup()
  await interaction.type(document.getElementById('current-password'), 'incorrect-synthetic-password')
  await interaction.type(document.getElementById('new-password'), 'new-valid-password')
  await interaction.type(document.getElementById('confirm-new-password'), 'new-valid-password')
  await interaction.click(screen.getByRole('button', { name: 'Update password' }))

  assert.equal((await screen.findByRole('alert')).textContent, 'The current password is incorrect.')
  assert.equal(document.getElementById('current-password').value, 'incorrect-synthetic-password')
  assert.equal(calls.find((call) => call.method === 'POST').input, '/api/auth/change-password')
})

test('WEB-UI-PROFILE-020 denied session revocation preserves the session and keeps verification open for retry', async () => {
  const calls = installFetch({
    'GET /api/auth/sessions': jsonResponse([session('session-current', true), session('session-remote')]),
    'DELETE /api/auth/sessions/session-remote': jsonResponse({ detail: 'The current password is incorrect.' }, 403),
  })
  renderInApp(createElement(ProfilePage), { path: '/profile', auth: makeAuthSessionValue({ user: makeAuthUser(), status: 'signed-in' }) })
  await screen.findByText('Signed-in device')
  await userEvent.setup().click(screen.getByRole('button', { name: 'End session' }))
  await userEvent.setup().type(document.getElementById('verify-current-password'), 'incorrect-synthetic-password')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Verify and continue' }))

  assert.equal((await screen.findByRole('alert')).textContent, 'The current password is incorrect.')
  assert.equal(document.querySelectorAll('#sessions li').length, 2)
  assert.ok(screen.getByRole('dialog', { name: 'End this device session?' }))
  assert.equal(calls.filter((call) => call.method === 'DELETE').length, 1)
})

test('WEB-UI-PROFILE-021 a failed current-session logout keeps the current session visible and safe', async () => {
  const user = makeAuthUser()
  installFetch({ 'GET /api/auth/sessions': jsonResponse([session('session-current', true)]) })
  renderInApp(createElement(ProfilePage), {
    path: '/profile',
    auth: makeAuthSessionValue({ user, status: 'signed-in', removeAccount: async () => { throw new Error('private upstream detail') } }),
  })
  await screen.findByText('This browser')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Sign out here' }))

  assert.equal((await screen.findByRole('alert')).textContent, 'That session could not be ended. It may have already expired.')
  assert.equal(document.querySelectorAll('#sessions li').length, 1)
  assert.doesNotMatch(document.body.textContent, /private upstream detail/)
})
