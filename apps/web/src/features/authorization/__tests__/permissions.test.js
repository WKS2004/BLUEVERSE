import assert from 'node:assert/strict'
import test from 'node:test'
import { hasAllPermissions, hasAnyPermission } from '../permissions.ts'

const user = (permissions) => ({ permissions })

test('a read grant does not imply update or delete', () => {
  const reader = user(['auth.role.read'])
  assert.equal(hasAllPermissions(reader, ['auth.role.read']), true)
  assert.equal(hasAllPermissions(reader, ['auth.role.read', 'auth.role.update']), false)
  assert.equal(hasAllPermissions(reader, ['auth.role.read', 'auth.role.delete']), false)
})

test('read plus update permits editing but not deletion', () => {
  const editor = user(['auth.role.read', 'auth.role.update'])
  assert.equal(hasAllPermissions(editor, ['auth.role.read', 'auth.role.update']), true)
  assert.equal(hasAllPermissions(editor, ['auth.role.read', 'auth.role.delete']), false)
})

test('legacy manage grants remain compatible with granular actions', () => {
  assert.equal(hasAllPermissions(user(['auth.user.read', 'auth.user.manage']), ['auth.user.read', 'auth.user.delete']), true)
})

test('combined authorization requires every requested permission', () => {
  const roleEditor = user(['auth.role.read', 'auth.role.update'])
  assert.equal(hasAllPermissions(roleEditor, ['auth.role.read', 'auth.role.update', 'auth.permission.read']), false)
  assert.equal(hasAnyPermission(roleEditor, ['auth.user.read', 'auth.role.read']), true)
  assert.equal(hasAnyPermission(null, ['auth.role.read']), false)
})
