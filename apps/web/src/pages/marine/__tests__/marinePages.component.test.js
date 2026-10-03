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
  userEvent,
  waitFor,
} from '../../../testSupport/reactTestHarness.js'

const [{ MarineConditionsPage, MarineHistoryPage, MarineSafetyProfilesPage }, { default: AppRoutes }] = await Promise.all([
  loadWebModule('/src/pages/marine/MarinePages.tsx'),
  loadWebModule('/src/app/routes.tsx'),
])
const originalFetch = globalThis.fetch

afterEach(() => {
  cleanup()
  resetTestBrowser()
  globalThis.fetch = originalFetch
})

after(async () => closeWebTestServer())

const surfingActivityId = '33333333-3333-3333-3333-333333333301'
const snorkelingActivityId = '33333333-3333-3333-3333-333333333302'

function makeSnapshot(overrides = {}) {
  return {
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
    ...overrides,
  }
}

function makeSuitability(overrides = {}) {
  return {
    status: 'SUITABLE',
    activityId: surfingActivityId,
    activityName: 'Surfing',
    location: { latitude: 6.025, longitude: 80.216 },
    requestedTime: '2026-09-27T08:00:00Z',
    evaluatedAt: '2026-09-27T07:56:00Z',
    conditions: { windSpeed: 12, waveHeight: 0.8, swellHeight: 0.6, rain: 0, weatherCode: 1 },
    source: 'Open-Meteo',
    retrievedAt: '2026-09-27T07:55:00Z',
    freshness: 'FRESH',
    missingFields: [],
    violations: [],
    cautionFactors: [],
    assessmentId: 'assessment-1',
    snapshotId: 'snapshot-1',
    ...overrides,
  }
}

function marineUser(permissions = ['marine.profile.read']) {
  return makeAuthUser({ permissions })
}

function makeProfile(overrides = {}) {
  return {
    id: 'profile-1',
    activityId: surfingActivityId,
    activityName: 'Surfing',
    maxWindSpeed: 35,
    maxWaveHeight: 1.5,
    maxSwellHeight: 1.2,
    cautionWindSpeed: null,
    cautionWaveHeight: null,
    cautionSwellHeight: null,
    isActive: true,
    version: 2,
    createdAt: '2026-09-27T00:00:00.000Z',
    updatedAt: '2026-09-27T01:00:00.000Z',
    ...overrides,
  }
}

test('WEB-MARINE-UI-001 the conditions page exposes labeled location, UTC period and activity inputs (ui-integration: marine-conditions)', () => {
  renderInApp(createElement(MarineConditionsPage), { path: '/marine/conditions', auth: makeAuthSessionValue({ user: marineUser(), status: 'signed-in' }) })

  assert.ok(screen.getByRole('heading', { name: 'Marine conditions' }))
  assert.ok(screen.getByLabelText('Latitude'))
  assert.ok(screen.getByLabelText('Longitude'))
  assert.ok(screen.getByLabelText(/Requested time/))
  const activitySelect = screen.getByLabelText('Activity')
  assert.ok(Array.from(activitySelect.options).some((option) => option.value === surfingActivityId && option.textContent === 'Surfing'))
  assert.ok(screen.getByText(/not a substitute for professional maritime navigation information|decision support/i))
})

test('WEB-MARINE-UI-002 a condition query fetches evidence and the server suitability result together (ui-integration: marine-conditions)', async () => {
  const calls = []
  globalThis.fetch = async (input, init = {}) => {
    calls.push({ input, init })
    if (String(input).startsWith('/api/marine/current')) return jsonResponse(makeSnapshot())
    return jsonResponse(makeSuitability())
  }

  renderInApp(createElement(MarineConditionsPage), { path: '/marine/conditions', auth: makeAuthSessionValue({ user: marineUser(), status: 'signed-in' }) })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Check conditions & suitability' }))

  await waitFor(() => assert.ok(screen.getByText('SERVER ASSESSMENT')))
  assert.equal(calls.length, 2)
  assert.equal(calls[0].input, '/api/marine/current?latitude=6.025&longitude=80.216')
  assert.equal(calls[1].input, '/api/marine/evaluate')
  assert.deepEqual(JSON.parse(calls[1].init.body), { activityId: surfingActivityId, latitude: 6.025, longitude: 80.216 })
  assert.ok(screen.getByText('Open-Meteo'))
  assert.ok(screen.getAllByText('FRESH').length >= 1)
})

