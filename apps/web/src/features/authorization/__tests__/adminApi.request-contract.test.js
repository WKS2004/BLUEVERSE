import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import {
  AdminApiError,
  createAdminRole,
  createAdminUser,
  deleteAdminRole,
  deleteAdminUser,
  getAdminPermissions,
  getAdminRoles,
  getAdminUsers,
  setRolePermissions,
  setUserRoles,
  updateAdminRole,
  updateAdminUser,
} from '../adminApi.ts'
import { closeWebTestServer, jsonResponse } from '../../../testSupport/reactTestHarness.js'

const originalFetch = globalThis.fetch

afterEach(() => {
  globalThis.fetch = originalFetch
})

after(async () => closeWebTestServer())

function captureFetch(response = jsonResponse({ id: 'fixture-id' })) {
  let captured
  globalThis.fetch = async (input, init = {}) => {
    captured = { input, init }
    return response
  }
  return () => captured
}

function assertJsonRequest(request, { path, method = 'GET', body }) {
  assert.equal(request.input, path)
  assert.equal(request.init.method, method)
  assert.equal(request.init.credentials, 'include')
  assert.deepEqual(request.init.headers, {
    Accept: 'application/json',
    ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
  })
  assert.equal(request.init.body, body === undefined ? undefined : JSON.stringify(body))
}

test('WEB-ADMIN-API-001 permission catalogue reads from the registered public Auth API', async () => {
  const permissions = [{ id: 'permission-1', code: 'auth.user.read', description: 'Read users' }]
  const getRequest = captureFetch(jsonResponse(permissions))

  assert.deepEqual(await getAdminPermissions(), permissions)
  assertJsonRequest(getRequest(), { path: '/api/auth/permissions' })
})

test('WEB-ADMIN-API-002 role listing reads from the registered public Auth API', async () => {
  const roles = [{ id: 'role-1', name: 'Coastal Reviewer', permissions: ['auth.user.read'] }]
  const getRequest = captureFetch(jsonResponse(roles))

  assert.deepEqual(await getAdminRoles(), roles)
  assertJsonRequest(getRequest(), { path: '/api/auth/roles' })
})

test('WEB-ADMIN-API-003 role creation serializes name and description without client-assigned grants', async () => {
  const role = { id: 'role-new', name: 'Coastal Reviewer', description: 'Reviews coastal records' }
  const getRequest = captureFetch(jsonResponse(role))

  assert.deepEqual(await createAdminRole(role.name, role.description), role)
  assertJsonRequest(getRequest(), { path: '/api/auth/roles', method: 'POST', body: { name: role.name, description: role.description } })
})

test('WEB-ADMIN-API-004 role update encodes identifiers and sends only editable details', async () => {
  const role = { id: 'role-1', name: 'Field Reviewer', description: 'Updated details' }
  const getRequest = captureFetch(jsonResponse(role))

  assert.deepEqual(await updateAdminRole('role/1', role.name, role.description), role)
  assertJsonRequest(getRequest(), { path: '/api/auth/roles/role%2F1', method: 'PUT', body: { name: role.name, description: role.description } })
})

test('WEB-ADMIN-API-005 permission assignment sends the permission code list to the role action route', async () => {
  const role = { id: 'role-1', name: 'Field Reviewer', permissions: ['auth.user.read', 'auth.user.update'] }
  const permissionCodes = ['auth.user.read', 'auth.user.update']
  const getRequest = captureFetch(jsonResponse(role))

  assert.deepEqual(await setRolePermissions('role-1', permissionCodes), role)
  assertJsonRequest(getRequest(), { path: '/api/auth/roles/role-1/permissions', method: 'POST', body: { permissionCodes } })
})

test('WEB-ADMIN-API-006 role deletion accepts only the API’s empty 204 success', async () => {
  const getRequest = captureFetch(jsonResponse(undefined, 204))

  await deleteAdminRole('role-1')
  assertJsonRequest(getRequest(), { path: '/api/auth/roles/role-1', method: 'DELETE' })
})

test('WEB-ADMIN-API-007 user listing reads from the registered public Auth API', async () => {
  const users = [{ id: 'user-1', email: 'visitor@example.test', roles: ['Visitor'] }]
  const getRequest = captureFetch(jsonResponse(users))

  assert.deepEqual(await getAdminUsers(), users)
  assertJsonRequest(getRequest(), { path: '/api/auth/users' })
})

