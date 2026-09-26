import assert from 'node:assert/strict'
import { test } from 'node:test'

import {
  AUTH_LOADING_CONTEXTS,
  COASTAL_LOADING_CONTEXT,
  authTransitionMinimumVisibleMs,
  beginLoadingScreen,
  getLoadingScreenContextSnapshot,
  getLoadingScreenSnapshot,
  shouldWashLoadingScreenAway,
  withLoadingScreen,
} from '../backendLoading.ts'

function deferred() {
  let resolve
  let reject
  const promise = new Promise((resolvePromise, rejectPromise) => {
    resolve = resolvePromise
    reject = rejectPromise
  })
  return { promise, resolve, reject }
}

function wait(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds))
}

test('WEB-LOADING-001 overlapping work shares one loading screen until all work settles', async () => {
  const firstRequest = deferred()
  const secondRequest = deferred()
  const first = withLoadingScreen(() => firstRequest.promise)
  const second = withLoadingScreen(() => secondRequest.promise)

  assert.equal(getLoadingScreenSnapshot(), true)

  firstRequest.resolve('first')
  assert.equal(await first, 'first')
  assert.equal(getLoadingScreenSnapshot(), true)

  secondRequest.resolve('second')
  assert.equal(await second, 'second')
  await wait(190)
  assert.equal(getLoadingScreenSnapshot(), false)
})

test('WEB-LOADING-002 immediate and slow failures both clear the loading screen', async () => {
  const quickFailure = withLoadingScreen(async () => { throw new Error('network unavailable') })
  assert.equal(getLoadingScreenSnapshot(), true)
  await assert.rejects(
    quickFailure,
    /network unavailable/,
  )
  assert.equal(getLoadingScreenSnapshot(), true)

  await wait(190)
  assert.equal(getLoadingScreenSnapshot(), false)

  const slowFailure = deferred()
  const failedRequest = withLoadingScreen(() => slowFailure.promise)
  assert.equal(getLoadingScreenSnapshot(), true)
  slowFailure.reject(new Error('service timed out'))
  await assert.rejects(failedRequest, /service timed out/)
  await wait(190)
  assert.equal(getLoadingScreenSnapshot(), false)
})

test('WEB-LOADING-003 auth work retains its transition above unrelated background work', async () => {
  const coastalRequest = deferred()
  const authRequest = deferred()
  const coastalWork = withLoadingScreen(() => coastalRequest.promise, COASTAL_LOADING_CONTEXT)
  const authWork = withLoadingScreen(() => authRequest.promise, AUTH_LOADING_CONTEXTS.signIn)

  assert.equal(getLoadingScreenContextSnapshot(), AUTH_LOADING_CONTEXTS.signIn)

  authRequest.resolve('signed in')
  await authWork
  assert.equal(getLoadingScreenContextSnapshot(), AUTH_LOADING_CONTEXTS.signIn)

  coastalRequest.resolve('loaded')
  await coastalWork
  await wait(190)
  assert.equal(getLoadingScreenSnapshot(), true)
  await wait(authTransitionMinimumVisibleMs + 100)
  assert.equal(getLoadingScreenSnapshot(), false)
})

test('WEB-LOADING-004 slow-load wave exit starts only after the two-second threshold', () => {
  assert.equal(shouldWashLoadingScreenAway(1999), false)
  assert.equal(shouldWashLoadingScreenAway(2000), true)
  assert.equal(shouldWashLoadingScreenAway(Number.NaN), false)
  assert.equal(shouldWashLoadingScreenAway(Number.POSITIVE_INFINITY), false)
})

test('WEB-AUTH-LOADING-003 keeps one authentication message through restore and initial page data', async () => {
  const finishTransition = beginLoadingScreen(AUTH_LOADING_CONTEXTS.registration)
  const finishRestore = beginLoadingScreen(AUTH_LOADING_CONTEXTS.sessionRestore)
  const finishActivation = beginLoadingScreen(AUTH_LOADING_CONTEXTS.switchAccount)

  assert.equal(getLoadingScreenContextSnapshot(), AUTH_LOADING_CONTEXTS.registration)

  finishTransition()
  assert.equal(getLoadingScreenContextSnapshot(), AUTH_LOADING_CONTEXTS.registration)

  finishActivation()
  assert.equal(getLoadingScreenContextSnapshot(), AUTH_LOADING_CONTEXTS.registration)

  finishRestore()
  const finishInitialPageData = beginLoadingScreen(AUTH_LOADING_CONTEXTS.sessions)
  assert.equal(getLoadingScreenSnapshot(), true)
  assert.equal(getLoadingScreenContextSnapshot(), AUTH_LOADING_CONTEXTS.registration)

  finishInitialPageData()
  await wait(authTransitionMinimumVisibleMs + 100)
  assert.equal(getLoadingScreenSnapshot(), false)
})
