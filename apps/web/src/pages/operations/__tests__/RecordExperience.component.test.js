import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { cleanup, closeWebTestServer, createElement, fireEvent, jsonResponse, loadWebModule, makeAuthSessionValue, makeAuthUser, renderInApp, resetTestBrowser, screen, userEvent, waitFor } from '../../../testSupport/reactTestHarness.js'

const { AssessmentDraftForm, AlertDraftForm } = await loadWebModule('/src/pages/operations/OperationsDraftForms.tsx')
const { default: OperationsSearch } = await loadWebModule('/src/pages/operations/OperationsSearch.tsx')
const { default: Page } = await loadWebModule('/src/pages/operations/CoastalOperationsPage.tsx')
const originalFetch = globalThis.fetch
const recordId = '00000000-0000-4000-8000-000000000011'
const planId = '00000000-0000-4000-8000-000000000012'
const zones = [{ id: 'Etc/UTC', country: 'Worldwide', location: 'UTC', rulesAvailable: true }, { id: 'Asia/Colombo', country: 'Sri Lanka', location: 'Colombo', rulesAvailable: true }]
afterEach(() => { cleanup(); resetTestBrowser(); globalThis.fetch = originalFetch })
after(async () => closeWebTestServer())

test('WEB-OPS-RECORD-001 normal title search defaults to all types; IDs require Advanced search (ui-integration: coastal-operations-assessment)', async () => {
  const queries = []
  renderInApp(createElement(OperationsSearch, { kind: 'assessments', canManage: true, onChange: (query) => queries.push(query) }))
  assert.equal(screen.getByLabelText('Coastal record type').value, '')
  assert.equal(screen.getByLabelText('Assessment or alert ID').disabled, true)
  fireEvent.change(screen.getByLabelText('Search records'), { target: { value: 'Rain review' } })
  await userEvent.click(screen.getByRole('button', { name: 'Search', exact: true }))
  assert.equal(queries.at(-1).search, 'Rain review'); assert.equal(queries.at(-1).recordId, undefined)
  await userEvent.click(screen.getByText('Advanced search'))
  await waitFor(() => assert.equal(screen.getByLabelText('Assessment or alert ID').disabled, false))
  fireEvent.change(screen.getByLabelText('Assessment or alert ID'), { target: { value: recordId } })
  await userEvent.click(screen.getByRole('button', { name: 'Search', exact: true }))
  assert.equal(queries.at(-1).recordId, recordId)
  await userEvent.click(screen.getByRole('button', { name: 'Reset filters' }))
  assert.deepEqual(queries.at(-1), {})
})

test('WEB-OPS-RECORD-002 drafts use named targets/plans and one database zone with local times (ui-integration: coastal-operations-assessment)', async () => {
  let body; let saved
  globalThis.fetch = async (path, init = {}) => path === '/api/operations/form-options' ? jsonResponse({ timeZones: zones, targets: { status: 'AVAILABLE', items: [{ id: recordId, title: 'Bentota beach', targetType: 'DESTINATION' }] }, plans: { status: 'AVAILABLE', items: [{ id: planId, title: 'Bentota coastal day' }] }, assessments: [] }) : (body = JSON.parse(init.body), jsonResponse({ assessmentId: recordId, ...body }, 201))
  renderInApp(createElement(AssessmentDraftForm, { existing: null, onCancel() {}, onSaved(result) { saved = result } }))
  await screen.findByRole('option', { name: 'Bentota coastal day' })
  assert.equal(screen.queryByLabelText('Coastal record ID'), null); assert.equal(screen.queryByLabelText(/offset at/), null)
  assert.equal(screen.getAllByLabelText(/Time zone/).length, 1)
  for (const [label, value] of [['Assessment title', 'Rain review'], ['Coastal record', recordId], ['Related coastal plan', planId], ['Starts at', '2026-10-01T09:00'], ['Ends at', '2026-10-01T12:00'], ['What should be reviewed?', 'Inspect the access path.']]) fireEvent.change(screen.getByLabelText(label), { target: { value } })
  fireEvent.change(screen.getByLabelText(/Time zone/), { target: { value: 'Asia/Colombo' } })
  await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))
  await waitFor(() => assert.ok(saved))
  assert.equal(body.title, 'Rain review'); assert.equal(body.targetId, recordId); assert.equal(body.sourceWorkflowId, planId)
  assert.equal(body.timeZoneId, 'Asia/Colombo'); assert.equal(body.periodStartsAt, '2026-10-01T09:00'); assert.equal(body.periodEndsAt, '2026-10-01T12:00')
})