test('WEB-MARINE-UI-003 invalid coordinates are rejected locally without any request (ui-integration: marine-conditions)', async () => {
  let requestCount = 0
  globalThis.fetch = async () => { requestCount += 1; return jsonResponse({}) }

  renderInApp(createElement(MarineConditionsPage), { path: '/marine/conditions', auth: makeAuthSessionValue({ user: marineUser(), status: 'signed-in' }) })
  await userEvent.setup().type(screen.getByLabelText('Latitude'), '999')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Check conditions & suitability' }))

  assert.equal((await screen.findByRole('alert')).textContent, 'Enter a valid latitude (−90 to 90) and longitude (−180 to 180) in decimal degrees.')
  assert.equal(requestCount, 0)
})

test('WEB-MARINE-UI-004 the suitability card shows violations, caution factors and missing evidence distinctly (ui-integration: marine-conditions)', async () => {
  globalThis.fetch = async (input) => {
    if (String(input).startsWith('/api/marine/current')) {
      return jsonResponse(makeSnapshot({ waveHeight: null, swellHeight: null, missingFields: ['waveHeight', 'swellHeight'] }))
    }
    return jsonResponse(makeSuitability({
      status: 'UNKNOWN',
      conditions: null,
      freshness: 'FRESH',
      missingFields: ['waveHeight', 'swellHeight'],
      violations: [],
      cautionFactors: ['windSpeed'],
    }))
  }

  renderInApp(createElement(MarineConditionsPage), { path: '/marine/conditions', auth: makeAuthSessionValue({ user: marineUser(), status: 'signed-in' }) })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Check conditions & suitability' }))

  await waitFor(() => assert.ok(screen.getByText('SERVER ASSESSMENT')))
  assert.equal(screen.queryByText('LIMITS EXCEEDED'), null)
  assert.ok(screen.getByText('MISSING OR STALE EVIDENCE'))
  assert.ok(screen.getByText(/Wave height was unavailable, so it could not support a positive classification\./))
  assert.ok(screen.getByText(/Wind speed is within the configured caution band\./))
  assert.equal(screen.queryByText(/Stale or incomplete evidence yields UNKNOWN, never a fabricated result\./), null)
})

test('WEB-MARINE-UI-005 an UNSUITABLE server verdict is preserved, never softened (ui-integration: marine-conditions)', async () => {
  globalThis.fetch = async (input) => {
    if (String(input).startsWith('/api/marine/current')) return jsonResponse(makeSnapshot({ waveHeight: 2.4, swellHeight: 1.6 }))
    return jsonResponse(makeSuitability({
      status: 'UNSUITABLE',
      conditions: { windSpeed: 18.4, waveHeight: 2.4, swellHeight: 1.6, rain: 0.2, weatherCode: 3 },
      violations: ['waveHeight 2.4 m exceeds maximum 1.5 m'],
    }))
  }

  renderInApp(createElement(MarineConditionsPage), { path: '/marine/conditions', auth: makeAuthSessionValue({ user: marineUser(), status: 'signed-in' }) })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Check conditions & suitability' }))

  await waitFor(() => assert.ok(screen.getByText('SERVER ASSESSMENT')))
  assert.ok(screen.getByText('UNSUITABLE'))
  assert.ok(screen.getByText('LIMITS EXCEEDED'))
  assert.ok(screen.getByText('waveHeight 2.4 m exceeds maximum 1.5 m'))
})

test('WEB-MARINE-UI-006 a provider outage surfaces a safe recovery message with its server detail (ui-integration: marine-conditions)', async () => {
  globalThis.fetch = async () => jsonResponse({ title: 'Marine Conditions Unavailable', detail: 'Condition information cannot be obtained right now. The suitability result would be UNKNOWN; retry later.', status: 503 }, 503)

  renderInApp(createElement(MarineConditionsPage), { path: '/marine/conditions', auth: makeAuthSessionValue({ user: marineUser(), status: 'signed-in' }) })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Check conditions & suitability' }))

  assert.equal((await screen.findByRole('alert')).textContent, 'Condition information cannot be obtained right now. The suitability result would be UNKNOWN; retry later.')
  assert.equal(screen.queryByText('SERVER ASSESSMENT'), null)
})

