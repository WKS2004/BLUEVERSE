import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import {
  cleanup,
  closeWebTestServer,
  createElement,
  jsonResponse,
  loadWebModule,
  makeAuthSessionValue,
  makeAuthUser,
  renderInApp,
  resetTestBrowser,
  screen,
  userEvent,
  waitFor,
} from '../../../testSupport/reactTestHarness.js'

const { AdminPermissionsPage, AdminRolesPage, AdminUsersPage } = await loadWebModule('/src/pages/admin/AdminPages.tsx')
const originalFetch = globalThis.fetch

afterEach(() => {
  cleanup()
  resetTestBrowser()
  globalThis.fetch = originalFetch
})

after(async () => closeWebTestServer())

const permission = (id, code, description = `Description for ${code}`) => ({ id, code, description, createdAt: '2026-01-01T00:00:00.000Z' })
const role = (overrides = {}) => ({
  id: 'role-1',
  name: 'Visitor',
  description: 'Coastal visitor access',
  isSystemRole: false,
  createdAt: '2026-01-01T00:00:00.000Z',
  permissions: ['auth.user.read'],
  ...overrides,
})
const adminUser = (overrides = {}) => ({
  id: 'user-1',
  email: 'visitor@example.test',
  fullName: 'Coastal Visitor',
  isActive: true,
  createdAt: '2026-01-01T00:00:00.000Z',
  roles: ['Visitor'],
  permissions: ['profile.read'],
  ...overrides,
})
const fullRolePermissions = ['auth.role.read', 'auth.role.create', 'auth.role.update', 'auth.role.delete', 'auth.permission.read']
const fullUserPermissions = ['auth.user.read', 'auth.user.create', 'auth.user.update', 'auth.user.delete', 'auth.role.read', 'auth.role.system.manage']

function installFetch(routes) {
  const calls = []
  const queues = Object.fromEntries(Object.entries(routes).map(([key, value]) => [key, Array.isArray(value) ? [...value] : [value]]))
  globalThis.fetch = async (input, init = {}) => {
    const method = init.method ?? 'GET'
    const key = `${method} ${input}`
    calls.push({ input, init, method, key })
    const queue = queues[key]
    if (!queue?.length) throw new Error(`Unexpected public API request: ${key}`)
    const result = queue.length > 1 ? queue.shift() : queue[0]
    return typeof result === 'function' ? result(calls.at(-1)) : result
  }
  return calls
}

function renderAdmin(Component, permissions) {
  const currentUser = makeAuthUser({ id: 'administrator-1', roles: ['Administrator'], permissions })
  return renderInApp(createElement(Component), { path: '/admin', auth: makeAuthSessionValue({ user: currentUser, status: 'signed-in' }) })
}

test('WEB-ADMIN-UI-001 permission catalogue lists current server codes and descriptions (ui-integration: auth-permission-administration)', async () => {
  const items = [permission('p1', 'auth.user.read', 'Read user accounts'), permission('p2', 'auth.role.update', 'Update role grants')]
  const calls = installFetch({ 'GET /api/auth/permissions': jsonResponse(items) })
  renderAdmin(AdminPermissionsPage, ['auth.permission.read'])

  assert.ok(await screen.findByText('Read user accounts'))
  assert.ok(screen.getByText('auth.user.read'))
  assert.ok(screen.getByText('Update role grants'))
  assert.ok(screen.getByText('2 permissions'))
  assert.equal(calls.length, 1)
  assert.equal(calls[0].input, '/api/auth/permissions')
})

test('WEB-ADMIN-UI-002 permission catalogue dependency failure shows a safe alert (ui-integration: auth-permission-administration)', async () => {
  installFetch({ 'GET /api/auth/permissions': new Response('private upstream host', { status: 503 }) })
  renderAdmin(AdminPermissionsPage, ['auth.permission.read'])

  assert.equal((await screen.findByRole('alert')).textContent, 'We couldn’t complete that administration request. Please try again.')
  assert.doesNotMatch(document.body.textContent, /private upstream host/)
})

