import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { act, cleanup, closeWebTestServer, createElement, fireEvent, jsonResponse, loadWebModule, makeAuthSessionValue, makeAuthUser, renderInApp, resetTestBrowser, screen, userEvent, waitFor, within } from '../../../testSupport/reactTestHarness.js'

const { default: Logs } = await loadWebModule('/src/pages/operations/OperationsLogsPage.tsx')
const { default: Search } = await loadWebModule('/src/pages/operations/OperationsSearch.tsx')
const { default: Page } = await loadWebModule('/src/pages/operations/CoastalOperationsPage.tsx')
const originalFetch = globalThis.fetch
afterEach(() => { cleanup(); resetTestBrowser(); globalThis.fetch = originalFetch })
after(() => closeWebTestServer())
const id = '00000000-0000-4000-8000-000000000002'
const evidenceId = '00000000-0000-4000-8000-000000000003'
const assessment = { assessmentId: id, workflowId: id, title: 'Coastal review', objective: 'Inspect beach access', workflowStatus: 'DRAFT', targetType: 'ACTIVITY', targetId: id, sourceWorkflowId: null, version: 2, createdAt: '2026-10-01T08:00Z', updatedAt: '2026-10-01T09:00Z', periodStartsAt: '2026-10-01T08:00Z', periodEndsAt: '2026-10-01T10:00Z', componentDependencies: [], aiDependencyStatus: 'NOT_CONNECTED', aiDispatchOutcome: 'NOT_REQUESTED' }
function auth(grants) { return makeAuthSessionValue({ user: makeAuthUser({ permissions: grants }), status: 'signed-in' }) }

test('WEB-OPS-LOGS-001 logs paginate, reset all page sizes, switch category and expand scoped audit (ui-integration: coastal-operations-logs)', async () => {
  const calls = []
  globalThis.fetch = async (input) => {
    const url = new URL(input, 'http://localhost'); calls.push(url)
    if (url.pathname.endsWith('/audit')) return jsonResponse({ items: [{ auditId: id, action: 'CANCELLED', createdAt: '2026-10-01T09:00Z', actorId: id, correlationId: 'cancel-review' }], nextCursor: null })
    if (url.pathname.endsWith('/alerts')) return jsonResponse({ items: [{ ...assessment, description: 'Notice', severity: 'LOW', visibility: 'OPERATIONS', validFrom: assessment.periodStartsAt, validUntil: assessment.periodEndsAt, alertId: id, title: 'Withdrawn notice', lifecycle: 'WITHDRAWN', version: 2, createdAt: assessment.createdAt, updatedAt: assessment.updatedAt }], nextCursor: null })
    return jsonResponse({ items: [{ ...assessment, title: url.searchParams.has('cursor') ? 'Older review' : 'Cancelled review', workflowStatus: 'CANCELLED' }], nextCursor: url.searchParams.has('cursor') ? null : 'older-page' })
  }
  renderInApp(createElement(Logs), { path: '/operations/logs', auth: auth(['operations.audit.read', 'operations.assessment.read', 'operations.alert.manage']) })
  await screen.findByText('Cancelled review')
  assert.equal(calls[0].pathname, '/api/operations/logs/assessments'); assert.equal(calls[0].searchParams.get('pageSize'), '25')
  assert.ok(screen.getAllByRole('link', { name: 'Logs' }).length >= 1)
  await userEvent.click(screen.getByRole('button', { name: 'View activity' }))
  await screen.findByText('Cancelled the draft'); assert.equal(calls.at(-1).pathname, `/api/operations/assessments/${id}/audit`)
  await userEvent.click(screen.getByRole('button', { name: 'Next' }))
  await screen.findByText('Older review'); assert.equal(calls.at(-1).searchParams.get('cursor'), 'older-page')
  await userEvent.click(screen.getByRole('button', { name: 'Previous' }))
  await screen.findByText('Cancelled review'); assert.equal(calls.at(-1).searchParams.has('cursor'), false)
  const select = screen.getByLabelText('Assessments per page')
  assert.deepEqual([...select.options].map((option) => option.value), ['5', '10', '25', '50', '100'])
  for (const size of ['5', '10', '25', '50', '100']) {
    fireEvent.change(select, { target: { value: size } })
    await waitFor(() => assert.equal(calls.at(-1).searchParams.get('pageSize'), size))
    assert.equal(calls.at(-1).searchParams.has('cursor'), false)
  }
  await userEvent.click(screen.getByRole('button', { name: 'Alert logs' }))
  await screen.findByText('Withdrawn notice'); assert.equal(calls.at(-1).pathname, '/api/operations/logs/alerts')
  assert.equal(calls.at(-1).searchParams.has('cursor'), false)
  assert.ok(screen.getByPlaceholderText('Search the Alerts'))
})

