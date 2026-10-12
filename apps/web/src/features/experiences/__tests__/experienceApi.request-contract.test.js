import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { closeWebTestServer, jsonResponse, loadWebModule } from '../../../testSupport/reactTestHarness.js'

const experienceApi = await loadWebModule('/src/features/experiences/experienceApi.ts')

const originalFetch = globalThis.fetch
const recordId = 'd1111111-1111-1111-1111-111111111111'
const scheduleId = 's1111111-1111-1111-1111-111111111111'
const destinationInput = { name: 'Mirissa Bay', region: 'Southern Province' }
const activityInput = { code: 'WHALE-WATCH', name: 'Responsible Whale Watching' }
const offeringInput = { title: 'Dawn Whale Expedition', price: 85, currency: 'USD' }
const scheduleInput = { startsAt: '2026-11-01T08:00:00Z', endsAt: '2026-11-01T10:00:00Z', timeZoneId: 'Asia/Colombo' }
const availabilityInput = { offeringId: recordId, startsAt: scheduleInput.startsAt, endsAt: scheduleInput.endsAt }

afterEach(() => {
  globalThis.fetch = originalFetch
})

after(async () => closeWebTestServer())

function assertJsonRequest(request, { path, method = 'GET', body }) {
  assert.equal(request.input, path)
  assert.equal(request.init.method, method)
  assert.equal(request.init.credentials, 'include')
  assert.deepEqual(request.init.headers, {
    Accept: 'application/json',
    ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
  })
  assert.equal(request.init.body, body === undefined ? undefined : JSON.stringify(body))
}

