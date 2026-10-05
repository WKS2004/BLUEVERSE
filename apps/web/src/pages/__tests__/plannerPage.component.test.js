// Adithya approved replacing UUID-entry expectations on 2026-10-05.
// PROJECT_REQUIREMENTS 16/17; planner-recommendations/result/saved-trips/
// trip-management. Preserve validation, permission and dependency coverage.
import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { act, cleanup, closeWebTestServer, createElement, fireEvent, jsonResponse, loadWebModule,
  makeAuthSessionValue, makeAuthUser, renderInApp, resetTestBrowser, screen, waitFor } from '../../testSupport/reactTestHarness.js'
const [{ default: PlannerPage }, { default: SavedTripsPage }, { default: ItineraryPage }, { Routes, Route }] = await Promise.all([
  loadWebModule('/src/pages/PlannerPage.tsx'), loadWebModule('/src/pages/SavedTripsPage.tsx'), loadWebModule('/src/pages/ItineraryPage.tsx'), import('react-router')])
const D = '3fa85f64-5717-4562-b3fc-2c963f66afa6', A = '4fa85f64-5717-4562-b3fc-2c963f66afa7', R = '5fa85f64-5717-4562-b3fc-2c963f66afa8', I = '6fa85f64-5717-4562-b3fc-2c963f66afa9'
const permissions = ['planner.recommendations.create', 'planner.recommendations.read', 'planner.itineraries.manage', 'planner.workflows.read', 'planner.biodiversity.read']
const start = new Date(Date.now() + 86400000).toISOString(), end = new Date(Date.parse(start) + 4 * 3600000).toISOString()
const options = { status: 'AVAILABLE', message: null, destinations: [{ destinationId: D, name: 'Mirissa', region: 'Southern coast', timeZone: 'Asia/Colombo', activities: [{ activityId: A, name: 'Guided snorkelling' }] }] }
const candidate = { destinationId: D, activityId: A, offeringId: A, title: 'A morning in the lagoon', scheduledStart: start, scheduledEnd: end,
  availabilityStatus: 'AVAILABLE', suitability: { status: 'SUITABLE', marineConditionTime: start, safetyProfileId: D }, operationalStatus: 'OPEN', biodiversityContext: null, fitScore: .95, reasons: ['Published and available.', 'Marine conditions match the activity.'], timeZone: 'Asia/Colombo' }
const recommendations = { recommendationId: R, workflowId: I, status: 'COMPLETED', generatedAt: new Date().toISOString(), candidates: [candidate], excludedCandidatesCount: 0, uncertaintyNotes: [] }
const trip = { itineraryId: I, ownerUserId: D, title: 'Mirissa morning', description: 'Pack reef-safe sunscreen', startsAt: start, endsAt: end, timeZone: 'Asia/Colombo', concurrencyVersion: 1, createdAt: start, updatedAt: start,
  items: [{ ...candidate, itemId: A, itineraryId: I, orderIndex: 0, lastSuitabilityStatus: 'UNKNOWN', lastAvailabilityStatus: 'UNKNOWN', lastOperationalStatus: 'UNKNOWN', advisoryNote: null }] }
