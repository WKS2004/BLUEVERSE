import { JSDOM, VirtualConsole } from 'jsdom'
import { fileURLToPath } from 'node:url'
import { createServer } from 'vite'

const virtualConsole = new VirtualConsole()
virtualConsole.on('jsdomError', (error) => {
  if (error.type === 'not-implemented' && /navigation to another Document/.test(error.message)) return
  console.error(error)
})

const dom = new JSDOM('<!doctype html><html><body></body></html>', {
  url: 'http://localhost/',
  pretendToBeVisual: true,
  virtualConsole,
})

Object.defineProperties(globalThis, {
  window: { configurable: true, value: dom.window },
  document: { configurable: true, value: dom.window.document },
  navigator: { configurable: true, value: dom.window.navigator },
  HTMLElement: { configurable: true, value: dom.window.HTMLElement },
  HTMLFormElement: { configurable: true, value: dom.window.HTMLFormElement },
  Node: { configurable: true, value: dom.window.Node },
  Event: { configurable: true, value: dom.window.Event },
  FormData: { configurable: true, value: dom.window.FormData },
  MouseEvent: { configurable: true, value: dom.window.MouseEvent },
  getComputedStyle: { configurable: true, value: dom.window.getComputedStyle.bind(dom.window) },
  requestAnimationFrame: { configurable: true, value: dom.window.requestAnimationFrame.bind(dom.window) },
  cancelAnimationFrame: { configurable: true, value: dom.window.cancelAnimationFrame.bind(dom.window) },
})

globalThis.IS_REACT_ACT_ENVIRONMENT = true
dom.window.matchMedia = (media) => ({
  matches: media === '(min-width: 640px)',
  media,
  onchange: null,
  addListener() {},
  removeListener() {},
  addEventListener() {},
  removeEventListener() {},
  dispatchEvent() { return false },
})
dom.window.scrollTo = () => {}
dom.window.confirm = () => false
dom.window.HTMLElement.prototype.scrollIntoView = () => {}

const [{ cleanup, render, screen, waitFor, within, fireEvent }, { createElement }, { MemoryRouter, useLocation }, { default: userEvent }] = await Promise.all([
  import('@testing-library/react'),
  import('react'),
  import('react-router'),
  import('@testing-library/user-event'),
])

const webRoot = fileURLToPath(new URL('../../', import.meta.url))
let viteServerPromise
let viteServer

async function getViteServer() {
  viteServerPromise ??= createServer({
    root: webRoot,
    configFile: `${webRoot}/vite.config.ts`,
    appType: 'custom',
    server: { middlewareMode: true, hmr: false, ws: false },
  })
  viteServer ??= await viteServerPromise
  return viteServer
}

const { AuthSessionContext } = await (await getViteServer()).ssrLoadModule('/src/features/auth/authSession.ts')
const { act } = await import('@testing-library/react')

export { act, cleanup, createElement, fireEvent, render, screen, userEvent, waitFor, within, AuthSessionContext, MemoryRouter }

export function jsonResponse(body, status = 200) {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
  })
}

export function makeAuthUser(overrides = {}) {
  return {
    id: 'user-1',
    email: 'coast@example.test',
    fullName: 'Coastal Guest',
    isActive: true,
    createdAt: '2026-01-01T00:00:00.000Z',
    roles: ['Visitor'],
    permissions: ['profile.read'],
    ...overrides,
  }
}

export function makeAuthSessionValue(overrides = {}) {
  return {
    user: null,
    accounts: [],
    status: 'signed-out',
    error: null,
    switchingAccountId: null,
    acceptAuthenticatedUser() {},
    async switchAccount() {},
    async removeAccount() {},
    forgetAccount() {},
    async signOut() {},
    async signOutEverywhere() {},
    async updateUser() { return null },
    ...overrides,
  }
}

export function renderInApp(element, { path = '/', auth = makeAuthSessionValue(), children = null } = {}) {
  function LocationProbe() {
    const location = useLocation()
    return createElement('span', { 'aria-hidden': true, 'data-testid': 'router-location' }, `${location.pathname}${location.search}${location.hash}`)
  }

  return render(
    createElement(AuthSessionContext.Provider, { value: auth },
      createElement(MemoryRouter, { initialEntries: [path] },
        element,
        children,
        createElement(LocationProbe),
      ),
    ),
  )
}

export function renderWithSessionProvider(element, AuthSessionProvider, { path = '/' } = {}) {
  function LocationProbe() {
    const location = useLocation()
    return createElement('span', { 'aria-hidden': true, 'data-testid': 'router-location' }, `${location.pathname}${location.search}${location.hash}`)
  }

  return render(
    createElement(MemoryRouter, { initialEntries: [path] },
      createElement(AuthSessionProvider, null, element),
      createElement(LocationProbe),
    ),
  )
}

export async function loadWebModule(path) {
  return (await getViteServer()).ssrLoadModule(path)
}

export async function closeWebTestServer() {
  if (!viteServerPromise) return
  const vite = await getViteServer()
  viteServerPromise = undefined
  viteServer = undefined
  await vite.close()
}

export function resetTestBrowser() {
  document.body.replaceChildren()
  window.localStorage.clear()
  window.sessionStorage.clear()
  window.confirm = () => false
}
