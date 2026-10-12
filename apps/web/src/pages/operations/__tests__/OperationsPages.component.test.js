import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { cleanup, closeWebTestServer, createElement, jsonResponse, loadWebModule, makeAuthSessionValue, makeAuthUser, renderInApp, resetTestBrowser, screen, userEvent, waitFor } from '../../../testSupport/reactTestHarness.js'

const { default: Page } = await loadWebModule('/src/pages/operations/CoastalOperationsPage.tsx')
const originalFetch = globalThis.fetch
afterEach(() => { cleanup(); resetTestBrowser(); globalThis.fetch = originalFetch })
after(() => closeWebTestServer())
const id = '00000000-0000-4000-8000-000000000002'
function renderPage(section, permissions) {
  renderInApp(createElement(Page, { section }), { path: section === 'assessments' ? '/operations/assessments' : '/operations/alerts', auth: makeAuthSessionValue({ user: makeAuthUser({ permissions }), status: 'signed-in' }) })
}
function mockList() {
  const calls = []
  globalThis.fetch = async (input) => { calls.push(new URL(input, 'http://localhost')); return jsonResponse({ items: [], nextCursor: null }) }
  return calls
}

test('WEB-OPS-PAGES-001 dedicated assessment page loads only assessments and sends draft ownership filters (ui-integration: coastal-operations-assessment)', async () => {
  const calls = mockList()
  renderPage('assessments', ['operations.assessment.read', 'operations.assessment.queue.read', 'operations.alert.manage'])
  await screen.findByText('No assessments to show yet')
  assert.ok(screen.getByRole('searchbox', { name: 'Search records' }))
  assert.equal(screen.queryByText('Useful updates for the coast'), null)
  assert.equal(calls.some((call) => call.pathname.includes('/alerts')), false)
  await userEvent.click(screen.getByRole('button', { name: 'Drafts' }))
  await waitFor(() => assert.equal(calls.at(-1).searchParams.get('onlyMine'), 'true'))
  assert.equal(calls.at(-1).searchParams.get('workflowStatus'), 'DRAFT')
  await userEvent.type(screen.getByRole('searchbox'), 'shoreline')
  await userEvent.click(screen.getByRole('button', { name: 'Search', exact: true }))
  await waitFor(() => assert.equal(calls.at(-1).searchParams.get('search'), 'shoreline'))
  await userEvent.click(screen.getByRole('button', { name: 'Reset filters' }))
  await waitFor(() => assert.equal(calls.at(-1).searchParams.has('search'), false))
  assert.equal(calls.at(-1).searchParams.has('cursor'), false)
})

test('WEB-OPS-PAGES-002 public alert readers cannot see draft/history filters or mutation actions (ui-integration: coastal-operations-alerts)', async () => {
  const calls = mockList()
  renderPage('alerts', ['operations.alert.read'])
  await screen.findByText('No advisories to show')
  assert.ok(screen.getByRole('heading', { name: 'Clear updates. Safer coastal days.' }))
  for (const name of ['Drafts', 'History', 'Prepare an advisory', 'New assessment']) assert.equal(screen.queryByRole('button', { name }), null)
  await userEvent.click(screen.getByRole('button', { name: 'Active', exact: true }))
  await waitFor(() => assert.equal(calls.at(-1).searchParams.get('lifecycle'), 'ACTIVE'))
  assert.equal(calls.some((call) => call.pathname.includes('/assessments')), false)
})

test('WEB-OPS-PAGES-003 publish-only grant shows publication and scoped activity without CRUD (ui-integration: coastal-operations-alerts)', async () => {
  const calls = []
  globalThis.fetch = async (input) => {
    const url = new URL(input, 'http://localhost'); calls.push(url)
    if (url.pathname.endsWith('/audit')) return jsonResponse({ items: [{ auditId: 'audit-1', action: 'CREATED', actorId: id, correlationId: 'reference-1', createdAt: '2026-10-01T08:00:00Z' }], nextCursor: null })
    return jsonResponse({ items: [{ alertId: id, targetType: 'ACTIVITY', targetId: id, title: 'Shoreline notice', description: 'Use the marked path.', severity: 'LOW', visibility: 'PUBLIC', lifecycle: 'PROPOSED', validFrom: '2026-10-01T08:00:00Z', validUntil: '2026-10-01T10:00:00Z', version: 1 }], nextCursor: null })
  }
  renderPage('alerts', ['operations.alert.publish', 'operations.audit.read'])
  await screen.findByText('Shoreline notice')
  assert.ok(screen.getByRole('button', { name: 'Publish advisory' }))
  for (const name of ['Edit draft', 'Withdraw draft', 'Resolve advisory', 'Prepare an advisory']) assert.equal(screen.queryByRole('button', { name }), null)
  await userEvent.click(screen.getByRole('button', { name: 'View activity' }))
  await screen.findByText('Created a draft')
  assert.equal(calls.at(-1).pathname, `/api/operations/alerts/${id}/audit`)
  assert.ok(screen.getByText('Record activity'))
})

test('WEB-OPS-PAGES-004 changing filters drops prior cursor and prevents stale pagination results (ui-integration: coastal-operations-alerts)', async () => {
  const calls = []
  let completeMore
  globalThis.fetch = async (input) => {
    const url = new URL(input, 'http://localhost'); calls.push(url)
    if (url.searchParams.has('cursor')) return new Promise((resolve) => { completeMore = resolve })
    return jsonResponse({ items: [], nextCursor: url.searchParams.has('search') ? null : 'next-1' })
  }
  renderPage('alerts', ['operations.alert.read'])
  await screen.findByRole('button', { name: 'Next', exact: true })
  await userEvent.click(screen.getByRole('button', { name: 'Next', exact: true }))
  await userEvent.type(screen.getByRole('searchbox'), 'rain')
  await userEvent.click(screen.getByRole('button', { name: 'Search', exact: true }))
  await waitFor(() => assert.equal(calls.at(-1).searchParams.get('search'), 'rain'))
  completeMore(jsonResponse({ items: [{ alertId: id, title: 'Stale notice', targetType: 'ACTIVITY', lifecycle: 'ACTIVE', description: 'stale', visibility: 'PUBLIC', severity: 'LOW' }], nextCursor: null }))
  await screen.findByText('No advisories to show')
  assert.equal(screen.queryByText('Stale notice'), null)
  assert.equal(calls.at(-1).searchParams.has('cursor'), false)
})
