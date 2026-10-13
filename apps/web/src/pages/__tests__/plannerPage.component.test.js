// Adithya approved replacing UUID-entry expectations on 2026-10-05.
// PROJECT_REQUIREMENTS 16/17; planner-recommendations/result/saved-trips/
// trip-management. Preserve validation, permission and dependency coverage.
import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { act, cleanup, closeWebTestServer, createElement, fireEvent, jsonResponse, loadWebModule,
  makeAuthSessionValue, makeAuthUser, renderInApp, resetTestBrowser, screen, waitFor, within, AuthSessionContext } from '../../testSupport/reactTestHarness.js'
import { useState } from 'react'
const [{ default: PlannerPage }, { default: PlannerPlanPage }, { default: SavedTripsPage }, { default: ItineraryPage }, { Routes, Route }] = await Promise.all([
  loadWebModule('/src/pages/PlannerPage.tsx'), loadWebModule('/src/pages/PlannerPlanPage.tsx'), loadWebModule('/src/pages/SavedTripsPage.tsx'), loadWebModule('/src/pages/ItineraryPage.tsx'), import('react-router')])
const { default: AppRoutes } = await loadWebModule('/src/app/routes.tsx')
const D = '3fa85f64-5717-4562-b3fc-2c963f66afa6', A = '4fa85f64-5717-4562-b3fc-2c963f66afa7', R = '5fa85f64-5717-4562-b3fc-2c963f66afa8', I = '6fa85f64-5717-4562-b3fc-2c963f66afa9'
const permissions = ['planner.recommendations.create', 'planner.recommendations.read', 'planner.itineraries.manage', 'planner.workflows.read', 'planner.biodiversity.read']
const start = new Date(Date.now() + 86400000).toISOString(), end = new Date(Date.parse(start) + 4 * 3600000).toISOString()
const options = { status: 'AVAILABLE', message: null, destinations: [{ destinationId: D, name: 'Mirissa', region: 'Southern coast', timeZone: 'Asia/Colombo', activities: [{ activityId: A, name: 'Guided snorkelling' }] }] }
const candidate = { destinationId: D, activityId: A, offeringId: A, title: 'A morning in the lagoon', scheduledStart: start, scheduledEnd: end,
  availabilityStatus: 'AVAILABLE', suitability: { status: 'SUITABLE', marineConditionTime: start, safetyProfileId: D }, operationalStatus: 'OPEN', biodiversityContext: null, fitScore: .95, reasons: ['Published and available.', 'Marine conditions match the activity.'], timeZone: 'Asia/Colombo' }
