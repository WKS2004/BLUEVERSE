import assert from 'node:assert/strict'
import test from 'node:test'

import { authEntryHref, authEntryHrefFor, safePostAuthDestination } from '../authNavigation.ts'

test('AUTH-NAV-001: restores a safe same-origin path, search, and hash after sign-in', () => {
  const destination = '/profile?section=security#sessions'
  const search = `?returnTo=${encodeURIComponent(destination)}`

  assert.equal(safePostAuthDestination(search), destination)
})

test('AUTH-NAV-002: rejects external, protocol-relative, and authentication return destinations', () => {
  for (const destination of [
    'https://outside.example',
    '//outside.example/path',
    '/signin?returnTo=%2Fprofile',
    '/signup',
  ]) {
    assert.equal(safePostAuthDestination(`?returnTo=${encodeURIComponent(destination)}`), '/')
  }
})

test('AUTH-NAV-003: builds React Router auth links that retain the current destination', () => {
  assert.equal(
    authEntryHref('/signin', { pathname: '/dashboard', search: '?view=coast', hash: '#overview' }),
    '/signin?returnTo=%2Fdashboard%3Fview%3Dcoast%23overview',
  )
  assert.equal(authEntryHref('/signup', { pathname: '/dashboard', search: '?view=coast', hash: '#overview' }), '/signup')
  assert.equal(authEntryHrefFor('/signup', '/profile#sessions'), '/signup')
})
