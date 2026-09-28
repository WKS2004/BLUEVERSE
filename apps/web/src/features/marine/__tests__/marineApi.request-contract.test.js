import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import {
  MarineApiError,
  createMarineSafetyProfile,
  deactivateMarineSafetyProfile,
  evaluateMarineSuitability,
  getMarineConditions,
  getMarineHistory,
  getMarineSafetyProfiles,
  updateMarineSafetyProfile,
} from '../marineApi.ts'
import { closeWebTestServer, jsonResponse } from '../../../testSupport/reactTestHarness.js'

const originalFetch = globalThis.fetch

afterEach(() => {
  globalThis.fetch = originalFetch
})

after(async () => closeWebTestServer())

function captureFetch(response = jsonResponse({ id: 'fixture-id' })) {
  let captured
  globalThis.fetch = async (input, init = {}) => {
    captured = { input, init }
    return response
  }
  return () => captured
}

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

const profile = {
  id: 'profile-1',
  activityId: '33333333-3333-3333-3333-333333333301',
  activityName: 'Surfing',
  maxWindSpeed: 35,
  maxWaveHeight: 1.5,
  maxSwellHeight: 1.2,
  cautionWindSpeed: 25,
  cautionWaveHeight: 1,
  cautionSwellHeight: 0.8,
  isActive: true,
  version: 2,
  createdAt: '2026-09-27T00:00:00.000Z',
  updatedAt: '2026-09-27T01:00:00.000Z',
}

const snapshot = {
  id: 'snapshot-1',
  latitude: 6.025,
  longitude: 80.216,
  forecastTime: '2026-09-27T08:00:00Z',
  retrievedAt: '2026-09-27T07:55:00Z',
  windSpeed: 18.4,
  waveHeight: 1.1,
  swellHeight: 0.9,
  rain: 0.2,
  weatherCode: 3,
  source: 'Open-Meteo',
  freshnessStatus: 'FRESH',
  missingFields: [],
}

const suitability = {
  status: 'UNSUITABLE',
  activityId: '33333333-3333-3333-3333-333333333301',
  activityName: 'Surfing',
  location: { latitude: 6.025, longitude: 80.216 },
  requestedTime: '2026-09-27T08:00:00Z',
  evaluatedAt: '2026-09-27T07:56:00Z',
  conditions: { windSpeed: 18.4, waveHeight: 2.4, swellHeight: 1.6, rain: 0.2, weatherCode: 3 },
  source: 'Open-Meteo',
  retrievedAt: '2026-09-27T07:55:00Z',
  freshness: 'FRESH',
  missingFields: [],
  violations: ['waveHeight 2.4 m exceeds maximum 1.5 m'],
  cautionFactors: [],
  assessmentId: 'assessment-1',
  snapshotId: 'snapshot-1',
}

test('WEB-MARINE-API-001 condition reads hit the registered public marine endpoint with query parameters', async () => {
  const getRequest = captureFetch(jsonResponse(snapshot))

  assert.deepEqual(await getMarineConditions(6.025, 80.216), snapshot)
  assertJsonRequest(getRequest(), { path: '/api/marine/current?latitude=6.025&longitude=80.216' })
})

test('WEB-MARINE-API-002 an optional requested time is sent as UTC without changing the endpoint', async () => {
  const getRequest = captureFetch(jsonResponse(snapshot))

  await getMarineConditions(6.025, 80.216, '2026-09-27T08:00:00.000Z')
  assertJsonRequest(getRequest(), { path: '/api/marine/current?latitude=6.025&longitude=80.216&time=2026-09-27T08%3A00%3A00.000Z' })
})

test('WEB-MARINE-API-003 history filters serialize onto the history endpoint', async () => {
  const getRequest = captureFetch(jsonResponse([snapshot]))

  const items = await getMarineHistory({ latitude: 6.025, longitude: 80.216, from: '2026-09-26T00:00:00.000Z', to: '2026-09-27T00:00:00.000Z' })
  assert.deepEqual(items, [snapshot])
  assertJsonRequest(getRequest(), {
    path: '/api/marine/history?latitude=6.025&longitude=80.216&from=2026-09-26T00%3A00%3A00.000Z&to=2026-09-27T00%3A00%3A00.000Z',
  })
})

test('WEB-MARINE-API-004 history without filters calls the bare history endpoint', async () => {
  const getRequest = captureFetch(jsonResponse([]))

  await getMarineHistory()
  assertJsonRequest(getRequest(), { path: '/api/marine/history?' })
})

test('WEB-MARINE-API-005 profile listing reads the registered safety-profile endpoint', async () => {
  const getRequest = captureFetch(jsonResponse([profile]))

  assert.deepEqual(await getMarineSafetyProfiles(), [profile])
  assertJsonRequest(getRequest(), { path: '/api/marine/safety-profiles' })
})

test('WEB-MARINE-API-006 the suitability evaluation posts activity, location and optional time', async () => {
  const getRequest = captureFetch(jsonResponse(suitability))

  const result = await evaluateMarineSuitability({ activityId: profile.activityId, latitude: 6.025, longitude: 80.216 })
  assert.deepEqual(result, suitability)
  assertJsonRequest(getRequest(), {
    path: '/api/marine/evaluate',
    method: 'POST',
    body: { activityId: profile.activityId, latitude: 6.025, longitude: 80.216 },
  })
})