const recommendations = { recommendationId: R, workflowId: I, status: 'COMPLETED', generatedAt: new Date().toISOString(), candidates: [candidate], excludedCandidatesCount: 0, uncertaintyNotes: [] }
const trip = { itineraryId: I, ownerUserId: D, title: 'Mirissa morning', description: 'Pack reef-safe sunscreen', startsAt: start, endsAt: end, timeZone: 'Asia/Colombo', concurrencyVersion: 1, createdAt: start, updatedAt: start, status: 'Draft',
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
    const key = `${init.method ?? 'GET'} ${input}`
    const route = routes[key]
    assert.notEqual(route, undefined, `Unexpected public request: ${input}`)
    return typeof route === 'function' ? route(input, init) : jsonResponse(route)
  }
  return calls
}
function render(path = '/planner/plan', auth = {}) {
  renderInApp(createElement(Routes, null,
    createElement(Route, { path: '/planner/plan', element: createElement(PlannerPlanPage) }),
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
  render('/planner/plan', { status: 'signed-out', user: null })
  assert.ok(screen.getByRole('heading', { name: 'Sign in to plan with the coast.' }))
  assert.equal(screen.queryByLabelText('Destination'), null)
  cleanup(); render('/planner/plan', { status: 'checking', user: null })
  assert.equal(screen.queryByLabelText('Destination'), null); assert.equal(calls.length, 0)
})
test('WEB-PLANNER-UI-000 recommendation home shows highlights and links to planning', async () => {
  install({ 'GET /api/planner/recommendations/highlights': [{ id: R, title: 'Lagoon morning', destinationId: D, destinationName: 'Mirissa', activityId: A, activityName: 'Guided snorkelling', startsAt: start, endsAt: end, timeZone: 'Asia/Colombo', availabilityStatus: 'AVAILABLE', suitabilityStatus: 'SUITABLE', operationalStatus: 'OPEN', offeringId: A, fitScore: .9, recommendationId: null, uncertaintyNotes: [], generatedAt: start, outcome: 'MATCHES_FOUND', itemCount: 1 }] })
  render('/planner')
  await screen.findByRole('heading', { name: 'Coastal days to inspire you.' })
  assert.ok(screen.getByText('Lagoon morning'))
  assert.equal(screen.getAllByRole('link', { name: 'Create a plan' })[0].getAttribute('href'), '/planner/plan')
  assert.equal(screen.queryByRole('complementary', { name: 'Account navigation' }), null)
  assert.equal(screen.queryByRole('link', { name: 'Plan history' }), null)
  assert.equal(screen.queryByLabelText('Destination'), null)
})
test('WEB-PLANNER-UI-027 recommendation empty and failed states offer recovery', async () => {
  install({ 'GET /api/planner/recommendations/highlights': [] }); render('/planner')
  await screen.findByRole('heading', { name: 'No current coastal ideas.' }); assert.ok(screen.getByRole('button', { name: 'Try again' }))
  cleanup(); install({ 'GET /api/planner/recommendations/highlights': () => jsonResponse({ detail: 'Offline' }, 503) }); render('/planner')
  await screen.findByRole('alert'); assert.ok(screen.getByRole('button', { name: 'Try again' }))
})
test('WEB-PLANNER-UI-002 named choices have labeled defaults and no UUID text fields', async () => {
  install(); render(); await ready()
  assert.equal(document.getElementById('planner-destination').tagName, 'SELECT')
  assert.ok(screen.getByRole('option', { name: 'Mirissa · Southern coast' }))
  assert.equal(document.getElementById('planner-duration').value, '4')
  assert.ok(screen.getByRole('option', { name: 'Steady · 4 hours' }))
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
test('WEB-PLANNER-UI-006 configured pace duration is constrained by the chosen trip window', async () => {
  const calls = install(); render(); await ready(); fill(); set('planner-ends', `${document.getElementById('planner-starts').value.slice(0, 11)}11:00`)
  set('planner-duration', '8'); submit(); assert.match(screen.getByRole('alert').textContent, /whole number of hours that fits within your trip/)
  assert.ok(screen.getByRole('option', { name: 'Relaxed · 2 hours' })); assert.ok(screen.getByRole('option', { name: 'Full day · 8 hours' }))
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
  assert.equal(screen.getByTestId('router-location').textContent, '/planner/plan')
})
test('WEB-PLANNER-UI-009 result evidence, cautions and uncertainty remain visible', async () => {
  install({ [`GET /api/planner/recommendations/${R}`]: { ...recommendations, candidates: [{ ...candidate, suitability: { ...candidate.suitability, status: 'CAUTION' } }], uncertaintyNotes: ['Wildlife context unavailable.'] } })
  render(`/planner/recommendations/${R}`); await screen.findByRole('heading', { name: candidate.title })
  assert.ok(screen.getByRole('heading', { name: 'Here’s what the checks found' })); assert.ok(screen.getByText('Suggestions returned').parentElement.textContent.includes('1'))
  assert.ok(screen.getByText('A caution to review')); assert.ok(screen.getByText('Why this fits'))
  assert.ok(screen.getByText('Wildlife context unavailable.')); assert.ok(screen.getByText(/No verified wildlife context was attached/))
  assert.equal(screen.queryByText('95%'), null)
})
test('WEB-PLANNER-UI-040 recommendation outcomes distinguish no matches from unavailable checks (ui-integration: planner-recommendation-result)', async () => {
  install({ [`GET /api/planner/recommendations/${R}`]: { ...recommendations, outcome: 'DEPENDENCIES_UNAVAILABLE', candidates: [], uncertaintyNotes: ['Marine conditions unavailable.'] } })
  render(`/planner/recommendations/${R}`)
  await screen.findByText(/Some live coastal checks were unavailable/)
  assert.ok(screen.getByRole('heading', { name: 'No verified matches for this trip.' }))
  assert.ok(screen.getByText('Marine conditions unavailable.'))
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
  install({ [`GET /api/planner/workflows/${I}`]: { workflowId: I, workflowType: 'Recommendation', status: 'FAILED', initiatorUserId: D, objective: 'Coastal day', createdAt: start, completedAt: end, resultSummary: null, failureReason: 'Please retry this search.', aiDependencyStatus: 'AVAILABLE', aiExecutionStatus: 'SAFE_FAILURE' } })
  render(`/planner/recommendations/${R}`); await screen.findByRole('heading', { name: candidate.title })
  fireEvent.click(screen.getByRole('button', { name: 'View coordination status' })); await screen.findByText('Please retry this search.')
  const statusPanel = screen.getByRole('heading', { name: 'Planning agent coordination' }).closest('section')
  assert.ok(within(statusPanel).getByText('Available')); assert.ok(within(statusPanel).getByText('Stopped safely'))
  assert.ok(within(statusPanel).getByText(/does not include separate responses from each delegated agent/))
})
test('WEB-PLANNER-UI-041 workflow inspection shows delegated agent findings and uncertainty', async () => {
  install({ [`GET /api/planner/workflows/${I}`]: {
    workflowId: I, workflowType: 'Recommendation', status: 'COMPLETED', initiatorUserId: D,
    objective: 'Plan a coastal day', createdAt: start, completedAt: end, resultSummary: null, failureReason: null,
    aiDependencyStatus: 'AVAILABLE', aiExecutionStatus: 'COMPLETED',
    aiExecutionSummary: {
      status: 'COMPLETED', objectiveSummary: 'Three coastal checks were gathered for your selected dates.',
      steps: [
        { agentId: 'experience-biodiversity', status: 'COMPLETED', summary: 'A published lagoon experience is available.', sourceReferences: ['experience-1'], uncertaintyNotes: [] },
        { agentId: 'marine-safety', status: 'COMPLETED', summary: 'Conditions are suitable for this activity.', sourceReferences: ['marine-1'], uncertaintyNotes: ['Check the local marine notice again before departure.'] },
      ],
      safetyProposal: null,
    },
  } })
  render(`/planner/recommendations/${R}`); await screen.findByRole('heading', { name: candidate.title })
  fireEvent.click(screen.getByRole('button', { name: 'View coordination status' }))
  assert.ok(await screen.findByText('Three coastal checks were gathered for your selected dates.'))
  assert.ok(screen.getByRole('heading', { name: 'Coastal experiences & wildlife' }))
  assert.ok(screen.getByRole('heading', { name: 'Marine conditions' }))
  assert.ok(screen.getByText('A published lagoon experience is available.'))
  assert.ok(screen.getByText('Check the local marine notice again before departure.'))
  assert.equal(screen.queryByText(/does not include separate responses/), null)
})
test('WEB-PLANNER-UI-014 optional biodiversity shows metadata without claiming a sighting', async () => {
  install({ [`GET /api/planner/biodiversity/predictions?destinationId=${D}&activityId=${A}`]: { contractVersion: 1, destinationId: D, activityId: A, status: 'AVAILABLE', message: 'Wildlife context is available. A prediction does not guarantee a sighting.', predictedSpecies: [{ speciesId: R, commonName: 'Green turtle', scientificName: 'Chelonia mydas', habitatSuitability: .8, confidenceLevel: 'HIGH' }], modelMetadata: { modelVersion: 'fixture-v1', inferenceTimestamp: start }, provenance: { provider: 'test-provider', modelVersion: 'fixture-v1', inferenceTimestamp: start }, limitations: 'Not a guaranteed sighting.' } })
  render(`/planner/recommendations/${R}`); await screen.findByRole('heading', { name: candidate.title })
  fireEvent.click(screen.getByText('Conditions & wildlife context')); fireEvent.click(screen.getByRole('button', { name: 'Check wildlife context' }))
  await screen.findByText('Green turtle (Chelonia mydas)'); assert.ok(screen.getByText('Not a guaranteed sighting.')); assert.ok(screen.getByText(/Model fixture-v1/))
})
test('WEB-PLANNER-UI-015 unavailable and empty wildlife predictions remain honest', async () => {
  install({ [`GET /api/planner/biodiversity/predictions?destinationId=${D}&activityId=${A}`]: { contractVersion: 1, destinationId: D, activityId: A, status: 'UNAVAILABLE', predictedSpecies: [], modelMetadata: null, limitations: 'No current prediction.' } })
  render(`/planner/recommendations/${R}`); await screen.findByRole('heading', { name: candidate.title })
  fireEvent.click(screen.getByText('Conditions & wildlife context')); fireEvent.click(screen.getByRole('button', { name: 'Check wildlife context' }))
  await screen.findByText('Wildlife predictions are temporarily unavailable. Please try again later.')
})
test('WEB-PLANNER-UI-016 experience level and wildlife opt-out are submitted faithfully', async () => {
  const calls = install({ 'POST /api/planner/recommendations': recommendations }); render(); await ready(); fill()
  set('planner-experience', 'BEGINNER'); fireEvent.click(screen.getByLabelText(/Include wildlife context/)); submit()
  await screen.findByRole('heading', { name: candidate.title }); const body = JSON.parse(calls.find(c => c.init.method === 'POST').init.body)
  assert.equal(body.experienceLevel, 'BEGINNER'); assert.equal(body.includeBiodiversityContext, false)
})
test('WEB-PLANNER-UI-017 permission denial prevents catalogue requests and hides save actions', async () => {
  const calls = install(); render('/planner/plan', { user: makeAuthUser({ permissions: [] }) })
  assert.ok(screen.getByRole('heading', { name: 'Coastal planning access' })); assert.equal(calls.length, 0)
  cleanup(); render(`/planner/recommendations/${R}`, { user: makeAuthUser({ permissions: ['planner.recommendations.read'] }) })
  await screen.findByRole('heading', { name: candidate.title }); assert.equal(screen.queryByRole('button', { name: /Plan around this experience/ }), null)
})
test('WEB-PLANNER-UI-018 save uses a named trip and the owner recommendation reference', async () => {
  const calls = install({ 'POST /api/planner/itineraries': trip }); render(`/planner/recommendations/${R}`)
  await screen.findByRole('heading', { name: candidate.title }); fireEvent.click(screen.getByRole('button', { name: /Plan around this experience/ }))
  fireEvent.change(screen.getByLabelText('Trip name'), { target: { value: 'Mirissa morning' } }); fireEvent.click(screen.getByRole('button', { name: 'Save my trip' }))
  await screen.findByRole('heading', { name: trip.title }); assert.ok(screen.getByText('Draft')); assert.ok(screen.getByRole('button', { name: 'Edit trip' })); assert.ok(screen.getByRole('button', { name: 'Cancel this draft' })); const body = JSON.parse(calls.find(c => c.init.method === 'POST').init.body)
  assert.equal(body.recommendationId, R); assert.equal(body.title, 'Mirissa morning'); assert.equal(body.items[0].offeringId, A)
})
test('WEB-PLANNER-UI-019 saved trips show empty recovery and owner trip links', async () => {
  install({ 'GET /api/planner/itineraries?page=1&pageSize=20': [] }); render('/planner/saved')
  await screen.findByRole('heading', { name: 'Your first coastal day awaits.' }); assert.equal(screen.getByRole('link', { name: /Create your first plan/ }).getAttribute('href'), '/planner/plan')
  cleanup(); install({ 'GET /api/planner/itineraries?page=1&pageSize=20': [trip, { ...trip, itineraryId: R, title: 'Confirmed coast day', status: 'Confirmed' }] }); render('/planner/saved')
  await screen.findByRole('heading', { name: trip.title }); assert.ok(screen.getAllByRole('link', { name: 'Open this trip' }).some(link => link.getAttribute('href') === `/planner/itineraries/${I}`))
  assert.ok(screen.getAllByText('Draft').some(element => element.tagName !== 'BUTTON')); assert.ok(screen.getAllByText('Confirmed').some(element => element.tagName !== 'BUTTON')); assert.equal(screen.queryByRole('button', { name: /edit|cancel|remove/i }), null)
})

test('WEB-PLANNER-UI-035 empty history presents one hero image, one creation action and the reference history controls', async () => {
  install({ 'GET /api/planner/itineraries?page=1&pageSize=20': [] }); render('/planner/saved')
  await screen.findByRole('heading', { name: 'Your first coastal day awaits.' })
  const main = within(screen.getByRole('main'))
  assert.equal(main.getAllByRole('img').length, 1)
  assert.equal(main.getAllByRole('link', { name: /Create your first plan/ }).length, 1)
  assert.equal(main.queryByRole('link', { name: /Create a new plan/ }), null)
  assert.ok(main.getByRole('group', { name: 'Filter plans on this page' }))
  assert.ok(main.getByRole('searchbox'))
  const paging = within(main.getByRole('navigation', { name: 'Saved trip pages' }))
  assert.equal(paging.getByRole('button', { name: 'Previous' }).disabled, true)
  assert.equal(paging.getByRole('button', { name: 'Next' }).disabled, true)
  assert.equal(paging.getByLabelText('Plans per page').value, '20')
  cleanup(); render('/planner/saved', { user: makeAuthUser({ permissions: ['planner.itineraries.manage'] }) })
  await screen.findByRole('heading', { name: 'Your first coastal day awaits.' })
  assert.equal(within(screen.getByRole('main')).getAllByRole('img').length, 1)
  assert.equal(screen.queryByRole('link', { name: /Create your first plan/ }), null)
})

test('WEB-PLANNER-UI-036 planning page tabs select the current route, change pages and respect grants', async () => {
  install({ 'GET /api/planner/itineraries?page=1&pageSize=20': [] }); render(); await ready()
  let tabs = within(screen.getByRole('navigation', { name: 'Planning pages' }))
  assert.equal(tabs.getByRole('link', { name: 'Create a plan' }).getAttribute('aria-current'), 'page')
  assert.equal(tabs.getByRole('link', { name: 'Plan history' }).getAttribute('aria-current'), null)
  assert.ok(screen.getByRole('region', { name: 'Coastal planning introduction' }))
  fireEvent.click(tabs.getByRole('link', { name: 'Plan history' }))
  await screen.findByRole('heading', { name: 'Your first coastal day awaits.' })
  tabs = within(screen.getByRole('navigation', { name: 'Planning pages' }))
  assert.equal(tabs.getByRole('link', { name: 'Plan history' }).getAttribute('aria-current'), 'page')
  assert.ok(screen.getByRole('region', { name: 'Plan history introduction' }))
  fireEvent.click(tabs.getByRole('link', { name: 'Create a plan' })); await ready()
  cleanup(); render('/planner/saved', { user: makeAuthUser({ permissions: ['planner.itineraries.manage'] }) })
  await screen.findByRole('heading', { name: 'Your first coastal day awaits.' })
  assert.equal(within(screen.getByRole('navigation', { name: 'Planning pages' })).queryByRole('link', { name: 'Create a plan' }), null)
})

test('WEB-PLANNER-UI-037 history page size reaches the public API and paging uses the unfiltered page length', async () => {
  const full = Array.from({ length: 25 }, (_, index) => {
    const id = I.slice(0, -2) + String(index + 10)
    return { ...trip, itineraryId: id, title: `Coastal day ${index + 1}`, items: trip.items.map(item => ({ ...item, itineraryId: id })) }
  })
  const calls = install({ 'GET /api/planner/itineraries?page=1&pageSize=20': [trip],
    'GET /api/planner/itineraries?page=1&pageSize=25': full, 'GET /api/planner/itineraries?page=2&pageSize=25': [] })
  render('/planner/saved'); await screen.findByRole('heading', { name: trip.title })
  fireEvent.change(screen.getByLabelText('Plans per page'), { target: { value: '25' } })
  await screen.findByRole('heading', { name: 'Coastal day 25' })
  fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'Coastal day 25' } })
  const paging = () => within(screen.getByRole('navigation', { name: 'Saved trip pages' }))
  assert.equal(paging().getByRole('button', { name: 'Next' }).disabled, false)
  fireEvent.click(paging().getByRole('button', { name: 'Next' }))
  await screen.findByRole('heading', { name: 'You’ve reached the end of your trips.' })
  assert.equal(screen.getByRole('searchbox').value, '')
  assert.equal(paging().getByRole('button', { name: 'Previous' }).disabled, false)
  assert.equal(paging().getByRole('button', { name: 'Next' }).disabled, true)
  fireEvent.click(paging().getByRole('button', { name: 'Previous' }))
  await screen.findByRole('heading', { name: 'Coastal day 25' })
  assert.ok(calls.every(call => call.init.credentials === 'include'))
  assert.deepEqual(calls.map(call => call.input), ['/api/planner/itineraries?page=1&pageSize=20', '/api/planner/itineraries?page=1&pageSize=25', '/api/planner/itineraries?page=2&pageSize=25', '/api/planner/itineraries?page=1&pageSize=25'])
})