test('WEB-ADMIN-API-008 user creation sends declared identity, synthetic credential and role fields', async () => {
  const input = { email: 'visitor@example.test', password: 'synthetic-password', fullName: 'Coastal Visitor', roleNames: ['Visitor'] }
  const user = { id: 'user-new', email: input.email, fullName: input.fullName, roles: input.roleNames }
  const getRequest = captureFetch(jsonResponse(user))

  assert.deepEqual(await createAdminUser(input), user)
  assertJsonRequest(getRequest(), { path: '/api/auth/users', method: 'POST', body: input })
})

test('WEB-ADMIN-API-009 user update encodes identifiers and preserves optional password omission', async () => {
  const input = { email: 'visitor@example.test', fullName: 'Coastal Visitor', isActive: false }
  const user = { id: 'user-1', ...input }
  const getRequest = captureFetch(jsonResponse(user))

  assert.deepEqual(await updateAdminUser('user/1', input), user)
  assertJsonRequest(getRequest(), { path: '/api/auth/users/user%2F1', method: 'PUT', body: input })
})

test('WEB-ADMIN-API-010 user update includes a replacement password only when supplied', async () => {
  const input = { email: 'visitor@example.test', fullName: 'Coastal Visitor', isActive: true, newPassword: 'replacement-synthetic-password' }
  const getRequest = captureFetch(jsonResponse({ id: 'user-1', ...input }))

  await updateAdminUser('user-1', input)
  assertJsonRequest(getRequest(), { path: '/api/auth/users/user-1', method: 'PUT', body: input })
})

test('WEB-ADMIN-API-011 role assignment sends role names to the user action route', async () => {
  const roleNames = ['Coastal Reviewer', 'Administrator']
  const user = { id: 'user-1', roles: roleNames }
  const getRequest = captureFetch(jsonResponse(user))

  assert.deepEqual(await setUserRoles('user-1', roleNames), user)
  assertJsonRequest(getRequest(), { path: '/api/auth/users/user-1/roles', method: 'POST', body: { roleNames } })
})

test('WEB-ADMIN-API-012 user deletion accepts only the API’s empty 204 success', async () => {
  const getRequest = captureFetch(jsonResponse(undefined, 204))

  await deleteAdminUser('user-1')
  assertJsonRequest(getRequest(), { path: '/api/auth/users/user-1', method: 'DELETE' })
})

test('WEB-ADMIN-API-013 a structured permission denial preserves status and server detail', async () => {
  globalThis.fetch = async () => jsonResponse({ status: 403, detail: 'The role editor grant is required.' }, 403)

  await assert.rejects(() => createAdminRole('New role', 'Synthetic description'), (error) => {
    assert.ok(error instanceof AdminApiError)
    assert.equal(error.status, 403)
    assert.equal(error.message, 'The role editor grant is required.')
    return true
  })
})

test('WEB-ADMIN-API-014 an unstructured permission denial uses a safe explanation', async () => {
  globalThis.fetch = async () => new Response('Forbidden', { status: 403 })

  await assert.rejects(() => getAdminUsers(), (error) => {
    assert.ok(error instanceof AdminApiError)
    assert.equal(error.status, 403)
    assert.equal(error.message, 'Your current roles do not allow this action.')
    return true
  })
})

test('WEB-ADMIN-API-015 non-403 failures use a safe fallback and retain the HTTP status', async () => {
  globalThis.fetch = async () => new Response('unavailable', { status: 503 })

  await assert.rejects(() => getAdminRoles(), (error) => {
    assert.ok(error instanceof AdminApiError)
    assert.equal(error.status, 503)
    assert.equal(error.message, 'We couldn’t complete that administration request. Please try again.')
    assert.doesNotMatch(error.message, /unavailable/)
    return true
  })
})

test('WEB-ADMIN-API-016 malformed successful responses fail with a safe read error', async () => {
  globalThis.fetch = async () => new Response('{not-json', { status: 200 })

  await assert.rejects(() => getAdminPermissions(), (error) => {
    assert.ok(error instanceof AdminApiError)
    assert.equal(error.status, 200)
    assert.equal(error.message, 'The server returned a response that could not be read.')
    return true
  })
})

test('WEB-ADMIN-API-017 an empty successful response is invalid except for explicit 204', async () => {
  globalThis.fetch = async () => new Response(null, { status: 200 })
  await assert.rejects(() => getAdminUsers(), (error) => error instanceof AdminApiError && error.status === 200)

  globalThis.fetch = async () => new Response(null, { status: 204 })
  await assert.doesNotReject(() => deleteAdminUser('user-1'))
})

test('WEB-ADMIN-API-018 transport failures reject unchanged for caller retry and recovery handling', async () => {
  const failure = new TypeError('offline fixture')
  globalThis.fetch = async () => { throw failure }

  await assert.rejects(() => getAdminRoles(), (error) => error === failure)
})
