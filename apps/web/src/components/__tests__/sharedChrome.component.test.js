import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import {
  act,
  cleanup,
  closeWebTestServer,
  createElement,
  fireEvent,
  loadWebModule,
  makeAuthSessionValue,
  makeAuthUser,
  renderInApp,
  resetTestBrowser,
  screen,
  userEvent,
  waitFor,
} from '../../testSupport/reactTestHarness.js'

const [{ default: BackendLoadingScreen }, { default: SiteFooter }, loading] = await Promise.all([
  loadWebModule('/src/components/feedback/BackendLoadingScreen.tsx'),
  loadWebModule('/src/components/layout/SiteFooter.tsx'),
  loadWebModule('/src/features/loading/backendLoading.ts'),
])
const originalMatchMedia = window.matchMedia
const originalScrollTo = window.scrollTo
const originalScrollY = Object.getOwnPropertyDescriptor(window, 'scrollY')
const originalInnerHeight = Object.getOwnPropertyDescriptor(window, 'innerHeight')
const originalScrollHeight = Object.getOwnPropertyDescriptor(document.documentElement, 'scrollHeight')

function restoreProperty(target, name, descriptor) {
  if (descriptor) Object.defineProperty(target, name, descriptor)
  else Reflect.deleteProperty(target, name)
}

afterEach(() => {
  cleanup()
  resetTestBrowser()
  window.matchMedia = originalMatchMedia
  window.scrollTo = originalScrollTo
  restoreProperty(window, 'scrollY', originalScrollY)
  restoreProperty(window, 'innerHeight', originalInnerHeight)
  restoreProperty(document.documentElement, 'scrollHeight', originalScrollHeight)
})

after(async () => closeWebTestServer())

function pause(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds))
}

test('WEB-UI-LOADING-001 shared loading screen announces the active account transition and settles accessibly', async () => {
  const finish = loading.beginLoadingScreen(loading.AUTH_LOADING_CONTEXTS.signIn)
  renderInApp(createElement(BackendLoadingScreen))

  const status = screen.getByRole('status', { name: 'Signing you in. Verifying your details and opening your account.' })
  assert.equal(status.getAttribute('aria-live'), 'polite')
  assert.equal(status.getAttribute('data-loading-mode'), 'authentication')
  assert.ok(screen.getByText('SECURE ACCOUNT TRANSITION'))
  assert.ok(screen.getByText('Signing you in'))

  finish()
  await act(async () => pause(1000))
  assert.equal(document.querySelector('[role="status"]'), null)
})

test('WEB-UI-LOADING-002 failed background work clears the shared screen without exposing its exception', async () => {
  const operation = loading.withLoadingScreen(async () => { throw new Error('private upstream detail') }, loading.COASTAL_LOADING_CONTEXT)
  renderInApp(createElement(BackendLoadingScreen))

  assert.ok(screen.getByRole('status', { name: 'A moment by the water. Loading BLUEVERSE' }))
  await act(async () => {
    await assert.rejects(operation, /private upstream detail/)
    await pause(190)
  })

  assert.equal(document.querySelector('[role="status"]'), null)
  assert.equal(screen.queryByText('private upstream detail'), null)
})

test('WEB-UI-FOOTER-001 guest account links preserve the current URL while coastal links return to the home sections', () => {
  renderInApp(createElement(SiteFooter), { path: '/dashboard?view=shore#map' })

  const accountNav = screen.getByRole('navigation', { name: 'Your BLUEVERSE account' })
  assert.equal(accountNav.querySelector('a[href="/signin?returnTo=%2Fdashboard%3Fview%3Dshore%23map"]').textContent, 'Sign in')
  assert.equal(accountNav.querySelector('a[href="/signup"]').textContent, 'Create account')
  assert.equal(screen.getByRole('navigation', { name: 'Explore BLUEVERSE' }).querySelector('a').getAttribute('href'), '/#our-story')
  assert.equal(screen.getByRole('navigation', { name: 'Our coastal focus' }).querySelector('a').getAttribute('href'), '/#what-matters')
})

test('WEB-UI-FOOTER-002 signed-in and compact footer states expose only the account links and footer content they support', () => {
  const user = makeAuthUser()
  const auth = makeAuthSessionValue({ user, status: 'signed-in' })
  renderInApp(createElement(SiteFooter), {
    path: '/profile',
    auth,
  })

  const accountNav = screen.getByRole('navigation', { name: 'Your BLUEVERSE account' })
  assert.equal(accountNav.querySelector('a[href="/profile"]').textContent, 'Profile')
  assert.equal(accountNav.querySelector('a[href="/dashboard"]').textContent, 'Dashboard')
  assert.equal(accountNav.querySelector('a[href^="/signin"]'), null)

  cleanup()
  renderInApp(createElement(SiteFooter, { compact: true, hideForShortScreens: true }), { path: '/profile', auth })
  const footer = document.querySelector('footer')
  assert.match(footer.textContent, /BLUEVERSE · Made with care for the coast\./)
  assert.match(footer.className, /\[@media\(max-height:700px\)\]:hidden/)
})

test('WEB-UI-FOOTER-003 back-to-top tracks the document boundary and follows reduced-motion preferences', async () => {
  Object.defineProperties(window, {
    scrollY: { configurable: true, writable: true, value: 0 },
    innerHeight: { configurable: true, value: 600 },
  })
  Object.defineProperty(document.documentElement, 'scrollHeight', { configurable: true, value: 1000 })
  const scrollCalls = []
  window.scrollTo = (options) => scrollCalls.push(options)
  renderInApp(createElement(SiteFooter))

  const button = document.querySelector('button[aria-label="Back to top"]')
  assert.equal(button.getAttribute('aria-hidden'), 'true')
  assert.equal(button.tabIndex, -1)

  window.scrollY = 100
  fireEvent.scroll(window)
  await waitFor(() => assert.equal(button.getAttribute('aria-hidden'), 'false'))
  await userEvent.setup().click(screen.getByRole('button', { name: 'Back to top' }))
  assert.deepEqual(scrollCalls, [{ top: 0, behavior: 'smooth' }])

  window.matchMedia = (media) => ({ ...originalMatchMedia(media), matches: media === '(prefers-reduced-motion: reduce)' })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Back to top' }))
  assert.deepEqual(scrollCalls.at(-1), { top: 0, behavior: 'instant' })

  window.scrollY = 0
  fireEvent.scroll(window)
  await waitFor(() => assert.equal(button.getAttribute('aria-hidden'), 'true'))
  assert.equal(button.tabIndex, -1)
})
