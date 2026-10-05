// Request-contract tests for the Smart Coastal Planner public API client.
// Requirement references (v1.component.coastal-planner, contract_id
// v1.component.coastal-planner; G00 decisions doc section 3.1; PROJECT_REQUIREMENTS.md
// sections 16, 17, 20-27, 40, 53):
//   WEB-PLANNER-API-001..005  public route/method/header/body parity for
//     POST /api/planner/recommendations, GET /api/planner/recommendations/{id},
//     GET /api/planner/workflows/{id} and GET /api/planner/biodiversity/predictions
//     (G00 section 3.1 candidate endpoints; ui-integration workflow
//     `planner-recommendations` operations planner-recommendations-create/-get,
//     planner-workflows-get, planner-biodiversity-predictions).
//   WEB-PLANNER-API-006..010  G00 section 3.3 error semantics: structured
//     ProblemDetails detail preservation, safe fallbacks, malformed/empty
//     response rejection and unchanged transport failures.
//   WEB-PLANNER-API-011       every planner request runs under the shared
//     coastal backend loading context (React component contract loading
//     feedback requirement).
// Clients test only the public API/gateway; no internal service host is
// referenced (implementation-plan section 2).

import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import {
  COASTAL_LOADING_CONTEXT,
  getLoadingScreenContextSnapshot,
  getLoadingScreenSnapshot,
} from '../../loading/backendLoading.ts'
import {
  createRecommendations,
  getBiodiversityPredictions,
  getRecommendation,
  getWorkflow,
  PlannerApiError,
} from '../plannerApi.ts'
import { closeWebTestServer, jsonResponse } from '../../../testSupport/reactTestHarness.js'

const DESTINATION_ID = '3fa85f64-5717-4562-b3fc-2c963f66afa6'
const ACTIVITY_ID = '4fa85f64-5717-4562-b3fc-2c963f66afa7'

const preferences = {
  targetDestinationId: DESTINATION_ID,
  startsAt: '2026-10-01T08:00:00.000Z',
  endsAt: '2026-10-01T18:00:00.000Z',
  durationHours: 8,
  preferredActivityIds: [ACTIVITY_ID],
  experienceLevel: 'INTERMEDIATE',
  includeBiodiversityContext: true,
}

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
  // GET requests omit init.method; fetch defaults them to GET.
  assert.equal(request.init.method ?? 'GET', method)
  assert.equal(request.init.credentials, 'include')
  assert.deepEqual(request.init.headers, {
    Accept: 'application/json',
    ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
  })
  assert.equal(request.init.body, body === undefined ? undefined : JSON.stringify(body))
}

test('WEB-PLANNER-API-001 recommendation creation posts the declared preference contract to the public gateway route', async () => {
  const result = {
    recommendationId: '5fa85f64-5717-4562-b3fc-2c963f66afa8',
    workflowId: '6fa85f64-5717-4562-b3fc-2c963f66afa9',
    status: 'COMPLETED',
    generatedAt: '2026-09-27T11:00:00Z',
    candidates: [],
    excludedCandidatesCount: 0,
    uncertaintyNotes: [],
  }
  const getRequest = captureFetch(jsonResponse(result))

  assert.deepEqual(await createRecommendations(preferences), result)
  assertJsonRequest(getRequest(), {
    path: '/api/planner/recommendations',
    method: 'POST',
    body: {
      targetDestinationId: DESTINATION_ID,
      startsAt: preferences.startsAt,
      endsAt: preferences.endsAt,
      durationHours: 8,
      preferredActivityIds: [ACTIVITY_ID],
      experienceLevel: 'INTERMEDIATE',
      includeBiodiversityContext: true,
    },
  })
})

test('WEB-PLANNER-API-002 recommendation retrieval reads the public recommendation route and encodes identifiers', async () => {
  const result = { recommendationId: 'recommendation-1', workflowId: 'workflow-1', status: 'COMPLETED', generatedAt: '2026-09-27T11:00:00Z', candidates: [], excludedCandidatesCount: 0, uncertaintyNotes: [] }
  const getRequest = captureFetch(jsonResponse(result))

  assert.deepEqual(await getRecommendation('recommendation/1'), result)
  assertJsonRequest(getRequest(), { path: '/api/planner/recommendations/recommendation%2F1' })
})

test('WEB-PLANNER-API-003 workflow status retrieval reads the public workflow route and encodes identifiers', async () => {
  const workflow = { workflowId: 'workflow-1', workflowType: 'Recommendation', status: 'COMPLETED', initiatorUserId: 'user-1', objective: 'Plan a coastal day', createdAt: '2026-09-27T10:00:00Z', completedAt: '2026-09-27T11:00:00Z', resultSummary: null, failureReason: null }
  const getRequest = captureFetch(jsonResponse(workflow))

  assert.deepEqual(await getWorkflow('workflow/1'), workflow)
  assertJsonRequest(getRequest(), { path: '/api/planner/workflows/workflow%2F1' })
})