test('WEB-PLANNER-UI-038 history refinement and reset operate on real condition metadata without extra requests', async () => {
  const reviewed = { ...trip, itineraryId: R, title: 'A reviewed day', items: trip.items.map(item => ({ ...item, lastSuitabilityStatus: 'SUITABLE', lastAvailabilityStatus: 'AVAILABLE', lastOperationalStatus: 'OPEN' })) }
  const calls = install({ 'GET /api/planner/itineraries?page=1&pageSize=20': [trip, reviewed] })
  render('/planner/saved'); await screen.findByRole('heading', { name: reviewed.title })
  const toggle = screen.getByRole('button', { name: 'Filter options' })
  assert.equal(toggle.getAttribute('aria-expanded'), 'false'); fireEvent.click(toggle)
  assert.equal(toggle.getAttribute('aria-expanded'), 'true')
  fireEvent.click(screen.getByRole('checkbox', { name: 'Only plans needing a conditions check' }))
  assert.equal(screen.queryByRole('heading', { name: reviewed.title }), null)
  assert.ok(screen.getByRole('heading', { name: trip.title }))
  fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'missing' } }); fireEvent.click(screen.getByRole('button', { name: 'Search plans' }))
  assert.ok(screen.getByRole('heading', { name: 'No plans match on this page.' }))
  fireEvent.click(screen.getByRole('button', { name: 'Reset plan filters' }))
  assert.ok(screen.getByRole('heading', { name: reviewed.title }))
  assert.equal(screen.getByRole('searchbox').value, '')
  assert.equal(screen.getByRole('checkbox').checked, false)
  assert.equal(calls.length, 1)
})
test('WEB-PLANNER-UI-020 editing preserves identity and sends optimistic version; conflicts keep the draft', async () => {
  const calls = install({ [`PUT /api/planner/itineraries/${I}`]: () => jsonResponse({ detail: 'Conflict' }, 409) })
  render(`/planner/itineraries/${I}`); await screen.findByRole('heading', { name: trip.title }); fireEvent.click(screen.getByRole('button', { name: 'Edit trip' }))
  fireEvent.change(screen.getByLabelText('Trip name'), { target: { value: 'A different name' } }); fireEvent.click(screen.getByRole('button', { name: 'Save changes' }))
  await screen.findByRole('alert'); assert.equal(screen.getByLabelText('Trip name').value, 'A different name')
  const body = JSON.parse(calls.find(c => c.init.method === 'PUT').init.body); assert.equal(body.items[0].itemId, A); assert.equal(body.concurrencyVersion, 1)
})
test('WEB-PLANNER-UI-021 condition reviews show previous evidence and preserve selected times', async () => {
  install({ [`GET /api/planner/itineraries/${I}`]: { ...trip, status: 'Confirmed' }, [`POST /api/planner/itineraries/${I}/re-evaluations`]: evaluation })
  render(`/planner/itineraries/${I}`); await screen.findByRole('heading', { name: trip.title }); fireEvent.click(screen.getByRole('button', { name: 'Review current conditions' }))
  assert.ok(screen.getByText('Confirmed')); assert.equal(screen.queryByRole('button', { name: 'Edit trip' }), null); assert.equal(screen.queryByRole('button', { name: 'Cancel this draft' }), null)
  await screen.findByText(/Your chosen experiences and times have been kept/); assert.ok(screen.getByText(/Not verified → Review caution/))
  assert.ok(screen.getByText('Review local guidance before proceeding.'))
})
test('WEB-PLANNER-UI-022 removing a stop supports undo before persistence', async () => {
  const calls = install(); render(`/planner/itineraries/${I}`); await screen.findByRole('heading', { name: trip.title })
  fireEvent.click(screen.getByRole('button', { name: 'Edit trip' })); fireEvent.click(screen.getByRole('button', { name: `Remove ${candidate.title}` }))
  await screen.findByText('A little room in your day.'); fireEvent.click(screen.getByRole('button', { name: 'Undo removal' }))
  assert.ok(screen.getByRole('heading', { name: candidate.title })); assert.equal(calls.filter(c => c.init.method === 'PUT').length, 0)
})
test('WEB-PLANNER-UI-023 cancelling a draft requires a named confirmation', async () => {
  const calls = install({ [`DELETE /api/planner/itineraries/${I}`]: () => new Response(null, { status: 204 }), 'GET /api/planner/itineraries?page=1&pageSize=20': [] })
  render(`/planner/itineraries/${I}`); await screen.findByRole('heading', { name: trip.title }); fireEvent.click(screen.getByRole('button', { name: 'Cancel this draft' }))
  assert.ok(screen.getByRole('heading', { name: 'Cancel this draft?' })); assert.equal(calls.filter(c => c.init.method === 'DELETE').length, 0)
  fireEvent.click(screen.getByRole('button', { name: 'Keep draft' })); assert.equal(calls.filter(c => c.init.method === 'DELETE').length, 0)
  fireEvent.click(screen.getByRole('button', { name: 'Cancel this draft' })); fireEvent.click(screen.getByRole('button', { name: 'Cancel draft' }))
  await screen.findByRole('heading', { name: 'Your first coastal day awaits.' }); assert.equal(calls.filter(c => c.init.method === 'DELETE').length, 1)
})

