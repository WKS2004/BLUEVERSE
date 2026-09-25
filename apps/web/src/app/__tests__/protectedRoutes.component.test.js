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
  waitFor,
} from '../../testSupport/reactTestHarness.js'

const { default: AppRoutes } = await loadWebModule('/src/app/routes.tsx')
const originalFetch = globalThis.fetch
const unknownPath = ['/sign', 'sin'].join('')

afterEach(() => {
  cleanup()
  resetTestBrowser()
  globalThis.fetch = originalFetch
})

after(async () => closeWebTestServer())

test('WEB-ROUTE-001 protected account destinations retain path, query and fragment through sign-in (ui-integration: auth-profile-management)', async () => {
  renderInApp(createElement(AppRoutes), {
    path: '/profile?tab=security#sessions',
    auth: makeAuthSessionValue({ status: 'signed-out' }),
  })

  assert.ok(await screen.findByRole('heading', { name: 'Welcome back.' }))
  await waitFor(() => assert.equal(screen.getByTestId('router-location').textContent, '/signin?returnTo=%2Fprofile%3Ftab%3Dsecurity%23sessions'))
  assert.equal(screen.getByRole('link', { name: 'Create an account' }).getAttribute('href'), '/signup')
})

test('WEB-ROUTE-002 session restoration does not render protected content before the account is checked (ui-integration: auth-profile-management)', () => {
  renderInApp(createElement(AppRoutes), { path: '/profile', auth: makeAuthSessionValue({ status: 'checking' }) })

  assert.equal(screen.queryByRole('heading', { name: 'Your profile, in your hands.' }), null)
  assert.equal(screen.queryByRole('heading', { name: 'Welcome back.' }), null)
  assert.equal(screen.getByTestId('router-location').textContent, '/profile')
})

test('WEB-ROUTE-003 unavailable account checks present safe sign-in recovery without exposing service details (ui-integration: auth-profile-management)', () => {
  renderInApp(createElement(AppRoutes), {
    path: '/profile?tab=security#sessions',
    auth: makeAuthSessionValue({ status: 'unavailable', error: 'database host or trace identifier' }),
  })

  assert.ok(screen.getByRole('heading', { name: 'We couldn’t verify your account.' }))
  const recoveryLink = screen.getByRole('link', { name: 'Continue to sign in' })
  assert.equal(recoveryLink.getAttribute('href'), '/signin?returnTo=%2Fprofile%3Ftab%3Dsecurity%23sessions')
  assert.doesNotMatch(document.body.textContent, /database host or trace identifier/)
})

test('WEB-ROUTE-004 admin entry selects the first currently granted read area (ui-integration: auth-admin-entry, auth-permission-administration)', async () => {
  globalThis.fetch = async (input, init = {}) => {
    assert.equal(input, '/api/auth/permissions')
    assert.equal(init.credentials, 'include')
    return jsonResponse([{ id: 'permission-1', code: 'auth.permission.read', description: 'Read the catalogue' }])
  }
  renderInApp(createElement(AppRoutes), {
    path: '/admin',
    auth: makeAuthSessionValue({ user: makeAuthUser({ permissions: ['auth.permission.read', 'auth.role.read', 'auth.user.read'] }), status: 'signed-in' }),
  })

  assert.ok(await screen.findByRole('heading', { name: 'Permissions' }))
  assert.equal(screen.getByTestId('router-location').textContent, '/admin/permissions')
  assert.ok(screen.getByText('auth.permission.read'))
})

test('WEB-ROUTE-005 a permission without the matching read grant cannot enter the protected admin route (ui-integration: auth-user-administration)', () => {
  let requests = 0
  globalThis.fetch = async () => { requests += 1; return jsonResponse([]) }
  renderInApp(createElement(AppRoutes), {
    path: '/admin/users',
    auth: makeAuthSessionValue({ user: makeAuthUser({ permissions: ['auth.user.create'] }), status: 'signed-in' }),
  })

  assert.ok(screen.getByRole('heading', { name: 'You don’t have access' }))
  assert.equal(screen.getByRole('link', { name: 'Return to your profile' }).getAttribute('href'), '/profile')
  assert.equal(requests, 0)
})

test('WEB-ROUTE-006 an account with no administration read grant cannot enter the administration index (ui-integration: auth-admin-entry)', () => {
  renderInApp(createElement(AppRoutes), {
    path: '/admin',
    auth: makeAuthSessionValue({ user: makeAuthUser({ permissions: ['profile.read'] }), status: 'signed-in' }),
  })

  assert.ok(screen.getByRole('heading', { name: 'You don’t have access' }))
  assert.equal(screen.getByTestId('router-location').textContent, '/admin')
})

test('WEB-ROUTE-007 unknown paths keep their address and provide branded recovery navigation (ui-integration: not-found-recovery)', () => {
  renderInApp(createElement(AppRoutes), { path: unknownPath, auth: makeAuthSessionValue({ status: 'signed-out' }) })

  assert.ok(screen.getByRole('heading', { name: 'This cove isn’t on our chart.' }))
  assert.equal(screen.getByTestId('router-location').textContent, '/signsin')
  assert.equal(screen.getByRole('link', { name: /Back to the coast/ }).getAttribute('href'), '/')
  assert.ok(document.querySelector('header'))
  assert.ok(document.querySelector('footer'))
})

test('WEB-ROUTE-008 extensionless server recovery remains inside the shared site shell (ui-integration: server-error-recovery)', () => {
  renderInApp(createElement(AppRoutes), { path: '/500', auth: makeAuthSessionValue({ status: 'signed-out' }) })

  assert.ok(screen.getByRole('heading', { name: 'A current interrupted the journey.' }))
  assert.equal(screen.getByTestId('router-location').textContent, '/500')
  assert.equal(screen.getByRole('button', { name: 'Try again' }).type, 'button')
  assert.ok(document.querySelector('header'))
  assert.ok(document.querySelector('footer'))
  assert.doesNotMatch(document.body.textContent, /TypeError|stack trace|exception/i)
})