test('WEB-MARINE-UI-007 history lists stored snapshots with source, times and freshness (ui-integration: marine-history)', async () => {
  globalThis.fetch = async () => jsonResponse([
    makeSnapshot(),
    makeSnapshot({ id: 'snapshot-2', latitude: 7.5, longitude: 81.2, freshnessStatus: 'STALE', missingFields: ['swellHeight'] }),
  ])

  renderInApp(createElement(MarineHistoryPage), { path: '/marine/history', auth: makeAuthSessionValue({ user: marineUser(), status: 'signed-in' }) })

  await waitFor(() => assert.ok(screen.getByText('2 snapshots')))
  assert.ok(screen.getByText('FRESH'))
  assert.ok(screen.getByText('STALE'))
  assert.ok(screen.getByText(/Missing: Swell height/))
  assert.ok(screen.getAllByText(/Open-Meteo/).length >= 1)
})

test('WEB-MARINE-UI-008 history filters serialize coordinates and the UTC window (ui-integration: marine-history)', async () => {
  let request = null
  globalThis.fetch = async (input, init = {}) => { request = { input, init }; return jsonResponse([]) }

  renderInApp(createElement(MarineHistoryPage), { path: '/marine/history', auth: makeAuthSessionValue({ user: marineUser(), status: 'signed-in' }) })
  await waitFor(() => assert.ok(screen.getByText('0 snapshots')))
  await userEvent.setup().type(screen.getByLabelText('Latitude'), '6.025')
  await userEvent.setup().type(screen.getByLabelText('Longitude'), '80.216')
  fireEvent.change(screen.getByLabelText(/From/), { target: { value: '2026-09-26T00:00' } })
  fireEvent.change(screen.getByLabelText(/To/), { target: { value: '2026-09-27T08:30' } })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Apply filters' }))

  await waitFor(() => assert.equal(
    request?.input,
    '/api/marine/history?latitude=6.025&longitude=80.216&from=2026-09-26T00%3A00%3A00.000Z&to=2026-09-27T08%3A30%3A00.000Z',
  ))
  assert.ok(screen.getByText(/No condition snapshots match these filters yet/))
})

test('WEB-MARINE-UI-009 safety profiles list configured limits and flag the active version (ui-integration: marine-safety-profiles)', async () => {
  globalThis.fetch = async () => jsonResponse([
    {
      id: 'profile-1',
      activityId: surfingActivityId,
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
    },
    {
      id: 'profile-0',
      activityId: snorkelingActivityId,
      activityName: 'Snorkeling',
      maxWindSpeed: 20,
      maxWaveHeight: 1,
      maxSwellHeight: 0.8,
      cautionWindSpeed: null,
      cautionWaveHeight: null,
      cautionSwellHeight: null,
      isActive: false,
      version: 1,
      createdAt: '2026-09-26T00:00:00.000Z',
      updatedAt: '2026-09-26T01:00:00.000Z',
    },
  ])

  renderInApp(createElement(MarineSafetyProfilesPage), { path: '/marine/safety-profiles', auth: makeAuthSessionValue({ user: marineUser(), status: 'signed-in' }) })

  await waitFor(() => assert.ok(screen.getByText('Version 2')))
  assert.ok(screen.getByText('ACTIVE'))
  assert.ok(screen.getByText('INACTIVE'))
  assert.ok(screen.getByLabelText(/Maximum wind speed/))
  assert.ok(screen.getByText(/Changing safety limits requires the marine profile manage permission/))
})