test('WEB-OPS-LOGS-002 missing audit/read grants prevent log requests (ui-integration: coastal-operations-logs)', async () => {
  let calls = 0; globalThis.fetch = async () => { calls++; return jsonResponse({ items: [], nextCursor: null }) }
  for (const grants of [[], ['operations.audit.read'], ['operations.assessment.read']]) {
    renderInApp(createElement(Logs), { auth: auth(grants) }); await screen.findByRole('alert'); assert.equal(calls, 0); cleanup()
  }
})

test('WEB-OPS-SEARCH-001 500ms debounce restarts, applies once and reset cancels pending text (ui-integration: coastal-operations-assessment)', (context) => {
  const calls = []; renderInApp(createElement(Search, { kind: 'assessments', canManage: true, onChange: (query) => calls.push(query) }))
  context.mock.timers.enable({ apis: ['setTimeout'] })
  fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'coast' } })
  act(() => context.mock.timers.tick(400)); assert.deepEqual(calls, [])
  fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'coastal' } })
  act(() => context.mock.timers.tick(499)); assert.deepEqual(calls, [])
  act(() => context.mock.timers.tick(1)); assert.deepEqual(calls, [{ search: 'coastal' }])
  fireEvent.click(screen.getByRole('button', { name: 'Search', exact: true })); assert.equal(calls.length, 1)
  fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'pending' } })
  fireEvent.click(screen.getByRole('button', { name: 'Reset filters' })); act(() => context.mock.timers.tick(1000))
  assert.deepEqual(calls, [{ search: 'coastal' }, {}])
})

test('WEB-OPS-LOGS-003 pending search keeps cards and local spinner, ignores stale results and recovers from failure (ui-integration: coastal-operations-logs)', async () => {
  let finish; let fail = false
  globalThis.fetch = async (input) => {
    const url = new URL(input, 'http://localhost')
    if (url.searchParams.get('search') === 'pending') return new Promise((resolve) => { finish = resolve })
    return fail ? jsonResponse({}, 503) : jsonResponse({ items: [assessment], nextCursor: null })
  }
  renderInApp(createElement(Logs), { auth: auth(['operations.assessment.read', 'operations.audit.read']) })
  await screen.findByText('Coastal review'); fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'pending' } })
  await userEvent.click(screen.getByRole('button', { name: 'Search', exact: true }))
  await waitFor(() => assert.ok(finish)); assert.ok(screen.getByText('Coastal review')); assert.ok(screen.getByText('Searching records'))
  assert.equal(screen.queryByText('A moment by the water'), null)
  await userEvent.click(screen.getByRole('button', { name: 'Reset filters' }))
  await waitFor(() => assert.equal(screen.getByRole('status').textContent, ''))
  await act(async () => finish(jsonResponse({ items: [{ ...assessment, title: 'Stale result' }], nextCursor: null })))
  assert.equal(screen.queryByText('Stale result'), null)
  fail = true; await userEvent.click(screen.getByRole('button', { name: 'Drafts' })); await screen.findByRole('alert'); assert.ok(screen.getByText('Coastal review'))
  fail = false; await userEvent.click(screen.getByRole('button', { name: 'Retry logs' })); await waitFor(() => assert.equal(screen.queryByRole('alert'), null))
})