test('WEB-ADMIN-UI-003 role read access alone exposes data but no create, edit or assignment actions (ui-integration: auth-role-administration)', async () => {
  const roles = [role()]
  const calls = installFetch({ 'GET /api/auth/roles': jsonResponse(roles) })
  renderAdmin(AdminRolesPage, ['auth.role.read'])

  assert.ok(await screen.findByRole('heading', { name: 'Visitor' }))
  assert.equal(screen.queryByRole('button', { name: 'Create role' }), null)
  assert.equal(screen.queryByRole('button', { name: 'Save role details' }), null)
  assert.equal(screen.queryByRole('button', { name: 'Save permissions' }), null)
  assert.equal(document.getElementById('role-name').disabled, true)
  assert.ok(screen.getByText(/Permission assignment requires role read and update access/))
  assert.deepEqual(calls.map((call) => call.key), ['GET /api/auth/roles'])
})

test('WEB-ADMIN-UI-004 creating a role trims text and reports it without client-granted permissions (ui-integration: auth-role-administration)', async () => {
  const created = role({ id: 'role-2', name: 'Coastal Reviewer', description: 'Reviews field reports', permissions: [] })
  const calls = installFetch({
    'GET /api/auth/roles': [jsonResponse([role()]), jsonResponse([role(), created])],
    'GET /api/auth/permissions': jsonResponse([permission('p1', 'auth.user.read')]),
    'POST /api/auth/roles': jsonResponse(created),
  })
  renderAdmin(AdminRolesPage, fullRolePermissions)
  await screen.findByRole('button', { name: /Visitor/ })
  const interaction = userEvent.setup()
  await interaction.type(document.getElementById('new-role-name'), '  Coastal Reviewer  ')
  await interaction.type(document.getElementById('new-role-description'), '  Reviews field reports  ')
  await interaction.click(screen.getByRole('button', { name: 'Create role' }))

  assert.equal((await screen.findByText('“Coastal Reviewer” was created with no permissions assigned.')).textContent, '“Coastal Reviewer” was created with no permissions assigned.')
  assert.deepEqual(JSON.parse(calls.find((call) => call.key === 'POST /api/auth/roles').init.body), {
    name: 'Coastal Reviewer',
    description: 'Reviews field reports',
  })
  assert.ok(screen.getByRole('heading', { name: 'Coastal Reviewer' }))
})

test('WEB-ADMIN-UI-005 role details and permission grants use separate authorized actions (ui-integration: auth-role-administration)', async () => {
  const initialRole = role()
  const updatedRole = role({ name: 'Coastal Guide', description: 'Updated description' })
  const calls = installFetch({
    'GET /api/auth/roles': jsonResponse([initialRole]),
    'GET /api/auth/permissions': jsonResponse([permission('p1', 'auth.user.read'), permission('p2', 'auth.user.update')]),
    'PUT /api/auth/roles/role-1': jsonResponse(updatedRole),
    'POST /api/auth/roles/role-1/permissions': jsonResponse(role({ permissions: [] })),
  })
  renderAdmin(AdminRolesPage, fullRolePermissions)
  await screen.findByRole('button', { name: /Visitor/ })
  const interaction = userEvent.setup()
  await interaction.clear(document.getElementById('role-name'))
  await interaction.type(document.getElementById('role-name'), '  Coastal Guide  ')
  await interaction.clear(document.getElementById('role-description'))
  await interaction.type(document.getElementById('role-description'), '  Updated description  ')
  await interaction.click(screen.getByRole('button', { name: 'Save role details' }))
  assert.equal((await screen.findByText('“Coastal Guide” was updated.')).textContent, '“Coastal Guide” was updated.')

  const permissionLabel = Array.from(document.querySelectorAll('fieldset label')).find((label) => label.textContent.includes('auth.user.read'))
  assert.ok(permissionLabel)
  await interaction.click(permissionLabel.querySelector('input'))
  await interaction.click(screen.getByRole('button', { name: 'Save permissions' }))
  await waitFor(() => assert.ok(document.body.textContent.includes('Permission assignments for “Visitor” were saved.')))

  const detailCall = calls.find((call) => call.method === 'PUT')
  assert.equal(detailCall.input, '/api/auth/roles/role-1')
  assert.deepEqual(JSON.parse(detailCall.init.body), { name: 'Coastal Guide', description: 'Updated description' })
  const grantCall = calls.find((call) => call.method === 'POST')
  assert.equal(grantCall.input, '/api/auth/roles/role-1/permissions')
  assert.deepEqual(JSON.parse(grantCall.init.body), { permissionCodes: [] })
})