test('WEB-MARINE-UI-010 a manager can save edited limits and sees the server version bump (ui-integration: marine-safety-profiles)', async () => {
  const calls = []
  globalThis.fetch = async (input, init = {}) => {
    calls.push({ input, init })
    if (calls.length === 1) {
      return jsonResponse([{
        id: 'profile-1',
        activityId: surfingActivityId,
        activityName: 'Surfing',
        maxWindSpeed: 35,
        maxWaveHeight: 1.5,
        maxSwellHeight: 1.2,
        cautionWindSpeed: null,
        cautionWaveHeight: null,
        cautionSwellHeight: null,
        isActive: true,
        version: 2,
        createdAt: '2026-09-27T00:00:00.000Z',
        updatedAt: '2026-09-27T01:00:00.000Z',
      }])
    }
    return jsonResponse({
      id: 'profile-1',
      activityId: surfingActivityId,
      activityName: 'Surfing',
      maxWindSpeed: 30,
      maxWaveHeight: 1.5,
      maxSwellHeight: 1.2,
      cautionWindSpeed: null,
      cautionWaveHeight: null,
      cautionSwellHeight: null,
      isActive: true,
      version: 3,
      createdAt: '2026-09-27T00:00:00.000Z',
      updatedAt: '2026-09-27T02:00:00.000Z',
    })
  }

  const user = marineUser(['marine.profile.read', 'marine.profile.manage'])
  renderInApp(createElement(MarineSafetyProfilesPage), { path: '/marine/safety-profiles', auth: makeAuthSessionValue({ user, status: 'signed-in' }) })

  await waitFor(() => assert.ok(screen.getByLabelText('Maximum wind speed (km/h)')))
  const windInput = document.getElementById('edit-profile-max-wind')
  await userEvent.setup().clear(windInput)
  await userEvent.setup().type(windInput, '30')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Save limits' }))

  await waitFor(() => assert.match(screen.getByRole('status').textContent, /version 3/))
  const putCall = calls.find((call) => call.init.method === 'PUT')
  assert.equal(putCall.input, '/api/marine/safety-profiles/profile-1')
  assert.deepEqual(JSON.parse(putCall.init.body), {
    maxWindSpeed: 30,
    maxWaveHeight: 1.5,
    maxSwellHeight: 1.2,
    isActive: true,
  })
  assert.equal(screen.getByRole('button', { name: 'Deactivate profile' }).type, 'button')
})

test('WEB-MARINE-UI-011 a caution band above its maximum is rejected by the server and reported safely (ui-integration: marine-safety-profiles)', async () => {
  let createCalls = 0
  globalThis.fetch = async (input, init = {}) => {
    if (init?.method === 'POST' && String(input).endsWith('/api/marine/safety-profiles')) {
      createCalls += 1
      return jsonResponse({ status: 400, detail: 'Caution wind speed must be below the maximum wind speed.' }, 400)
    }
    return jsonResponse([{
      id: 'profile-1',
      activityId: surfingActivityId,
      activityName: 'Surfing',
      maxWindSpeed: 35,
      maxWaveHeight: 1.5,
      maxSwellHeight: 1.2,
      cautionWindSpeed: null,
      cautionWaveHeight: null,
      cautionSwellHeight: null,
      isActive: true,
      version: 2,
      createdAt: '2026-09-27T00:00:00.000Z',
      updatedAt: '2026-09-27T01:00:00.000Z',
    }])
  }
  const user = marineUser(['marine.profile.read', 'marine.profile.manage'])
  renderInApp(createElement(MarineSafetyProfilesPage), { path: '/marine/safety-profiles', auth: makeAuthSessionValue({ user, status: 'signed-in' }) })

  await waitFor(() => assert.ok(document.getElementById('create-profile-max-wind')))
  await userEvent.setup().type(document.getElementById('create-profile-max-wind'), '30')
  await userEvent.setup().type(document.getElementById('create-profile-caution-wind'), '35')
  await userEvent.setup().type(document.getElementById('create-profile-max-wave'), '1.5')
  await userEvent.setup().type(document.getElementById('create-profile-max-swell'), '1.2')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Create profile' }))

  assert.equal((await screen.findByRole('alert')).textContent, 'Caution wind speed must be below the maximum wind speed.')
  assert.equal(createCalls, 1, 'the create form must submit exactly once')
  assert.equal(screen.getByRole('button', { name: 'Create profile' }).disabled, false)
})

