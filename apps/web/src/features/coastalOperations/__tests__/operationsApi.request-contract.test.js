import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { closeWebTestServer, jsonResponse, loadWebModule } from '../../../testSupport/reactTestHarness.js'

const {
  CoastalOperationsApiError,
  createAssessment,
  decideAlert,
  getEvidenceImage,
  listAssessments,
  uploadAssessmentEvidence,
} = await loadWebModule('/src/features/coastalOperations/operationsApi.ts')
const originalFetch = globalThis.fetch

afterEach(() => { globalThis.fetch = originalFetch })
after(async () => closeWebTestServer())

test('WEB-OPS-API-001 assessment queue calls the registered public route with the bounded page size and browser session', async () => {
  let captured
  globalThis.fetch = async (input, init) => {
    captured = { input, init }
    return jsonResponse({ items: [], nextCursor: null })
  }

  assert.deepEqual(await listAssessments(), { items: [], nextCursor: null })
  assert.equal(captured.input, '/api/operations/assessments?pageSize=100')
  assert.equal(captured.init.credentials, 'include')
  assert.deepEqual(captured.init.headers, { Accept: 'application/json' })
})

test('WEB-OPS-API-002 assessment creation sends the explicit coastal contract with an idempotency key', async () => {
  let captured
  const result = { assessmentId: 'assessment-1', workflowId: 'workflow-1' }
  globalThis.fetch = async (input, init) => {
    captured = { input, init }
    return jsonResponse(result, 201)
  }

  assert.deepEqual(await createAssessment({
    targetType: 'DESTINATION',
    targetId: '00000000-0000-4000-8000-000000000001',
    periodStartsAt: '2026-10-01T09:00:00+05:30',
    periodEndsAt: '2026-10-01T12:00:00+05:30',
    objective: 'Review the access route after heavy rain.',
  }), result)
  assert.equal(captured.input, '/api/operations/assessments')
  assert.equal(captured.init.method, 'POST')
  assert.equal(captured.init.credentials, 'include')
  assert.match(captured.init.headers['Idempotency-Key'], /^[0-9a-f-]{36}$|^\d+-[0-9a-f]+$/i)
  assert.deepEqual(JSON.parse(captured.init.body), {
    targetType: 'DESTINATION',
    targetId: '00000000-0000-4000-8000-000000000001',
    sourceWorkflowId: null,
    periodStartsAt: '2026-10-01T09:00:00+05:30',
    periodEndsAt: '2026-10-01T12:00:00+05:30',
    objective: 'Review the access route after heavy rain.',
  })
})

test('WEB-OPS-API-003 alert decisions use the public decisions route and a unique idempotency key', async () => {
  let captured
  globalThis.fetch = async (input, init) => {
    captured = { input, init }
    return jsonResponse({ decisionId: 'decision-1' })
  }

  await decideAlert('alert-1', 'PUBLISH', 3)
  assert.equal(captured.input, '/api/operations/alerts/alert-1/decisions')
  assert.equal(captured.init.method, 'POST')
  assert.equal(captured.init.credentials, 'include')
  assert.match(captured.init.headers['Idempotency-Key'], /^(?:[0-9a-f-]{36}|\d+-[0-9a-f]+)$/i)
  assert.deepEqual(JSON.parse(captured.init.body), { decision: 'PUBLISH', expectedVersion: 3 })
})

test('WEB-OPS-API-004 evidence upload sends the image as multipart without overriding its boundary', async () => {
  let captured
  globalThis.fetch = async (input, init) => {
    captured = { input, init }
    return jsonResponse({ evidenceId: 'evidence-1', mediaType: 'image/png', byteLength: 4 })
  }
  const image = new window.File(['png!'], 'shoreline.png', { type: 'image/png' })

  await uploadAssessmentEvidence('assessment-1', image)
  assert.equal(captured.input, '/api/operations/assessments/assessment-1/evidence')
  assert.equal(captured.init.method, 'POST')
  assert.equal(captured.init.credentials, 'include')
  assert.deepEqual(captured.init.headers, { Accept: 'application/json' })
  assert.equal(captured.init.body.get('Image').name, 'shoreline.png')
  assert.equal(captured.init.body.get('Image').type, 'image/png')
})

test('WEB-OPS-API-005 evidence content is only returned when the public response is PNG', async () => {
  let captured
  globalThis.fetch = async (input, init) => {
    captured = { input, init }
    return new Response('not an image', { status: 200, headers: { 'Content-Type': 'application/json' } })
  }

  await assert.rejects(getEvidenceImage('assessment-1', 'evidence-1'), CoastalOperationsApiError)
  assert.equal(captured.input, '/api/operations/assessments/assessment-1/evidence/evidence-1')
  assert.equal(captured.init.credentials, 'include')
  assert.deepEqual(captured.init.headers, { Accept: 'image/png' })
})

test('WEB-OPS-API-006 authorization failures show a safe message instead of server details', async () => {
  globalThis.fetch = async () => jsonResponse({ detail: 'private service host and stack trace' }, 403)

  await assert.rejects(listAssessments(), (error) => {
    assert.ok(error instanceof CoastalOperationsApiError)
    assert.equal(error.status, 403)
    assert.equal(error.message, 'Your current permissions do not allow this action.')
    assert.doesNotMatch(error.message, /private service|stack trace/i)
    return true
  })
})