test('WEB-ADMIN-UI-006 system roles remain read-only and cannot be deleted (ui-integration: auth-role-administration)', async () => {
  const system = role({ id: 'system-role', name: 'Administrator', isSystemRole: true, permissions: ['auth.role.read'] })
  installFetch({
    'GET /api/auth/roles': jsonResponse([system]),
    'GET /api/auth/permissions': jsonResponse([permission('p1', 'auth.role.read')]),
  })
  renderAdmin(AdminRolesPage, fullRolePermissions)

  await screen.findByRole('heading', { name: 'Administrator' })
  assert.equal(document.getElementById('role-name').disabled, true)
  assert.equal(screen.queryByRole('button', { name: 'Save role details' }), null)
  assert.equal(screen.queryByRole('button', { name: 'Delete role' }), null)
  assert.equal(screen.queryByRole('button', { name: 'Save permissions' }), null)
  assert.ok(screen.getByText(/System role names, permissions and deletion are controlled by the service deployment/))
})

test('WEB-ADMIN-UI-007 role deletion stops when user confirmation is declined (ui-integration: auth-role-administration)', async () => {
  const calls = installFetch({
    'GET /api/auth/roles': jsonResponse([role()]),
    'GET /api/auth/permissions': jsonResponse([permission('p1', 'auth.user.read')]),
  })
  window.confirm = () => false
  renderAdmin(AdminRolesPage, fullRolePermissions)
  await screen.findByRole('heading', { name: 'Visitor' })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Delete role' }))

  assert.equal(calls.some((call) => call.method === 'DELETE'), false)
  assert.equal(screen.queryByText('“Visitor” was deleted.'), null)
})

test('WEB-ADMIN-UI-008 role deletion runs only after confirmation and refreshes the list (ui-integration: auth-role-administration)', async () => {
  const calls = installFetch({
    'GET /api/auth/roles': [jsonResponse([role()]), jsonResponse([])],
    'GET /api/auth/permissions': jsonResponse([permission('p1', 'auth.user.read')]),
    'DELETE /api/auth/roles/role-1': jsonResponse(undefined, 204),
  })
  window.confirm = () => true
  renderAdmin(AdminRolesPage, fullRolePermissions)
  await screen.findByRole('heading', { name: 'Visitor' })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Delete role' }))

  assert.equal((await screen.findByText('“Visitor” was deleted.')).textContent, '“Visitor” was deleted.')
  assert.equal(calls.find((call) => call.method === 'DELETE').input, '/api/auth/roles/role-1')
  assert.ok(screen.getByText('No roles are available.'))
})

test('WEB-ADMIN-UI-009 user read access alone does not expose create or mutation actions (ui-integration: auth-user-administration)', async () => {
  const users = [adminUser()]
  const calls = installFetch({ 'GET /api/auth/users': jsonResponse(users) })
  renderAdmin(AdminUsersPage, ['auth.user.read'])

  assert.ok(await screen.findByRole('heading', { name: 'User accounts' }))
  assert.ok(screen.getByRole('button', { name: /Coastal Visitor/ }))
  assert.equal(screen.queryByRole('button', { name: 'Create account' }), null)
  assert.equal(screen.queryByRole('button', { name: 'Save account details' }), null)
  assert.equal(screen.queryByRole('button', { name: 'Save roles' }), null)
  assert.equal(document.getElementById('user-full-name').disabled, true)
  assert.equal(document.getElementById('user-email').disabled, true)
  assert.deepEqual(calls.map((call) => call.key), ['GET /api/auth/users'])
})