test('WEB-MARINE-UI-012 accounts without the marine read grant are refused before any request (ui-integration: marine-conditions)', () => {
  renderInApp(createElement(AppRoutes), {
    path: '/marine/conditions',
    auth: makeAuthSessionValue({ user: marineUser(['profile.read']), status: 'signed-in' }),
  })

  assert.ok(screen.getByRole('heading', { name: 'This coastal service needs permission.' }))
  assert.equal(screen.getByRole('link', { name: 'Return to your dashboard' }).getAttribute('href'), '/dashboard')
  assert.equal(screen.queryByRole('button', { name: 'Check conditions & suitability' }), null)
})

test('WEB-MARINE-UI-013 the marine routes are registered inside the shared signed-in shell (ui-integration: marine-conditions)', () => {
  renderInApp(createElement(AppRoutes), {
    path: '/marine/safety-profiles',
    auth: makeAuthSessionValue({ user: marineUser(), status: 'signed-in' }),
  })

  assert.ok(document.querySelector('header'))
  assert.ok(document.querySelector('footer'))
  assert.ok(screen.getByRole('heading', { name: 'Safety profiles' }))
  assert.equal(screen.getByTestId('router-location').textContent, '/marine/safety-profiles')
})

test('WEB-MARINE-UI-014 a selected activity and local date-time reach both APIs as the same UTC moment (ui-integration: marine-conditions)', async () => {
  const calls = []
  globalThis.fetch = async (input, init = {}) => {
    calls.push({ input, init })
    return String(input).startsWith('/api/marine/current')
      ? jsonResponse(makeSnapshot())
      : jsonResponse(makeSuitability({ activityId: snorkelingActivityId, activityName: 'Snorkeling' }))
  }

  renderInApp(createElement(MarineConditionsPage), { path: '/marine/conditions', auth: makeAuthSessionValue({ user: marineUser(), status: 'signed-in' }) })
  fireEvent.change(screen.getByLabelText(/Requested time/), { target: { value: '2026-10-03T15:45' } })
  await userEvent.setup().selectOptions(screen.getByLabelText('Activity'), snorkelingActivityId)
  await userEvent.setup().click(screen.getByRole('button', { name: 'Check conditions & suitability' }))

  await waitFor(() => assert.ok(screen.getByText('SERVER ASSESSMENT')))
  assert.equal(calls.length, 2)
  assert.equal(calls[0].input, '/api/marine/current?latitude=6.025&longitude=80.216&time=2026-10-03T15%3A45%3A00.000Z')
  assert.deepEqual(JSON.parse(calls[1].init.body), {
    activityId: snorkelingActivityId,
    latitude: 6.025,
    longitude: 80.216,
    dateTime: '2026-10-03T15:45:00.000Z',
  })
  assert.equal(calls[0].init.credentials, 'include')
  assert.equal(calls[1].init.credentials, 'include')
})

test('WEB-MARINE-UI-015 a CAUTION assessment remains a server result with its caution factors (ui-integration: marine-conditions)', async () => {
  globalThis.fetch = async (input) => String(input).startsWith('/api/marine/current')
    ? jsonResponse(makeSnapshot())
    : jsonResponse(makeSuitability({ status: 'CAUTION', cautionFactors: ['waveHeight'] }))

  renderInApp(createElement(MarineConditionsPage), { path: '/marine/conditions', auth: makeAuthSessionValue({ user: marineUser(), status: 'signed-in' }) })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Check conditions & suitability' }))

  await waitFor(() => assert.ok(screen.getByText('SERVER ASSESSMENT')))
  assert.ok(screen.getByText('CAUTION'))
  assert.ok(screen.getByText('CAUTION FACTORS'))
  assert.ok(screen.getByText(/Wave height is within the configured caution band/))
  assert.equal(screen.queryByText('LIMITS EXCEEDED'), null)
})

test('WEB-MARINE-UI-016 a history permission denial is shown safely without hiding the page shell (ui-integration: marine-history)', async () => {
  globalThis.fetch = async () => jsonResponse({ status: 403, detail: 'Your current roles do not allow this action.' }, 403)

  renderInApp(createElement(MarineHistoryPage), { path: '/marine/history', auth: makeAuthSessionValue({ user: marineUser(), status: 'signed-in' }) })

  assert.equal((await screen.findByRole('alert')).textContent, 'Your current roles do not allow this action.')
  assert.ok(screen.getByRole('heading', { name: 'Condition history' }))
})