test('WEB-PLANNER-API-004 biodiversity predictions request both the destination and the optional activity scope', async () => {
  const prediction = { destinationId: DESTINATION_ID, activityId: ACTIVITY_ID, status: 'AVAILABLE', predictedSpecies: [], modelMetadata: null, limitations: 'Contextual prediction only.' }
  const getRequest = captureFetch(jsonResponse(prediction))

  assert.deepEqual(await getBiodiversityPredictions(DESTINATION_ID, ACTIVITY_ID), prediction)
  assertJsonRequest(getRequest(), { path: `/api/planner/biodiversity/predictions?destinationId=${DESTINATION_ID}&activityId=${ACTIVITY_ID}` })
})

test('WEB-PLANNER-API-005 biodiversity predictions omit the activity parameter when no activity scope is requested', async () => {
  const prediction = { destinationId: DESTINATION_ID, activityId: null, status: 'AVAILABLE', predictedSpecies: [], modelMetadata: null, limitations: 'Contextual prediction only.' }
  const getRequest = captureFetch(jsonResponse(prediction))

  assert.deepEqual(await getBiodiversityPredictions(DESTINATION_ID, null), prediction)
  assertJsonRequest(getRequest(), { path: `/api/planner/biodiversity/predictions?destinationId=${DESTINATION_ID}` })
})

test('WEB-PLANNER-API-006 a structured denial preserves the HTTP status and the server detail', async () => {
  globalThis.fetch = async () => jsonResponse({ status: 403, detail: 'The planner.recommendations.create permission is required.' }, 403)

  await assert.rejects(() => createRecommendations(preferences), (error) => {
    assert.ok(error instanceof PlannerApiError)
    assert.equal(error.status, 403)
    assert.equal(error.message, 'The planner.recommendations.create permission is required.')
    return true
  })
})

test('WEB-PLANNER-API-007 an unstructured denial uses the planner safe explanation without leaking transport detail', async () => {
  globalThis.fetch = async () => new Response('Forbidden', { status: 403 })

  await assert.rejects(() => getRecommendation('recommendation-1'), (error) => {
    assert.ok(error instanceof PlannerApiError)
    assert.equal(error.status, 403)
    assert.equal(error.message, 'We couldn’t complete that just now. Please try again.')
    assert.doesNotMatch(error.message, /Forbidden/)
    return true
  })
})

test('WEB-PLANNER-API-008 a malformed successful response fails with a safe read error', async () => {
  globalThis.fetch = async () => new Response('{not-json', { status: 200 })

  await assert.rejects(() => getWorkflow('workflow-1'), (error) => {
    assert.ok(error instanceof PlannerApiError)
    assert.equal(error.status, 200)
    assert.equal(error.message, 'The server returned a response that could not be read.')
    return true
  })
})

test('WEB-PLANNER-API-009 an empty successful response is invalid for every planner operation', async () => {
  globalThis.fetch = async () => new Response(null, { status: 200 })
  await assert.rejects(() => getRecommendation('recommendation-1'), (error) => error instanceof PlannerApiError && error.status === 200 && error.message === 'The server returned an empty response.')

  globalThis.fetch = async () => new Response(null, { status: 204 })
  await assert.rejects(() => getBiodiversityPredictions(DESTINATION_ID, null), (error) => error instanceof PlannerApiError && error.status === 204 && error.message === 'The server returned an empty response.')
})

test('WEB-PLANNER-API-010 transport failures reject unchanged for caller retry and recovery handling', async () => {
  const failure = new TypeError('offline fixture')
  globalThis.fetch = async () => { throw failure }

  await assert.rejects(() => createRecommendations(preferences), (error) => error === failure)
})

test('WEB-PLANNER-API-011 planner requests run under the shared coastal backend loading context', async () => {
  let releaseFetch
  globalThis.fetch = () => new Promise((resolve) => {
    releaseFetch = () => resolve(jsonResponse({ recommendationId: 'recommendation-1', workflowId: 'workflow-1', status: 'COMPLETED', generatedAt: '2026-09-27T11:00:00Z', candidates: [], excludedCandidatesCount: 0, uncertaintyNotes: [] }))
  })

  const pending = createRecommendations(preferences)
  assert.equal(getLoadingScreenSnapshot(), true)
  assert.deepEqual(getLoadingScreenContextSnapshot(), COASTAL_LOADING_CONTEXT)

  // withLoadingScreen defers the operation through a microtask, so the
  // The request and its release handle are not wired up synchronously.
  await new Promise((resolve) => setTimeout(resolve, 10))
  assert.equal(typeof releaseFetch, 'function')
  releaseFetch()
  await pending
  await waitForLoadingScreenHidden()
})

async function waitForLoadingScreenHidden() {
  const deadline = Date.now() + 2000
  while (getLoadingScreenSnapshot()) {
    if (Date.now() > deadline) throw new Error('Loading screen did not hide after the planner request settled')
    await new Promise((resolve) => setTimeout(resolve, 25))
  }
}