test('WEB-MARINE-API-007 the evaluation omits dateTime when the caller does not request a period', async () => {
  const getRequest = captureFetch(jsonResponse(suitability))

  await evaluateMarineSuitability({ activityId: profile.activityId, latitude: 6.025, longitude: 80.216, dateTime: '2026-09-27T08:00:00.000Z' })
  assertJsonRequest(getRequest(), {
    path: '/api/marine/evaluate',
    method: 'POST',
    body: { activityId: profile.activityId, latitude: 6.025, longitude: 80.216, dateTime: '2026-09-27T08:00:00.000Z' },
  })
})


test('WEB-MARINE-API-010 profile creation posts the activity reference and limits with units', async () => {
  const getRequest = captureFetch(jsonResponse(profile))
  const input = {
    activityId: profile.activityId,
    maxWindSpeed: 35,
    maxWaveHeight: 1.5,
    maxSwellHeight: 1.2,
    cautionWindSpeed: 25,
    cautionWaveHeight: 1,
    cautionSwellHeight: 0.8,
  }

  await createMarineSafetyProfile(input)
  assertJsonRequest(getRequest(), { path: '/api/marine/safety-profiles', method: 'POST', body: input })
})

test('WEB-MARINE-API-011 profile update sends limits, optional caution bands and active state', async () => {
  const getRequest = captureFetch(jsonResponse(profile))
  const input = { maxWindSpeed: 30, maxWaveHeight: 2, maxSwellHeight: 1.6, isActive: true }

  await updateMarineSafetyProfile('profile-1', input)
  assertJsonRequest(getRequest(), { path: '/api/marine/safety-profiles/profile-1', method: 'PUT', body: input })
})

test('WEB-MARINE-API-012 profile deactivation accepts only the API’s empty 204 success', async () => {
  const getRequest = captureFetch(jsonResponse(undefined, 204))

  await deactivateMarineSafetyProfile('profile-1')
  assertJsonRequest(getRequest(), { path: '/api/marine/safety-profiles/profile-1', method: 'DELETE' })
})

test('WEB-MARINE-API-013 a structured provider outage preserves its server detail and 503 status', async () => {
  globalThis.fetch = async () => jsonResponse({ status: 503, detail: 'Condition information cannot be obtained right now.' }, 503)

  await assert.rejects(() => getMarineConditions(6.025, 80.216), (error) => {
    assert.ok(error instanceof MarineApiError)
    assert.equal(error.status, 503)
    assert.equal(error.message, 'Condition information cannot be obtained right now.')
    return true
  })
})

test('WEB-MARINE-API-014 an unstructured outage falls back to a safe coastal explanation', async () => {
  globalThis.fetch = async () => new Response('upstream unavailable', { status: 503 })

  await assert.rejects(() => getMarineSafetyProfiles(), (error) => {
    assert.ok(error instanceof MarineApiError)
    assert.equal(error.status, 503)
    assert.match(error.message, /Marine conditions cannot be retrieved right now/)
    assert.doesNotMatch(error.message, /upstream/)
    return true
  })
})

test('WEB-MARINE-API-015 a permission denial keeps status 403 and a safe explanation', async () => {
  globalThis.fetch = async () => new Response('Forbidden', { status: 403 })

  await assert.rejects(() => getMarineSafetyProfiles(), (error) => {
    assert.ok(error instanceof MarineApiError)
    assert.equal(error.status, 403)
    assert.equal(error.message, 'Your current roles do not allow this action.')
    return true
  })
})

test('WEB-MARINE-API-016 other failures use a safe fallback while retaining the HTTP status', async () => {
  globalThis.fetch = async () => jsonResponse({ status: 404, detail: 'No safety profile is configured for that activity.' }, 404)

  await assert.rejects(() => evaluateMarineSuitability({ activityId: profile.activityId, latitude: 6.025, longitude: 80.216 }), (error) => {
    assert.ok(error instanceof MarineApiError)
    assert.equal(error.status, 404)
    assert.equal(error.message, 'No safety profile is configured for that activity.')
    return true
  })
})

test('WEB-MARINE-API-017 malformed successful responses fail with a safe read error', async () => {
  globalThis.fetch = async () => new Response('{not-json', { status: 200 })

  await assert.rejects(() => getMarineConditions(6.025, 80.216), (error) => {
    assert.ok(error instanceof MarineApiError)
    assert.equal(error.status, 200)
    assert.equal(error.message, 'The server returned a response that could not be read.')
    return true
  })
})

test('WEB-MARINE-API-018 an empty successful body is invalid except for explicit 204', async () => {
  globalThis.fetch = async () => new Response(null, { status: 200 })
  await assert.rejects(() => getMarineHistory(), (error) => error instanceof MarineApiError && error.status === 200)

  globalThis.fetch = async () => new Response(null, { status: 204 })
  await assert.doesNotReject(() => deactivateMarineSafetyProfile('profile-1'))
})

test('WEB-MARINE-API-019 transport failures reject unchanged for caller recovery handling', async () => {
  const failure = new TypeError('offline fixture')
  globalThis.fetch = async () => { throw failure }

  await assert.rejects(() => getMarineConditions(6.025, 80.216), (error) => error === failure)
})