test('WEB-ADMIN-UI-010 user creation trims identity values and does not assign unreadable roles (ui-integration: auth-user-administration)', async () => {
  const created = adminUser({ id: 'user-2', fullName: 'New Coastal Visitor', email: 'new@example.test', roles: [] })
  const calls = installFetch({
    'GET /api/auth/users': [jsonResponse([]), jsonResponse([created])],
    'POST /api/auth/users': jsonResponse(created),
  })
  renderAdmin(AdminUsersPage, ['auth.user.read', 'auth.user.create'])
  const interaction = userEvent.setup()
  await interaction.type(document.getElementById('new-user-name'), '  New Coastal Visitor  ')
  await interaction.type(document.getElementById('new-user-email'), 'new@example.test')
  await interaction.type(document.getElementById('new-user-password'), 'synthetic-password')
  await interaction.click(screen.getByRole('button', { name: 'Create account' }))

  assert.equal((await screen.findByText('The account for New Coastal Visitor was created.')).textContent, 'The account for New Coastal Visitor was created.')
  const createCall = calls.find((call) => call.method === 'POST')
  assert.deepEqual(JSON.parse(createCall.init.body), {
    email: 'new@example.test',
    password: 'synthetic-password',
    fullName: 'New Coastal Visitor',
    roleNames: [],
  })
})

test('WEB-ADMIN-UI-011 user updates send editable fields and optional password in one authorized action (ui-integration: auth-user-administration)', async () => {
  const initial = adminUser()
  const updated = adminUser({ fullName: 'Updated Visitor', email: 'updated@example.test', isActive: false })
  const calls = installFetch({
    'GET /api/auth/users': jsonResponse([initial]),
    'GET /api/auth/roles': jsonResponse([role()]),
    'PUT /api/auth/users/user-1': jsonResponse(updated),
  })
  renderAdmin(AdminUsersPage, fullUserPermissions)
  await screen.findByRole('heading', { name: 'Coastal Visitor' })
  const interaction = userEvent.setup()
  await interaction.clear(document.getElementById('user-full-name'))
  await interaction.type(document.getElementById('user-full-name'), ' Updated Visitor ')
  await interaction.clear(document.getElementById('user-email'))
  await interaction.type(document.getElementById('user-email'), 'updated@example.test')
  await interaction.click(screen.getByRole('checkbox', { name: 'Account is active' }))
  await interaction.type(document.getElementById('user-new-password'), 'replacement-synthetic-password')
  await interaction.click(screen.getByRole('button', { name: 'Save account details' }))

  assert.equal((await screen.findByText('The account for Updated Visitor was updated.')).textContent, 'The account for Updated Visitor was updated.')
  const updateCall = calls.find((call) => call.method === 'PUT')
  assert.equal(updateCall.input, '/api/auth/users/user-1')
  assert.deepEqual(JSON.parse(updateCall.init.body), {
    email: 'updated@example.test',
    fullName: 'Updated Visitor',
    isActive: false,
    newPassword: 'replacement-synthetic-password',
  })
})

test('WEB-ADMIN-UI-012 role changes require user read, user update and role read grants together (ui-integration: auth-user-administration)', async () => {
  const initial = adminUser({ roles: [] })
  const assigned = adminUser({ roles: ['Visitor', 'Field Reviewer'] })
  const calls = installFetch({
    'GET /api/auth/users': jsonResponse([initial]),
    'GET /api/auth/roles': jsonResponse([role(), role({ id: 'role-2', name: 'Field Reviewer', permissions: [] })]),
    'POST /api/auth/users/user-1/roles': jsonResponse(assigned),
  })
  renderAdmin(AdminUsersPage, fullUserPermissions)
  await screen.findByRole('heading', { name: 'Coastal Visitor' })
  const fieldReviewer = Array.from(document.querySelectorAll('fieldset label')).find((label) => label.textContent.includes('Field Reviewer'))
  assert.ok(fieldReviewer)
  await userEvent.setup().click(fieldReviewer.querySelector('input'))
  await userEvent.setup().click(screen.getByRole('button', { name: 'Save roles' }))

  assert.equal((await screen.findByText('Role assignments for Coastal Visitor were saved.')).textContent, 'Role assignments for Coastal Visitor were saved.')
  const assignment = calls.find((call) => call.method === 'POST')
  assert.equal(assignment.input, '/api/auth/users/user-1/roles')
  assert.deepEqual(JSON.parse(assignment.init.body), { roleNames: ['Field Reviewer'] })
})

