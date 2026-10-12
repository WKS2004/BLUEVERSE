// Planner tab motion and inline reads; planner-plan / planner-saved-trips.
import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { act, cleanup, closeWebTestServer, createElement, fireEvent, jsonResponse,
  loadWebModule, makeAuthSessionValue, makeAuthUser, renderInApp, resetTestBrowser,
  screen, waitFor, within } from '../../testSupport/reactTestHarness.js'

const { default: AppRoutes } = await loadWebModule('/src/app/routes.tsx')
const { getLoadingScreenSnapshot, subscribeToLoadingScreen } = await loadWebModule('/src/features/loading/backendLoading.ts')
const originalFetch = globalThis.fetch
const permissions = ['planner.recommendations.create', 'planner.itineraries.manage']
afterEach(() => { cleanup(); resetTestBrowser(); globalThis.fetch = originalFetch })
after(closeWebTestServer)

function deferred() {
  let resolve
  const promise = new Promise(done => { resolve = done })
  return { promise, resolve }
}

test('WEB-PLANNER-UI-039 both tab directions keep the page visible during delayed reads and ignore departed results', async () => {
  const firstCatalogue = deferred(), secondCatalogue = deferred(), history = deferred()
  let catalogueCalls = 0
  const calls = []
  globalThis.fetch = (input, init) => {
    calls.push({ input, init })
    if (input === '/api/planner/catalogue') return ++catalogueCalls === 1 ? firstCatalogue.promise : secondCatalogue.promise
    assert.equal(input, '/api/planner/itineraries?page=1&pageSize=20')
    return history.promise
  }
  renderInApp(createElement(AppRoutes), { path: '/planner/plan', auth: makeAuthSessionValue({ status: 'signed-in', user: makeAuthUser({ permissions }) }) })
  await screen.findByRole('heading', { name: 'Create your coastal plan.' })
  await waitFor(() => assert.equal(getLoadingScreenSnapshot(), false)) // initial lazy module may load once
  assert.ok(document.querySelector('.motion-safe\\:animate-planner-create'))
  const activations = []
  const unsubscribe = subscribeToLoadingScreen(() => activations.push(getLoadingScreenSnapshot()))
  try {
    fireEvent.click(within(screen.getByRole('navigation', { name: 'Planning pages' })).getByRole('link', { name: 'Plan history' }))
    await screen.findByRole('heading', { name: 'Your plan history.' })
    assert.ok(screen.getByText('Opening your saved trips…'))
    assert.ok(document.querySelector('.motion-safe\\:animate-planner-history'))
    assert.equal(getLoadingScreenSnapshot(), false)
    assert.equal(within(screen.getByRole('navigation', { name: 'Planning pages' })).getByRole('link', { name: 'Plan history' }).getAttribute('aria-current'), 'page')
    fireEvent.click(within(screen.getByRole('navigation', { name: 'Planning pages' })).getByRole('link', { name: 'Create a plan' }))
    await screen.findByRole('heading', { name: 'Create your coastal plan.' })
    assert.ok(screen.getByText('Preparing your coastal planner…'))
    assert.ok(document.querySelector('.motion-safe\\:animate-planner-create'))
    assert.equal(getLoadingScreenSnapshot(), false)
    await act(async () => {
      history.resolve(jsonResponse([]))
      firstCatalogue.resolve(jsonResponse({ detail: 'Departed read failed' }, 503))
      secondCatalogue.resolve(jsonResponse({ status: 'UNAVAILABLE', message: 'Current destinations unavailable.', destinations: [] }))
    })
    await screen.findByRole('heading', { name: 'Destinations aren’t available right now.' })
    assert.equal(screen.queryByText('Departed read failed'), null)
    assert.equal(screen.queryByRole('heading', { name: 'Your first coastal day awaits.' }), null)
    assert.deepEqual(activations, [])
    assert.equal(calls.length, 3)
    assert.ok(calls.every(call => call.init.credentials === 'include' && call.init.signal instanceof AbortSignal))
  } finally { unsubscribe() }
})