const evaluation = { evaluationId: R, itineraryId: I, evaluatedAt: start, hasChanges: true, requiresReview: true, concurrencyVersion: 2, summary: 'Review local guidance.', items: [{ itemId: A, previousAvailability: 'UNKNOWN', previousSuitability: 'UNKNOWN', previousOperationalStatus: 'UNKNOWN', currentAvailability: 'AVAILABLE', currentSuitability: 'CAUTION', currentOperationalStatus: 'OPEN', suggestedAction: 'REVIEW_CONDITIONS', advisoryMessage: 'Check the wind', marineConditionTime: start }] }
const originalFetch = globalThis.fetch
window.HTMLDialogElement.prototype.showModal = function () { this.open = true }
window.HTMLDialogElement.prototype.close = function () { this.open = false }
afterEach(() => { cleanup(); resetTestBrowser(); globalThis.fetch = originalFetch })
after(closeWebTestServer)
function install(overrides = {}) {
  const calls = []
  const routes = { 'GET /api/planner/catalogue': options, [`GET /api/planner/recommendations/${R}`]: recommendations,
    [`GET /api/planner/itineraries/${I}`]: trip, [`GET /api/planner/itineraries/${I}/re-evaluations`]: [], ...overrides }
  globalThis.fetch = async (input, init = {}) => {
    calls.push({ input, init })
    const route = routes[`${init.method ?? 'GET'} ${input}`]
    assert.notEqual(route, undefined, `Unexpected public request: ${input}`)
    return typeof route === 'function' ? route(input, init) : jsonResponse(route)
  }
  return calls
}
function render(path = '/planner', auth = {}) {
  renderInApp(createElement(Routes, null,
    createElement(Route, { path: '/planner', element: createElement(PlannerPage) }),
    createElement(Route, { path: '/planner/recommendations/:recommendationId', element: createElement(PlannerPage) }),
    createElement(Route, { path: '/planner/saved', element: createElement(SavedTripsPage) }),
    createElement(Route, { path: '/planner/itineraries/:itineraryId', element: createElement(ItineraryPage) })),
  { path, auth: makeAuthSessionValue({ status: 'signed-in', user: makeAuthUser({ permissions }), ...auth }) })
}
async function ready() { await screen.findByLabelText('Destination') }
function set(id, value) { fireEvent.change(document.getElementById(id), { target: { value } }) }
function fill() {
  set('planner-destination', D)
  // Destination-local wall clocks are independent of the browser time zone.
  const next = new Date(Date.now() + 86400000).toISOString().slice(0, 10)
  set('planner-starts', `${next}T09:00`); set('planner-ends', `${next}T17:00`)
}
function submit() { fireEvent.click(screen.getByRole('button', { name: /Find my coastal day/ })) }