test('WEB-PLANNER-UI-026 confirming a draft warns and leaves confirmed trip read-only', async () => {
  const calls = install({ [`POST /api/planner/itineraries/${I}/confirm`]: { ...trip, status: 'Confirmed' } })
  render(`/planner/itineraries/${I}`); await screen.findByRole('heading', { name: trip.title })
  fireEvent.click(screen.getByRole('button', { name: 'Confirm this trip' }))
  assert.ok(screen.getByText(/can no longer be edited or cancelled/)); assert.equal(calls.filter(c => c.input.endsWith('/confirm')).length, 0)
  fireEvent.click(screen.getByRole('button', { name: 'Keep as draft' })); assert.equal(calls.filter(c => c.input.endsWith('/confirm')).length, 0)
  fireEvent.click(screen.getByRole('button', { name: 'Confirm this trip' })); fireEvent.click(screen.getByRole('button', { name: 'Confirm trip' }))
  await screen.findByText('Confirmed'); assert.equal(screen.queryByRole('button', { name: 'Edit trip' }), null); assert.equal(screen.queryByRole('button', { name: 'Cancel this draft' }), null)
  assert.ok(screen.getByRole('button', { name: 'Review current conditions' }))
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

// React-first redesign approved on 2026-10-12. Keep server ownership,
// permission, validation, failure and persistence assertions above intact.
test('WEB-PLANNER-UI-028 introduction is available to guests, restoring accounts and users without planner grants', async () => {
  for (const auth of [
    { status: 'signed-out', user: null }, { status: 'checking', user: null },
    { status: 'unavailable', user: null }, { status: 'signed-in', user: makeAuthUser({ permissions: [] }) },
  ]) {
    const calls = install({ 'GET /api/planner/recommendations/highlights': [] })
    render('/planner', auth)
    assert.ok(screen.getByRole('heading', { name: /A coastal day, made for you/ }))
    assert.ok(screen.getByRole('heading', { name: 'Your plan, in three simple steps.' }))
    assert.ok(screen.getByRole('link', { name: /Let’s plan your trip/ }))
    await screen.findByRole('heading', { name: 'No current coastal ideas.' })
    assert.deepEqual(calls.map(c => c.input), ['/api/planner/recommendations/highlights'])
    assert.equal(screen.queryByLabelText('Destination'), null)
    assert.equal(document.querySelectorAll('main').length, 1)
    assert.equal(screen.queryByRole('complementary', { name: 'Account navigation' }), null)
    assert.equal(screen.queryByRole('link', { name: 'The planner' }), null)
    cleanup()
  }
})

function LoginFlow() {
  const [user, setUser] = useState(null)
  return createElement(AuthSessionContext.Provider, { value: makeAuthSessionValue({
    status: user ? 'signed-in' : 'signed-out', user, acceptAuthenticatedUser: setUser,
  }) }, createElement(AppRoutes))
}

test('WEB-PLANNER-UI-029 guest CTA signs in then returns to the actual creation page', async () => {
  const authenticatedUser = makeAuthUser({ permissions })
  const calls = install({ 'GET /api/planner/recommendations/highlights': [],
    'POST /api/auth/login': { token: null, user: authenticatedUser, roles: authenticatedUser.roles, permissions } })
  renderInApp(createElement(LoginFlow), { path: '/planner' })
  fireEvent.click(await screen.findByRole('link', { name: /Let’s plan your trip/ }))
  await screen.findByRole('heading', { name: 'Welcome back.' })
  assert.equal(screen.getByTestId('router-location').textContent, '/signin?returnTo=%2Fplanner%2Fplan')
  assert.equal(calls.some(c => c.input === '/api/planner/catalogue'), false)
  fireEvent.change(screen.getByLabelText('Email address'), { target: { value: 'coast@example.test' } })
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'fixture-password' } })
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
  await screen.findByLabelText('Destination')
  assert.ok(screen.getByRole('heading', { name: 'Create your coastal plan.' }))
  assert.equal(screen.getByTestId('router-location').textContent, '/planner/plan')
  const loginCall = calls.find(c => c.input === '/api/auth/login')
  assert.equal(loginCall.init.credentials, 'include')
  assert.equal(JSON.parse(loginCall.init.body).useCookies, true)
  assert.equal(calls.filter(c => c.input === '/api/planner/catalogue').length, 1)
})