test('WEB-ADMIN-UI-013 system-role assignments remain protected without system-role management (ui-integration: auth-user-administration)', async () => {
  const systemRole = role({ id: 'system-role', name: 'Administrator', isSystemRole: true })
  const selected = adminUser({ roles: ['Administrator'] })
  installFetch({
    'GET /api/auth/users': jsonResponse([selected]),
    'GET /api/auth/roles': jsonResponse([systemRole, role()]),
  })
  renderAdmin(AdminUsersPage, ['auth.user.read', 'auth.user.update', 'auth.user.delete', 'auth.role.read'])

  await screen.findByRole('heading', { name: 'Coastal Visitor' })
  assert.equal(screen.getByRole('button', { name: 'Save roles' }).disabled, true)
  assert.ok(screen.getByRole('button', { name: 'Delete account' }))
  assert.equal(document.querySelector('fieldset').disabled, true)
  assert.ok(screen.getByText(/System-role assignments are protected/))
  assert.ok(Array.from(document.querySelectorAll('select option')).every((option) => option.value !== 'Administrator'))
})

test('WEB-ADMIN-UI-014 deleting the current administrator account is never offered (ui-integration: auth-user-administration)', async () => {
  const current = adminUser({ id: 'administrator-1', email: 'admin@example.test', fullName: 'Current Administrator' })
  installFetch({
    'GET /api/auth/users': jsonResponse([current]),
    'GET /api/auth/roles': jsonResponse([role()]),
  })
  renderAdmin(AdminUsersPage, fullUserPermissions)

  await screen.findByRole('heading', { name: 'Current Administrator' })
  assert.equal(screen.queryByRole('button', { name: 'Delete account' }), null)
  assert.equal(document.getElementById('user-email').disabled, true)
  assert.ok(screen.getByText('You can’t change your own email through this screen.'))
})

test('WEB-ADMIN-UI-015 account deletion respects confirmation and refreshes the list (ui-integration: auth-user-administration)', async () => {
  const target = adminUser()
  const calls = installFetch({
    'GET /api/auth/users': [jsonResponse([target]), jsonResponse([])],
    'GET /api/auth/roles': jsonResponse([role()]),
    'DELETE /api/auth/users/user-1': jsonResponse(undefined, 204),
  })
  renderAdmin(AdminUsersPage, fullUserPermissions)
  await screen.findByRole('heading', { name: 'Coastal Visitor' })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Delete account' }))
  assert.equal(calls.some((call) => call.method === 'DELETE'), false)

  window.confirm = () => true
  await userEvent.setup().click(screen.getByRole('button', { name: 'Delete account' }))

  assert.equal((await screen.findByText('The account for Coastal Visitor was deleted.')).textContent, 'The account for Coastal Visitor was deleted.')
  assert.equal(calls.find((call) => call.method === 'DELETE').input, '/api/auth/users/user-1')
  assert.ok(screen.getByText('No accounts are available.'))
})

test('WEB-ADMIN-UI-016 denied mutation reports the API message and preserves the selected record (ui-integration: auth-role-administration)', async () => {
  installFetch({
    'GET /api/auth/roles': jsonResponse([role()]),
    'GET /api/auth/permissions': jsonResponse([permission('p1', 'auth.user.read')]),
    'PUT /api/auth/roles/role-1': jsonResponse({ detail: 'The role update grant is required.' }, 403),
  })
  renderAdmin(AdminRolesPage, fullRolePermissions)
  await screen.findByRole('heading', { name: 'Visitor' })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Save role details' }))

  assert.equal((await screen.findByRole('alert')).textContent, 'The role update grant is required.')
  assert.ok(screen.getByRole('heading', { name: 'Visitor' }))
})