test('WEB-PLANNER-UI-001 signed-out and checking states do not make planner requests', () => {
  const calls = install()
  render('/planner', { status: 'signed-out', user: null })
  assert.ok(screen.getByRole('heading', { name: 'Sign in to plan with the coast.' }))
  assert.equal(screen.queryByLabelText('Destination'), null)
  cleanup(); render('/planner', { status: 'checking', user: null })
  assert.equal(screen.queryByLabelText('Destination'), null); assert.equal(calls.length, 0)
})
test('WEB-PLANNER-UI-002 named choices have labeled defaults and no UUID text fields', async () => {
  install(); render(); await ready()
  assert.equal(document.getElementById('planner-destination').tagName, 'SELECT')
  assert.ok(screen.getByRole('option', { name: 'Mirissa · Southern coast' }))
  assert.equal(document.getElementById('planner-duration').value, '4')
  assert.equal(screen.getByLabelText('Your experience level').value, 'INTERMEDIATE')
  assert.equal(screen.getByLabelText(/Include wildlife context/).checked, true)
  assert.equal(document.querySelector('input[placeholder*="3fa85"]'), null)
  set('planner-destination', D); assert.ok(screen.getByLabelText('Guided snorkelling'))
  assert.match(screen.getByText(/All times here are local/).textContent, /Asia\/Colombo/)
})
test('WEB-PLANNER-UI-003 destination selection is required before submission', async () => {
  const calls = install(); render(); await ready(); submit()
  assert.equal(screen.getByRole('alert').textContent, 'Choose a destination to start planning.')
  assert.equal(calls.filter(c => c.init.method === 'POST').length, 0)
})
test('WEB-PLANNER-UI-004 missing trip dates preserve choices and make no submission', async () => {
  const calls = install(); render(); await ready(); set('planner-destination', D); submit()
  assert.match(screen.getByRole('alert').textContent, /Choose both/)
  assert.equal(screen.getByLabelText('Destination').value, D); assert.equal(calls.length, 1)
})
test('WEB-PLANNER-UI-005 end before start is rejected', async () => {
  install(); render(); await ready(); fill(); set('planner-ends', document.getElementById('planner-starts').value); submit()
  assert.match(screen.getByRole('alert').textContent, /end of your trip must be after/)
})
test('WEB-PLANNER-UI-006 fractional, negative and too-long durations are rejected', async () => {
  const calls = install(); render(); await ready(); fill()
  for (const hours of ['0', '-1', '1.5', '9']) { set('planner-duration', hours); submit(); assert.match(screen.getByRole('alert').textContent, /whole number of hours/) }
  assert.equal(calls.length, 1)
})
test('WEB-PLANNER-UI-007 past dates and dates beyond the planning horizon are rejected', async () => {
  install(); render(); await ready(); fill(); set('planner-starts', '2020-01-01T09:00'); submit()
  assert.match(screen.getByRole('alert').textContent, /future dates within the next 30 days/)
  fill(); set('planner-ends', new Date(Date.now() + 32 * 86400000).toISOString().slice(0, 16)); submit()
  assert.match(screen.getByRole('alert').textContent, /next 30 days/)
})
test('WEB-PLANNER-UI-008 activities are selected by name and sent as canonical IDs', async () => {
  const calls = install({ 'POST /api/planner/recommendations': recommendations }); render(); await ready(); fill()
  fireEvent.click(screen.getByLabelText('Guided snorkelling')); submit()
  await screen.findByRole('heading', { name: candidate.title })
  const body = JSON.parse(calls.find(c => c.init.method === 'POST').init.body)
  assert.deepEqual(body.preferredActivityIds, [A]); assert.equal(body.targetDestinationId, D)
  assert.match(body.startsAt, /T03:30:00.000Z$/)
  assert.match(screen.getByTestId('router-location').textContent, new RegExp(R))
})
test('WEB-PLANNER-UI-009 result evidence, cautions and uncertainty remain visible', async () => {
  install({ [`GET /api/planner/recommendations/${R}`]: { ...recommendations, candidates: [{ ...candidate, suitability: { ...candidate.suitability, status: 'CAUTION' } }], uncertaintyNotes: ['Wildlife context unavailable.'] } })
  render(`/planner/recommendations/${R}`); await screen.findByRole('heading', { name: candidate.title })
  assert.ok(screen.getByText('A caution to review')); assert.ok(screen.getByText('Why this fits'))
  assert.ok(screen.getByText('Wildlife context unavailable.')); assert.ok(screen.getByText(/No verified wildlife context was attached/))
  assert.equal(screen.queryByText('95%'), null)
})
test('WEB-PLANNER-UI-010 required dependency failure shows a recoverable empty state', async () => {
  install({ [`GET /api/planner/recommendations/${R}`]: { ...recommendations, candidates: [], uncertaintyNotes: ['Marine conditions unavailable.'] } })
  render(`/planner/recommendations/${R}`); await screen.findByRole('heading', { name: 'No verified matches for this trip.' })
  assert.ok(screen.getByRole('link', { name: 'Adjust your trip' })); assert.equal(screen.queryByRole('button', { name: /Plan around this experience/ }), null)
})
test('WEB-PLANNER-UI-011 duplicate submissions are disabled and failed requests retain form values', async () => {
  let reject
  const calls = install({ 'POST /api/planner/recommendations': () => new Promise((_, fail) => { reject = fail }) })
  render(); await ready(); fill(); submit(); await waitFor(() => assert.equal(typeof reject, 'function'))
  assert.equal(screen.getByRole('button', { name: /Finding experiences/ }).disabled, true)
  await act(async () => reject(new TypeError('offline')))
  assert.match(screen.getByRole('alert').textContent, /changes are still here/)
  assert.equal(screen.getByLabelText('Destination').value, D); assert.equal(calls.filter(c => c.init.method === 'POST').length, 1)
})
test('WEB-PLANNER-UI-012 absent catalogue explains recovery without asking for UUIDs', async () => {
  install({ 'GET /api/planner/catalogue': { status: 'UNAVAILABLE', destinations: [], message: 'Please try again later.' } })
  render(); await screen.findByText('Destinations aren’t available right now.')
  assert.ok(screen.getByRole('button', { name: 'Try again' })); assert.ok(screen.getByRole('link', { name: 'View saved trips' }))
  assert.equal(screen.queryByLabelText('Destination'), null)
})
test('WEB-PLANNER-UI-013 workflow inspection uses the public route and reports failures', async () => {
  install({ [`GET /api/planner/workflows/${I}`]: { workflowId: I, workflowType: 'Recommendation', status: 'FAILED', initiatorUserId: D, objective: 'Coastal day', createdAt: start, completedAt: end, resultSummary: null, failureReason: 'Please retry this search.' } })
  render(`/planner/recommendations/${R}`); await screen.findByRole('heading', { name: candidate.title })
  fireEvent.click(screen.getByRole('button', { name: 'View search status' })); await screen.findByText('Please retry this search.')
})
test('WEB-PLANNER-UI-014 optional biodiversity shows metadata without claiming a sighting', async () => {
  install({ [`GET /api/planner/biodiversity/predictions?destinationId=${D}&activityId=${A}`]: { destinationId: D, activityId: A, status: 'AVAILABLE', predictedSpecies: [{ speciesId: R, commonName: 'Green turtle', scientificName: 'Chelonia mydas', habitatSuitability: .8, confidenceLevel: 'HIGH' }], modelMetadata: { modelVersion: 'fixture-v1', inferenceTimestamp: start }, limitations: 'Not a guaranteed sighting.' } })
  render(`/planner/recommendations/${R}`); await screen.findByRole('heading', { name: candidate.title })
  fireEvent.click(screen.getByText('Conditions & wildlife context')); fireEvent.click(screen.getByRole('button', { name: 'Check wildlife context' }))
  await screen.findByText('Green turtle (Chelonia mydas)'); assert.ok(screen.getByText('Not a guaranteed sighting.')); assert.ok(screen.getByText(/Model fixture-v1/))
})
test('WEB-PLANNER-UI-015 unavailable and empty wildlife predictions remain honest', async () => {
  install({ [`GET /api/planner/biodiversity/predictions?destinationId=${D}&activityId=${A}`]: { destinationId: D, activityId: A, status: 'UNAVAILABLE', predictedSpecies: [], modelMetadata: null, limitations: 'No current prediction.' } })
  render(`/planner/recommendations/${R}`); await screen.findByRole('heading', { name: candidate.title })
  fireEvent.click(screen.getByText('Conditions & wildlife context')); fireEvent.click(screen.getByRole('button', { name: 'Check wildlife context' }))
  await screen.findByText('Wildlife predictions are unavailable right now.')
})
test('WEB-PLANNER-UI-016 experience level and wildlife opt-out are submitted faithfully', async () => {
  const calls = install({ 'POST /api/planner/recommendations': recommendations }); render(); await ready(); fill()
  set('planner-experience', 'BEGINNER'); fireEvent.click(screen.getByLabelText(/Include wildlife context/)); submit()
  await screen.findByRole('heading', { name: candidate.title }); const body = JSON.parse(calls.find(c => c.init.method === 'POST').init.body)
  assert.equal(body.experienceLevel, 'BEGINNER'); assert.equal(body.includeBiodiversityContext, false)
})
test('WEB-PLANNER-UI-017 permission denial prevents catalogue requests and hides save actions', async () => {
  const calls = install(); render('/planner', { user: makeAuthUser({ permissions: [] }) })
  assert.ok(screen.getByRole('heading', { name: 'Coastal planning access' })); assert.equal(calls.length, 0)
  cleanup(); render(`/planner/recommendations/${R}`, { user: makeAuthUser({ permissions: ['planner.recommendations.read'] }) })
  await screen.findByRole('heading', { name: candidate.title }); assert.equal(screen.queryByRole('button', { name: /Plan around this experience/ }), null)
})
test('WEB-PLANNER-UI-018 save uses a named trip and the owner recommendation reference', async () => {
  const calls = install({ 'POST /api/planner/itineraries': trip }); render(`/planner/recommendations/${R}`)
  await screen.findByRole('heading', { name: candidate.title }); fireEvent.click(screen.getByRole('button', { name: /Plan around this experience/ }))
  fireEvent.change(screen.getByLabelText('Trip name'), { target: { value: 'Mirissa morning' } }); fireEvent.click(screen.getByRole('button', { name: 'Save my trip' }))
  await screen.findByRole('heading', { name: trip.title }); const body = JSON.parse(calls.find(c => c.init.method === 'POST').init.body)
  assert.equal(body.recommendationId, R); assert.equal(body.title, 'Mirissa morning'); assert.equal(body.items[0].offeringId, A)
})
test('WEB-PLANNER-UI-019 saved trips show empty recovery and owner trip links', async () => {
  install({ 'GET /api/planner/itineraries?page=1&pageSize=20': [] }); render('/planner/saved')
  await screen.findByRole('heading', { name: 'Your first coastal day awaits.' }); assert.ok(screen.getByRole('link', { name: 'Find a coastal experience' }))
  cleanup(); install({ 'GET /api/planner/itineraries?page=1&pageSize=20': [trip] }); render('/planner/saved')
  await screen.findByRole('heading', { name: trip.title }); assert.equal(screen.getByRole('link', { name: 'Open this trip' }).getAttribute('href'), `/planner/itineraries/${I}`)
})
test('WEB-PLANNER-UI-020 editing preserves identity and sends optimistic version; conflicts keep the draft', async () => {
  const calls = install({ [`PUT /api/planner/itineraries/${I}`]: () => jsonResponse({ detail: 'Conflict' }, 409) })
  render(`/planner/itineraries/${I}`); await screen.findByRole('heading', { name: trip.title }); fireEvent.click(screen.getByRole('button', { name: 'Edit trip' }))
  fireEvent.change(screen.getByLabelText('Trip name'), { target: { value: 'A different name' } }); fireEvent.click(screen.getByRole('button', { name: 'Save changes' }))
  await screen.findByRole('alert'); assert.equal(screen.getByLabelText('Trip name').value, 'A different name')
  const body = JSON.parse(calls.find(c => c.init.method === 'PUT').init.body); assert.equal(body.items[0].itemId, A); assert.equal(body.concurrencyVersion, 1)
})
test('WEB-PLANNER-UI-021 condition reviews show previous evidence and preserve selected times', async () => {
  install({ [`POST /api/planner/itineraries/${I}/re-evaluations`]: evaluation })
  render(`/planner/itineraries/${I}`); await screen.findByRole('heading', { name: trip.title }); fireEvent.click(screen.getByRole('button', { name: 'Review current conditions' }))
  await screen.findByText(/Your chosen experiences and times have been kept/); assert.ok(screen.getByText(/Not verified → Review caution/))
  assert.ok(screen.getByText('Review local guidance before proceeding.'))
})
test('WEB-PLANNER-UI-022 removing a stop supports undo before persistence', async () => {
  const calls = install(); render(`/planner/itineraries/${I}`); await screen.findByRole('heading', { name: trip.title })
  fireEvent.click(screen.getByRole('button', { name: 'Edit trip' })); fireEvent.click(screen.getByRole('button', { name: `Remove ${candidate.title}` }))
  await screen.findByText('A little room in your day.'); fireEvent.click(screen.getByRole('button', { name: 'Undo removal' }))
  assert.ok(screen.getByRole('heading', { name: candidate.title })); assert.equal(calls.filter(c => c.init.method === 'PUT').length, 0)
})
test('WEB-PLANNER-UI-023 deleting a trip requires a named confirmation', async () => {
  const calls = install({ [`DELETE /api/planner/itineraries/${I}`]: () => new Response(null, { status: 204 }), 'GET /api/planner/itineraries?page=1&pageSize=20': [] })
  render(`/planner/itineraries/${I}`); await screen.findByRole('heading', { name: trip.title }); fireEvent.click(screen.getByRole('button', { name: 'Delete this trip' }))
  assert.ok(screen.getByRole('heading', { name: 'Delete this saved trip?' })); assert.equal(calls.filter(c => c.init.method === 'DELETE').length, 0)
  fireEvent.click(screen.getByRole('button', { name: 'Keep my trip' })); assert.equal(calls.filter(c => c.init.method === 'DELETE').length, 0)
  fireEvent.click(screen.getByRole('button', { name: 'Delete this trip' })); fireEvent.click(screen.getByRole('button', { name: 'Delete trip' }))
  await screen.findByRole('heading', { name: 'Your first coastal day awaits.' }); assert.equal(calls.filter(c => c.init.method === 'DELETE').length, 1)
})

