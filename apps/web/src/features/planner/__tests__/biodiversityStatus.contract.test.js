import assert from 'node:assert/strict'
import { afterEach, test } from 'node:test'
import { getBiodiversityPredictions } from '../plannerApi.ts'

const originalFetch = globalThis.fetch
const destinationId = '3fa85f64-5717-4562-b3fc-2c963f66afa6'
const activityId = '4fa85f64-5717-4562-b3fc-2c963f66afa7'
const neutralMessages = {
  available: 'Wildlife context is available. A prediction does not guarantee a sighting.',
  not_requested: 'Wildlife context was not requested.',
  unavailable: 'Wildlife predictions are temporarily unavailable. Please try again later.',
  invalid: 'Wildlife predictions could not be verified for this location.',
  stale: 'The wildlife prediction is out of date. Request a fresh result.',
}

afterEach(() => { globalThis.fetch = originalFetch })

test('WEB-PLANNER-BIO-STATUS-001 known statuses are normalized and non-available predictions are discarded', async t => {
  for (const [status, message] of Object.entries(neutralMessages)) {
    await t.test(status, async () => {
      globalThis.fetch = async () => new Response(JSON.stringify({
        contractVersion: 1,
        destinationId,
        activityId,
        status,
        message: status === 'available' ? message : 'untrusted provider message',
        uncertaintyText: 'untrusted provider uncertainty',
        limitations: 'Fixture limitation',
        predictedSpecies: [{ speciesId: 'synthetic', scientificName: 'Synthetic', commonName: 'Fixture', habitatSuitability: 0.8, confidenceLevel: 'synthetic' }],
        modelMetadata: { modelVersion: 'fixture-v1', inferenceTimestamp: '2026-10-11T00:00:00Z' },
        provenance: { provider: 'test-provider', modelVersion: 'fixture-v1', inferenceTimestamp: '2026-10-11T00:00:00Z' },
      }), { status: 200, headers: { 'content-type': 'application/json' } })

      const result = await getBiodiversityPredictions(destinationId, activityId)
      assert.equal(result.contractVersion, 1)
      assert.equal(result.status, status)
      assert.equal(result.message, message)
      if (status === 'available') assert.equal(result.predictedSpecies.length, 1)
      else {
        assert.equal(result.limitations, message)
        assert.deepEqual(result.predictedSpecies, [])
        assert.equal(result.modelMetadata, null)
        assert.equal(result.provenance, null)
        assert.equal(result.uncertaintyText, null)
      }
    })
  }
})

test('WEB-PLANNER-BIO-STATUS-003 unsupported contract versions fail closed', async () => {
  globalThis.fetch = async () => new Response(JSON.stringify({
    contractVersion: 2,
    destinationId,
    activityId,
    status: 'available',
    message: neutralMessages.available,
    predictedSpecies: [{ speciesId: 'synthetic', scientificName: 'Synthetic', commonName: 'Fixture', habitatSuitability: 0.8, confidenceLevel: 'synthetic' }],
    modelMetadata: { modelVersion: 'fixture-v2', inferenceTimestamp: '2026-10-11T00:00:00Z' },
    provenance: { provider: 'future-provider', modelVersion: 'fixture-v2', inferenceTimestamp: '2026-10-11T00:00:00Z' },
    uncertaintyText: 'untrusted future uncertainty',
  }), { status: 200, headers: { 'content-type': 'application/json' } })

  const result = await getBiodiversityPredictions(destinationId, activityId)

  assert.equal(result.status, 'unavailable')
  assert.equal(result.message, neutralMessages.unavailable)
  assert.deepEqual(result.predictedSpecies, [])
  assert.equal(result.uncertaintyText, null)
})

test('WEB-PLANNER-BIO-STATUS-004 available without provenance fails closed', async () => {
  globalThis.fetch = async () => new Response(JSON.stringify({
    contractVersion: 1,
    destinationId,
    activityId,
    status: 'available',
    predictedSpecies: [{ speciesId: 'synthetic', scientificName: 'Synthetic', commonName: 'Fixture', habitatSuitability: 0.8, confidenceLevel: 'synthetic' }],
    modelMetadata: { modelVersion: 'fixture-v1', inferenceTimestamp: '2026-10-11T00:00:00Z' },
    uncertaintyText: 'untrusted provider uncertainty',
  }), { status: 200, headers: { 'content-type': 'application/json' } })

  const result = await getBiodiversityPredictions(destinationId, activityId)

  assert.equal(result.status, 'unavailable')
  assert.deepEqual(result.predictedSpecies, [])
  assert.equal(result.modelMetadata, null)
  assert.equal(result.uncertaintyText, null)
})

test('WEB-PLANNER-BIO-STATUS-002 an unknown status becomes unavailable and its prediction is discarded', async () => {
  globalThis.fetch = async () => new Response(JSON.stringify({
    contractVersion: 1,
    destinationId,
    activityId,
    status: 'future-provider-status',
    predictedSpecies: [{ speciesId: 'synthetic', scientificName: 'Synthetic', commonName: 'Fixture', habitatSuitability: 0.8, confidenceLevel: 'synthetic' }],
    modelMetadata: { modelVersion: 'fixture-v1', inferenceTimestamp: '2026-10-11T00:00:00Z' },
    provenance: { provider: 'future-provider', modelVersion: 'fixture-v1', inferenceTimestamp: '2026-10-11T00:00:00Z' },
    uncertaintyText: 'untrusted provider uncertainty',
  }), { status: 200, headers: { 'content-type': 'application/json' } })

  const result = await getBiodiversityPredictions(destinationId, activityId)

  assert.equal(result.status, 'unavailable')
  assert.equal(result.message, neutralMessages.unavailable)
  assert.deepEqual(result.predictedSpecies, [])
  assert.equal(result.modelMetadata, null)
  assert.equal(result.uncertaintyText, null)
})
