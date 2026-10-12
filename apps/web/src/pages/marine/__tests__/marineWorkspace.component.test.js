import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { useNavigate } from 'react-router'
import { act, cleanup, closeWebTestServer, createElement, jsonResponse, loadWebModule, makeAuthSessionValue, makeAuthUser, renderInApp, resetTestBrowser, screen, userEvent, waitFor } from '../../../testSupport/reactTestHarness.js'

const [{ default: AppRoutes }, { default: RouteScrollManager }, { default: BackendLoadingScreen }, loading, api] = await Promise.all([
  loadWebModule('/src/app/routes.tsx'),
  loadWebModule('/src/app/RouteScrollManager.tsx'),
  loadWebModule('/src/components/feedback/BackendLoadingScreen.tsx'),
  loadWebModule('/src/features/loading/backendLoading.ts'),
  loadWebModule('/src/features/marine/marineApi.ts'),
])
const originalFetch = globalThis.fetch
const originalScrollTo = window.scrollTo
let pending = []
afterEach(async () => {
  for (const finish of pending.splice(0)) finish(jsonResponse([]))
  cleanup(); resetTestBrowser(); globalThis.fetch = originalFetch; window.scrollTo = originalScrollTo
  await new Promise(resolve => setTimeout(resolve, 200))
})
after(closeWebTestServer)

function HistoryButtons() {
  const navigate = useNavigate()
  return createElement('div', null,
    createElement('button', { onClick: () => navigate(-1) }, 'Previous workflow'),
    createElement('button', { onClick: () => navigate(1) }, 'Next workflow'),
    createElement('button', { onClick: () => navigate('/dashboard') }, 'Open dashboard'),
    createElement('button', { onClick: () => navigate('/marine/conditions') }, 'Return to Marine'),
  )
}
function mount(permissions = ['marine.profile.read']) {
  return renderInApp(createElement('div', null, createElement(RouteScrollManager), createElement(BackendLoadingScreen), createElement(AppRoutes), createElement(HistoryButtons)), {
    path: '/marine/conditions', auth: makeAuthSessionValue({ status: 'signed-in', user: makeAuthUser({ permissions }) }),
  })
}
const workflowLink = name => [...screen.getByRole('navigation', { name: 'Marine workflows' }).querySelectorAll('a')].find(link => link.textContent === name)
const settle = async () => {
  await act(async () => { for (const finish of pending.splice(0)) finish(jsonResponse([])) })
  await waitFor(() => assert.equal(loading.getLoadingScreenSnapshot(), false))
}

test('WEB-MARINE-TABS-001 persistent heading and unchanged pill navigation precede all three animated heroes; slow and rapid tab reads stay inline (ui-integration: marine-conditions)', async () => {
  let hold = false
  const calls = []
  globalThis.fetch = (input, init) => {
    calls.push({ input: String(input), init })
    return hold ? new Promise(resolve => pending.push(resolve)) : Promise.resolve(jsonResponse([]))
  }
  mount()
  await waitFor(() => assert.equal(loading.getLoadingScreenSnapshot(), false))
  const heading = screen.getByRole('heading', { level: 1, name: 'Marine & safety' })
  const navigation = screen.getByRole('navigation', { name: 'Marine workflows' })
  assert.equal(document.querySelectorAll('h1').length, 1)
  assert.ok(heading.compareDocumentPosition(navigation) & Node.DOCUMENT_POSITION_FOLLOWING)
  assert.match(navigation.className, /rounded-3xl/)
  assert.match(navigation.className, /bg-white/)
  assert.match(navigation.parentElement.className, /sticky top-\[76px\] z-40/)
  assert.match(workflowLink('Coastal conditions').className, /rounded-full/)
  assert.match(workflowLink('Coastal conditions').className, /bg-coast-deep text-white/)
  const firstPanel = document.querySelector('[data-marine-workflow]')
  const scrolls = []
  window.scrollTo = options => scrolls.push(options)
  const anchor = document.getElementById('marine-workflow-start')
  anchor.getBoundingClientRect = () => ({ top: 180 })
  hold = true
  await userEvent.setup().click(workflowLink('Coastal evidence'))
  assert.equal(screen.getByRole('heading', { level: 1, name: 'Marine & safety' }), heading)
  assert.equal(screen.getByRole('navigation', { name: 'Marine workflows' }), navigation)
  assert.ok(screen.getByRole('heading', { level: 2, name: 'Marine history' }))
  assert.ok(screen.getByText('Loading saved coastal evidence…'))
  assert.equal(loading.getLoadingScreenSnapshot(), false)
  assert.equal(document.querySelector('.blueverse-loader-backdrop'), null)
  assert.equal(pending.length, 3)
  assert.equal(workflowLink('Coastal evidence').getAttribute('aria-current'), 'page')
  const secondPanel = document.querySelector('[data-marine-workflow]')
  assert.notEqual(secondPanel, firstPanel)
  assert.match(secondPanel.className, /motion-safe:animate-coast-tab-change/)
  assert.ok(navigation.compareDocumentPosition(secondPanel) & Node.DOCUMENT_POSITION_FOLLOWING)
  assert.deepEqual(scrolls, [])
  // Switch again before the previous tab's data resolves.
  await userEvent.setup().click(workflowLink('Activity safety limits'))
  assert.equal(pending.length, 5)
  assert.equal(loading.getLoadingScreenSnapshot(), false)
  assert.ok(screen.getByText('Loading activity safety limits…'))
  await settle()
  assert.ok(screen.getByRole('heading', { level: 2, name: 'Safety profiles' }))
  assert.equal(screen.queryByRole('heading', { level: 2, name: 'Marine history' }), null)
  assert.ok(calls.every(call => call.init.credentials === 'include' && call.init.method === 'GET'))
})