test('WEB-PLANNER-UI-024 adding a suggestion to an existing trip preserves identity and sends the current version', async () => {
  const earlier = { ...trip, startsAt: new Date(Date.parse(start) - 7200000).toISOString(), concurrencyVersion: 3,
    items: [{ ...trip.items[0], scheduledStart: new Date(Date.parse(start) - 7200000).toISOString(), scheduledEnd: start, orderIndex: 10 }] }
  const calls = install({ 'GET /api/planner/itineraries?page=1&pageSize=20': [earlier],
    [`GET /api/planner/itineraries/${I}`]: earlier, [`PUT /api/planner/itineraries/${I}`]: { ...trip, concurrencyVersion: 4 } })
  render(`/planner/recommendations/${R}`)
  fireEvent.click(await screen.findByRole('button', { name: /Plan around this experience/ }))
  fireEvent.click(screen.getByRole('button', { name: 'Add to a saved trip instead' }))
  fireEvent.change(await screen.findByLabelText('Save to'), { target: { value: I } })
  fireEvent.click(screen.getByRole('button', { name: 'Save my trip' }))
  await screen.findByRole('heading', { name: trip.title })
  const body = JSON.parse(calls.find(c => c.init.method === 'PUT').init.body)
  assert.equal(body.concurrencyVersion, 3)
  assert.equal(body.recommendationId, R)
  assert.equal(body.items[0].itemId, A)
  assert.deepEqual(body.items.map(i => i.orderIndex), [0, 1])
  assert.equal(body.items[1].scheduledStart, candidate.scheduledStart)
  assert.equal(calls.filter(c => c.init.method === 'POST').length, 0)
})

test('WEB-PLANNER-UI-025 a trip edit warns before tab closure and cancellation removes the warning', async () => {
  install(); render(`/planner/itineraries/${I}`)
  await screen.findByRole('heading', { name: trip.title })
  fireEvent.click(screen.getByRole('button', { name: 'Edit trip' }))
  fireEvent.change(screen.getByLabelText('Trip name'), { target: { value: 'Unsaved coastal day' } })
  const pending = new window.Event('beforeunload', { cancelable: true })
  window.dispatchEvent(pending)
  assert.equal(pending.defaultPrevented, true)
  assert.equal(screen.getByLabelText('Trip name').value, 'Unsaved coastal day')
  fireEvent.click(screen.getByRole('button', { name: 'Cancel changes' }))
  const settled = new window.Event('beforeunload', { cancelable: true })
  window.dispatchEvent(settled)
  assert.equal(settled.defaultPrevented, false)
})
