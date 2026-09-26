import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import {
  cleanup,
  closeWebTestServer,
  createElement,
  jsonResponse,
  loadWebModule,
  render,
  resetTestBrowser,
  screen,
  userEvent,
} from '../../testSupport/reactTestHarness.js'

const { default: App } = await loadWebModule('/src/app/App.tsx')
const originalFetch = globalThis.fetch
const transitionNoticeKey = 'blueverse.auth-transition-notice'

afterEach(() => {
  cleanup()
  resetTestBrowser()
  globalThis.fetch = originalFetch
})

after(async () => closeWebTestServer())

test('WEB-UI-APP-001 restored account notices appear once in the app shell and can be dismissed accessibly', async () => {
  const notice = 'Your password has changed. The account you were using was signed out for security.'
  const requests = []
  window.sessionStorage.setItem(transitionNoticeKey, notice)
  globalThis.fetch = async (input) => {
    requests.push(input)
    return jsonResponse({ detail: 'The browser has no active account.' }, 401)
  }

  render(createElement(App))

  assert.equal((await screen.findByText(notice)).closest('[role="status"]').getAttribute('role'), 'status')
  assert.ok(await screen.findByRole('heading', { name: /Closer to the coast/ }))
  assert.equal(window.sessionStorage.getItem(transitionNoticeKey), null)
  assert.ok(requests.includes('/api/auth/me'))
  assert.ok(requests.includes('/api/auth/refresh'))
  assert.ok(requests.every((target) => target.startsWith('/api/')))

  await userEvent.setup().click(screen.getByRole('button', { name: 'Dismiss account update' }))
  assert.equal(screen.queryByText(notice), null)
})
