import assert from 'node:assert/strict'
import { afterEach, test } from 'node:test'
import { deleteItinerary, getEvaluationHistory, getPlannerCatalogue, getWorkflow, PlannerApiError } from '../plannerApi.ts'

const original = globalThis.fetch
afterEach(() => { globalThis.fetch = original })
const json = body => new Response(JSON.stringify(body), {headers:{'Content-Type':'application/json'}})
test('WEB-PLANNER-SCHEMA-001 malformed catalogue copy is rejected before rendering', async () => {
  globalThis.fetch = async () => json({status:'UNAVAILABLE', message:{private:'invalid'}, destinations:[]})
  await assert.rejects(getPlannerCatalogue(), error => error instanceof PlannerApiError && error.status === 502)
})
test('WEB-PLANNER-SCHEMA-002 invalid review timestamps are rejected before date formatting', async () => {
  globalThis.fetch = async () => json([{evaluationId:'review', evaluatedAt:'not-a-date', summary:'Review', hasChanges:false, requiresReview:false, concurrencyVersion:1, items:[]}])
  await assert.rejects(getEvaluationHistory('trip'), error => error instanceof PlannerApiError && error.status === 502)
})
test('WEB-PLANNER-SCHEMA-003 malformed workflow summaries cannot become React children', async () => {
  globalThis.fetch = async () => json({workflowId:'workflow', status:'COMPLETED', createdAt:new Date().toISOString(), resultSummary:{invalid:true}})
  await assert.rejects(getWorkflow('workflow'), error => error instanceof PlannerApiError && error.status === 502)
})
test('WEB-PLANNER-SCHEMA-004 deletion requires the declared empty response and keeps authorization failures', async () => {
  globalThis.fetch = async () => json({deleted:true})
  await assert.rejects(deleteItinerary('trip'), error => error instanceof PlannerApiError && error.status === 502)
  globalThis.fetch = async () => new Response(JSON.stringify({detail:'Denied'}), {status:403, headers:{'Content-Type':'application/problem+json'}})
  await assert.rejects(deleteItinerary('trip'), error => error instanceof PlannerApiError && error.status === 403)
  globalThis.fetch = async () => new Response(null, {status:204})
  assert.equal(await deleteItinerary('trip'), undefined)
})