test('WEB-EXP-API-001 every Experience operation uses its public route, HTTP verb, query and JSON contract', async () => {
  const cases = [
    { path: '/api/experiences/destinations?query=Mirissa+Bay&region=Southern+Province&status=DRAFT&page=2&pageSize=10', run: () => experienceApi.getDestinations({ query: 'Mirissa Bay', region: 'Southern Province', status: 'DRAFT', page: 2, pageSize: 10 }) },
    { path: `/api/experiences/destinations/${recordId}`, run: () => experienceApi.getDestinationById(recordId) },
    { path: '/api/experiences/destinations', method: 'POST', body: destinationInput, run: () => experienceApi.createDestination(destinationInput) },
    { path: `/api/experiences/destinations/${recordId}`, method: 'PUT', body: destinationInput, run: () => experienceApi.updateDestination(recordId, destinationInput) },
    { path: `/api/experiences/destinations/${recordId}`, method: 'DELETE', run: () => experienceApi.deleteDestination(recordId) },
    { path: `/api/experiences/destinations/${recordId}/publication-evaluations`, method: 'POST', body: { status: 'PUBLISHED' }, run: () => experienceApi.evaluateDestinationPublication(recordId, 'PUBLISHED') },
    { path: `/api/experiences/destinations/${recordId}/publication`, method: 'PATCH', body: { status: 'PUBLISHED' }, run: () => experienceApi.updateDestinationPublication(recordId, 'PUBLISHED') },
    { path: `/api/experiences/destinations/${recordId}/marine-conditions`, run: () => experienceApi.getDestinationMarineConditions(recordId) },
    { path: `/api/experiences/destinations/${recordId}/operational-advisories`, run: () => experienceApi.getDestinationOperationalAdvisories(recordId) },
    { path: `/api/experiences/destinations/${recordId}/biodiversity`, run: () => experienceApi.getDestinationBiodiversity(recordId) },
    { path: '/api/experiences/activities?destinationId=d1111111-1111-1111-1111-111111111111&category=Wildlife&query=whale+watch&status=PUBLISHED&page=3&pageSize=5', run: () => experienceApi.getActivities({ destinationId: recordId, category: 'Wildlife', query: 'whale watch', status: 'PUBLISHED', page: 3, pageSize: 5 }) },
    { path: `/api/experiences/activities/${recordId}`, run: () => experienceApi.getActivityById(recordId) },
    { path: '/api/experiences/activities', method: 'POST', body: activityInput, run: () => experienceApi.createActivity(activityInput) },
    { path: `/api/experiences/activities/${recordId}`, method: 'PUT', body: activityInput, run: () => experienceApi.updateActivity(recordId, activityInput) },
    { path: `/api/experiences/activities/${recordId}`, method: 'DELETE', run: () => experienceApi.deleteActivity(recordId) },
    { path: `/api/experiences/activities/${recordId}/publication-evaluations`, method: 'POST', body: { status: 'PUBLISHED' }, run: () => experienceApi.evaluateActivityPublication(recordId, 'PUBLISHED') },
    { path: `/api/experiences/activities/${recordId}/publication`, method: 'PATCH', body: { status: 'PUBLISHED' }, run: () => experienceApi.updateActivityPublication(recordId, 'PUBLISHED') },
    { path: '/api/experiences/offerings?activityId=a1111111-1111-1111-1111-111111111111&destinationId=d1111111-1111-1111-1111-111111111111&status=PUBLISHED&page=2&pageSize=7', run: () => experienceApi.getOfferings({ activityId: 'a1111111-1111-1111-1111-111111111111', destinationId: recordId, status: 'PUBLISHED', page: 2, pageSize: 7 }) },
    { path: `/api/experiences/offerings/${recordId}`, run: () => experienceApi.getOfferingById(recordId) },
    { path: '/api/experiences/offerings', method: 'POST', body: offeringInput, run: () => experienceApi.createOffering(offeringInput) },
    { path: `/api/experiences/offerings/${recordId}`, method: 'PUT', body: offeringInput, run: () => experienceApi.updateOffering(recordId, offeringInput) },
    { path: `/api/experiences/offerings/${recordId}`, method: 'DELETE', run: () => experienceApi.deleteOffering(recordId) },
    { path: `/api/experiences/offerings/${recordId}/publication-evaluations`, method: 'POST', body: { status: 'PUBLISHED' }, run: () => experienceApi.evaluateOfferingPublication(recordId, 'PUBLISHED') },
    { path: `/api/experiences/offerings/${recordId}/publication`, method: 'PATCH', body: { status: 'PUBLISHED' }, run: () => experienceApi.updateOfferingPublication(recordId, 'PUBLISHED') },
    { path: `/api/experiences/offerings/${recordId}/schedules?from=2026-11-01&to=2026-11-30`, run: () => experienceApi.getOfferingSchedules(recordId, { from: '2026-11-01', to: '2026-11-30' }) },
    { path: `/api/experiences/offerings/${recordId}/schedules`, method: 'POST', body: scheduleInput, run: () => experienceApi.addOfferingSchedule(recordId, scheduleInput) },
    { path: `/api/experiences/offerings/${recordId}/schedules/${scheduleId}`, method: 'PUT', body: scheduleInput, run: () => experienceApi.updateOfferingSchedule(recordId, scheduleId, scheduleInput) },
    { path: `/api/experiences/offerings/${recordId}/schedules/${scheduleId}`, method: 'DELETE', run: () => experienceApi.deleteOfferingSchedule(recordId, scheduleId) },
    { path: '/api/experiences/availability/evaluations', method: 'POST', body: availabilityInput, run: () => experienceApi.evaluateAvailability(availabilityInput) },
    { path: '/api/experiences/favourites', run: () => experienceApi.getUserFavourites() },
    { path: `/api/experiences/favourites/Destination/${recordId}`, method: 'PUT', run: () => experienceApi.addFavourite('Destination', recordId) },
    { path: `/api/experiences/favourites/Destination/${recordId}`, method: 'DELETE', run: () => experienceApi.removeFavourite('Destination', recordId) },
    { path: '/api/experiences/map/config', run: () => experienceApi.getMapConfig() },
    { path: '/api/experiences/map/search?q=Hikkaduwa%20%26%20reefs', run: () => experienceApi.searchMapPlaces('Hikkaduwa & reefs') },
    { path: '/api/experiences/nearby?q=Mirissa+Bay&radiusMeters=5000&limit=4', run: () => experienceApi.getNearbyExperiences({ location: 'Mirissa Bay', radiusMeters: 5000, limit: 4 }) },
    { path: '/api/experiences/nearby?latitude=5.9482&longitude=80.4578&radiusMeters=2500&limit=3', run: () => experienceApi.getNearbyExperiences({ latitude: 5.9482, longitude: 80.4578, radiusMeters: 2500, limit: 3 }) },
    { path: '/api/experiences/dependencies/status', run: () => experienceApi.getDependenciesStatus() },
    { path: '/api/experiences/agent/context', run: () => experienceApi.getAgentContext() },
  ]

  for (const contract of cases) {
    let captured
    globalThis.fetch = async (input, init = {}) => {
      captured = { input: String(input), init }
      return contract.method === 'DELETE' ? jsonResponse(undefined, 204) : jsonResponse({})
    }

    await contract.run()
    assertJsonRequest(captured, contract)
  }
})

