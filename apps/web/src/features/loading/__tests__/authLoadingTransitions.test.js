import assert from 'node:assert/strict'
import { test } from 'node:test'

import {
  AUTH_LOADING_CONTEXTS,
  authTransitionMinimumVisibleMs,
  consumeAuthLoadingContextAfterReload,
  getLoadingScreenContextSnapshot,
  getLoadingScreenSnapshot,
  preserveAuthLoadingContextForReload,
  withLoadingScreen,
} from '../backendLoading.ts'

function wait(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds))
}

test('WEB-AUTH-LOADING-001 keeps a quick authentication transition visible long enough to notice', async () => {
  const request = withLoadingScreen(async () => 'signed in', AUTH_LOADING_CONTEXTS.signIn)

  assert.equal(getLoadingScreenSnapshot(), true)
  assert.equal(getLoadingScreenContextSnapshot(), AUTH_LOADING_CONTEXTS.signIn)
  assert.equal(await request, 'signed in')

  await wait(250)
  assert.equal(getLoadingScreenSnapshot(), true)
  await wait(authTransitionMinimumVisibleMs)
  assert.equal(getLoadingScreenSnapshot(), false)
})

test('WEB-AUTH-LOADING-002 preserves one validated auth context for the next document load', () => {
  const values = new Map()
  const originalWindow = Object.getOwnPropertyDescriptor(globalThis, 'window')
  Object.defineProperty(globalThis, 'window', {
    configurable: true,
    value: {
      sessionStorage: {
        getItem: (key) => values.get(key) ?? null,
        removeItem: (key) => values.delete(key),
        setItem: (key, value) => values.set(key, value),
      },
    },
  })

  try {
    preserveAuthLoadingContextForReload('registration')
    assert.equal(consumeAuthLoadingContextAfterReload(), AUTH_LOADING_CONTEXTS.registration)
    assert.equal(values.size, 0)

    values.set('blueverse.pending-auth-transition', 'untrusted|invalid')
    assert.equal(consumeAuthLoadingContextAfterReload(), null)
    assert.equal(values.size, 0)
  } finally {
    if (originalWindow) Object.defineProperty(globalThis, 'window', originalWindow)
    else delete globalThis.window
  }
})
