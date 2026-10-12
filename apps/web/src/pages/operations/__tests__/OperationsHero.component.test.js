import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { cleanup, closeWebTestServer, createElement, jsonResponse, loadWebModule, makeAuthSessionValue, makeAuthUser, renderInApp, resetTestBrowser, screen, userEvent, within } from '../../../testSupport/reactTestHarness.js'

const { default: Page } = await loadWebModule('/src/pages/operations/CoastalOperationsPage.tsx')
const { default: Logs } = await loadWebModule('/src/pages/operations/OperationsLogsPage.tsx')
const originalFetch = globalThis.fetch
afterEach(() => { cleanup(); resetTestBrowser(); globalThis.fetch = originalFetch })
after(() => closeWebTestServer())
const auth = makeAuthSessionValue({ status: 'signed-in', user: makeAuthUser({ permissions: ['operations.assessment.read', 'operations.alert.read', 'operations.audit.read'] }) })

for (const [index, section, heading, label, asset] of [
  ['001', 'assessments', 'Look after the places we share.', 'COASTAL ASSESSMENTS', 'assessment-hero.png'],
  ['002', 'alerts', 'Clear updates. Safer coastal days.', 'COASTAL ADVISORIES & ALERTS', 'alerts-hero.png'],
]) test(`WEB-OPS-HERO-${index} ${section} presents a page introduction and photo above readable copy while retaining section tabs (ui-integration: coastal-operations-${section === 'assessments' ? 'assessment' : 'alerts'})`, async () => {
  globalThis.fetch = async () => jsonResponse({ items: [], nextCursor: null })
  renderInApp(createElement(Page, { section }), { auth })
  await screen.findByRole('heading', { name: section === 'assessments' ? 'No assessments to show yet' : 'No advisories to show' })
  const introduction = screen.getByRole('heading', { name: 'Manage Coastal Operations', level: 1 })
  const title = screen.getByRole('heading', { name: heading, level: 2 })
  const hero = title.closest('section')
  const image = within(hero).getByRole('img')
  assert.ok(image.getAttribute('src').includes(asset))
  assert.ok(image.getAttribute('alt').length > 20)
  assert.ok(introduction.compareDocumentPosition(hero) & 4)
  assert.ok(image.compareDocumentPosition(title) & 4)
  assert.ok(hero.compareDocumentPosition(screen.getByRole('searchbox')) & 4)
  assert.ok(within(hero).getByText(label))
  assert.ok(within(hero).getByText('BLUEVERSE · COASTAL OPERATIONS'))
  assert.equal(image.parentElement.contains(title), false)
  const tabs = screen.getByRole('navigation', { name: 'Coastal record sections' })
  const selected = within(tabs).getByRole('link', { name: section === 'assessments' ? 'Assessments' : 'Alerts' })
  assert.equal(selected.getAttribute('aria-current'), 'page')
  assert.match(selected.className, /rounded-full/)
  assert.match(selected.className, /bg-coast-deep text-white/)
})

test('WEB-OPS-HERO-003 Logs hero describes the selected category without changing its read-only empty records workflow (ui-integration: coastal-operations-logs)', async () => {
  const requests = []
  globalThis.fetch = async (input, init) => {
    requests.push({ url: new URL(input, 'http://localhost'), method: init?.method || 'GET' })
    return jsonResponse({ items: [], nextCursor: null })
  }
  renderInApp(createElement(Logs), { path: '/operations/logs?kind=assessments', auth })
  await screen.findByRole('heading', { name: 'No assessments to show yet' })
  assert.ok(screen.getByRole('heading', { name: 'Coastal Operations Logs', level: 1 }))
  const hero = screen.getByRole('heading', { name: 'Every coastal decision, accounted for.', level: 2 }).closest('section')
  assert.ok(within(hero).getByText('COASTAL ASSESSMENT LOGS'))
  assert.ok(within(hero).getByRole('img').getAttribute('src').includes('operations-logs-hero.webp'))
  await userEvent.click(screen.getByRole('button', { name: 'Alert logs' }))
  await screen.findByRole('heading', { name: 'No advisories to show' })
  assert.ok(within(hero).getByText('COASTAL ALERT LOGS'))
  assert.equal(within(hero).queryByText('COASTAL ASSESSMENT LOGS'), null)
  assert.equal(screen.getByRole('button', { name: 'Alert logs' }).getAttribute('aria-pressed'), 'true')
  assert.ok(requests.some(({ url }) => url.pathname === '/api/operations/logs/alerts'))
  assert.ok(requests.every(({ url, method }) => url.pathname.startsWith('/api/') && method === 'GET'))
  assert.equal(screen.queryByRole('button', { name: 'New assessment' }), null)
  assert.equal(screen.queryByRole('button', { name: 'Prepare an advisory' }), null)
})
