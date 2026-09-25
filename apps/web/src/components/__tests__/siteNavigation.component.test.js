import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import {
  cleanup,
  closeWebTestServer,
  createElement,
  fireEvent,
  loadWebModule,
  makeAuthSessionValue,
  makeAuthUser,
  renderInApp,
  resetTestBrowser,
  screen,
  userEvent,
} from '../../testSupport/reactTestHarness.js'

const [{ default: SiteHeader }, { default: AccountAreaNavigation }] = await Promise.all([
  loadWebModule('/src/components/layout/SiteHeader.tsx'),
  loadWebModule('/src/components/account/AccountAreaNavigation.tsx'),
])
const originalFetch = globalThis.fetch

afterEach(() => {
  cleanup()
  resetTestBrowser()
  globalThis.fetch = originalFetch
})

after(async () => closeWebTestServer())

function account(id, fullName, email) { return { id, fullName, email } }

test('WEB-UI-NAV-001 guest header exposes sign-in and registration routes for desktop and mobile (ui-integration: home)', () => {
  renderInApp(createElement(SiteHeader, { active: 'home' }), { path: '/?campaign=shore' })

  const signInLinks = screen.getAllByRole('link', { name: 'Sign in' })
  assert.equal(signInLinks.length, 2)
  assert.ok(signInLinks.every((link) => link.getAttribute('href') === '/signin?returnTo=%2F%3Fcampaign%3Dshore'))
  assert.equal(screen.getAllByRole('link', { name: 'Create account' }).length, 2)
  assert.equal(document.getElementById('desktop-account-trigger'), null)
  assert.equal(document.getElementById('mobile-account-trigger'), null)
  assert.equal(screen.getByRole('link', { name: 'BLUEVERSE home' }).getAttribute('href'), '/')
})

test('WEB-UI-NAV-002 mobile navigation disclosure reflects expanded state and closes after route selection (ui-integration: home)', async () => {
  renderInApp(createElement(SiteHeader, { active: 'home' }), { path: '/' })
  const toggle = screen.getByRole('button', { name: 'Open menu' })
  const mobileMenu = document.getElementById('mobile-site-menu')

  assert.equal(toggle.getAttribute('aria-expanded'), 'false')
  await userEvent.setup().click(toggle)
  assert.equal(screen.getByRole('button', { name: 'Close menu' }).getAttribute('aria-expanded'), 'true')
  assert.equal(mobileMenu.classList.contains('visible'), true)
  const storyLinks = Array.from(mobileMenu.querySelectorAll('a')).filter((link) => link.textContent === 'Our story')
  assert.equal(storyLinks.length, 1)
  assert.equal(storyLinks[0].getAttribute('href'), '/#our-story')
  await userEvent.setup().click(storyLinks[0])
  assert.equal(screen.getByTestId('router-location').textContent, '/#our-story')
  assert.equal(screen.getByRole('button', { name: 'Open menu' }).getAttribute('aria-expanded'), 'false')
})

test('WEB-UI-NAV-003 session restoration suppresses premature guest account links (ui-integration: auth-signin)', () => {
  renderInApp(createElement(SiteHeader), { auth: makeAuthSessionValue({ status: 'checking' }) })

  assert.equal(screen.queryByRole('link', { name: 'Sign in' }), null)
  assert.equal(screen.queryByRole('link', { name: 'Create account' }), null)
  assert.equal(document.getElementById('desktop-account-trigger'), null)
  assert.equal(document.getElementById('mobile-account-trigger'), null)
})

test('WEB-UI-NAV-004 signed-in account menu shows the active account, account capacity and profile actions (ui-integration: auth-signin)', async () => {
  const user = makeAuthUser({ fullName: 'Aru Visitor' })
  const accounts = [account(user.id, user.fullName, user.email), account('user-2', 'Other Visitor', 'other@example.test')]
  renderInApp(createElement(SiteHeader), { auth: makeAuthSessionValue({ user, accounts, status: 'signed-in' }) })

  const trigger = document.getElementById('desktop-account-trigger')
  await userEvent.setup().click(trigger)
  const menu = document.getElementById('desktop-account-menu')
  assert.equal(trigger.getAttribute('aria-expanded'), 'true')
  assert.match(menu.textContent, /Aru Visitor/)
  assert.match(menu.textContent, /2 \/ 5/)
  assert.equal(menu.querySelector('button[aria-current="true"]').disabled, true)
  assert.equal(menu.querySelector('button[aria-current="true"]').getAttribute('aria-current'), 'true')
  assert.equal(menu.querySelector('nav[aria-label="Account menu"] a[href="/profile"]').textContent.trim(), 'Profile ↗')
  assert.equal(menu.querySelector('nav[aria-label="Account menu"] a[href="/dashboard"]').textContent.trim(), 'Dashboard ↗')
  assert.ok(menu.querySelector('a[href^="/signin?returnTo="]'))
  assert.ok(menu.querySelector('a[href="/signup"]'))
  assert.ok(menu.querySelector('button[aria-label="Account menu"]') === null)
})

