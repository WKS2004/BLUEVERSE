import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { cleanup, closeWebTestServer, createElement, loadWebModule, makeAuthSessionValue, makeAuthUser, renderInApp, resetTestBrowser, screen, userEvent } from '../../testSupport/reactTestHarness.js'

const [{ default: SiteHeader }, { default: AccountAreaNavigation }] = await Promise.all([
  loadWebModule('/src/components/layout/SiteHeader.tsx'),
  loadWebModule('/src/components/account/AccountAreaNavigation.tsx'),
])
afterEach(() => { cleanup(); resetTestBrowser() })
after(closeWebTestServer)

for (const path of ['/admin', '/admin/permissions', '/admin/roles', '/admin/users']) {
  test(`WEB-MARINE-ENTRY-001 ${path} omits home-section links and the empty mobile marketing menu (ui-integration: auth-admin-entry)`, () => {
    renderInApp(createElement(SiteHeader), { path, auth: makeAuthSessionValue({ status: 'signed-in', user: makeAuthUser({ permissions: ['auth.permission.read'] }) }) })
    for (const name of ['Our story', 'What matters', 'Our coast']) assert.equal(screen.queryByRole('link', { name }), null)
    assert.equal(screen.queryByRole('navigation', { name: 'Main navigation' }), null)
    assert.equal(document.getElementById('mobile-site-menu'), null)
    assert.equal(screen.queryByRole('button', { name: 'Open menu' }), null)
    assert.ok(document.getElementById('desktop-account-trigger'))
    assert.ok(document.getElementById('mobile-account-trigger'))
    assert.equal(screen.getByRole('link', { name: 'BLUEVERSE home' }).getAttribute('href'), '/')
  })
}

for (const surface of ['desktop', 'mobile']) {
  test(`WEB-MARINE-ENTRY-002 ${surface} account menu exposes Marine from Admin by permission and navigates to the registered route (ui-integration: marine-conditions)`, async () => {
    renderInApp(createElement(SiteHeader), { path: '/admin/permissions', auth: makeAuthSessionValue({ status: 'signed-in', user: makeAuthUser({ roles: [], permissions: ['marine.profile.read'] }) }) })
    await userEvent.setup().click(document.getElementById(`${surface}-account-trigger`))
    const menu = document.getElementById(`${surface}-account-menu`)
    const link = menu.querySelector('a[href="/marine/conditions"]')
    assert.ok(link)
    assert.match(link.textContent, /Marine & safety/)
    await userEvent.setup().click(link)
    assert.equal(screen.getByTestId('router-location').textContent, '/marine/conditions')
    assert.equal(document.getElementById(`${surface}-account-menu`), null)
  })
}

for (const permissions of [[], ['marine.profile.manage']]) {
  test(`WEB-MARINE-ENTRY-003 role names and ${JSON.stringify(permissions)} do not bypass the read grant (ui-integration: marine-conditions)`, async () => {
    const auth = makeAuthSessionValue({ status: 'signed-in', user: makeAuthUser({ roles: ['Admin'], permissions }) })
    renderInApp(createElement(SiteHeader), { path: '/admin/permissions', auth })
    await userEvent.setup().click(document.getElementById('desktop-account-trigger'))
    assert.equal(document.getElementById('desktop-account-menu').querySelector('a[href="/marine/conditions"]'), null)
  })
}

test('WEB-MARINE-ENTRY-004 sidebar exposes all three workflows after the current account has the read grant (ui-integration: marine-conditions)', () => {
  renderInApp(createElement(AccountAreaNavigation, { active: 'marine' }), { path: '/marine/conditions', auth: makeAuthSessionValue({ status: 'signed-in', user: makeAuthUser({ permissions: ['marine.profile.read'] }) }) })
  assert.ok(screen.getAllByRole('link', { name: 'Marine & safety' }).every(link => link.getAttribute('href') === '/marine/conditions'))
  for (const [name, path] of [['Coastal conditions', '/marine/conditions'], ['Coastal evidence', '/marine/history'], ['Activity safety limits', '/marine/safety-profiles']]) {
    assert.ok(screen.getAllByRole('link', { name }).every(link => link.getAttribute('href') === path))
  }
})

test('WEB-MARINE-ENTRY-005 only the home route retains the three home-section links (ui-integration: foundation-home)', () => {
  renderInApp(createElement(SiteHeader), { path: '/?shore=west' })
  const desktop = screen.getByRole('navigation', { name: 'Main navigation' })
  assert.deepEqual([...desktop.querySelectorAll('a')].map(link => link.getAttribute('href')), ['/#our-story', '/#what-matters', '/#our-coast'])
})