test('WEB-MARINE-TABS-002 browser back/forward retain inline loading and scroll the workflow start into view only when already scrolled past it (ui-integration: marine-condition-history)', async () => {
  let hold = false
  globalThis.fetch = () => hold ? new Promise(resolve => pending.push(resolve)) : Promise.resolve(jsonResponse([]))
  mount()
  await waitFor(() => assert.equal(loading.getLoadingScreenSnapshot(), false))
  const anchor = document.getElementById('marine-workflow-start')
  const scrolls = []
  anchor.getBoundingClientRect = () => ({ top: -400 })
  anchor.scrollIntoView = options => scrolls.push(options)
  await userEvent.setup().click(workflowLink('Coastal evidence'))
  assert.deepEqual(scrolls, [{ block: 'start', behavior: 'instant' }])
  hold = true
  await userEvent.setup().click(screen.getByRole('button', { name: 'Previous workflow' }))
  assert.equal(screen.getByTestId('router-location').textContent, '/marine/conditions')
  assert.equal(loading.getLoadingScreenSnapshot(), false)
  await settle()
  await userEvent.setup().click(screen.getByRole('button', { name: 'Next workflow' }))
  assert.equal(screen.getByTestId('router-location').textContent, '/marine/history')
  assert.equal(loading.getLoadingScreenSnapshot(), false)
  await settle()
  assert.equal(scrolls.length, 3)
})

test('WEB-MARINE-TABS-003 inline read errors retain status/details; explicit reads, mutations and Auth retain their loading feedback (ui-integration: marine-safety-profile-management)', async () => {
  const calls = []
  globalThis.fetch = async (input, init) => { calls.push({ input, init }); return jsonResponse({ detail: 'Current read grant is required.' }, 403) }
  await assert.rejects(api.getMarineHistory({}, false), error => error instanceof api.MarineApiError && error.status === 403 && error.message === 'Current read grant is required.')
  assert.equal(loading.getLoadingScreenSnapshot(), false)
  assert.equal(calls[0].init.credentials, 'include')
  assert.equal(calls[0].init.method, 'GET')
  globalThis.fetch = () => new Promise(resolve => pending.push(resolve))
  const explicitRead = api.getMarineSafetyProfiles()
  assert.equal(loading.getLoadingScreenSnapshot(), true)
  await waitFor(() => assert.equal(pending.length, 1))
  for (const finish of pending.splice(0)) finish(jsonResponse([]))
  assert.deepEqual(await explicitRead, [])
  await waitFor(() => assert.equal(loading.getLoadingScreenSnapshot(), false))
  const mutation = api.deactivateMarineSafetyProfile('profile-to-confirm')
  assert.equal(loading.getLoadingScreenSnapshot(), true)
  await waitFor(() => assert.equal(pending.length, 1))
  for (const finish of pending.splice(0)) finish(new Response(null, { status: 204 }))
  assert.equal(await mutation, undefined)
  await waitFor(() => assert.equal(loading.getLoadingScreenSnapshot(), false))
  const finishAuth = loading.beginLoadingScreen(loading.AUTH_LOADING_CONTEXTS.sessionRestore)
  const inlineRead = api.getMarineSafetyProfiles(false)
  assert.equal(loading.getLoadingScreenContextSnapshot().mode, 'authentication')
  assert.equal(loading.getLoadingScreenSnapshot(), true)
  for (const finish of pending.splice(0)) finish(jsonResponse([]))
  await inlineRead
  assert.equal(loading.getLoadingScreenSnapshot(), true)
  finishAuth()
  await waitFor(() => assert.equal(loading.getLoadingScreenSnapshot(), false))
})