test('WEB-UI-NAV-005 account switching selects a different account and preserves the transition context (ui-integration: auth-signin)', async () => {
  const user = makeAuthUser()
  const calls = []
  const switchAccount = async (accountId) => { calls.push(accountId) }
  const accounts = [account(user.id, user.fullName, user.email), account('user-2', 'Other Visitor', 'other@example.test')]
  renderInApp(createElement(SiteHeader), { auth: makeAuthSessionValue({ user, accounts, status: 'signed-in', switchAccount }) })

  await userEvent.setup().click(document.getElementById('desktop-account-trigger'))
  const menu = document.getElementById('desktop-account-menu')
  const switchButton = Array.from(menu.querySelectorAll('button')).find((button) => button.textContent.includes('Other Visitor'))
  assert.ok(switchButton)
  await userEvent.setup().click(switchButton)

  assert.deepEqual(calls, ['user-2'])
  assert.match(window.sessionStorage.getItem('blueverse.pending-auth-transition'), /^switchAccount\|\d+$/)
  assert.equal(document.getElementById('desktop-account-menu'), null)
})

test('WEB-UI-NAV-006 rejected account switching keeps the menu available and shows a recovery message (ui-integration: auth-signin)', async () => {
  const user = makeAuthUser()
  const accounts = [account(user.id, user.fullName, user.email), account('user-2', 'Other Visitor', 'other@example.test')]
  renderInApp(createElement(SiteHeader), { auth: makeAuthSessionValue({ user, accounts, status: 'signed-in', switchAccount: async () => { throw new Error('private fixture') } }) })

  await userEvent.setup().click(document.getElementById('desktop-account-trigger'))
  const switchButton = Array.from(document.querySelectorAll('#desktop-account-menu button')).find((button) => button.textContent.includes('Other Visitor'))
  await userEvent.setup().click(switchButton)

  assert.equal(document.querySelector('#desktop-account-menu [role="alert"]').textContent, 'We could not switch to that account. It may need you to sign in again.')
  assert.ok(document.getElementById('desktop-account-menu'))
  assert.doesNotMatch(document.body.textContent, /private fixture/)
})

test('WEB-UI-NAV-007 five saved accounts replace add-account links with the capacity guidance (ui-integration: auth-signin)', async () => {
  const user = makeAuthUser()
  const accounts = Array.from({ length: 5 }, (_, index) => account(`user-${index}`, `Visitor ${index}`, `visitor${index}@example.test`))
  renderInApp(createElement(SiteHeader), { auth: makeAuthSessionValue({ user, accounts, status: 'signed-in' }) })

  await userEvent.setup().click(document.getElementById('desktop-account-trigger'))
  const menu = document.getElementById('desktop-account-menu')
  assert.match(menu.textContent, /5 \/ 5/)
  assert.match(menu.textContent, /Remove an account from this browser before adding another/)
  assert.equal(menu.querySelector('a[href^="/signin?"]'), null)
  assert.equal(menu.querySelector('a[href="/signup"]'), null)
})

test('WEB-UI-NAV-008 Escape closes the account disclosure and restores focus to its trigger (ui-integration: auth-signin)', async () => {
  const user = makeAuthUser()
  renderInApp(createElement(SiteHeader), { auth: makeAuthSessionValue({ user, accounts: [account(user.id, user.fullName, user.email)], status: 'signed-in' }) })
  const trigger = document.getElementById('desktop-account-trigger')
  await userEvent.setup().click(trigger)

  fireEvent.keyDown(document, { key: 'Escape' })

  assert.equal(document.getElementById('desktop-account-menu'), null)
  assert.equal(document.activeElement.id, 'desktop-account-trigger')
})

test('WEB-UI-NAV-009 an outside pointer press closes the account disclosure (ui-integration: auth-signin)', async () => {
  const user = makeAuthUser()
  renderInApp(createElement(SiteHeader), { auth: makeAuthSessionValue({ user, accounts: [account(user.id, user.fullName, user.email)], status: 'signed-in' }) })
  await userEvent.setup().click(document.getElementById('desktop-account-trigger'))

  fireEvent(document.body, new window.Event('pointerdown', { bubbles: true }))

  assert.equal(document.getElementById('desktop-account-menu'), null)
})

test('WEB-UI-NAV-010 profile and dashboard navigation are shared while administration children follow permission grants (ui-integration: auth-profile-management, coastal-overview-dashboard, auth-admin-entry)', () => {
  const navigation = (permissions) => renderInApp(createElement(AccountAreaNavigation, { active: 'admin' }), {
    auth: makeAuthSessionValue({ user: makeAuthUser({ permissions }), status: 'signed-in' }),
  })

  navigation(['profile.read'])
  assert.equal(screen.queryByRole('link', { name: 'Administration' }), null)
  assert.deepEqual(Array.from(document.querySelectorAll('a[href="/profile"], a[href="/dashboard"]')).map((link) => link.getAttribute('href')).sort(), ['/dashboard', '/dashboard', '/profile', '/profile'])
  cleanup()

  navigation(['auth.permission.read', 'auth.user.read'])
  const adminLinks = Array.from(document.querySelectorAll('a[href^="/admin"]')).map((link) => link.getAttribute('href')).sort()
  assert.deepEqual(adminLinks, ['/admin', '/admin', '/admin/permissions', '/admin/permissions', '/admin/users', '/admin/users'])
  assert.equal(document.querySelector('a[href="/admin/roles"]'), null)
})