test('WEB-MARINE-UI-017 a permitted manager creates a profile and sees the refreshed server record (ui-integration: marine-safety-profiles)', async () => {
  const calls = []
  const created = makeProfile({
    id: 'profile-snorkeling',
    activityId: snorkelingActivityId,
    activityName: 'Snorkeling',
    maxWindSpeed: 40,
    maxWaveHeight: 2,
    maxSwellHeight: 1.5,
    version: 1,
  })
  globalThis.fetch = async (input, init = {}) => {
    calls.push({ input, init })
    if (init.method === 'POST') return jsonResponse(created)
    return jsonResponse(calls.some((call) => call.init.method === 'POST') ? [created] : [])
  }

  renderInApp(createElement(MarineSafetyProfilesPage), {
    path: '/marine/safety-profiles',
    auth: makeAuthSessionValue({ user: marineUser(['marine.profile.read', 'marine.profile.manage']), status: 'signed-in' }),
  })
  await waitFor(() => assert.ok(screen.getByLabelText('Activity')))
  await userEvent.setup().selectOptions(screen.getByLabelText('Activity'), snorkelingActivityId)
  await userEvent.setup().type(document.getElementById('create-profile-max-wind'), '40')
  await userEvent.setup().type(document.getElementById('create-profile-max-wave'), '2')
  await userEvent.setup().type(document.getElementById('create-profile-max-swell'), '1.5')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Create profile' }))

  await waitFor(() => assert.match(screen.getByRole('status').textContent, /Snorkeling safety profile is now active/))
  assert.deepEqual(calls.map((call) => [call.input, call.init.method]), [
    ['/api/marine/safety-profiles', 'GET'],
    ['/api/marine/safety-profiles', 'POST'],
    ['/api/marine/safety-profiles', 'GET'],
  ])
  assert.deepEqual(JSON.parse(calls[1].init.body), {
    activityId: snorkelingActivityId,
    maxWindSpeed: 40,
    maxWaveHeight: 2,
    maxSwellHeight: 1.5,
  })
  assert.ok(calls.every((call) => call.init.credentials === 'include'))
  assert.ok(screen.getByRole('heading', { name: 'Snorkeling' }))
})

test('WEB-MARINE-UI-018 read-only marine permission cannot expose profile management actions (ui-integration: marine-safety-profiles)', async () => {
  globalThis.fetch = async () => jsonResponse([makeProfile()])

  renderInApp(createElement(MarineSafetyProfilesPage), {
    path: '/marine/safety-profiles',
    auth: makeAuthSessionValue({ user: marineUser(), status: 'signed-in' }),
  })

  await waitFor(() => assert.ok(screen.getByRole('heading', { name: 'Surfing' })))
  assert.equal(screen.queryByRole('button', { name: 'Create profile' }), null)
  assert.equal(screen.queryByRole('button', { name: 'Deactivate profile' }), null)
  assert.equal(screen.getByRole('button', { name: 'Save limits' }).disabled, true)
  assert.equal(document.getElementById('edit-profile-max-wind').disabled, true)
  assert.ok(screen.getByText(/Changing safety limits requires the marine profile manage permission/))
})

test('WEB-MARINE-UI-019 invalid profile limits do not send a mutation request (ui-integration: marine-safety-profiles)', async () => {
  const calls = []
  globalThis.fetch = async (input, init = {}) => {
    calls.push({ input, init })
    return jsonResponse([])
  }

  renderInApp(createElement(MarineSafetyProfilesPage), {
    path: '/marine/safety-profiles',
    auth: makeAuthSessionValue({ user: marineUser(['marine.profile.read', 'marine.profile.manage']), status: 'signed-in' }),
  })
  await waitFor(() => assert.ok(document.getElementById('create-profile-max-wind')))
  fireEvent.change(document.getElementById('create-profile-max-wind'), { target: { value: '0' } })
  fireEvent.change(document.getElementById('create-profile-max-wave'), { target: { value: '1.5' } })
  fireEvent.change(document.getElementById('create-profile-max-swell'), { target: { value: '1.2' } })
  fireEvent.submit(document.getElementById('create-profile-max-wind').closest('form'))

  assert.equal((await screen.findByRole('alert')).textContent, 'Enter positive maximum limits: wind up to 1000 km/h, wave and swell up to 50 m.')
  assert.equal(calls.length, 1)
  assert.equal(calls[0].init.method, 'GET')
  assert.equal(calls[0].input, '/api/marine/safety-profiles')
})

