import assert from 'node:assert/strict'
import { afterEach, test } from 'node:test'

import {
  clearSignedInAccounts,
  getPreferredAccountId,
  readSignedInAccounts,
  rememberSignedInAccount,
} from '../accountStore.ts'

const previousWindow = globalThis.window

function createStorage(seed = {}) {
  const values = new Map(Object.entries(seed))
  return {
    getItem(key) { return values.get(key) ?? null },
    setItem(key, value) { values.set(key, String(value)) },
    removeItem(key) { values.delete(key) },
    values,
  }
}

afterEach(() => {
  if (previousWindow === undefined) delete globalThis.window
  else globalThis.window = previousWindow
})

test('WEB-MULTI-ACCOUNT-001 migrates minimal account summaries and caps the saved chooser at five', () => {
  const storage = createStorage({
    'blueverse.signed-in-accounts': JSON.stringify([
      { id: 'account-1', fullName: 'First User', email: 'first@example.test', token: 'never-store-this' },
    ]),
    'blueverse.active-account-id': 'account-1',
  })
  globalThis.window = { localStorage: storage }

  assert.deepEqual(readSignedInAccounts(), [
    { id: 'account-1', fullName: 'First User', email: 'first@example.test' },
  ])
  assert.equal(getPreferredAccountId(), 'account-1')
  assert.equal(storage.values.has('blueverse.signed-in-accounts'), false)
  assert.equal(storage.values.has('blueverse.active-account-id'), false)

  for (let index = 2; index <= 6; index += 1) {
    rememberSignedInAccount({
      id: `account-${index}`,
      fullName: `User ${index}`,
      email: `user${index}@example.test`,
      isActive: true,
      createdAt: '2026-01-01T00:00:00Z',
      roles: [],
      permissions: [],
      token: 'never-store-this',
    })
  }

  const accounts = readSignedInAccounts()
  assert.equal(accounts.length, 5)
  assert.deepEqual(accounts.map((account) => account.id), [
    'account-6', 'account-5', 'account-4', 'account-3', 'account-2',
  ])
  assert.equal(JSON.stringify(accounts).includes('never-store-this'), false)
  assert.equal(getPreferredAccountId(), 'account-6')

  clearSignedInAccounts()
  assert.deepEqual(readSignedInAccounts(), [])
  assert.equal(getPreferredAccountId(), null)
})
