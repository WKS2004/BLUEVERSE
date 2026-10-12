import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import {
  cleanup,
  closeWebTestServer,
  createElement,
  fireEvent,
  jsonResponse,
  loadWebModule,
  makeAuthSessionValue,
  makeAuthUser,
  renderInApp,
  resetTestBrowser,
  screen,
  userEvent,
} from '../../../testSupport/reactTestHarness.js'

const { default: CoastalOperationsPage } = await loadWebModule('/src/pages/operations/CoastalOperationsPage.tsx')
const originalFetch = globalThis.fetch
const targetId = '00000000-0000-4000-8000-000000000001'
const assessmentId = '00000000-0000-4000-8000-000000000002'

afterEach(() => {
  cleanup()
  resetTestBrowser()
  globalThis.fetch = originalFetch
})
after(async () => closeWebTestServer())

function assessment(overrides = {}) {
  return {
    assessmentId,
    workflowId: '00000000-0000-4000-8000-000000000003',
    targetType: 'DESTINATION',
    targetId,
    sourceWorkflowId: null,
    title: 'Rain access review', timeZoneId: 'Asia/Colombo',
    periodStartsAt: '2026-10-01T03:30:00Z',
    periodEndsAt: '2026-10-01T06:30:00Z',
    objective: 'Review the access route after heavy rain.',
    workflowStatus: 'SUBMITTED',
    aiDependencyStatus: 'NOT_CONNECTED',
    aiDispatchOutcome: 'NOT_STARTED',
    aiDispatchRetryable: true,
    componentDependencies: [{ service: 'experience-biodiversity', status: 'AVAILABLE', attempts: 1, retries: 0, retryable: false, checkedAt: '2026-09-28T04:00:00Z' }],
    version: 1,
    createdAt: '2026-09-28T04:00:00Z',
    updatedAt: '2026-09-28T04:00:00Z',
    ...overrides,
  }
}

function installFetch(routes) {
  const calls = []
  globalThis.fetch = async (input, init = {}) => {
    const request = { input, init, method: init.method ?? 'GET', path: new URL(input, 'http://localhost').pathname }
    calls.push(request)
    const response = routes[`${request.method} ${request.path}`]
    if (response instanceof Function) return response(request)
    if (!response) throw new Error(`Unexpected public API request: ${request.method} ${input}`)
    return response
  }
  return calls
}

function renderOperations(permissions) {
  renderInApp(createElement(CoastalOperationsPage), {
    path: '/operations/assessments',
    auth: makeAuthSessionValue({
      user: makeAuthUser({ permissions }),
      status: 'signed-in',
    }),
  })
}

test('WEB-OPS-UI-001 accounts without Coastal Operations grants cannot read service data (ui-integration: coastal-operations-assessment)', () => {
  const calls = installFetch({})
  renderOperations(['profile.read'])

  assert.ok(screen.getByRole('heading', { name: 'This workspace is not available to your account.' }))
  assert.equal(calls.length, 0)
  assert.equal(screen.queryByRole('button', { name: 'New assessment' }), null)
})

test('WEB-OPS-UI-002 current assessment response is shown without inventing a proposal or decision action (ui-integration: coastal-operations-assessment)', async () => {
  const item = assessment()
  const calls = installFetch({
    'GET /api/operations/assessments': () => jsonResponse({ items: [item], nextCursor: null }),
    [`GET /api/operations/assessments/${assessmentId}`]: () => jsonResponse({ assessment: item, decisions: [], evidence: [] }),
    [`GET /api/operations/targets/DESTINATION/${targetId}/status`]: () => jsonResponse({ targetType: 'DESTINATION', targetId, operationalState: 'OPEN', stateVersion: 1, updatedAt: item.updatedAt }),
    [`GET /api/operations/targets/DESTINATION/${targetId}/history`]: () => jsonResponse({ items: [], nextCursor: null }),
  })
  renderOperations(['operations.assessment.read', 'operations.assessment.decide', 'operations.target.status.read', 'operations.target.history.read'])

  await screen.findByText(item.title)
  await userEvent.click(screen.getByText('Open review details'))
  await screen.findByText('Coastal context was recorded, but automated proposals are not available yet. No operational change has been suggested or applied.')
  assert.ok(screen.getByText('Coastal experience'))
  assert.ok(screen.getByText('Open'))
  assert.equal(screen.queryByRole('button', { name: /approve|apply decision/i }), null)
  assert.equal(calls.some((call) => call.method === 'POST' && call.path.includes('/decisions')), false)
})