test('WEB-MARINE-UI-020 cancelling profile deactivation keeps the profile active and sends no delete request (ui-integration: marine-safety-profiles)', async () => {
  const calls = []
  let confirmation = ''
  window.confirm = (message) => { confirmation = message; return false }
  globalThis.fetch = async (input, init = {}) => {
    calls.push({ input, init })
    return jsonResponse([makeProfile()])
  }

  renderInApp(createElement(MarineSafetyProfilesPage), {
    path: '/marine/safety-profiles',
    auth: makeAuthSessionValue({ user: marineUser(['marine.profile.read', 'marine.profile.manage']), status: 'signed-in' }),
  })
  await waitFor(() => assert.ok(screen.getByRole('button', { name: 'Deactivate profile' })))
  await userEvent.setup().click(screen.getByRole('button', { name: 'Deactivate profile' }))

  assert.match(confirmation, /Assessments keep their history/)
  assert.equal(calls.length, 1)
  assert.equal(calls[0].input, '/api/marine/safety-profiles')
  assert.equal(screen.getByText('ACTIVE').textContent, 'ACTIVE')
  assert.equal(screen.queryByRole('status'), null)
})

test('WEB-MARINE-UI-021 confirming profile deactivation sends DELETE, refreshes state and preserves the history notice (ui-integration: marine-safety-profiles)', async () => {
  const calls = []
  window.confirm = () => true
  globalThis.fetch = async (input, init = {}) => {
    calls.push({ input, init })
    if (init.method === 'DELETE') return jsonResponse(undefined, 204)
    return jsonResponse(calls.some((call) => call.init.method === 'DELETE') ? [] : [makeProfile()])
  }

  renderInApp(createElement(MarineSafetyProfilesPage), {
    path: '/marine/safety-profiles',
    auth: makeAuthSessionValue({ user: marineUser(['marine.profile.read', 'marine.profile.manage']), status: 'signed-in' }),
  })
  await waitFor(() => assert.ok(screen.getByRole('button', { name: 'Deactivate profile' })))
  await userEvent.setup().click(screen.getByRole('button', { name: 'Deactivate profile' }))

  await waitFor(() => assert.match(screen.getByRole('status').textContent, /Surfing safety profile was deactivated/))
  assert.deepEqual(calls.map((call) => [call.input, call.init.method]), [
    ['/api/marine/safety-profiles', 'GET'],
    ['/api/marine/safety-profiles/profile-1', 'DELETE'],
    ['/api/marine/safety-profiles', 'GET'],
  ])
  assert.ok(screen.getByText(/No safety profiles are configured yet/))
  assert.ok(screen.getByRole('status').textContent.includes('Assessments keep their history'))
})

test('WEB-MARINE-UI-022 an expired session shows safe recovery and no marine assessment data (ui-integration: marine-conditions)', async () => {
  const calls = []
  globalThis.fetch = async (input, init = {}) => {
    calls.push({ input, init })
    return new Response('Unauthorized: revoked session session-123', { status: 401 })
  }

  renderInApp(createElement(MarineConditionsPage), {
    path: '/marine/conditions',
    auth: makeAuthSessionValue({ user: marineUser(), status: 'signed-in' }),
  })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Check conditions & suitability' }))

  const alert = await screen.findByRole('alert')
  assert.equal(alert.textContent, 'We couldn’t complete that marine request. Please try again.')
  assert.doesNotMatch(alert.textContent, /revoked session|session-123|Unauthorized/)
  assert.equal(screen.queryByText('SERVER ASSESSMENT'), null)
  assert.equal(calls.length, 2)
  assert.ok(calls.every((call) => call.init.credentials === 'include'))
})
