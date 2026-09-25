import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import {
  cleanup,
  closeWebTestServer,
  createElement,
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

const originalFetch = globalThis.fetch
const [{ default: LoginPage }, { default: RegistrationPage }, { default: HomePage }, { default: DashboardPage }] = await Promise.all([
  loadWebModule('/src/pages/LoginPage.tsx'),
  loadWebModule('/src/pages/RegistrationPage.tsx'),
  loadWebModule('/src/pages/HomePage.tsx'),
  loadWebModule('/src/pages/DashboardPage.tsx'),
])

afterEach(() => {
  cleanup()
  resetTestBrowser()
  globalThis.fetch = originalFetch
})

after(async () => closeWebTestServer())

function authResponse(user = makeAuthUser()) {
  return {
    token: null,
    expiresAt: '2026-09-25T00:00:00.000Z',
    deviceId: 'synthetic-device',
    deviceKey: null,
    refreshToken: null,
    sessionExpiresAt: '2026-10-25T00:00:00.000Z',
    rememberMe: false,
    user,
    roles: user.roles,
    permissions: user.permissions,
  }
}

test('WEB-UI-AUTH-001 sign-in exposes labeled credentials, remember-me and an accessible password toggle (ui-integration: auth-signin)', async () => {
  renderInApp(createElement(LoginPage), { path: '/signin?returnTo=%2Fdashboard' })

  assert.ok(screen.getByRole('heading', { name: 'Welcome back.' }))
  assert.equal(screen.getByRole('textbox', { name: 'Email address' }).type, 'email')
  assert.equal(screen.getByLabelText('Password').type, 'password')
  assert.ok(screen.getByRole('checkbox', { name: 'Keep me signed in for 30 days' }))
  assert.equal(screen.getByRole('button', { name: 'Show password' }).getAttribute('aria-pressed'), 'false')
  assert.equal(screen.getByRole('link', { name: 'Create an account' }).getAttribute('href'), '/signup')

  await userEvent.setup().click(screen.getByRole('button', { name: 'Show password' }))
  assert.equal(screen.getByLabelText('Password').type, 'text')
  assert.equal(screen.getByRole('button', { name: 'Hide password' }).getAttribute('aria-pressed'), 'true')
})

test('WEB-UI-AUTH-002 sign-in sends trimmed email, cookie login, and return destination before showing the API error (ui-integration: auth-signin)', async () => {
  let request
  globalThis.fetch = async (input, init) => {
    request = { input, init }
    return jsonResponse({ title: 'Unauthorized', detail: 'The sign-in details were not accepted.' }, 401)
  }

  renderInApp(createElement(LoginPage), { path: '/signin?returnTo=%2Fdashboard%3Ftab%3Dcoast%23overview' })
  const user = userEvent.setup()
  await user.type(screen.getByRole('textbox', { name: 'Email address' }), 'visitor@example.test')
  await user.type(screen.getByLabelText('Password'), 'synthetic-password')
  await user.click(screen.getByRole('button', { name: 'Sign in' }))

  const alert = await screen.findByRole('alert')
  assert.equal(alert.textContent, 'The sign-in details were not accepted.')
  assert.equal(request.input, '/api/auth/login')
  assert.deepEqual(JSON.parse(request.init.body), {
    email: 'visitor@example.test',
    password: 'synthetic-password',
    rememberMe: false,
    useCookies: true,
  })
  assert.equal(screen.getByRole('button', { name: 'Sign in' }).disabled, false)
})

test('WEB-UI-AUTH-003 sign-in blocks submission while session restoration is pending (ui-integration: auth-signin)', async () => {
  let requestCount = 0
  globalThis.fetch = async () => { requestCount += 1; return jsonResponse(authResponse()) }

  renderInApp(createElement(LoginPage), { path: '/signin', auth: makeAuthSessionValue({ status: 'checking' }) })

  assert.equal(screen.getByRole('button', { name: 'Sign in' }).disabled, true)
  assert.equal(document.querySelector('form').getAttribute('aria-busy'), 'true')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Sign in' }))
  assert.equal(requestCount, 0)
})

test('WEB-UI-AUTH-004 successful sign-in accepts the user and routes to a safe return destination (ui-integration: auth-signin)', async () => {
  const accepted = []
  const user = makeAuthUser()
  globalThis.fetch = async () => jsonResponse(authResponse(user))

  renderInApp(createElement(LoginPage), {
    path: '/signin?returnTo=%2Fdashboard%3Ftab%3Dcoast%23overview',
    auth: makeAuthSessionValue({ acceptAuthenticatedUser: (value) => accepted.push(value) }),
  })
  const interaction = userEvent.setup()
  await interaction.type(screen.getByRole('textbox', { name: 'Email address' }), user.email)
  await interaction.type(screen.getByLabelText('Password'), 'synthetic-password')
  await interaction.click(screen.getByRole('button', { name: 'Sign in' }))

  await waitFor(() => assert.equal(screen.getByTestId('router-location').textContent, '/dashboard?tab=coast#overview'))
  assert.deepEqual(accepted, [user])
  assert.match(window.sessionStorage.getItem('blueverse.pending-auth-transition'), /^signIn\|\d+$/)
})

test('WEB-UI-AUTH-005 unsafe external sign-in return paths are replaced by the home destination (ui-integration: auth-signin)', async () => {
  globalThis.fetch = async () => jsonResponse(authResponse())
  renderInApp(createElement(LoginPage), { path: '/signin?returnTo=https%3A%2F%2Fevil.example%2F' })

  const interaction = userEvent.setup()
  await interaction.type(screen.getByRole('textbox', { name: 'Email address' }), 'visitor@example.test')
  await interaction.type(screen.getByLabelText('Password'), 'synthetic-password')
  await interaction.click(screen.getByRole('button', { name: 'Sign in' }))

  await waitFor(() => assert.equal(screen.getByTestId('router-location').textContent, '/'))
})

test('WEB-UI-AUTH-006 registration has required identity and confirmation controls with password feedback (ui-integration: auth-registration)', async () => {
  renderInApp(createElement(RegistrationPage), { path: '/signup' })

  assert.ok(screen.getByRole('heading', { name: 'Find your place by the sea.' }))
  assert.equal(screen.getByRole('textbox', { name: 'Full name' }).maxLength, 100)
  assert.equal(screen.getByRole('textbox', { name: 'Email address' }).type, 'email')
  assert.equal(document.getElementById('register-password').minLength, 8)
  assert.ok(screen.getByRole('checkbox', { name: 'Keep me signed in for 30 days' }))

  const interaction = userEvent.setup()
  await interaction.type(document.getElementById('register-password'), 'synthetic-password')
  const confirmPassword = document.getElementById('register-confirm-password')
  await interaction.type(confirmPassword, 'different-password')
  assert.equal(confirmPassword.getAttribute('aria-invalid'), 'true')
  assert.ok(screen.getByText('Passwords do not match yet.'))
})

test('WEB-UI-AUTH-007 registration rejects mismatched passwords without calling Auth (ui-integration: auth-registration)', async () => {
  let requestCount = 0
  globalThis.fetch = async () => { requestCount += 1; return jsonResponse(authResponse()) }
  renderInApp(createElement(RegistrationPage), { path: '/signup' })

  const interaction = userEvent.setup()
  await interaction.type(screen.getByRole('textbox', { name: 'Full name' }), 'Coastal Visitor')
  await interaction.type(screen.getByRole('textbox', { name: 'Email address' }), 'visitor@example.test')
  await interaction.type(document.getElementById('register-password'), 'synthetic-password')
  await interaction.type(screen.getByLabelText('Confirm password'), 'different-password')
  await interaction.click(screen.getByRole('button', { name: 'Create account' }))

  assert.equal((await screen.findByRole('alert')).textContent, 'Your passwords do not match yet.')
  assert.equal(requestCount, 0)
})

test('WEB-UI-AUTH-008 registration rejects a missing meaningful name before sending a request (ui-integration: auth-registration)', async () => {
  let requestCount = 0
  globalThis.fetch = async () => { requestCount += 1; return jsonResponse(authResponse()) }
  renderInApp(createElement(RegistrationPage), { path: '/signup' })

  const interaction = userEvent.setup()
  await interaction.type(screen.getByRole('textbox', { name: 'Full name' }), '   ')
  await interaction.type(screen.getByRole('textbox', { name: 'Email address' }), 'visitor@example.test')
  await interaction.type(document.getElementById('register-password'), 'synthetic-password')
  await interaction.type(screen.getByLabelText('Confirm password'), 'synthetic-password')
  await interaction.click(screen.getByRole('button', { name: 'Create account' }))

  assert.equal((await screen.findByRole('alert')).textContent, 'Enter your name to create an account.')
  assert.equal(requestCount, 0)
})

test('WEB-UI-AUTH-009 registration account capacity is explained and prevents a sixth account request (ui-integration: auth-registration)', async () => {
  let requestCount = 0
  globalThis.fetch = async () => { requestCount += 1; return jsonResponse(authResponse()) }
  const accounts = Array.from({ length: 5 }, (_, index) => ({ id: `user-${index}`, fullName: `User ${index}`, email: `user${index}@example.test` }))
  renderInApp(createElement(RegistrationPage), {
    path: '/signup',
    auth: makeAuthSessionValue({ user: makeAuthUser(), accounts, status: 'signed-in' }),
  })

  assert.ok(screen.getByText(/Five accounts are already saved on this device/).closest('[role="status"]'))
  assert.equal(screen.getByRole('button', { name: 'Create account' }).disabled, true)
  assert.equal(requestCount, 0)
})

test('WEB-UI-AUTH-010 successful registration accepts the created user and opens Profile (ui-integration: auth-registration)', async () => {
  const accepted = []
  const user = makeAuthUser({ id: 'registered-user', fullName: 'Registered Visitor' })
  globalThis.fetch = async () => jsonResponse(authResponse(user))
  renderInApp(createElement(RegistrationPage), {
    path: '/signup',
    auth: makeAuthSessionValue({ acceptAuthenticatedUser: (value) => accepted.push(value) }),
  })

  const interaction = userEvent.setup()
  await interaction.type(screen.getByRole('textbox', { name: 'Full name' }), ' Registered Visitor ')
  await interaction.type(screen.getByRole('textbox', { name: 'Email address' }), 'visitor@example.test')
  await interaction.type(document.getElementById('register-password'), 'synthetic-password')
  await interaction.type(screen.getByLabelText('Confirm password'), 'synthetic-password')
  await interaction.click(screen.getByRole('button', { name: 'Create account' }))

  await waitFor(() => assert.equal(screen.getByTestId('router-location').textContent, '/profile'))
  assert.deepEqual(accepted, [user])
  assert.match(window.sessionStorage.getItem('blueverse.pending-auth-transition'), /^registration\|\d+$/)
})

test('WEB-UI-HOME-001 guest home links retain their current path as the sign-in return destination (ui-integration: home)', () => {
  renderInApp(createElement(HomePage), { path: '/?campaign=shore' })

  assert.ok(screen.getByRole('heading', { name: /Closer to the coast/ }))
  assert.equal(screen.getByRole('link', { name: /Sign in to your account/ }).getAttribute('href'), '/signin?returnTo=%2F%3Fcampaign%3Dshore')
  assert.equal(screen.getByRole('link', { name: 'Create an account' }).getAttribute('href'), '/signup')
  assert.ok(screen.getByRole('img', { name: 'A quiet palm-lined tropical cove in soft daylight' }))
})

test('WEB-UI-HOME-002 signed-in home presents account routes instead of guest sign-in prompts (ui-integration: home)', () => {
  const user = makeAuthUser({ fullName: 'Lanka Coastal Visitor' })
  renderInApp(createElement(HomePage), { path: '/', auth: makeAuthSessionValue({ user, status: 'signed-in' }) })

  assert.ok(screen.getByRole('heading', { name: 'Good to have you here, Lanka.' }))
  assert.equal(screen.getByRole('link', { name: 'Open your overview' }).getAttribute('href'), '/dashboard')
  assert.equal(screen.getByRole('link', { name: 'View your profile' }).getAttribute('href'), '/profile')
  assert.equal(screen.queryByRole('link', { name: /Sign in to your account/ }), null)
})

test('WEB-UI-DASH-001 signed-in dashboard shows the active account and each coastal focus area (ui-integration: coastal-overview-dashboard)', () => {
  const user = makeAuthUser({ fullName: 'Aru Coastal Guest' })
  renderInApp(createElement(DashboardPage), { path: '/dashboard', auth: makeAuthSessionValue({ user, status: 'signed-in' }) })

  assert.ok(screen.getByRole('heading', { name: 'A clearer view of your coastal journey.' }))
  assert.ok(screen.getByRole('heading', { name: 'Welcome, Aru.' }))
  assert.ok(screen.getByText(user.email))
  for (const title of ['Coastal discovery', 'Marine awareness', 'Shared stewardship']) {
    assert.ok(screen.getByRole('heading', { name: title }))
  }
  assert.equal(screen.getByRole('link', { name: /Review your profile/ }).getAttribute('href'), '/profile#personal-details')
})

test('WEB-UI-DASH-002 signed-out dashboard retains the requested protected destination and presents account recovery (ui-integration: coastal-overview-dashboard)', () => {
  renderInApp(createElement(DashboardPage), { path: '/dashboard', auth: makeAuthSessionValue({ status: 'signed-out' }) })

  assert.ok(screen.getByRole('heading', { name: 'Sign in to make this space yours.' }))
  assert.ok(screen.getAllByRole('link', { name: 'Sign in' }).every((link) => link.getAttribute('href') === '/signin?returnTo=%2Fdashboard'))
  assert.ok(screen.getAllByRole('link', { name: 'Create account' }).every((link) => link.getAttribute('href') === '/signup'))
})

test('WEB-UI-DASH-003 unavailable dashboard explains the account dependency without exposing service details (ui-integration: coastal-overview-dashboard)', () => {
  renderInApp(createElement(DashboardPage), { path: '/dashboard', auth: makeAuthSessionValue({ status: 'unavailable', error: 'private fixture detail' }) })

  assert.ok(screen.getByText('We couldn’t check your account just now. Sign in again or try in a little while.'))
  assert.equal(screen.queryByText('private fixture detail'), null)
})
