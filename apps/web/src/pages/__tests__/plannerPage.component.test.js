// Component tests for the Smart Coastal Planner page.
// Requirement references (v1.component.coastal-planner, contract_id
// v1.component.coastal-planner sections 5, 8 and 11; G00 decisions doc
// sections 3.1-3.3 and 7.1-7.2; PROJECT_REQUIREMENTS.md sections 16, 17,
// 20-27, 40, 53; ui-integration workflow `planner-recommendations` at
// route /planner):
//   WEB-PLANNER-UI-001..002  signed-out/checking states and the labeled
//     constraint form with documented defaults (device-capability parity:
//     semantic date/time inputs).
//   WEB-PLANNER-UI-003..008  request validation: identifiers, required
//     and unreadable dates, ordering, duration boundaries, the 30-day
//     planning horizon and optional activity identifiers (G00 section 3.3
//     400/422 semantics; component section 5.1).
//   WEB-PLANNER-UI-009..012  nominal submission through the public API,
//     candidate evidence rendering, honest empty results, duplicate
//     submission prevention and safe retryable dependency failure
//     (component sections 5.2, 8 and 10).
//   WEB-PLANNER-UI-013       planning workflow status, completion and
//     failure detail through the public workflow route (component section 8
//     Monitoring).
//   WEB-PLANNER-UI-014..015  optional biodiversity context through the
//     public prediction route, including provenance, empty-species and
//     unavailable states, and the explicit not-requested state (component
//     sections 5.2.4, 8 and 10; biodiversity is never a safety signal).
//   WEB-PLANNER-UI-016       the caller's experience level and biodiversity
//     opt-out are submitted faithfully (data minimization, section 3).
// Clients test only the public API/gateway; every request target is a
// registered /api/planner/... route (implementation-plan section 2).

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
  waitFor,
} from '../../testSupport/reactTestHarness.js'

const [{ default: PlannerPage }] = await Promise.all([
  loadWebModule('/src/pages/PlannerPage.tsx'),
])

const DESTINATION_ID = '3fa85f64-5717-4562-b3fc-2c963f66afa6'
const ACTIVITY_ID = '4fa85f64-5717-4562-b3fc-2c963f66afa7'
const SECOND_ACTIVITY_ID = 'afa85f64-5717-4562-b3fc-2c963f66afb3'
const RECOMMENDATION_ID = '5fa85f64-5717-4562-b3fc-2c963f66afa8'
const WORKFLOW_ID = '6fa85f64-5717-4562-b3fc-2c963f66afa9'

const HOUR_MS = 60 * 60 * 1000
const DAY_MS = 24 * HOUR_MS

function pad(value) {
  return String(value).padStart(2, '0')
}

