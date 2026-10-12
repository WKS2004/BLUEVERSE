import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { cleanup, closeWebTestServer, createElement, fireEvent, jsonResponse, loadWebModule, makeAuthSessionValue, makeAuthUser, renderInApp, resetTestBrowser, screen, userEvent, waitFor } from '../../../testSupport/reactTestHarness.js'

const [{ MarineConditionsPage, MarineHistoryPage, MarineSafetyProfilesPage }, { default: MarineWorkspaceLayout }] = await Promise.all([
  loadWebModule('/src/pages/marine/MarinePages.tsx'),
  loadWebModule('/src/pages/marine/MarineWorkspaceLayout.tsx'),
])
const originalFetch = globalThis.fetch
afterEach(() => { cleanup(); resetTestBrowser(); globalThis.fetch = originalFetch })
after(closeWebTestServer)

// Synthetic reviewed criteria and evidence; these are not production safety limits.
const activityId = '33333333-3333-3333-3333-333333333301'
const catalogue = [{ id: activityId, name: 'Surfing', activityType: 'Surfing', isActive: true }]
const timestamp = '2026-10-08T08:00:00Z'
const snapshot = { id: 'snapshot-web', latitude: 6.025, longitude: 80.216, forecastTime: timestamp, retrievedAt: timestamp, windSpeed: 18, waveHeight: 1, swellHeight: 0.6, rain: 0, weatherCode: 1, source: 'Open-Meteo', freshnessStatus: 'FRESH', missingFields: [] }
const assessment = { id: 'assessment-web', activityId, activityName: 'Surfing', conditionSnapshotId: snapshot.id, safetyProfileId: 'profile-web', profileVersion: 1, latitude: snapshot.latitude, longitude: snapshot.longitude, requestedTime: timestamp, forecastTime: timestamp, evaluatedAt: timestamp, result: 'SUITABLE', violations: [], cautionFactors: [], missingFields: [], conditions: snapshot, conditionRetrievedAt: timestamp, criteria: [], evidenceCompleteness: 'LEGACY_INCOMPLETE', source: snapshot.source, freshnessStatus: 'FRESH' }
const profile = { id: 'profile-web', activityId, activityName: 'Surfing', maxWindSpeed: 35, maxWaveHeight: 1.5, maxSwellHeight: 1.2, cautionWindSpeed: null, cautionWaveHeight: null, cautionSwellHeight: null, windCriteriaSource: 'Synthetic wind source', windCriteriaRationale: 'Synthetic supervised activity criteria.', waveCriteriaSource: 'Synthetic wave source', waveCriteriaRationale: 'Synthetic supervised activity criteria.', swellCriteriaSource: 'Synthetic swell source', swellCriteriaRationale: 'Synthetic supervised activity criteria.', isActive: false, version: 1, createdAt: timestamp, updatedAt: timestamp, createdByUserId: 'another-manager', reviewedByUserId: null, reviewedAt: null, effectiveFrom: null, effectiveTo: null }

function renderPage(Page, path, manage = false) {
  return renderInApp(createElement(MarineWorkspaceLayout, null, createElement(Page)), { path, auth: makeAuthSessionValue({ status: 'signed-in', user: makeAuthUser({ permissions: ['marine.profile.read', ...(manage ? ['marine.profile.manage'] : [])] }) }) })
}
function transport(operation) {
  globalThis.fetch = (input, init = {}) => String(input) === '/api/marine/activities' ? Promise.resolve(jsonResponse(catalogue)) : operation(String(input), init)
}
function fillDraft(overrides = {}) {
  const values = { 'max-wind': '35', 'max-wave': '1.5', 'max-swell': '1.2', windCriteriaSource: 'Synthetic wind source', windCriteriaRationale: 'Synthetic supervised activity criteria.', waveCriteriaSource: 'Synthetic wave source', waveCriteriaRationale: 'Synthetic supervised activity criteria.', swellCriteriaSource: 'Synthetic swell source', swellCriteriaRationale: 'Synthetic supervised activity criteria.', ...overrides }
  for (const [field, value] of Object.entries(values)) fireEvent.change(document.getElementById(`create-profile-${field}`), { target: { value } })
}