test('WEB-EXP-API-002 activity list handles the API array and paged response shapes', async () => {
  globalThis.fetch = async () => jsonResponse([{ id: 'activity-1', name: 'Reef walk' }])
  assert.deepEqual(await experienceApi.getActivities(), {
    total: 1,
    page: 1,
    pageSize: 1,
    items: [{ id: 'activity-1', name: 'Reef walk' }],
  })

  globalThis.fetch = async () => jsonResponse({ total: 8, page: 2, pageSize: 3, items: [{ id: 'activity-4' }] })
  assert.deepEqual(await experienceApi.getActivities(), {
    total: 8,
    page: 2,
    pageSize: 3,
    items: [{ id: 'activity-4' }],
  })
})

test('WEB-EXP-API-003 HTTP failures preserve status and API detail with useful auth fallbacks', async () => {
  globalThis.fetch = async () => jsonResponse({ detail: 'Sign in before saving this experience.' }, 401)
  await assert.rejects(experienceApi.addFavourite('Destination', recordId), (error) => {
    assert.ok(error instanceof experienceApi.ExperienceApiError)
    assert.equal(error.status, 401)
    assert.equal(error.message, 'Sign in before saving this experience.')
    assert.deepEqual(error.errorData, { detail: 'Sign in before saving this experience.' })
    return true
  })

  globalThis.fetch = async () => new Response('Forbidden', { status: 403 })
  await assert.rejects(experienceApi.createDestination(destinationInput), (error) => {
    assert.ok(error instanceof experienceApi.ExperienceApiError)
    assert.equal(error.status, 403)
    assert.match(error.message, /does not have permission/i)
    return true
  })
})

test('WEB-EXP-API-004 malformed, empty and failed network responses produce typed client errors', async () => {
  globalThis.fetch = async () => new Response('{not-json', { status: 200 })
  await assert.rejects(experienceApi.getMapConfig(), (error) => {
    assert.ok(error instanceof experienceApi.ExperienceApiError)
    assert.equal(error.status, 200)
    assert.match(error.message, /empty or unreadable/i)
    return true
  })

  globalThis.fetch = async () => new Response(null, { status: 200 })
  await assert.rejects(experienceApi.getMapConfig(), (error) => {
    assert.equal(error.status, 200)
    assert.match(error.message, /empty or unreadable/i)
    return true
  })

  const networkFailure = new TypeError('offline')
  globalThis.fetch = async () => { throw networkFailure }
  await assert.rejects(experienceApi.getDestinations(), (error) => {
    assert.ok(error instanceof experienceApi.ExperienceApiError)
    assert.equal(error.status, 0)
    assert.match(error.message, /check your connection/i)
    assert.equal(error.errorData, networkFailure)
    return true
  })
})

test('WEB-EXP-API-005 204 deletion responses resolve to void without requiring a response body', async () => {
  let captured
  globalThis.fetch = async (input, init) => {
    captured = { input: String(input), init }
    return new Response(null, { status: 204 })
  }

  assert.equal(await experienceApi.deleteDestination(recordId), undefined)
  assertJsonRequest(captured, { path: `/api/experiences/destinations/${recordId}`, method: 'DELETE' })
})