test('WEB-OPS-EVIDENCE-001 focused draft removal confirms red action with current version; publication hides all authored edits (ui-integration: coastal-operations-assessment)', async () => {
  let image = true; let version = 2; let deletion
  const grants = ['operations.assessment.read', 'operations.assessment.update', 'operations.assessment.delete', 'operations.evidence.upload', 'operations.evidence.read']
  globalThis.fetch = async (input, init = {}) => {
    const url = new URL(input, 'http://localhost')
    if (init.method === 'DELETE') { deletion = { url, init }; image = false; version++; return jsonResponse({ assessmentVersion: version }) }
    if (url.pathname.endsWith(`/${id}`)) return jsonResponse({ assessment: { ...assessment, version }, decisions: [], evidence: image ? [{ evidenceId, byteLength: 100, inspectionStatus: 'AVAILABLE' }] : [] })
    return jsonResponse({ items: [{ ...assessment, version }], nextCursor: null })
  }
  renderInApp(createElement(Page, { section: 'assessments' }), { auth: auth(grants) })
  await screen.findByText('Coastal review'); await userEvent.click(screen.getByRole('button', { name: /Coastal review/ }))
  await screen.findByRole('button', { name: 'Remove image' })
  assert.equal(screen.queryByRole('searchbox'), null); assert.equal(screen.queryByRole('heading', { name: 'Look after the places we share.' }), null)
  assert.ok(screen.getByRole('button', { name: 'Back to Assessments' })); assert.ok(document.querySelector('header')); assert.ok(document.querySelector('footer'))
  await userEvent.click(screen.getByRole('button', { name: 'Remove image' }))
  const dialog = screen.getByRole('dialog'); assert.ok(within(dialog).getByRole('button', { name: 'Confirm removal' }).className.includes('bg-red-700'))
  await userEvent.click(within(dialog).getByRole('button', { name: 'Keep image' })); assert.equal(deletion, undefined)
  await userEvent.click(screen.getByRole('button', { name: 'Remove image' })); await userEvent.click(screen.getByRole('button', { name: 'Confirm removal' }))
  await screen.findByText('No evidence images have been added.'); assert.equal(deletion.url.pathname, `/api/operations/assessments/${id}/evidence/${evidenceId}`)
  assert.deepEqual(JSON.parse(deletion.init.body), { expectedVersion: 2 }); assert.match(new Headers(deletion.init.headers).get('content-type'), /application\/json/)
  cleanup()
  globalThis.fetch = async (input) => new URL(input, 'http://localhost').pathname.endsWith(`/${id}`)
    ? jsonResponse({ assessment: { ...assessment, workflowStatus: 'SUBMITTED' }, decisions: [], evidence: [{ evidenceId, byteLength: 100, inspectionStatus: 'AVAILABLE' }] })
    : jsonResponse({ items: [{ ...assessment, workflowStatus: 'SUBMITTED' }], nextCursor: null })
  renderInApp(createElement(Page, { section: 'assessments' }), { auth: auth(grants) })
  await screen.findByText('Coastal review'); await userEvent.click(screen.getByRole('button', { name: /Coastal review/ })); await screen.findByRole('button', { name: 'View image' })
  for (const name of ['Remove image', 'Edit draft', 'Cancel draft']) assert.equal(screen.queryByRole('button', { name }), null)
  assert.equal(screen.queryByText('Add PNG evidence'), null)
})

for (const kind of ['assessments', 'alerts']) test(`WEB-OPS-WORKSPACE-${kind === 'assessments' ? '001' : '002'} ${kind} creation replaces hero and records, retains chrome and restores search on Back (ui-integration: coastal-operations-${kind === 'assessments' ? 'assessment' : 'alerts'})`, async () => {
  globalThis.fetch = async (input) => input === '/api/operations/form-options'
    ? jsonResponse({ timeZones: [{ id: 'Etc/UTC', country: 'Worldwide', location: 'UTC', rulesAvailable: true }], targets: { status: 'NOT_CONNECTED', items: [] }, plans: { status: 'NOT_CONNECTED', items: [] }, assessments: [] })
    : jsonResponse({ items: [], nextCursor: null })
  renderInApp(createElement(Page, { section: kind }), { auth: auth(['operations.assessment.create', 'operations.assessment.read', 'operations.alert.create', 'operations.alert.read']) })
  await screen.findByPlaceholderText(kind === 'assessments' ? 'Search the Assessments' : 'Search the Alerts')
  const hero = document.querySelector('main section img')
  assert.ok(hero.getAttribute('src').includes(kind === 'assessments' ? 'assessment-hero.png' : 'alerts-hero.png'))
  fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'Saved filter' } })
  await userEvent.click(screen.getByRole('button', { name: 'Search', exact: true }))
  const trigger = kind === 'assessments' ? 'New assessment' : 'Prepare an advisory'
  await userEvent.click(screen.getByRole('button', { name: trigger }))
  await screen.findByRole('button', { name: 'Save draft' })
  assert.equal(screen.queryByRole('searchbox'), null); assert.equal(document.querySelector('main section img'), null)
  assert.ok(document.querySelector('header')); assert.ok(document.querySelector('footer')); assert.ok(screen.getByRole('button', { name: `Back to ${kind === 'assessments' ? 'Assessments' : 'Alerts'}` }))
  assert.ok(screen.getByRole('button', { name: 'Cancel', exact: true }).className.includes('text-red-800'))
  await userEvent.click(screen.getByRole('button', { name: `Back to ${kind === 'assessments' ? 'Assessments' : 'Alerts'}` }))
  assert.equal(screen.getByRole('searchbox').value, 'Saved filter')
  await waitFor(() => assert.equal(document.activeElement, screen.getByRole('button', { name: trigger })))
})
