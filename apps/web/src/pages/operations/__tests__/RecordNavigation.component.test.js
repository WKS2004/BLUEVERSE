import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { cleanup, closeWebTestServer, createElement, fireEvent, jsonResponse, loadWebModule, makeAuthSessionValue, makeAuthUser, renderInApp, resetTestBrowser, screen, userEvent, waitFor } from '../../../testSupport/reactTestHarness.js'
const { default: Page } = await loadWebModule('/src/pages/operations/CoastalOperationsPage.tsx')
const { default: Logs } = await loadWebModule('/src/pages/operations/OperationsLogsPage.tsx')
const { default: Activity } = await loadWebModule('/src/pages/operations/OperationsActivity.tsx')
const originalFetch = globalThis.fetch
afterEach(() => { cleanup(); resetTestBrowser(); globalThis.fetch = originalFetch })
after(() => closeWebTestServer())
const id = '00000000-0000-4000-8000-000000000002'
const record = { assessmentId: id, workflowId: id, title: 'Review beach access', objective: 'Inspect dunes', workflowStatus: 'DRAFT', targetType: 'ACTIVITY', targetId: id, sourceWorkflowId: null, version: 2, createdAt: '2026-10-01T08:00Z', updatedAt: '2026-10-01T09:00Z', periodStartsAt: '2026-10-01T08:00Z', periodEndsAt: '2026-10-01T10:00Z', componentDependencies: [], aiDependencyStatus: 'NOT_CONNECTED', aiDispatchOutcome: 'NOT_REQUESTED' }
const alert = { ...record, alertId: id, description: 'Stay on the marked path', severity: 'LOW', visibility: 'PUBLIC', lifecycle: 'PROPOSED', validFrom: record.periodStartsAt, validUntil: record.periodEndsAt }
const options = { timeZones: [{ id: 'Etc/UTC', country: 'Worldwide', location: 'UTC', rulesAvailable: true }], targets: { status: 'NOT_CONNECTED', items: [] }, plans: { status: 'NOT_CONNECTED', items: [] }, assessments: [] }
function auth(grants) { return makeAuthSessionValue({ status: 'signed-in', user: makeAuthUser({ permissions: grants }) }) }
function fixture(input, entity = record) {
  const url = new URL(input, 'http://localhost')
  if (url.pathname.endsWith('/form-options')) return jsonResponse(options)
  if (url.pathname.endsWith('/audit')) return jsonResponse({ items: [], nextCursor: null })
  if (url.pathname.endsWith('/' + id)) return jsonResponse({ assessment: entity, decisions: [], evidence: [] })
  return jsonResponse({ items: [url.pathname.endsWith('/alerts') ? alert : entity], nextCursor: null })
}
for (const kind of ['assessments', 'alerts']) test(`WEB-OPS-NAV-001 ${kind} refresh restores create and current draft edit (ui-integration: coastal-operations-${kind === 'assessments' ? 'assessment' : 'alerts'})`, async () => {
  globalThis.fetch = async (input) => fixture(input)
  const session = auth([`operations.${kind === 'assessments' ? 'assessment' : 'alert'}.create`, `operations.${kind === 'assessments' ? 'assessment' : 'alert'}.read`, `operations.${kind === 'assessments' ? 'assessment' : 'alert'}.update`])
  for (const view of ['create', 'edit']) {
    renderInApp(createElement(Page, { section: kind }), { path: `/operations/${kind}?view=${view}${view === 'edit' ? '&id=' + id : ''}`, auth: session })
    await screen.findByRole('button', { name: view === 'edit' ? 'Update draft' : 'Save draft' })
    assert.equal(screen.queryByRole('searchbox'), null); assert.ok(document.querySelector('header')); assert.ok(document.querySelector('footer'))
    if (view === 'edit') assert.equal(screen.getByLabelText(kind === 'assessments' ? 'Assessment title' : 'Advisory title').value, record.title)
    await userEvent.click(screen.getByRole('button', { name: `Back to ${kind === 'assessments' ? 'Assessments' : 'Alerts'}` }))
    await screen.findByRole('searchbox'); cleanup()
  }
})
test('WEB-OPS-NAV-002 published missing malformed and denied edit links show recovery without mutation (ui-integration: coastal-operations-assessment)', async () => {
  const calls = []
  for (const scenario of ['published', 'missing', 'malformed', 'denied']) {
    globalThis.fetch = async (input, init) => { calls.push(init?.method || 'GET'); return String(input).endsWith('/' + id) && scenario === 'missing' ? jsonResponse({}, 404) : fixture(input, { ...record, workflowStatus: scenario === 'published' ? 'SUBMITTED' : 'DRAFT' }) }
    renderInApp(createElement(Page, { section: 'assessments' }), { path: `/operations/assessments?view=edit&id=${scenario === 'malformed' ? 'invalid' : id}`, auth: auth(scenario === 'denied' ? ['operations.assessment.read'] : ['operations.assessment.read', 'operations.assessment.update']) })
    await screen.findByRole('alert'); assert.equal(screen.queryByRole('button', { name: 'Update draft' }), null)
    assert.ok(screen.getByRole('button', { name: 'Back to Assessments' })); cleanup()
  }
  assert.ok(calls.every((method) => method === 'GET'))
})
for (const kind of ['assessments', 'alerts']) test(`WEB-OPS-NAV-003 ${kind} use shared cards and real cursor pages for every allowed size (ui-integration: coastal-operations-${kind === 'assessments' ? 'assessment' : 'alerts'})`, async () => {
  const calls = []
  globalThis.fetch = async (input) => { const url = new URL(input, 'http://localhost'); calls.push(url); return jsonResponse({ items: [{ ...(kind === 'assessments' ? record : alert), title: url.searchParams.has('cursor') ? 'Older page' : 'First page' }], nextCursor: url.searchParams.has('cursor') ? null : 'next' }) }
  renderInApp(createElement(Page, { section: kind }), { auth: auth([`operations.${kind === 'assessments' ? 'assessment' : 'alert'}.read`, 'operations.audit.read']) })
  await screen.findByText('First page'); assert.equal(calls[0].searchParams.get('pageSize'), '25')
  assert.ok(screen.getByRole('navigation', { name: 'Coastal record sections' }).className.includes('sticky'))
  assert.ok(document.querySelector('article.bg-white')); assert.ok(screen.getByText(/Updated Oct/)); assert.ok(screen.getByRole('button', { name: 'View activity' }))
  await userEvent.click(screen.getByRole('button', { name: 'Next', exact: true })); await screen.findByText('Older page'); assert.equal(screen.queryByText('First page'), null)
  await userEvent.click(screen.getByRole('button', { name: 'Previous', exact: true })); await screen.findByText('First page')
  const select = screen.getByLabelText(`${kind === 'assessments' ? 'Assessments' : 'Alerts'} per page`)
  for (const size of ['5','10','25','50','100']) { fireEvent.change(select, { target: { value: size } }); await waitFor(() => assert.equal(calls.at(-1).searchParams.get('pageSize'), size)); assert.equal(calls.at(-1).searchParams.has('cursor'), false) }
})
test('WEB-OPS-NAV-004 Logs cards open the full assessment and return to Logs with shared chrome (ui-integration: coastal-operations-logs)', async () => {
  globalThis.fetch = async (input) => fixture(input)
  renderInApp(createElement(Logs), { path: '/operations/logs', auth: auth(['operations.audit.read', 'operations.assessment.read']) })
  await screen.findByText(record.title)
  await userEvent.click(screen.getByRole('button', { name: /Review beach access/ }))
  await screen.findByText('ASSESSMENT DETAILS'); assert.equal(screen.queryByRole('searchbox'), null)
  assert.ok(screen.getByRole('button', { name: 'Back to Logs' })); assert.ok(document.querySelector('footer'))
  await userEvent.click(screen.getByRole('button', { name: 'Back to Logs' })); await screen.findByRole('searchbox')
  assert.ok(screen.getByRole('button', { name: 'Assessment logs' }))
})
test('WEB-OPS-AUDIT-DETAIL-001 activity explains exact values actor roles time and honest legacy gaps (ui-integration: coastal-operations-assessment)', async () => {
  globalThis.fetch = async () => jsonResponse({ items: [{ auditId: 'new', action: 'UPDATED', summary: 'Updated the draft (assessment)', actorName: 'Coastal Steward', actorRoles: ['Field Officer'], recordTitle: 'Updated title', actorId: id, correlationId: 'edit', createdAt: record.updatedAt, changes: [{ field: 'Title', before: 'Original title', after: 'Updated title' }] }, { auditId: 'old', action: 'CREATED', actorId: id, correlationId: 'old', createdAt: record.createdAt }], nextCursor: null })
  renderInApp(createElement(Activity, { kind: 'assessment', id, revision: 1 }))
  await screen.findByText('Updated the draft (assessment)'); assert.ok(screen.getByText('Coastal Steward · Field Officer'))
  assert.ok(screen.getByText(/Original title/)); assert.ok(screen.getByText('Role not recorded', { exact: false }))
  assert.ok(screen.getByText('Field details were not recorded for this event.')); assert.equal(document.querySelector('time').getAttribute('dateTime'), record.updatedAt)
})

test('WEB-OPS-NAV-005 Logs assessment details stay read-only and return to Logs (ui-integration: coastal-operations-logs)', async () => {
  globalThis.fetch = async (input) => fixture(input)
  renderInApp(createElement(Logs), { path: '/operations/logs', auth: auth(['operations.audit.read', 'operations.assessment.read', 'operations.assessment.update']) })
  await screen.findByText(record.title)
  await userEvent.click(screen.getByRole('button', { name: /Review beach access/ }))
  await screen.findByText('ASSESSMENT DETAILS')
  assert.ok(screen.getByText(/retained assessment is shown read-only/))
  for (const name of ['Edit draft', 'Publish assessment', 'Cancel draft']) assert.equal(screen.queryByRole('button', { name, exact: true }), null)
  assert.equal(screen.queryByLabelText('Assessment title'), null)
  assert.ok(document.querySelector('header')); assert.ok(document.querySelector('footer'))
  await userEvent.click(screen.getByRole('button', { name: 'Back to Logs' }))
  await screen.findByRole('searchbox')
  assert.ok(screen.getByRole('button', { name: 'Assessment logs' }))
})