test('WEB-UI-NAV-011 account area disclosures expose state and matching target IDs on desktop and mobile (ui-integration: auth-profile-management)', async () => {
  renderInApp(createElement(AccountAreaNavigation, { active: 'profile' }), { auth: makeAuthSessionValue({ user: makeAuthUser(), status: 'signed-in' }) })

  const desktopToggle = document.querySelector('button[aria-controls="profile-subnav-desktop"]')
  const mobileToggle = document.querySelector('button[aria-controls="profile-subnav-mobile"]')
  assert.equal(desktopToggle.getAttribute('aria-expanded'), 'true')
  assert.equal(mobileToggle.getAttribute('aria-expanded'), 'true')
  await userEvent.setup().click(mobileToggle)
  assert.equal(mobileToggle.getAttribute('aria-expanded'), 'false')
  assert.equal(desktopToggle.getAttribute('aria-expanded'), 'false')
  assert.equal(document.getElementById('profile-subnav-mobile'), null)
})

test('WEB-UI-NAV-012 account disclosure trigger exposes controlled panel relationship (ui-integration: auth-signin)', async () => {
  const user = makeAuthUser()
  renderInApp(createElement(SiteHeader), { auth: makeAuthSessionValue({ user, accounts: [account(user.id, user.fullName, user.email)], status: 'signed-in' }) })
  const trigger = document.getElementById('desktop-account-trigger')

  assert.equal(trigger.getAttribute('aria-expanded'), 'false')
  assert.equal(trigger.hasAttribute('aria-controls'), false)
  await userEvent.setup().click(trigger)
  assert.equal(trigger.getAttribute('aria-controls'), 'desktop-account-menu')
  assert.equal(trigger.getAttribute('aria-expanded'), 'true')
})

test('WEB-UI-NAV-013 successful sign-out preserves its transition notice and closes the account menu (ui-integration: auth-signin)', async () => {
  const calls = []
  const user = makeAuthUser()
  renderInApp(createElement(SiteHeader), { auth: makeAuthSessionValue({
    user,
    accounts: [account(user.id, user.fullName, user.email)],
    status: 'signed-in',
    signOut: async () => { calls.push('signOut') },
  }) })
  await userEvent.setup().click(document.getElementById('desktop-account-trigger'))
  const signOutButton = Array.from(document.querySelectorAll('#desktop-account-menu button')).find((button) => button.textContent.includes('Sign out from this device'))
  await userEvent.setup().click(signOutButton)

  assert.deepEqual(calls, ['signOut'])
  assert.match(window.sessionStorage.getItem('blueverse.pending-auth-transition'), /^signOut\|\d+$/)
  assert.equal(document.getElementById('desktop-account-menu'), null)
})

test('WEB-UI-NAV-014 failed sign-out leaves the account menu open with a safe retry message (ui-integration: auth-signin)', async () => {
  const user = makeAuthUser()
  renderInApp(createElement(SiteHeader), { auth: makeAuthSessionValue({
    user,
    accounts: [account(user.id, user.fullName, user.email)],
    status: 'signed-in',
    signOut: async () => { throw new Error('private fixture') },
  }) })
  await userEvent.setup().click(document.getElementById('desktop-account-trigger'))
  const signOutButton = Array.from(document.querySelectorAll('#desktop-account-menu button')).find((button) => button.textContent.includes('Sign out from this device'))
  await userEvent.setup().click(signOutButton)

  assert.equal(document.querySelector('#desktop-account-menu [role="alert"]').textContent, 'We could not sign this account out from this device just now. Please try again.')
  assert.ok(document.getElementById('desktop-account-menu'))
  assert.doesNotMatch(document.body.textContent, /private fixture/)
})

test('WEB-UI-NAV-015 an in-progress account switch disables other account rows (ui-integration: auth-signin)', async () => {
  const user = makeAuthUser()
  renderInApp(createElement(SiteHeader), { auth: makeAuthSessionValue({
    user,
    accounts: [account(user.id, user.fullName, user.email), account('user-2', 'Other Visitor', 'other@example.test')],
    status: 'signed-in',
    switchingAccountId: 'user-2',
  }) })
  await userEvent.setup().click(document.getElementById('desktop-account-trigger'))

  const rows = Array.from(document.querySelectorAll('#desktop-account-menu button[aria-current], #desktop-account-menu button:not([aria-label])'))
  const currentRow = rows.find((button) => button.getAttribute('aria-current') === 'true')
  const otherRow = rows.find((button) => button.textContent.includes('Other Visitor'))
  assert.equal(currentRow.disabled, true)
  assert.equal(otherRow.disabled, true)
})