test('WEB-MARINE-WORKFLOW-001 three registered workflows have accessible current-page navigation (ui-integration: marine-conditions)', async () => {
  transport(async () => jsonResponse([]))
  renderPage(MarineConditionsPage, '/marine/conditions')
  const navigation = screen.getByRole('navigation', { name: 'Marine workflows' })
  const links = [...navigation.querySelectorAll('a')]
  assert.deepEqual(links.map(link => link.getAttribute('href')), ['/marine/conditions', '/marine/history', '/marine/safety-profiles'])
  const labels = ['Coastal conditions', 'Coastal evidence', 'Activity safety limits']
  assert.deepEqual(links.map(link => link.textContent), labels)
  const sidebar = screen.getByRole('complementary', { name: 'Account navigation' })
  for (const [index, label] of labels.entries()) {
    const matches = [...sidebar.querySelectorAll('a')].filter(link => link.textContent === label)
    assert.equal(matches.length, 2) // Desktop and mobile sidebar use the same names.
    assert.ok(matches.every(link => link.getAttribute('href') === links[index].getAttribute('href')))
  }
  assert.equal(links[0].getAttribute('aria-current'), 'page')
  assert.equal(links[1].hasAttribute('aria-current'), false)
  assert.ok(screen.getByRole('heading', { name: 'A clearer check before you go' }))
  await waitFor(() => assert.ok(screen.getByRole('option', { name: 'Surfing' })))
})

for (const [status, heading] of [['SUITABLE', 'Within the reviewed limits'], ['CAUTION', 'Extra care is needed'], ['UNSUITABLE', 'Outside the reviewed limits'], ['UNKNOWN', 'There is not enough reliable evidence']]) {
  test(`WEB-MARINE-WORKFLOW-002 ${status} explains the unchanged server result (ui-integration: marine-conditions)`, async () => {
    const writes = []
    transport(async (input, init) => {
      writes.push({ input, init })
      return jsonResponse({ status, activityId, activityName: 'Surfing', location: { latitude: snapshot.latitude, longitude: snapshot.longitude }, requestedTime: timestamp, forecastTime: timestamp, evaluatedAt: timestamp, conditions: snapshot, source: snapshot.source, retrievedAt: timestamp, freshness: 'FRESH', missingFields: status === 'UNKNOWN' ? ['waveHeight'] : [], violations: status === 'UNSUITABLE' ? ['Reviewed maximum exceeded'] : [], cautionFactors: status === 'CAUTION' ? ['waveHeight'] : [], assessmentId: assessment.id, snapshotId: snapshot.id })
    })
    renderPage(MarineConditionsPage, '/marine/conditions')
    await waitFor(() => assert.equal(screen.getByRole('button', { name: 'Check conditions & suitability' }).disabled, false))
    await userEvent.setup().click(screen.getByRole('button', { name: 'Check conditions & suitability' }))
    assert.ok(await screen.findByRole('heading', { name: heading }))
    assert.ok(screen.getByText(status))
    assert.equal(writes.length, 1)
    assert.equal(writes[0].input, '/api/marine/evaluate')
    assert.equal(writes[0].init.method, 'POST')
    assert.equal(JSON.parse(writes[0].init.body).activityId, activityId)
    assert.equal(writes[0].init.credentials, 'include')
  })
}

test('WEB-MARINE-WORKFLOW-003 condition-only lookup explicitly labels missing evidence without making an assessment (ui-integration: marine-conditions)', async () => {
  const calls = []
  transport(async (input, init) => { calls.push({ input, init }); return jsonResponse({ ...snapshot, waveHeight: null, missingFields: ['waveHeight'] }) })
  renderPage(MarineConditionsPage, '/marine/conditions')
  await userEvent.setup().click(screen.getByRole('button', { name: 'View conditions only' }))
  assert.ok(await screen.findByText(/Unavailable evidence: Wave height/))
  assert.ok(screen.getByText('Not reported'))
  assert.equal(calls.length, 1)
  assert.ok(calls[0].input.startsWith('/api/marine/current?'))
  assert.equal(calls[0].init.method, 'GET')
  assert.equal(screen.queryByText('SERVER ASSESSMENT'), null)
})