function localDateTimeValue(date) {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

function futureDate(offsetMs) {
  return new Date(Date.now() + offsetMs)
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

function candidate(overrides = {}) {
  return {
    destinationId: DESTINATION_ID,
    activityId: ACTIVITY_ID,
    offeringId: '7fa85f64-5717-4562-b3fc-2c963f66afb0',
    title: 'Guided Snorkeling Tour',
    scheduledStart: '2026-10-01T09:00:00Z',
    scheduledEnd: '2026-10-01T11:30:00Z',
    availabilityStatus: 'AVAILABLE',
    suitability: { status: 'SUITABLE', marineConditionTime: '2026-10-01T09:00:00Z', safetyProfileId: '8fa85f64-5717-4562-b3fc-2c963f66afb1' },
    operationalStatus: 'OPEN',
    biodiversityContext: { speciesName: 'Chelonia mydas (Green Sea Turtle)', probability: 0.82, uncertainty: 'LOW', predictionTimestamp: '2026-09-27T10:30:00Z' },
    fitScore: 0.95,
    reasons: ['Optimal wave and tide conditions'],
    ...overrides,
  }
}

function recommendationResult(overrides = {}) {
  return {
    recommendationId: RECOMMENDATION_ID,
    workflowId: WORKFLOW_ID,
    status: 'COMPLETED',
    generatedAt: '2026-09-27T11:00:00Z',
    candidates: [candidate()],
    excludedCandidatesCount: 2,
    uncertaintyNotes: ['Marine conditions are unavailable for one destination.'],
    ...overrides,
  }
}

function workflowStatus(overrides = {}) {
  return {
    workflowId: WORKFLOW_ID,
    workflowType: 'Recommendation',
    status: 'COMPLETED',
    initiatorUserId: 'user-1',
    objective: 'Plan a guided snorkeling day',
    createdAt: '2026-09-27T10:00:00Z',
    completedAt: '2026-09-27T11:00:00Z',
    resultSummary: 'Assembled candidates from published, available offerings.',
    failureReason: null,
    ...overrides,
  }
}

function biodiversityPrediction(overrides = {}) {
  return {
    destinationId: DESTINATION_ID,
    activityId: ACTIVITY_ID,
    status: 'AVAILABLE',
    predictedSpecies: [{
      speciesId: 'bfa85f64-5717-4562-b3fc-2c963f66afb4',
      scientificName: 'Chelonia mydas',
      commonName: 'Green Sea Turtle',
      habitatSuitability: 0.84,
      confidenceLevel: 'HIGH',
    }],
    modelMetadata: { modelVersion: 'it3091-v1.2', inferenceTimestamp: '2026-09-27T10:00:00Z' },
    limitations: 'Contextual prediction only. Not a guarantee of wildlife sighting or site safety.',
    ...overrides,
  }
}

const originalFetch = globalThis.fetch

afterEach(() => {
  cleanup()
  resetTestBrowser()
  globalThis.fetch = originalFetch
})

after(async () => closeWebTestServer())

function installFetch(routes) {
  const calls = []
  globalThis.fetch = async (input, init = {}) => {
    const method = init.method ?? 'GET'
    const call = { input, init, method }
    calls.push(call)
    const response = routes[`${method} ${input}`]
    if (response instanceof Function) return response(call)
    if (!response) throw new Error(`Unexpected public API request: ${method} ${input}`)
    return response
  }
  return calls
}

function renderPlanner(auth = {}) {
  renderInApp(createElement(PlannerPage), {
    path: '/planner',
    auth: makeAuthSessionValue({ user: makeAuthUser(), status: 'signed-in', ...auth }),
  })
}

function setConstraint(id, value) {
  fireEvent.change(document.getElementById(id), { target: { value } })
}

function fillValidConstraints({ destinationId = DESTINATION_ID, startsAt = localDateTimeValue(futureDate(2 * HOUR_MS)), endsAt = localDateTimeValue(futureDate(6 * HOUR_MS)), durationHours = '4', activityIds = '' } = {}) {
  setConstraint('planner-destination', destinationId)
  setConstraint('planner-starts', startsAt)
  setConstraint('planner-ends', endsAt)
  setConstraint('planner-duration', durationHours)
  setConstraint('planner-activities', activityIds)
}

function submitConstraints() {
  fireEvent.click(screen.getByRole('button', { name: 'Generate recommendations' }))
}

function assertValidationError(message) {
  // Validation problems render in the form's polite live region,
  // matching the repository's error-announcement convention.
  const error = screen.getByText(message)
  assert.equal(error.getAttribute('aria-live'), 'polite')
}

test('WEB-PLANNER-UI-001 signed-out and checking planners show no constraint form (ui-integration: planner-recommendations)', () => {
  renderInApp(createElement(PlannerPage), { path: '/planner', auth: makeAuthSessionValue({ status: 'signed-out' }) })

  const section = screen.getByRole('heading', { name: 'Sign in to plan with the coast.' }).closest('section')
  assert.ok(section.querySelector('a').getAttribute('href') === '/signin')
  assert.equal(screen.getByText('Recommendations are generated for your account so saved plans stay yours.').closest('section'), section)
  assert.equal(document.getElementById('planner-destination'), null)

  cleanup()
  renderPlanner({ user: null, status: 'checking' })
  assert.equal(screen.queryByRole('heading', { name: 'Sign in to plan with the coast.' }), null)
  assert.equal(document.getElementById('planner-destination'), null)
})

test('WEB-PLANNER-UI-002 the constraint form is fully labeled with documented defaults (ui-integration: planner-recommendations)', () => {
  installFetch({})
  renderPlanner()

  assert.ok(screen.getByRole('heading', { name: 'Trip constraints' }))
  assert.equal(document.getElementById('planner-destination').getAttribute('placeholder'), '3fa85f64-5717-4562-b3fc-2c963f66afa9')
  assert.equal(document.getElementById('planner-starts').type, 'datetime-local')
  assert.equal(document.getElementById('planner-ends').type, 'datetime-local')
  assert.equal(document.getElementById('planner-duration').type, 'number')
  assert.equal(document.getElementById('planner-duration').value, '4')
  assert.equal(document.getElementById('planner-duration').min, '1')
  const experience = document.getElementById('planner-experience')
  assert.equal(experience.value, 'INTERMEDIATE')
  assert.deepEqual(Array.from(experience.options).map((option) => option.value), ['BEGINNER', 'INTERMEDIATE', 'ADVANCED'])
  assert.deepEqual(Array.from(experience.options).map((option) => option.textContent), ['Beginner', 'Intermediate', 'Advanced'])
  const biodiversity = screen.getByLabelText('Include biodiversity context for each candidate')
  assert.equal(biodiversity.checked, true)
})

test('WEB-PLANNER-UI-003 the destination must be a valid identifier before any request (ui-integration: planner-recommendations)', () => {
  const calls = installFetch({})
  renderPlanner()
  fillValidConstraints({ destinationId: 'not-a-guid' })
  submitConstraints()

  assertValidationError('Enter the target destination as a valid GUID (for example 3fa85f64-5717-4562-b3fc-2c963f66afa9).')
  assert.equal(calls.length, 0)

  cleanup()
  installFetch({})
  renderPlanner()
  fillValidConstraints({ destinationId: '   ' })
  submitConstraints()
  assertValidationError('Enter the target destination as a valid GUID (for example 3fa85f64-5717-4562-b3fc-2c963f66afa9).')
})

test('WEB-PLANNER-UI-004 both trip dates are required (ui-integration: planner-recommendations)', () => {
  const calls = installFetch({})
  renderPlanner()
  setConstraint('planner-destination', DESTINATION_ID)
  submitConstraints()

  assertValidationError('Choose both a start and an end date for the trip.')
  assert.equal(calls.length, 0)

  cleanup()
  installFetch({})
  renderPlanner()
  setConstraint('planner-destination', DESTINATION_ID)
  setConstraint('planner-starts', localDateTimeValue(futureDate(2 * HOUR_MS)))
  submitConstraints()
  assertValidationError('Choose both a start and an end date for the trip.')
  assert.equal(calls.length, 0)
})

test('WEB-PLANNER-UI-005 date ranges must be readable and ordered (ui-integration: planner-recommendations)', () => {
  const calls = installFetch({})
  renderPlanner()
  // Semantic datetime-local inputs refuse unreadable values at the input
  // level (the value sanitizes to empty), so an unparseable date can never
  // reach the request; the form reports the missing date instead (G00
  // section 3.3 keeps malformed payloads a 400 the client must not cause).
  setConstraint('planner-destination', DESTINATION_ID)
  setConstraint('planner-starts', 'not-a-date')
  assert.equal(document.getElementById('planner-starts').value, '')
  setConstraint('planner-ends', localDateTimeValue(futureDate(6 * HOUR_MS)))
  submitConstraints()

  assertValidationError('Choose both a start and an end date for the trip.')
  assert.equal(calls.length, 0)

  const reversed = [
    { startsAt: localDateTimeValue(futureDate(6 * HOUR_MS)), endsAt: localDateTimeValue(futureDate(2 * HOUR_MS)) },
    { startsAt: localDateTimeValue(futureDate(2 * HOUR_MS)), endsAt: localDateTimeValue(futureDate(2 * HOUR_MS)) },
  ]
  for (const dates of reversed) {
    cleanup()
    installFetch({})
    renderPlanner()
    fillValidConstraints(dates)
    submitConstraints()
    assertValidationError('The end date must be after the start date.')
  }
  assert.equal(calls.length, 0)
})

test('WEB-PLANNER-UI-006 planned hours must be positive and fit the selected window (ui-integration: planner-recommendations)', () => {
  const invalid = [
    { durationHours: '0', message: 'Planned hours must be a positive number.' },
    { durationHours: '-2', message: 'Planned hours must be a positive number.' },
    { durationHours: 'abc', message: 'Planned hours must be a positive number.' },
    { durationHours: '4.5', message: 'Planned hours must fit within the selected date range.' },
  ]
  for (const { durationHours, message } of invalid) {
    cleanup()
    const calls = installFetch({})
    renderPlanner()
    fillValidConstraints({ durationHours })
    submitConstraints()
    assertValidationError(message)
    assert.equal(calls.length, 0)
  }
})

test('WEB-PLANNER-UI-007 planning dates must be future dates within the 30-day horizon (ui-integration: planner-recommendations)', () => {
  const invalid = [
    { startsAt: localDateTimeValue(new Date(Date.now() - HOUR_MS)), endsAt: localDateTimeValue(futureDate(6 * HOUR_MS)) },
    { startsAt: localDateTimeValue(futureDate(2 * HOUR_MS)), endsAt: localDateTimeValue(futureDate(31 * DAY_MS)) },
  ]
  for (const dates of invalid) {
    cleanup()
    const calls = installFetch({})
    renderPlanner()
    fillValidConstraints(dates)
    submitConstraints()
    assertValidationError('Planning dates must be in the future and within the next 30 days.')
    assert.equal(calls.length, 0)
  }
})

test('WEB-PLANNER-UI-008 optional preferred activities must be valid identifiers (ui-integration: planner-recommendations)', () => {
  const calls = installFetch({})
  renderPlanner()
  fillValidConstraints({ activityIds: `${ACTIVITY_ID}, not-a-guid` })
  submitConstraints()

  assertValidationError('Preferred activity IDs must be valid GUIDs, separated by commas.')
  assert.equal(calls.length, 0)
})

test('WEB-PLANNER-UI-009 valid constraints submit the declared request and render ranked candidate evidence (ui-integration: planner-recommendations)', async () => {
  const startsValue = localDateTimeValue(futureDate(2 * HOUR_MS))
  const endsValue = localDateTimeValue(futureDate(6 * HOUR_MS))
  const calls = installFetch({ 'POST /api/planner/recommendations': jsonResponse(recommendationResult()) })
  renderPlanner()
  fillValidConstraints({ activityIds: `  ${ACTIVITY_ID},  ${SECOND_ACTIVITY_ID}  ` })
  submitConstraints()

  assert.equal((await screen.findByRole('heading', { name: '1 candidate for your coast.' })).textContent, '1 candidate for your coast.')
  assert.equal(calls.length, 1)
  assert.equal(calls[0].method, 'POST')
  assert.equal(calls[0].input, '/api/planner/recommendations')
  assert.equal(calls[0].init.credentials, 'include')
  assert.deepEqual(calls[0].init.headers, { 'Content-Type': 'application/json', Accept: 'application/json' })
  assert.deepEqual(JSON.parse(calls[0].init.body), {
    targetDestinationId: DESTINATION_ID,
    startsAt: new Date(startsValue).toISOString(),
    endsAt: new Date(endsValue).toISOString(),
    durationHours: 4,
    preferredActivityIds: [ACTIVITY_ID, SECOND_ACTIVITY_ID],
    experienceLevel: 'INTERMEDIATE',
    includeBiodiversityContext: true,
  })

  assert.ok(screen.getByText('CANDIDATES · COMPLETED'))
  assert.ok(screen.getByText('2 candidates were excluded by safety, availability or operational rules.'))
  assert.ok(screen.getByText('Marine conditions are unavailable for one destination.'))
  assert.ok(screen.getByRole('heading', { name: 'Guided Snorkeling Tour' }))
  assert.ok(screen.getByText('Fit 95%'))
  assert.ok(screen.getByText('SUITABLE'))
  assert.ok(screen.getByText('OPEN'))
  assert.ok(screen.getByText('AVAILABLE'))
  assert.ok(screen.getByText('Chelonia mydas (Green Sea Turtle) (LOW)'))
  assert.ok(screen.getByText('Optimal wave and tide conditions'))
  const startsText = new Date('2026-10-01T09:00:00Z').toLocaleString()
  const endsText = new Date('2026-10-01T11:30:00Z').toLocaleString()
  assert.ok(screen.getByText(new RegExp(`^${escapeRegExp(startsText)}\\s*–\\s*${escapeRegExp(endsText)}$`)))
})

test('WEB-PLANNER-UI-010 an eligible-empty result is reported honestly without fabricated candidates (ui-integration: planner-recommendations)', async () => {
  installFetch({ 'POST /api/planner/recommendations': jsonResponse(recommendationResult({ candidates: [], excludedCandidatesCount: 0, uncertaintyNotes: [] })) })
  renderPlanner()
  fillValidConstraints()
  submitConstraints()

  assert.equal((await screen.findByRole('heading', { name: 'No candidates this time.' })).textContent, 'No candidates this time.')
  assert.equal(screen.queryByText(/were excluded by safety/), null)
  assert.equal(screen.queryByRole('heading', { name: 'Guided Snorkeling Tour' }), null)
})

test('WEB-PLANNER-UI-011 duplicate submissions are prevented while a recommendation request is in flight (ui-integration: planner-recommendations)', async () => {
  let releaseFetch
  const calls = installFetch({
    'POST /api/planner/recommendations': () => new Promise((resolve) => {
      releaseFetch = () => resolve(jsonResponse(recommendationResult()))
    }),
  })
  renderPlanner()
  fillValidConstraints()
  submitConstraints()

  const submitButton = await screen.findByRole('button', { name: 'Planning…' })
  assert.equal(submitButton.disabled, true)
  fireEvent.click(submitButton)
  assert.equal(calls.length, 1)

  releaseFetch()
  assert.ok(await screen.findByRole('heading', { name: '1 candidate for your coast.' }))
  assert.equal(calls.length, 1)
})

test('WEB-PLANNER-UI-012 recommendation dependency failures are safe and the form stays retryable (ui-integration: planner-recommendations)', async () => {
  let respond = () => jsonResponse({ status: 503, detail: 'The marine conditions service is unavailable.' }, 503)
  const calls = installFetch({ 'POST /api/planner/recommendations': () => respond() })
  renderPlanner()
  fillValidConstraints()
  submitConstraints()

  assert.equal((await screen.findByText('The marine conditions service is unavailable.')).textContent, 'The marine conditions service is unavailable.')
  assert.equal(screen.queryByRole('heading', { name: 'Guided Snorkeling Tour' }), null)

  respond = () => jsonResponse(recommendationResult())
  submitConstraints()
  assert.ok(await screen.findByRole('heading', { name: '1 candidate for your coast.' }))
  assert.equal(calls.length, 2)
})

test('WEB-PLANNER-UI-013 the planning workflow status, completion and failure detail are reported (ui-integration: planner-recommendations)', async () => {
  installFetch({
    'POST /api/planner/recommendations': jsonResponse(recommendationResult()),
    [`GET /api/planner/workflows/${WORKFLOW_ID}`]: jsonResponse(workflowStatus()),
  })
  renderPlanner()
  fillValidConstraints()
  submitConstraints()
  await screen.findByRole('heading', { name: '1 candidate for your coast.' })
  fireEvent.click(screen.getByRole('button', { name: 'Planning workflow status' }))

  assert.equal((await screen.findByText('WORKFLOW Recommendation')).textContent, 'WORKFLOW Recommendation')
  assert.ok(screen.getByText('Status: COMPLETED'))
  assert.ok(screen.getByText('Assembled candidates from published, available offerings.'))
  assert.ok(screen.getByText('Plan a guided snorkeling day' in {} ? '' : /Created .+, completed .+/))

  cleanup()
  installFetch({
    'POST /api/planner/recommendations': jsonResponse(recommendationResult()),
    [`GET /api/planner/workflows/${WORKFLOW_ID}`]: jsonResponse(workflowStatus({ status: 'FAILED', completedAt: null, resultSummary: null, failureReason: 'Peer marine conditions service timed out.' })),
  })
  renderPlanner()
  fillValidConstraints()
  submitConstraints()
  await screen.findByRole('heading', { name: '1 candidate for your coast.' })
  fireEvent.click(screen.getByRole('button', { name: 'Planning workflow status' }))

  assert.ok(await screen.findByText('Status: FAILED'))
  assert.ok(screen.getByText('Peer marine conditions service timed out.'))
  assert.equal(screen.queryByText(/completed /), null)
})

test('WEB-PLANNER-UI-014 candidate biodiversity context is inspected through the public prediction route with provenance (ui-integration: planner-recommendations)', async () => {
  let releaseFetch
  const calls = installFetch({
    'POST /api/planner/recommendations': jsonResponse(recommendationResult()),
    [`GET /api/planner/biodiversity/predictions?destinationId=${DESTINATION_ID}&activityId=${ACTIVITY_ID}`]: () => new Promise((resolve) => {
      releaseFetch = () => resolve(jsonResponse(biodiversityPrediction()))
    }),
  })
  renderPlanner()
  fillValidConstraints()
  submitConstraints()
  await screen.findByRole('heading', { name: '1 candidate for your coast.' })

  const inspectButton = screen.getByRole('button', { name: 'Inspect biodiversity context' })
  fireEvent.click(inspectButton)
  const loadingButton = await screen.findByRole('button', { name: 'Inspecting…' })
  assert.equal(loadingButton.disabled, true)
  // One recommendation POST plus the biodiversity prediction GET.
  assert.equal(calls.length, 2)

  releaseFetch()
  assert.ok(await screen.findByText('PREDICTED SPECIES · AVAILABLE'))
  assert.equal(calls.length, 2)
  assert.equal(calls[1].method, 'GET')
  assert.equal(calls[1].input, `/api/planner/biodiversity/predictions?destinationId=${DESTINATION_ID}&activityId=${ACTIVITY_ID}`)
  assert.equal(calls[1].init.credentials, 'include')
  assert.ok(screen.getByText('Green Sea Turtle — Chelonia mydas, habitat suitability 84%, HIGH confidence'))
  assert.ok(screen.getByText('Model it3091-v1.2'))
  assert.ok(screen.getByText('Contextual prediction only. Not a guarantee of wildlife sighting or site safety.'))
  await waitFor(() => assert.equal(screen.getByRole('button', { name: 'Inspect biodiversity context' }).disabled, false))
})

test('WEB-PLANNER-UI-015 biodiversity inspection reports empty results and safe failures; candidates without activities stay not requested (ui-integration: planner-recommendations)', async () => {
  installFetch({
    'POST /api/planner/recommendations': jsonResponse(recommendationResult()),
    [`GET /api/planner/biodiversity/predictions?destinationId=${DESTINATION_ID}&activityId=${ACTIVITY_ID}`]: jsonResponse(biodiversityPrediction({ predictedSpecies: [], modelMetadata: null })),
  })
  renderPlanner()
  fillValidConstraints()
  submitConstraints()
  await screen.findByRole('heading', { name: '1 candidate for your coast.' })
  fireEvent.click(screen.getByRole('button', { name: 'Inspect biodiversity context' }))

  assert.ok(await screen.findByText('No species predictions were returned for this candidate.'))
  assert.equal(screen.queryByText(/Model /), null)

  cleanup()
  installFetch({
    'POST /api/planner/recommendations': jsonResponse(recommendationResult()),
    [`GET /api/planner/biodiversity/predictions?destinationId=${DESTINATION_ID}&activityId=${ACTIVITY_ID}`]: jsonResponse({ status: 503, detail: 'The biodiversity inference service is unavailable.' }, 503),
  })
  renderPlanner()
  fillValidConstraints()
  submitConstraints()
  await screen.findByRole('heading', { name: '1 candidate for your coast.' })
  fireEvent.click(screen.getByRole('button', { name: 'Inspect biodiversity context' }))

  assert.equal((await screen.findByText('The biodiversity inference service is unavailable.')).textContent, 'The biodiversity inference service is unavailable.')
  assert.equal(screen.queryByLabelText('Biodiversity prediction'), null)

  cleanup()
  installFetch({
    'POST /api/planner/recommendations': jsonResponse(recommendationResult({ candidates: [candidate({ activityId: null, biodiversityContext: null })] })),
  })
  renderPlanner()
  fillValidConstraints()
  submitConstraints()
  await screen.findByRole('heading', { name: '1 candidate for your coast.' })

  assert.equal(screen.queryByRole('button', { name: 'Inspect biodiversity context' }), null)
  assert.ok(screen.getByText('Not requested'))
})

test('WEB-PLANNER-UI-016 the experience level and biodiversity opt-out are submitted faithfully (ui-integration: planner-recommendations)', async () => {
  const calls = installFetch({ 'POST /api/planner/recommendations': jsonResponse(recommendationResult({ candidates: [] })) })
  renderPlanner()
  fillValidConstraints()
  fireEvent.change(document.getElementById('planner-experience'), { target: { value: 'ADVANCED' } })
  fireEvent.click(screen.getByLabelText('Include biodiversity context for each candidate'))
  submitConstraints()

  await screen.findByRole('heading', { name: 'No candidates this time.' })
  assert.equal(calls.length, 1)
  assert.deepEqual(JSON.parse(calls[0].init.body), {
    targetDestinationId: DESTINATION_ID,
    startsAt: new Date(document.getElementById('planner-starts').value).toISOString(),
    endsAt: new Date(document.getElementById('planner-ends').value).toISOString(),
    durationHours: 4,
    preferredActivityIds: [],
    experienceLevel: 'ADVANCED',
    includeBiodiversityContext: false,
  })
})
