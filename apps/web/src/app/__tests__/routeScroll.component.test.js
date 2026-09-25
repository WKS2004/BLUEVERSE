import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { Link } from 'react-router'
import {
  cleanup,
  closeWebTestServer,
  createElement,
  loadWebModule,
  renderInApp,
  resetTestBrowser,
  screen,
  userEvent,
  waitFor,
} from '../../testSupport/reactTestHarness.js'

const { default: RouteScrollManager } = await loadWebModule('/src/app/RouteScrollManager.tsx')
const startPath = '/profile'
const nextPath = '/dashboard'
const sectionPath = '/dashboard#target'
const originalScrollTo = window.scrollTo
const originalMatchMedia = window.matchMedia
const originalRequestAnimationFrame = window.requestAnimationFrame
const originalCancelAnimationFrame = window.cancelAnimationFrame

afterEach(() => {
  cleanup()
  resetTestBrowser()
  window.scrollTo = originalScrollTo
  window.matchMedia = originalMatchMedia
  window.requestAnimationFrame = originalRequestAnimationFrame
  window.cancelAnimationFrame = originalCancelAnimationFrame
})

after(async () => closeWebTestServer())

function scrollFixture() {
  return createElement('div', null,
    createElement(RouteScrollManager),
    createElement('div', { id: 'target' }),
    createElement(Link, { to: nextPath }, 'Open next route'),
    createElement(Link, { to: sectionPath }, 'Open coastal section'),
  )
}

test('WEB-UI-SCROLL-001 pathname changes without a fragment return to the top immediately', async () => {
  const calls = []
  window.scrollTo = (options) => calls.push(options)
  renderInApp(scrollFixture(), { path: startPath })

  assert.deepEqual(calls, [{ top: 0, behavior: 'instant' }])
  await userEvent.setup().click(screen.getByRole('link', { name: 'Open next route' }))
  await waitFor(() => assert.equal(screen.getByTestId('router-location').textContent, nextPath))
  assert.deepEqual(calls, [{ top: 0, behavior: 'instant' }, { top: 0, behavior: 'instant' }])
})

test('WEB-UI-SCROLL-002 hash navigation scrolls smoothly and cancels a pending frame after a second route change', async () => {
  const frames = []
  const cancelled = []
  const pending = new Set()
  let nextFrameId = 0
  window.requestAnimationFrame = (callback) => {
    const id = ++nextFrameId
    frames.push({ id, callback })
    pending.add(id)
    return id
  }
  window.cancelAnimationFrame = (id) => { cancelled.push(id); pending.delete(id) }
  const scrollIntoView = []
  renderInApp(scrollFixture(), { path: startPath })
  document.getElementById('target').scrollIntoView = (options) => scrollIntoView.push(options)

  await userEvent.setup().click(screen.getByRole('link', { name: 'Open coastal section' }))
  await waitFor(() => assert.equal(screen.getByTestId('router-location').textContent, sectionPath))
  assert.equal(frames.length, 1)
  pending.delete(frames[0].id)
  frames[0].callback()
  assert.deepEqual(scrollIntoView, [{ behavior: 'smooth' }])

  await userEvent.setup().click(screen.getByRole('link', { name: 'Open next route' }))
  await waitFor(() => assert.equal(screen.getByTestId('router-location').textContent, nextPath))
  await userEvent.setup().click(screen.getByRole('link', { name: 'Open coastal section' }))
  await waitFor(() => assert.equal(frames.length, 2))
  await userEvent.setup().click(screen.getByRole('link', { name: 'Open next route' }))
  await waitFor(() => assert.equal(screen.getByTestId('router-location').textContent, nextPath))
  assert.deepEqual(cancelled, [1, 2])
  assert.equal(scrollIntoView.length, 1)
})

test('WEB-UI-SCROLL-003 reduced-motion preference makes hash navigation instantaneous', () => {
  window.matchMedia = (media) => ({ ...originalMatchMedia(media), matches: media === '(prefers-reduced-motion: reduce)' })
  const frames = []
  window.requestAnimationFrame = (callback) => { frames.push(callback); return frames.length }
  window.cancelAnimationFrame = () => {}
  renderInApp(scrollFixture(), { path: sectionPath })
  const scrollIntoView = []
  document.getElementById('target').scrollIntoView = (options) => scrollIntoView.push(options)

  assert.equal(frames.length, 1)
  frames[0]()
  assert.deepEqual(scrollIntoView, [{ behavior: 'instant' }])
})