test('WEB-OPS-RECORD-003 unavailable associations still permit titled drafts without fake IDs (ui-integration: coastal-operations-alerts)', async () => {
  let body
  globalThis.fetch = async (path, init = {}) => path === '/api/operations/form-options' ? jsonResponse({ timeZones: zones, targets: { status: 'NOT_CONNECTED', items: [] }, plans: { status: 'NOT_CONNECTED', items: [] }, assessments: [] }) : (body = JSON.parse(init.body), jsonResponse({ alertId: recordId, ...body }, 201))
  renderInApp(createElement(AlertDraftForm, { existing: null, onCancel() {}, onSaved() {} }))
  await screen.findByRole('option', { name: /Sri Lanka/ })
  assert.equal(screen.queryByLabelText('Related assessment ID'), null)
  for (const [label, value] of [['Advisory title', 'Rain warning'], ['Visible from', '2026-10-01T09:00'], ['Valid until', '2026-10-01T12:00'], ['Advisory message', 'Follow the marked path.']]) fireEvent.change(screen.getByLabelText(label), { target: { value } })
  await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))
  await waitFor(() => assert.ok(body))
  assert.equal(body.title, 'Rain warning'); assert.equal(body.targetId, undefined); assert.equal(body.assessmentId, null); assert.equal(body.timeZoneId, 'Etc/UTC')
})

test('WEB-OPS-RECORD-004 failed option loading disables save and offers recovery (ui-integration: coastal-operations-assessment)', async () => {
  let failure = true
  globalThis.fetch = async () => failure ? jsonResponse({}, 503) : jsonResponse({ timeZones: zones, targets: { status: 'NOT_CONNECTED', items: [] }, plans: { status: 'NOT_CONNECTED', items: [] }, assessments: [] })
  renderInApp(createElement(AssessmentDraftForm, { existing: null, onCancel() {}, onSaved() {} }))
  await screen.findByRole('alert'); assert.equal(screen.getByRole('button', { name: 'Save draft' }).disabled, true)
  failure = false; await userEvent.click(screen.getByRole('button', { name: 'Retry selection lists' }))
  await waitFor(() => assert.equal(screen.getByRole('button', { name: 'Save draft' }).disabled, false))
})

test('WEB-OPS-RECORD-005 pages stack hero before search and omit bottom lookup/header navigation (ui-integration: coastal-operations-assessment)', async () => {
  globalThis.fetch = async () => jsonResponse({items:[],nextCursor:null})
  renderInApp(createElement(Page,{section:'assessments'}),{path:'/operations/assessments',auth:makeAuthSessionValue({user:makeAuthUser({permissions:['operations.assessment.read','operations.target.status.read']}),status:'signed-in'})})
  await screen.findByText('No assessments to show yet')
  const hero=screen.getByRole('heading',{name:'Look after the places we share.'}).closest('section')
  const search=screen.getByRole('region',{name:'Search assessments'})
  assert.ok(hero.compareDocumentPosition(search) & 4)
  assert.equal(hero.parentElement.className.includes('xl:grid-cols-2'),false)
  assert.equal(screen.queryByText('Check a coastal record'),null)
  assert.equal(document.querySelector('header').textContent.includes('Coastal Operations'),false)
})

for (const kind of ['assessment', 'alert']) test(`WEB-OPS-RECORD-006 ${kind} edits preserve server civil times even when browser rules lag (ui-integration: coastal-operations-${kind === 'assessment' ? 'assessment' : 'alerts'})`, async () => {
  let body; let saved
  const zone = 'Asia/Future_Zone'
  globalThis.fetch = async (path, init = {}) => path === '/api/operations/form-options'
    ? jsonResponse({ timeZones: [...zones, { id: zone, country: 'Test location', location: 'Future zone', rulesAvailable: true }], targets: { status: 'NOT_CONNECTED', items: [] }, plans: { status: 'NOT_CONNECTED', items: [] }, assessments: [] })
    : (body = JSON.parse(init.body), jsonResponse({ ...body }))
  const common = { title: 'Saved coastal review', targetType: 'DESTINATION', targetId: recordId, timeZoneId: zone, version: 2 }
  const existing = kind === 'assessment'
    ? { ...common, assessmentId: recordId, objective: 'Inspect access.', periodStartsAt: '2026-10-01T03:30Z', periodEndsAt: '2026-10-01T06:30Z', periodStartsLocal: '2026-10-01T09:00', periodEndsLocal: '2026-10-01T12:00' }
    : { ...common, alertId: recordId, description: 'Use the marked path.', severity: 'LOW', visibility: 'OPERATIONS', validFrom: '2026-10-01T03:30Z', validUntil: '2026-10-01T06:30Z', validFromLocal: '2026-10-01T09:00', validUntilLocal: '2026-10-01T12:00' }
  renderInApp(createElement(kind === 'assessment' ? AssessmentDraftForm : AlertDraftForm, { existing, onCancel() {}, onSaved(result) { saved = result } }))
  await screen.findByRole('option', { name: /Future zone/ })
  assert.equal(screen.getByLabelText(kind === 'assessment' ? 'Starts at' : 'Visible from').value, '2026-10-01T09:00')
  assert.equal(screen.getByLabelText(kind === 'assessment' ? 'Ends at' : 'Valid until').value, '2026-10-01T12:00')
  await userEvent.click(screen.getByRole('button', { name: 'Update draft' }))
  await waitFor(() => assert.ok(saved))
  assert.equal(body.expectedVersion, 2); assert.equal(body.timeZoneId, zone)
  assert.equal(body[kind === 'assessment' ? 'periodStartsAt' : 'validFrom'], '2026-10-01T09:00')
  assert.equal(body[kind === 'assessment' ? 'periodEndsAt' : 'validUntil'], '2026-10-01T12:00')
})