test('WEB-PLANNER-UI-030 public introduction opens creation; personal sidebar opens history and defaults to creation', async () => {
  const calls = install({ 'GET /api/planner/recommendations/highlights': [], 'GET /api/planner/itineraries?page=1&pageSize=20': [trip] })
  renderInApp(createElement(AppRoutes), { path: '/planner', auth: makeAuthSessionValue({ status: 'signed-in', user: makeAuthUser({ permissions }) }) })
  await screen.findByRole('heading', { name: /A coastal day, made for you/ })
  assert.equal(screen.queryByRole('link', { name: 'The planner' }), null)
  assert.equal(screen.queryByRole('complementary', { name: 'Account navigation' }), null)
  const mobileMenu = within(screen.getByRole('navigation', { name: 'Mobile navigation' }))
  assert.equal(mobileMenu.queryByRole('link', { name: 'Create a plan' }), null)
  assert.equal(mobileMenu.queryByRole('link', { name: 'Plan history' }), null)
  const sidebar = () => within(screen.getByRole('complementary', { name: 'Account navigation' }))
  fireEvent.click(screen.getByRole('link', { name: /Let’s plan your trip/ }))
  await screen.findByLabelText('Destination')
  assert.equal(screen.getByTestId('router-location').textContent, '/planner/plan')
  assert.equal(document.querySelectorAll('main').length, 1)
  assert.ok(sidebar().getAllByRole('link', { name: 'Coastal planner' }).every(link => link.getAttribute('href') === '/planner/plan'))
  assert.equal(screen.queryByRole('navigation', { name: 'Main navigation' }), null)
  assert.equal(screen.queryByRole('link', { name: 'The planner' }), null)
  assert.ok(sidebar().getAllByRole('link', { name: 'Create a plan' }).every(link => link.getAttribute('aria-current') === 'page'))
  fireEvent.click(sidebar().getAllByRole('link', { name: 'Plan history' })[0])
  await screen.findByRole('heading', { name: trip.title })
  assert.ok(screen.getByRole('heading', { name: 'Your plan history.' }))
  assert.equal(screen.getByTestId('router-location').textContent, '/planner/saved')
  assert.equal(document.querySelectorAll('main').length, 1)
  assert.ok(sidebar().getAllByRole('link', { name: 'Plan history' }).every(link => link.getAttribute('aria-current') === 'page'))
  assert.equal(calls.some(c => c.input === '/api/auth/login'), false)
  assert.equal(calls.find(c => c.input.includes('itineraries?page')).init.credentials, 'include')
})