test('WEB-OPS-UI-003 assessment creation selects named records and submits one zone with local dates (ui-integration: coastal-operations-assessment)', async () => {
  let createdRequest
  const created = assessment({ assessmentId: '00000000-0000-4000-8000-000000000004' })
  const calls = installFetch({
    'GET /api/operations/form-options': () => jsonResponse({ timeZones: [{ id: 'Asia/Colombo', country: 'Sri Lanka', location: 'Colombo', rulesAvailable: true }, { id: 'Etc/UTC', country: 'Worldwide', location: 'UTC', rulesAvailable: true }], targets: { status: 'AVAILABLE', items: [{ id: targetId, title: 'Bentota beach', targetType: 'DESTINATION' }] }, plans: { status: 'NOT_CONNECTED', items: [] }, assessments: [] }),
    'POST /api/operations/assessments': (request) => {
      createdRequest = request
      return jsonResponse(created, 201)
    },
  })
  renderOperations(['operations.assessment.create'])
  await userEvent.click(screen.getByRole('button', { name: 'New assessment' }))

  await screen.findByRole('option', { name: 'Bentota beach' })
  assert.equal(screen.queryByLabelText('Coastal record ID'), null)
  fireEvent.change(screen.getByLabelText('Assessment title'), { target: { value: 'Rain access review' } })
  fireEvent.change(screen.getByLabelText('Coastal record'), { target: { value: targetId } })
  fireEvent.change(screen.getByLabelText('Starts at'), { target: { value: '2026-10-01T09:00' } })
  fireEvent.change(screen.getByLabelText(/Time zone/), { target: { value: 'Asia/Colombo' } })
  fireEvent.change(screen.getByLabelText('Ends at'), { target: { value: '2026-10-01T12:00' } })
  fireEvent.change(screen.getByLabelText(/What should be reviewed/), { target: { value: 'Review the access route after heavy rain.' } })
  await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))

  await screen.findByText('Assessment draft saved. Submit it when you are ready to check coastal context.')
  assert.equal(calls.length, 2)
  assert.equal(createdRequest.input, '/api/operations/assessments')
  assert.equal(createdRequest.init.credentials, 'include')
  assert.equal(createdRequest.init.method, 'POST')
  assert.match(createdRequest.init.headers['Idempotency-Key'], /^[0-9a-f-]{36}$|^\d+-[0-9a-f]+$/i)
  assert.deepEqual(JSON.parse(createdRequest.init.body), {
    targetType: 'DESTINATION',
    targetId,
    sourceWorkflowId: null,
    title: 'Rain access review', timeZoneId: 'Asia/Colombo',
    periodStartsAt: '2026-10-01T09:00',
    periodEndsAt: '2026-10-01T12:00',
    objective: 'Review the access route after heavy rain.',
  })
})

test('WEB-OPS-UI-004 advisory managers can edit proposed drafts while publish remains separately permissioned (ui-integration: coastal-operations-assessment)', async () => {
  const alert = {
    alertId: '00000000-0000-4000-8000-000000000010',
    targetType: 'DESTINATION', targetId, assessmentId: null,
    title: 'Rough water near the inlet', description: 'Use the marked shoreline path.',
    severity: 'HIGH', visibility: 'PUBLIC', lifecycle: 'PROPOSED',
    validFrom: '2026-10-01T09:00:00Z', validUntil: '2026-10-02T09:00:00Z',
    version: 1, createdAt: '2026-09-28T04:00:00Z', updatedAt: '2026-09-28T04:00:00Z',
  }
  installFetch({ 'GET /api/operations/alerts': () => jsonResponse({ items: [alert], nextCursor: null }) })
  renderOperations(['operations.alert.read', 'operations.alert.manage'])

  await screen.findByText(alert.title)
  assert.ok(screen.getByRole('button', { name: 'Edit draft' }))
  assert.equal(screen.queryByRole('button', { name: 'Publish advisory' }), null)
})