test('WEB-MARINE-WORKFLOW-004 grouped criteria accept exact upper boundaries and preserve cited draft values (ui-integration: marine-safety-profile-management)', async () => {
  const writes = []
  transport(async (input, init) => {
    if (init.method === 'POST') { writes.push({ input, init }); return jsonResponse(profile, 201) }
    return jsonResponse(writes.length ? [profile] : [])
  })
  renderPage(MarineSafetyProfilesPage, '/marine/safety-profiles', true)
  await waitFor(() => assert.equal(screen.getByRole('button', { name: 'Create profile' }).disabled, false))
  for (const name of ['Wind criteria', 'Wave criteria', 'Swell criteria']) assert.ok(screen.getByRole('group', { name }))
  fillDraft({ 'max-wind': '1000', 'max-wave': '50', 'max-swell': '50', 'caution-wind': '999.99' })
  fireEvent.submit(document.getElementById('create-profile-max-wind').form)
  await waitFor(() => assert.match(screen.getByRole('status').textContent, /saved and awaiting review/))
  assert.equal(writes.length, 1)
  const body = JSON.parse(writes[0].init.body)
  assert.equal(body.maxWindSpeed, 1000)
  assert.equal(body.maxWaveHeight, 50)
  assert.equal(body.maxSwellHeight, 50)
  assert.equal(body.cautionWindSpeed, 999.99)
  assert.equal(body.windCriteriaSource, 'Synthetic wind source')
  assert.equal(body.activityId, activityId)
  assert.equal(writes[0].input, '/api/marine/safety-profiles')
  assert.equal(writes[0].init.credentials, 'include')
})

for (const [overrides, message] of [[{ 'max-wave': '1.234' }, /at most two decimal places/], [{ 'caution-wind': '35' }, /lower than its matching maximum/], [{ 'caution-swell': '1.21' }, /lower than its matching maximum/]]) {
  test(`WEB-MARINE-WORKFLOW-005 invalid profile ${JSON.stringify(overrides)} preserves the draft without mutation (ui-integration: marine-safety-profile-management)`, async () => {
    const writes = []
    transport(async (input, init) => { if (init.method !== 'GET') writes.push({ input, init }); return jsonResponse([]) })
    renderPage(MarineSafetyProfilesPage, '/marine/safety-profiles', true)
    await waitFor(() => assert.equal(screen.getByRole('button', { name: 'Create profile' }).disabled, false))
    fillDraft(overrides)
    fireEvent.submit(document.getElementById('create-profile-max-wind').form)
    assert.match((await screen.findByRole('alert')).textContent, message)
    assert.equal(writes.length, 0)
    for (const [field, value] of Object.entries(overrides)) assert.equal(document.getElementById(`create-profile-${field}`).value, value)
    assert.equal(document.getElementById('create-profile-windCriteriaSource').value, 'Synthetic wind source')
  })
}

test('WEB-MARINE-WORKFLOW-006 one unavailable history dataset leaves the other visible and refresh recovers (ui-integration: marine-condition-history)', async () => {
  let failed = true
  transport(async input => input.startsWith('/api/marine/history') ? jsonResponse([snapshot]) : failed ? jsonResponse({ detail: 'Assessment records are unavailable.' }, 503) : jsonResponse([assessment]))
  renderPage(MarineHistoryPage, '/marine/history')
  assert.ok(await screen.findByText(/Assessment history is unavailable/))
  assert.ok(screen.getByText(/Wind speed: 18 km\/h/))
  assert.equal(screen.queryByText('No suitability assessments match these filters.'), null)
  failed = false
  await userEvent.setup().click(screen.getByRole('button', { name: 'Refresh history' }))
  assert.ok(await screen.findByRole('heading', { name: 'Surfing · SUITABLE' }))
  assert.equal(screen.queryByText(/Assessment history is unavailable/), null)
  assert.equal(screen.queryByRole('alert'), null)
})