test('WEB-PLANNER-UI-031 creation shows every returned candidate and saves the chosen suggestion', async () => {
  const second = { ...candidate, offeringId: R, title: 'Sunset kayaking' }
  const saved = { ...trip, title: 'My sunset day', items: [{ ...trip.items[0], ...second }] }
  const calls = install({ 'POST /api/planner/recommendations': { ...recommendations, candidates: [candidate, second] },
    'POST /api/planner/itineraries': saved, [`GET /api/planner/itineraries/${I}`]: saved })
  render(); await ready(); fill(); submit()
  await screen.findByRole('heading', { name: second.title })
  assert.equal(screen.getAllByRole('radio').length, 2)
  fireEvent.click(screen.getByRole('radio', { name: `Choose ${second.title}` }))
  assert.equal(screen.getByRole('radio', { name: `Choose ${second.title}` }).checked, true)
  fireEvent.click(screen.getByRole('button', { name: /Save a draft plan/ }))
  assert.equal(screen.getByLabelText('Trip name').value, second.title)
  fireEvent.change(screen.getByLabelText('Trip name'), { target: { value: saved.title } })
  fireEvent.click(screen.getByRole('button', { name: 'Save my trip' }))
  await screen.findByRole('heading', { name: saved.title })
  const body = JSON.parse(calls.find(c => c.input === '/api/planner/itineraries').init.body)
  assert.equal(body.title, saved.title)
  assert.equal(body.recommendationId, R)
  assert.equal(body.items.length, 1)
  assert.equal(body.items[0].offeringId, second.offeringId)
  assert.equal(body.items[0].scheduledStart, second.scheduledStart)
  assert.equal(body.items[0].orderIndex, 0)
})

