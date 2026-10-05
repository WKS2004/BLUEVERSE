import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { cleanup, closeWebTestServer, createElement, jsonResponse, loadWebModule, makeAuthSessionValue, makeAuthUser, renderInApp, resetTestBrowser, screen, userEvent } from '../../../testSupport/reactTestHarness.js'

const { default: Page } = await loadWebModule('/src/pages/operations/CoastalOperationsPage.tsx')
const { default: Logs } = await loadWebModule('/src/pages/operations/OperationsLogsPage.tsx')
const { default: Activity } = await loadWebModule('/src/pages/operations/OperationsActivity.tsx')
const originalFetch = globalThis.fetch
afterEach(() => { cleanup(); resetTestBrowser(); globalThis.fetch = originalFetch })
after(() => closeWebTestServer())

const id = '00000000-0000-4000-8000-000000000002'
const assessment = { assessmentId: id, workflowId: id, title: 'Review beach access', objective: 'Inspect dunes', workflowStatus: 'DRAFT', targetType: 'ACTIVITY', targetId: id, sourceWorkflowId: null, version: 2, createdAt: '2026-10-01T08:00Z', updatedAt: '2026-10-01T09:00Z', periodStartsAt: '2026-10-01T08:00Z', periodEndsAt: '2026-10-01T10:00Z', componentDependencies: [], aiDependencyStatus: 'NOT_CONNECTED', aiDispatchOutcome: 'NOT_REQUESTED' }
const alert = { ...assessment, alertId: id, description: 'Stay on the marked path', severity: 'LOW', visibility: 'PUBLIC', lifecycle: 'PROPOSED', validFrom: assessment.periodStartsAt, validUntil: assessment.periodEndsAt }
const options = { timeZones: [{ id: 'Etc/UTC', country: 'Worldwide', location: 'UTC', rulesAvailable: true }], targets: { status: 'NOT_CONNECTED', items: [] }, plans: { status: 'NOT_CONNECTED', items: [] }, assessments: [] }
const auth = (permissions) => makeAuthSessionValue({ status: 'signed-in', user: makeAuthUser({ permissions }) })
function respond(input) {
  const url = new URL(input, 'http://localhost')
  if (url.pathname.endsWith('/form-options')) return jsonResponse(options)
  if (url.pathname.endsWith('/audit')) return jsonResponse({ items: [{ auditId: 'audit-1', action: 'UPDATED', summary: 'Updated the advisory', actorId: id, actorName: 'Coastal Steward', actorRoles: ['Field Officer'], correlationId: 'correlation-42', createdAt: alert.updatedAt, changes: [] }], nextCursor: null })
  if (url.pathname.endsWith(`/${id}`)) return jsonResponse({ assessment, decisions: [], evidence: [] })
  return jsonResponse({ items: [url.pathname.includes('/alerts') ? alert : assessment], nextCursor: null })
}

test('WEB-OPS-UX-001 sticky section tabs align beneath the responsive header and start directional transitions', async () => {
  globalThis.fetch = async (input) => respond(input)
  renderInApp(createElement(Page, { section: 'assessments' }), { auth: auth(['operations.assessment.read', 'operations.alert.read']) })
  await screen.findByText('Review beach access')
  const tabs = screen.getByRole('navigation', { name: 'Coastal record sections' })
  assert.match(tabs.className, /sticky top-\[76px\]/)
  const main = tabs.closest('main')
  assert.equal(document.querySelector('header').nextElementSibling, main)
  assert.equal(main.firstElementChild, tabs)
  const accountNav = main.children[1]
  assert.equal(accountNav.tagName, 'ASIDE')
  assert.equal(accountNav.getAttribute('aria-label'), 'Account navigation')
  assert.match(accountNav.className, /order-2/)
  assert.match(accountNav.className, /lg:row-span-2/)
  assert.match(main.children[2].className, /order-3/)
  assert.ok(accountNav)
  assert.match(accountNav.className, /lg:sticky lg:top-\[76px\]/)
  assert.match(accountNav.className, /lg:h-\[calc\(100dvh-76px\)\]/)
  assert.match(accountNav.className, /lg:max-h-\[calc\(100dvh-76px\)\]/)
  assert.match(tabs.className, /lg:col-start-2 lg:row-start-1/)
  assert.equal(document.querySelector('.coastal-section-panel').style.viewTransitionName, '')
  await userEvent.click([...tabs.querySelectorAll('a')].find((link) => link.textContent.trim() === 'Alerts'))
  assert.equal(document.documentElement.dataset.coastalDirection, 'forward')
})