test('WEB-MARINE-TABS-004 manage-only permissions cannot mount the workspace or call its services (ui-integration: marine-conditions)', () => {
  let calls = 0
  globalThis.fetch = async () => { calls++; return jsonResponse([]) }
  mount(['marine.profile.manage'])
  assert.ok(screen.getByRole('heading', { name: 'This coastal service needs permission.' }))
  assert.equal(screen.queryByRole('navigation', { name: 'Marine workflows' }), null)
  assert.equal(calls, 0)
})

test('WEB-MARINE-TABS-005 a failed inline profile read shows a recoverable error and an explicit retry retains loading feedback (ui-integration: marine-safety-profile-management)', async () => {
  let fail = false
  const profileCalls = []
  globalThis.fetch = async (input, init) => {
    if (String(input) !== '/api/marine/safety-profiles') return jsonResponse([])
    profileCalls.push(init)
    return fail ? jsonResponse({ detail: 'The profile service is unavailable.' }, 503) : jsonResponse([])
  }
  mount()
  await waitFor(() => assert.equal(loading.getLoadingScreenSnapshot(), false))
  fail = true
  await userEvent.setup().click(workflowLink('Activity safety limits'))
  assert.ok(await screen.findByText('The profile service is unavailable.'))
  assert.equal(loading.getLoadingScreenSnapshot(), false)
  assert.equal(profileCalls.length, 1)
  globalThis.fetch = (input, init) => {
    assert.equal(String(input), '/api/marine/safety-profiles')
    profileCalls.push(init)
    return new Promise(resolve => pending.push(resolve))
  }
  await userEvent.setup().click(screen.getByRole('button', { name: 'Retry profiles' }))
  await waitFor(() => assert.equal(pending.length, 1))
  assert.equal(loading.getLoadingScreenSnapshot(), true)
  await settle()
  assert.equal(profileCalls.length, 2)
  assert.ok(profileCalls.every(init => init.method === 'GET' && init.credentials === 'include'))
  assert.equal(screen.queryByText('The profile service is unavailable.'), null)
  assert.equal(screen.queryByRole('button', { name: 'Retry profiles' }), null)
})

test('WEB-MARINE-TABS-006 keyboard tab navigation preserves focus; leaving and re-entering Marine restores initial-entry loading (ui-integration: marine-conditions)', async () => {
  let hold = false
  globalThis.fetch = () => hold ? new Promise(resolve => pending.push(resolve)) : Promise.resolve(jsonResponse([]))
  mount()
  await waitFor(() => assert.equal(loading.getLoadingScreenSnapshot(), false))
  const next = workflowLink('Coastal evidence')
  next.focus()
  hold = true
  await userEvent.setup().keyboard('{Enter}')
  assert.equal(document.activeElement, next)
  assert.equal(screen.getByTestId('router-location').textContent, '/marine/history')
  assert.equal(loading.getLoadingScreenSnapshot(), false)
  await settle()
  hold = false
  await userEvent.setup().click(screen.getByRole('button', { name: 'Open dashboard' }))
  assert.equal(screen.queryByRole('navigation', { name: 'Marine workflows' }), null)
  hold = true
  await userEvent.setup().click(screen.getByRole('button', { name: 'Return to Marine' }))
  await waitFor(() => assert.equal(pending.length, 1))
  assert.equal(loading.getLoadingScreenSnapshot(), true)
  assert.ok(screen.getByRole('heading', { level: 1, name: 'Marine & safety' }))
  await settle()
})