test('WEB-PLANNER-UI-032 history filters distinguish draft, confirmed and past plans and search only the current page', async () => {
  const past = { ...trip, itineraryId: D, title: 'A day we remember', status: 'Confirmed',
    startsAt: '2026-09-01T02:00:00Z', endsAt: '2026-09-01T06:00:00Z' }
  const confirmed = { ...trip, itineraryId: R, title: 'Confirmed coastal day', status: 'Confirmed' }
  const calls = install({ 'GET /api/planner/itineraries?page=1&pageSize=20': [trip, confirmed, past] })
  render('/planner/saved'); await screen.findByRole('heading', { name: trip.title })
  const controls = within(screen.getByRole('group', { name: 'Filter plans on this page' }))
  fireEvent.click(controls.getByRole('button', { name: 'Drafts' }))
  assert.equal(controls.getByRole('button', { name: 'Drafts' }).getAttribute('aria-pressed'), 'true')
  assert.ok(screen.getByRole('heading', { name: trip.title }))
  assert.equal(screen.queryByRole('heading', { name: confirmed.title }), null)
  fireEvent.click(controls.getByRole('button', { name: 'Past' }))
  assert.ok(screen.getByRole('heading', { name: past.title }))
  assert.equal(screen.queryByRole('heading', { name: trip.title }), null)
  fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'not a plan name' } })
  assert.ok(screen.getByRole('heading', { name: 'No plans match on this page.' }))
  fireEvent.click(screen.getByRole('button', { name: 'Show all plans on this page' }))
  assert.ok(screen.getByRole('heading', { name: confirmed.title }))
  assert.equal(screen.getByRole('searchbox').value, '')
  assert.equal(calls.length, 1)
})