test('WEB-OPS-UX-002 Alert fields stay read-only until Edit draft, and the edit view omits activity', async () => {
  globalThis.fetch = async (input) => {
    const url = new URL(input, 'http://localhost')
    if (url.pathname.endsWith('/form-options')) return jsonResponse(options)
    if (url.pathname.endsWith('/audit')) return jsonResponse({ items: [], nextCursor: null })
    if (url.pathname.endsWith(`/${id}`)) return jsonResponse(alert)
    return jsonResponse({ items: [alert], nextCursor: null })
  }
  renderInApp(createElement(Page, { section: 'alerts' }), { auth: auth(['operations.alert.read', 'operations.alert.update', 'operations.audit.read']) })
  await screen.findByText('Stay on the marked path')
  assert.ok(screen.getByRole('button', { name: 'Edit draft' }))
  assert.equal(screen.queryByLabelText('Advisory title'), null)
  await userEvent.click(screen.getByRole('button', { name: /Open advisory details/ }))
  await screen.findByText(/Review the audience, dates and coastal link here/)
  assert.ok(screen.getByRole('button', { name: 'Edit draft' }))
  assert.equal(screen.queryByRole('button', { name: 'Publish advisory' }), null)
  await userEvent.click(screen.getByRole('button', { name: 'Edit draft' }))
  await screen.findByRole('button', { name: 'Update draft' })
  assert.equal(screen.queryByRole('region', { name: 'Record activity' }), null)
})

test('WEB-OPS-UX-003 Logs restore Alert logs from the URL and keep opened records read-only', async () => {
  const requests = []
  globalThis.fetch = async (input) => { requests.push(new URL(input, 'http://localhost')); return respond(input) }
  renderInApp(createElement(Logs), { path: '/operations/logs?kind=alerts', auth: auth(['operations.audit.read', 'operations.assessment.read', 'operations.alert.read', 'operations.alert.update']) })
  await screen.findByText('Stay on the marked path')
  assert.ok(screen.getByRole('button', { name: 'Alert logs' }).getAttribute('aria-pressed') === 'true')
  const tabs = screen.getByLabelText('Log record category')
  assert.match(tabs.className, /sticky top-\[76px\]/)
  const main = tabs.closest('main')
  assert.equal(document.querySelector('header').nextElementSibling, main)
  assert.equal(main.firstElementChild, tabs)
  const accountNav = main.children[1]
  assert.equal(accountNav.tagName, 'ASIDE')
  assert.equal(accountNav.getAttribute('aria-label'), 'Account navigation')
  assert.match(accountNav.className, /order-2/)
  assert.match(accountNav.className, /lg:row-span-2/)
  assert.match(main.children[2].className, /order-3/)
  assert.ok(accountNav)
  assert.match(accountNav.className, /lg:sticky lg:top-\[76px\]/)
  assert.match(accountNav.className, /lg:h-\[calc\(100dvh-76px\)\]/)
  assert.match(accountNav.className, /lg:max-h-\[calc\(100dvh-76px\)\]/)
  assert.match(tabs.className, /lg:col-start-2 lg:row-start-1/)
  assert.ok(requests.some((url) => url.pathname.endsWith('/logs/alerts')))
  assert.equal(screen.getByRole('region', { name: 'Coastal Operations logs' }).dataset.coastalDirection, undefined)
  await userEvent.click(screen.getByRole('button', { name: 'Assessment logs' }))
  await screen.findByText('Review beach access')
  assert.equal(screen.getByRole('region', { name: 'Coastal Operations logs' }).dataset.coastalDirection, 'back')
  await userEvent.click(screen.getByRole('button', { name: 'Alert logs' }))
  await screen.findByText('Stay on the marked path')
  assert.equal(screen.getByRole('region', { name: 'Coastal Operations logs' }).dataset.coastalDirection, 'forward')
  await userEvent.click(screen.getByRole('button', { name: /Open advisory details/ }))
  await new Promise((resolve) => setTimeout(resolve, 50))
  await screen.findByText(/shown read-only alongside its activity history/)
  for (const name of ['Edit draft', 'Withdraw draft', 'Publish advisory', 'Resolve advisory']) assert.equal(screen.queryByRole('button', { name }), null)
  assert.ok(screen.getByRole('button', { name: 'Back to Logs' }))
})

test('WEB-OPS-UX-004 correlation references are available only as a styled Logs detail', async () => {
  globalThis.fetch = async () => jsonResponse({ items: [{ auditId: 'audit-1', action: 'UPDATED', actorId: id, actorName: 'Coastal Steward', actorRoles: ['Field Officer'], correlationId: 'correlation-42', createdAt: alert.updatedAt, changes: [] }], nextCursor: null })
  renderInApp(createElement(Activity, { kind: 'alert', id, revision: 1 }))
  await screen.findByText('Updated the record')
  assert.equal(screen.queryByText('Reference details'), null)
  cleanup()
  renderInApp(createElement(Activity, { kind: 'alert', id, revision: 1, showReference: true }))
  await screen.findByText('Updated the record')
  assert.ok(screen.getByText('Reference details'))
  assert.equal(screen.queryByText(new RegExp(`Actor: ${id}`)), null)
  assert.equal(screen.queryByText('Coastal Steward · Field Officer'), null)
  await userEvent.click(screen.getByText('Reference details'))
  assert.ok(screen.getByText('correlation-42'))
})