test('WEB-MARINE-WORKFLOW-007 filtered history retains last readings and updates successful assessment data (ui-integration: marine-condition-history)', async () => {
  let failed = false
  const calls = []
  transport(async (input, init) => {
    calls.push({ input, init })
    if (input.startsWith('/api/marine/history')) return failed ? jsonResponse({ detail: 'Condition history is unavailable.' }, 503) : jsonResponse([snapshot])
    return jsonResponse(failed ? [{ ...assessment, result: 'UNKNOWN', missingFields: ['waveHeight'] }] : [])
  })
  renderPage(MarineHistoryPage, '/marine/history')
  await waitFor(() => assert.ok(screen.getByText(/Wind speed: 18 km\/h/)))
  failed = true
  fireEvent.change(screen.getByLabelText('Assessment result'), { target: { value: 'UNKNOWN' } })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Apply filters' }))
  assert.ok(await screen.findByRole('heading', { name: 'Surfing · UNKNOWN' }))
  assert.ok(screen.getByText(/Showing the last loaded readings/))
  assert.ok(screen.getByText(/Wind speed: 18 km\/h/))
  assert.ok(calls.some(call => call.input.includes('result=UNKNOWN')))
  assert.ok(calls.every(call => call.init.method === 'GET' && call.init.credentials === 'include'))
})

test('WEB-MARINE-WORKFLOW-008 failed profile load is distinct from an empty list and retry restores read-only inspection (ui-integration: marine-safety-profile-management)', async () => {
  let failed = true
  transport(async () => failed ? jsonResponse({ detail: 'Saved profile data is unavailable.' }, 503) : jsonResponse([profile]))
  renderPage(MarineSafetyProfilesPage, '/marine/safety-profiles')
  assert.ok(await screen.findByText('Saved profiles are unavailable. Retry to retrieve the records.'))
  assert.equal(screen.queryByText('No safety profiles are configured.'), null)
  failed = false
  await userEvent.setup().click(screen.getByRole('button', { name: 'Retry profiles' }))
  assert.ok(await screen.findByRole('heading', { name: 'Surfing' }))
  assert.equal(screen.getByRole('button', { name: 'Save limits' }).disabled, true)
  assert.equal(screen.queryByRole('button', { name: 'Deactivate profile' }), null)
  assert.equal(screen.queryByRole('alert'), null)
})

test('WEB-MARINE-WORKFLOW-009 server validation rejection preserves criteria and sends the mutation once (ui-integration: marine-safety-profile-management)', async () => {
  const writes = []
  transport(async (input, init) => {
    if (init.method === 'POST') {
      writes.push({ input, init })
      return jsonResponse({ status: 400, detail: 'Safety profile validation failed. Review the submitted criteria.' }, 400)
    }
    return jsonResponse([])
  })
  renderPage(MarineSafetyProfilesPage, '/marine/safety-profiles', true)
  await waitFor(() => assert.equal(screen.getByRole('button', { name: 'Create profile' }).disabled, false))
  fillDraft()
  await userEvent.setup().click(screen.getByRole('button', { name: 'Create profile' }))
  assert.equal((await screen.findByRole('alert')).textContent, 'Safety profile validation failed. Review the submitted criteria.')
  assert.equal(writes.length, 1)
  assert.equal(writes[0].input, '/api/marine/safety-profiles')
  assert.equal(JSON.parse(writes[0].init.body).maxWindSpeed, 35)
  assert.equal(document.getElementById('create-profile-max-wind').value, '35')
  assert.equal(document.getElementById('create-profile-windCriteriaSource').value, 'Synthetic wind source')
  assert.equal(screen.getByRole('button', { name: 'Create profile' }).disabled, false)
  assert.equal(screen.queryByText(/saved and awaiting review/), null)
})