test('WEB-PLANNER-UI-033 changing preferences clears old suggestions while retaining the edited form', async () => {
  install({ 'POST /api/planner/recommendations': recommendations })
  render(); await ready(); fill(); submit(); await screen.findByRole('heading', { name: candidate.title })
  set('planner-duration', '2')
  assert.equal(screen.queryByRole('radio'), null)
  assert.equal(screen.queryByRole('button', { name: /Save a draft plan/ }), null)
  assert.equal(screen.getByLabelText('Destination').value, D)
  assert.equal(screen.getByLabelText('Time for an experience').value, '2')
})

test('WEB-PLANNER-UI-034 history errors allow retry without exposing other account data', async () => {
  let attempts = 0
  const calls = install({ 'GET /api/planner/itineraries?page=1&pageSize=20': () => ++attempts === 1
    ? jsonResponse({ detail: 'database connection detail' }, 503) : jsonResponse([trip]) })
  render('/planner/saved'); await screen.findByRole('alert')
  assert.doesNotMatch(document.body.textContent, /database connection detail/)
  fireEvent.click(screen.getByRole('button', { name: 'Try again' }))
  await screen.findByRole('heading', { name: trip.title })
  assert.equal(attempts, 2)
  assert.ok(calls.every(c => c.init.credentials === 'include'))
  cleanup()
  const denied = install()
  render('/planner/saved', { user: makeAuthUser({ permissions: ['planner.recommendations.create'] }) })
  assert.ok(screen.getByRole('heading', { name: 'Coastal planning access' }))
  assert.equal(denied.length, 0)
})
