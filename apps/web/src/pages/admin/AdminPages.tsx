import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import type { ReactNode } from 'react'
import { Link, Navigate } from 'react-router'
import AccountAreaNavigation from '../../components/account/AccountAreaNavigation'
import SiteFooter from '../../components/layout/SiteFooter'
import SiteHeader from '../../components/layout/SiteHeader'
import { AdminApiError, createAdminRole, createAdminUser, deleteAdminRole, deleteAdminUser, getAdminPermissions, getAdminRoles, getAdminUsers, setRolePermissions, setUserRoles, updateAdminRole, updateAdminUser } from '../../features/authorization/adminApi'
import type { AdminPermission, AdminRole, AdminUser } from '../../features/authorization/adminApi'
import { hasAllPermissions, hasAnyPermission } from '../../features/authorization/permissions'
import { useAuthSession } from '../../features/auth/authSession'

const cardClass = 'min-w-0 rounded-3xl border border-coast-line bg-white p-5 shadow-[0_12px_35px_rgba(24,57,76,0.06)] sm:p-7'
const inputClass = 'mt-2 min-h-11 w-full min-w-0 rounded-2xl border border-coast-line bg-white px-4 py-2.5 text-sm text-coast-ink outline-none transition focus:border-coast-blue focus:ring-2 focus:ring-coast-glass'
const primaryButton = 'inline-flex min-h-10 items-center justify-center rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white transition hover:-translate-y-0.5 hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:cursor-not-allowed disabled:opacity-50'
const secondaryButton = 'inline-flex min-h-10 items-center justify-center rounded-full border border-coast-line bg-white px-4 text-sm font-bold text-coast-deep transition hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:cursor-not-allowed disabled:opacity-50'
const dangerButton = 'inline-flex min-h-10 items-center justify-center rounded-full border border-red-200 bg-red-50 px-4 text-sm font-bold text-red-800 transition hover:bg-red-100 focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-red-700 disabled:cursor-not-allowed disabled:opacity-50'

function PageFrame({ title, intro, children }: { title: string; intro: string; children: ReactNode }) {
  return <div className="flex min-h-screen flex-col bg-coast-paper font-sans text-coast-ink">
    <SiteHeader />
    <main className="mx-auto grid w-full max-w-[90rem] flex-1 grid-cols-1 content-start gap-6 px-4 py-6 sm:px-8 sm:py-10 lg:grid-cols-[15rem_minmax(0,1fr)] lg:items-start lg:gap-8 lg:px-6 lg:py-0">
      <AccountAreaNavigation active="admin" />
      <div className="min-w-0 lg:py-12">
        <header className="mb-7 sm:mb-9">
          <p className="text-[11px] font-extrabold tracking-[0.18em] text-coast-blue">BLUEVERSE ADMINISTRATION</p>
          <h1 className="mt-2 break-words font-display text-4xl leading-tight tracking-[-0.05em] sm:text-5xl">{title}</h1>
          <p className="mt-3 max-w-3xl text-sm leading-6 text-coast-muted sm:text-base">{intro}</p>
        </header>
        {children}
      </div>
    </main>
    <SiteFooter />
  </div>
}

function ErrorMessage({ children }: { children: string }) {
  return <p className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm leading-6 text-red-900" role="alert">{children}</p>
}

function SuccessMessage({ children }: { children: string }) {
  return <p className="rounded-2xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm leading-6 text-emerald-900" role="status">{children}</p>
}

function getError(error: unknown) {
  return error instanceof AdminApiError ? error.message : 'We couldn’t reach administration just now. Please try again.'
}

export function AdminIndexPage() {
  const { user } = useAuthSession()
  if (hasAllPermissions(user, ['auth.permission.read'])) return <Navigate replace to="/admin/permissions" />
  if (hasAllPermissions(user, ['auth.role.read'])) return <Navigate replace to="/admin/roles" />
  if (hasAllPermissions(user, ['auth.user.read'])) return <Navigate replace to="/admin/users" />
  return <AdminAccessDenied />
}

export function AdminAccessDenied() {
  return <PageFrame intro="Administration pages are available to accounts whose current roles grant the required access." title="You don’t have access">
    <section className={cardClass}>
      <p className="text-sm leading-6 text-coast-muted">Ask an administrator to review your role assignments if you need access to these tools.</p>
      <Link className={`${primaryButton} mt-5`} to="/profile">Return to your profile</Link>
    </section>
  </PageFrame>
}

export function AdminPermissionsPage() {
  const [permissions, setPermissions] = useState<AdminPermission[]>([])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let active = true
    void getAdminPermissions().then((items) => {
      if (active) setPermissions(items)
    }).catch((reason: unknown) => {
      if (active) setError(getError(reason))
    }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])

  return <PageFrame intro="Review the application’s permission catalogue. Permission access is granted through roles and each management action checks the account’s current assignments on the server." title="Permissions">
    <div className="grid gap-5">
      {error && <ErrorMessage>{error}</ErrorMessage>}
      <section className={cardClass}>
        <div className="flex flex-wrap items-end justify-between gap-4 border-b border-coast-line pb-5">
          <div><p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">ACCESS CATALOGUE</p><h2 className="mt-2 font-display text-2xl tracking-[-0.035em]">Available permissions</h2></div>
          <span className="rounded-full bg-coast-sage px-3 py-1.5 text-xs font-extrabold text-coast-deep">{permissions.length} permissions</span>
        </div>
        {loading ? null
          : <ul className="divide-y divide-coast-line">
            {permissions.map((permission) => <li className="grid min-w-0 gap-1 py-4 sm:grid-cols-[minmax(12rem,0.8fr)_minmax(0,1.2fr)] sm:gap-5" key={permission.id}>
              <code className="break-all text-sm font-bold text-coast-deep">{permission.code}</code><span className="text-sm leading-6 text-coast-muted">{permission.description}</span>
            </li>)}
          </ul>}
        <div className="mt-5 rounded-2xl bg-coast-sage/70 p-4 text-sm leading-6 text-coast-deep">
          Permission codes are application-defined. To grant or remove a permission, update a non-system role’s assignments on the <Link className="font-extrabold underline underline-offset-4" to="/admin/roles">Roles page</Link>.
        </div>
      </section>
    </div>
  </PageFrame>
}

export function AdminRolesPage() {
  const { user } = useAuthSession()
  const canCreate = hasAllPermissions(user, ['auth.role.read', 'auth.role.create'])
  const canUpdate = hasAllPermissions(user, ['auth.role.read', 'auth.role.update'])
  const canDelete = hasAllPermissions(user, ['auth.role.read', 'auth.role.delete'])
  const canAssignPermissions = hasAllPermissions(user, ['auth.role.read', 'auth.role.update', 'auth.permission.read'])
  const [roles, setRoles] = useState<AdminRole[]>([])
  const [permissions, setPermissions] = useState<AdminPermission[]>([])
  const [selectedId, setSelectedId] = useState('')
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [selectedCodes, setSelectedCodes] = useState<string[]>([])
  const [newName, setNewName] = useState('')
  const [newDescription, setNewDescription] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [editorRoleId, setEditorRoleId] = useState('')
  const selected = roles.find((role) => role.id === selectedId) ?? null

  if (selected && editorRoleId !== selected.id) {
    setEditorRoleId(selected.id)
    setName(selected.name)
    setDescription(selected.description)
    setSelectedCodes(selected.permissions)
  }

  const loadRoles = useCallback(async () => {
    const items = await getAdminRoles()
    setRoles(items)
    setSelectedId((current) => items.some((role) => role.id === current) ? current : items[0]?.id ?? '')
  }, [setRoles, setSelectedId])

  useEffect(() => {
    let active = true
    void Promise.all([getAdminRoles(), canAssignPermissions ? getAdminPermissions() : Promise.resolve([])]).then(([roleItems, permissionItems]) => {
      if (!active) return
      setRoles(roleItems)
      setSelectedId(roleItems[0]?.id ?? '')
      setPermissions(permissionItems)
    }).catch((reason: unknown) => {
      if (active) setError(getError(reason))
    }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [canAssignPermissions])

  async function createRole(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setBusy(true); setError(null); setNotice(null)
    try {
      const created = await createAdminRole(newName.trim(), newDescription.trim())
      setNewName(''); setNewDescription('')
      await loadRoles()
      setSelectedId(created.id)
      setNotice(`“${created.name}” was created with no permissions assigned.`)
    } catch (reason) { setError(getError(reason)) } finally { setBusy(false) }
  }

  async function saveRole(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selected || selected.isSystemRole || !canUpdate) return
    setBusy(true); setError(null); setNotice(null)
    try {
      const updated = await updateAdminRole(selected.id, name.trim(), description.trim())
      setRoles((items) => items.map((role) => role.id === updated.id ? updated : role))
      setNotice(`“${updated.name}” was updated.`)
    } catch (reason) { setError(getError(reason)) } finally { setBusy(false) }
  }

  async function savePermissions() {
    if (!selected || selected.isSystemRole || !canAssignPermissions) return
    setBusy(true); setError(null); setNotice(null)
    try {
      const updated = await setRolePermissions(selected.id, selectedCodes)
      setRoles((items) => items.map((role) => role.id === updated.id ? updated : role))
      setNotice(`Permission assignments for “${updated.name}” were saved.`)
    } catch (reason) { setError(getError(reason)) } finally { setBusy(false) }
  }

  async function removeRole() {
    if (!selected || selected.isSystemRole || !canDelete || !window.confirm(`Delete the “${selected.name}” role? Users assigned this role will lose its permissions.`)) return
    setBusy(true); setError(null); setNotice(null)
    try {
      await deleteAdminRole(selected.id)
      const removedName = selected.name
      await loadRoles()
      setNotice(`“${removedName}” was deleted.`)
    } catch (reason) { setError(getError(reason)) } finally { setBusy(false) }
  }

  const assignedCount = selectedCodes.length
  return <PageFrame intro="Create and maintain role access. Read access and each write action are checked separately, and system roles are protected by the server." title="Roles">
    <div className="grid gap-5">
      {error && <ErrorMessage>{error}</ErrorMessage>}{notice && <SuccessMessage>{notice}</SuccessMessage>}
      {canCreate && <section className={cardClass}>
        <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">NEW ROLE</p>
        <form className="mt-4 grid gap-4 md:grid-cols-[minmax(0,1fr)_minmax(0,1.5fr)_auto] md:items-end" onSubmit={createRole}>
          <label className="text-sm font-bold" htmlFor="new-role-name">Role name<input className={inputClass} id="new-role-name" maxLength={50} onChange={(event) => setNewName(event.target.value)} required value={newName} /></label>
          <label className="text-sm font-bold" htmlFor="new-role-description">Description<input className={inputClass} id="new-role-description" maxLength={200} onChange={(event) => setNewDescription(event.target.value)} value={newDescription} /></label>
          <button className={primaryButton} disabled={busy} type="submit">Create role</button>
        </form>
      </section>}

      <div className="grid min-w-0 gap-5 xl:grid-cols-[minmax(15rem,0.78fr)_minmax(0,1.4fr)]">
        <section aria-label="Role list" className={`${cardClass} self-start`}>
          <div className="flex items-center justify-between gap-3 border-b border-coast-line pb-4"><h2 className="font-display text-xl">All roles</h2><span className="rounded-full bg-coast-sage px-3 py-1 text-xs font-bold text-coast-deep">{roles.length}</span></div>
          {loading ? null : roles.length === 0 ? <p className="py-5 text-sm text-coast-muted">No roles are available.</p> : <ul className="mt-3 grid gap-2">
            {roles.map((role) => <li key={role.id}><button aria-current={role.id === selectedId ? 'true' : undefined} className={`w-full rounded-2xl border px-4 py-3 text-left transition ${role.id === selectedId ? 'border-coast-blue bg-coast-sage/70' : 'border-transparent hover:border-coast-line hover:bg-coast-paper'}`} onClick={() => setSelectedId(role.id)} type="button"><span className="flex flex-wrap items-center justify-between gap-2"><span className="break-words font-bold text-coast-deep">{role.name}</span>{role.isSystemRole && <span className="rounded-full bg-coast-deep px-2 py-1 text-[10px] font-extrabold text-white">SYSTEM</span>}</span><span className="mt-1 block text-xs text-coast-muted">{role.permissions.length} assigned permissions</span></button></li>)}
          </ul>}
        </section>

        {selected ? <section className={cardClass}>
          <div className="flex flex-wrap items-start justify-between gap-3 border-b border-coast-line pb-4"><div><p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">ROLE DETAILS</p><h2 className="mt-1 break-words font-display text-2xl">{selected.name}</h2></div>{selected.isSystemRole && <span className="rounded-full bg-coast-sand px-3 py-1.5 text-xs font-extrabold text-coast-deep">Managed by BLUEVERSE</span>}</div>
          <form className="mt-5 grid gap-4 sm:grid-cols-2" onSubmit={saveRole}>
            <label className="text-sm font-bold" htmlFor="role-name">Name<input className={inputClass} disabled={!canUpdate || selected.isSystemRole || busy} id="role-name" maxLength={50} onChange={(event) => setName(event.target.value)} required value={name} /></label>
            <label className="text-sm font-bold" htmlFor="role-description">Description<input className={inputClass} disabled={!canUpdate || selected.isSystemRole || busy} id="role-description" maxLength={200} onChange={(event) => setDescription(event.target.value)} value={description} /></label>
            {canUpdate && !selected.isSystemRole && <div className="flex flex-wrap gap-2 sm:col-span-2"><button className={primaryButton} disabled={busy} type="submit">Save role details</button>{canDelete && <button className={dangerButton} disabled={busy} onClick={() => void removeRole()} type="button">Delete role</button>}</div>}
          </form>
          <div className="mt-6 border-t border-coast-line pt-5">
            <div className="flex flex-wrap items-end justify-between gap-3"><div><h3 className="font-display text-xl">Assigned permissions</h3><p className="mt-1 text-sm text-coast-muted">{assignedCount} selected. Unchecking a permission removes that grant from the role.</p></div>{canAssignPermissions && !selected.isSystemRole && <button className={secondaryButton} disabled={busy} onClick={() => void savePermissions()} type="button">Save permissions</button>}</div>
            {canAssignPermissions ? <fieldset className="mt-4 grid gap-2 sm:grid-cols-2" disabled={!canAssignPermissions || selected.isSystemRole || busy}>
              <legend className="sr-only">Permissions assigned to {selected.name}</legend>
              {permissions.map((permission) => <label className={`flex min-w-0 items-start gap-3 rounded-2xl border border-coast-line p-3 ${selected.isSystemRole ? 'opacity-70' : 'hover:bg-coast-paper'}`} key={permission.id}>
                <input checked={selectedCodes.includes(permission.code)} className="mt-1 h-4 w-4 shrink-0 accent-coast-blue" onChange={(event) => setSelectedCodes((current) => event.target.checked ? [...current, permission.code] : current.filter((code) => code !== permission.code))} type="checkbox" />
                <span className="min-w-0"><code className="break-all text-xs font-bold text-coast-deep">{permission.code}</code><span className="mt-1 block text-xs leading-5 text-coast-muted">{permission.description}</span></span>
              </label>)}
            </fieldset> : <p className="mt-4 rounded-2xl bg-coast-paper p-4 text-sm leading-6 text-coast-muted">Permission assignment requires role read and update access together with permission catalogue read access.</p>}
            {selected.isSystemRole && <p className="mt-4 rounded-2xl bg-coast-sage p-4 text-sm leading-6 text-coast-deep">System role names, permissions and deletion are controlled by the service deployment.</p>}
          </div>
        </section> : loading ? null : <section className={cardClass}><p className="text-sm text-coast-muted">Choose a role to review its grants.</p></section>}
      </div>
    </div>
  </PageFrame>
}

export function AdminUsersPage() {
  const { user } = useAuthSession()
  const canCreate = hasAllPermissions(user, ['auth.user.read', 'auth.user.create'])
  const canUpdate = hasAllPermissions(user, ['auth.user.read', 'auth.user.update'])
  const canDelete = hasAllPermissions(user, ['auth.user.read', 'auth.user.delete'])
  const canAssignRoles = hasAllPermissions(user, ['auth.user.read', 'auth.user.update', 'auth.role.read'])
  const canReadRoles = hasAllPermissions(user, ['auth.role.read'])
  const canManageSystemRoles = hasAllPermissions(user, ['auth.role.system.manage'])
  const [users, setUsers] = useState<AdminUser[]>([])
  const [roles, setRoles] = useState<AdminRole[]>([])
  const [selectedId, setSelectedId] = useState('')
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [active, setActive] = useState(true)
  const [newPassword, setNewPassword] = useState('')
  const [selectedRoles, setSelectedRoles] = useState<string[]>([])
  const [createName, setCreateName] = useState('')
  const [createEmail, setCreateEmail] = useState('')
  const [createPassword, setCreatePassword] = useState('')
  const [createRoles, setCreateRoles] = useState<string[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [editorUserId, setEditorUserId] = useState('')
  const selected = users.find((item) => item.id === selectedId) ?? null
  const systemRoleNames = useMemo(() => new Set(roles.filter((role) => role.isSystemRole).map((role) => role.name)), [roles])

  if (selected && editorUserId !== selected.id) {
    setEditorUserId(selected.id)
    setFullName(selected.fullName)
    setEmail(selected.email)
    setActive(selected.isActive)
    setSelectedRoles(selected.roles)
    setNewPassword('')
  }

  const loadUsers = useCallback(async () => {
    const items = await getAdminUsers()
    setUsers(items)
    setSelectedId((current) => items.some((item) => item.id === current) ? current : items[0]?.id ?? '')
  }, [setSelectedId, setUsers])

  useEffect(() => {
    let mounted = true
    void Promise.all([getAdminUsers(), canReadRoles ? getAdminRoles() : Promise.resolve([])]).then(([userItems, roleItems]) => {
      if (!mounted) return
      setUsers(userItems); setRoles(roleItems); setSelectedId(userItems[0]?.id ?? '')
    }).catch((reason: unknown) => { if (mounted) setError(getError(reason)) }).finally(() => { if (mounted) setLoading(false) })
    return () => { mounted = false }
  }, [canReadRoles])

  async function createUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setBusy(true); setError(null); setNotice(null)
    try {
      const created = await createAdminUser({ email: createEmail.trim(), password: createPassword, fullName: createName.trim(), roleNames: canReadRoles ? createRoles : [] })
      setCreateName(''); setCreateEmail(''); setCreatePassword(''); setCreateRoles([])
      await loadUsers(); setSelectedId(created.id); setNotice(`The account for ${created.fullName} was created.`)
    } catch (reason) { setError(getError(reason)) } finally { setBusy(false) }
  }

  async function saveUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selected || !canUpdate) return
    setBusy(true); setError(null); setNotice(null)
    try {
      const updated = await updateAdminUser(selected.id, { email: email.trim(), fullName: fullName.trim(), isActive: active, ...(newPassword ? { newPassword } : {}) })
      setUsers((items) => items.map((item) => item.id === updated.id ? updated : item)); setNewPassword('')
      setNotice(`The account for ${updated.fullName} was updated.`)
    } catch (reason) { setError(getError(reason)) } finally { setBusy(false) }
  }

  async function saveUserRoles() {
    if (!selected || !canAssignRoles) return
    setBusy(true); setError(null); setNotice(null)
    try {
      const updated = await setUserRoles(selected.id, selectedRoles)
      setUsers((items) => items.map((item) => item.id === updated.id ? updated : item))
      setNotice(`Role assignments for ${updated.fullName} were saved.`)
    } catch (reason) { setError(getError(reason)) } finally { setBusy(false) }
  }

  async function removeUser() {
    if (!selected || !canDelete || selected.id === user?.id || !window.confirm(`Delete the account for ${selected.fullName}? This cannot be undone.`)) return
    setBusy(true); setError(null); setNotice(null)
    try {
      await deleteAdminUser(selected.id)
      const removedName = selected.fullName
      await loadUsers(); setNotice(`The account for ${removedName} was deleted.`)
    } catch (reason) { setError(getError(reason)) } finally { setBusy(false) }
  }

  return <PageFrame intro="Review accounts, update their details, assign or remove roles, and delete accounts when permitted. The server verifies each action against current account roles and protected system-role rules." title="User accounts">
    <div className="grid gap-5">
      {error && <ErrorMessage>{error}</ErrorMessage>}{notice && <SuccessMessage>{notice}</SuccessMessage>}
      {canCreate && <section className={cardClass}>
        <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">CREATE ACCOUNT</p>
        <form className="mt-4 grid gap-4 sm:grid-cols-2 xl:grid-cols-4" onSubmit={createUser}>
          <label className="text-sm font-bold" htmlFor="new-user-name">Full name<input className={inputClass} id="new-user-name" maxLength={100} onChange={(event) => setCreateName(event.target.value)} required value={createName} /></label>
          <label className="text-sm font-bold" htmlFor="new-user-email">Email<input className={inputClass} id="new-user-email" onChange={(event) => setCreateEmail(event.target.value)} required type="email" value={createEmail} /></label>
          <label className="text-sm font-bold" htmlFor="new-user-password">Temporary password<input className={inputClass} id="new-user-password" minLength={8} onChange={(event) => setCreatePassword(event.target.value)} required type="password" value={createPassword} /></label>
          <div className="flex items-end"><button className={`${primaryButton} w-full`} disabled={busy} type="submit">Create account</button></div>
          {canReadRoles && <label className="text-sm font-bold sm:col-span-2 xl:col-span-4" htmlFor="new-user-roles">Initial roles <span className="font-normal text-coast-muted">(optional)</span>
            <select className={`${inputClass} min-h-28`} id="new-user-roles" multiple onChange={(event) => setCreateRoles(Array.from(event.currentTarget.selectedOptions, (option) => option.value))} value={createRoles}>
              {roles.filter((role) => canManageSystemRoles || !role.isSystemRole).map((role) => <option key={role.id} value={role.name}>{role.name}{role.isSystemRole ? ' · system' : ''}</option>)}
            </select><span className="mt-1 block text-xs font-normal text-coast-muted">Use Ctrl or Command to select more than one. System roles require elevated access.</span>
          </label>}
        </form>
      </section>}

      <div className="grid min-w-0 gap-5 xl:grid-cols-[minmax(15rem,0.8fr)_minmax(0,1.4fr)]">
        <section aria-label="User list" className={`${cardClass} self-start`}>
          <div className="flex items-center justify-between gap-3 border-b border-coast-line pb-4"><h2 className="font-display text-xl">Accounts</h2><span className="rounded-full bg-coast-sage px-3 py-1 text-xs font-bold text-coast-deep">{users.length}</span></div>
          {loading ? null : users.length === 0 ? <p className="py-5 text-sm text-coast-muted">No accounts are available.</p> : <ul className="mt-3 grid gap-2">
            {users.map((item) => <li key={item.id}><button aria-current={item.id === selectedId ? 'true' : undefined} className={`w-full rounded-2xl border px-4 py-3 text-left transition ${item.id === selectedId ? 'border-coast-blue bg-coast-sage/70' : 'border-transparent hover:border-coast-line hover:bg-coast-paper'}`} onClick={() => setSelectedId(item.id)} type="button"><span className="flex min-w-0 items-center justify-between gap-3"><span className="min-w-0 break-words font-bold text-coast-deep">{item.fullName}</span><span className={`shrink-0 rounded-full px-2 py-1 text-[10px] font-extrabold ${item.isActive ? 'bg-emerald-50 text-emerald-800' : 'bg-coast-paper text-coast-muted'}`}>{item.isActive ? 'ACTIVE' : 'PAUSED'}</span></span><span className="mt-1 block break-all text-xs text-coast-muted">{item.email}</span><span className="mt-2 flex flex-wrap gap-1">{item.roles.length ? item.roles.map((role) => <span className="rounded-full bg-white px-2 py-1 text-[10px] font-bold text-coast-deep" key={role}>{role}</span>) : <span className="text-xs text-coast-muted">No roles assigned</span>}</span></button></li>)}
          </ul>}
        </section>

        {selected ? <section className={cardClass}>
          <div className="flex flex-wrap items-start justify-between gap-3 border-b border-coast-line pb-4"><div><p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">ACCOUNT DETAILS</p><h2 className="mt-1 break-words font-display text-2xl">{selected.fullName}</h2></div>{selected.id === user?.id && <span className="rounded-full bg-coast-sand px-3 py-1.5 text-xs font-extrabold text-coast-deep">Your account</span>}</div>
          <form className="mt-5 grid gap-4 sm:grid-cols-2" onSubmit={saveUser}>
            <label className="text-sm font-bold" htmlFor="user-full-name">Full name<input className={inputClass} disabled={!canUpdate || busy} id="user-full-name" maxLength={100} onChange={(event) => setFullName(event.target.value)} required value={fullName} /></label>
            <label className="text-sm font-bold" htmlFor="user-email">Email<input className={inputClass} disabled={!canUpdate || busy || selected.id === user?.id} id="user-email" onChange={(event) => setEmail(event.target.value)} required type="email" value={email} />{selected.id === user?.id && <span className="mt-1 block text-xs font-normal text-coast-muted">You can’t change your own email through this screen.</span>}</label>
            <label className="flex min-h-11 items-center gap-3 text-sm font-semibold sm:col-span-2"><input checked={active} className="h-4 w-4 accent-coast-blue" disabled={!canUpdate || busy} onChange={(event) => setActive(event.target.checked)} type="checkbox" />Account is active</label>
            {canUpdate && <label className="text-sm font-bold sm:col-span-2" htmlFor="user-new-password">Set a new password <span className="font-normal text-coast-muted">(optional; at least 8 characters)</span><input autoComplete="new-password" className={inputClass} disabled={busy} id="user-new-password" minLength={8} onChange={(event) => setNewPassword(event.target.value)} type="password" value={newPassword} /></label>}
            {canUpdate && <div className="flex flex-wrap gap-2 sm:col-span-2"><button className={primaryButton} disabled={busy} type="submit">Save account details</button>{canDelete && selected.id !== user?.id && <button className={dangerButton} disabled={busy} onClick={() => void removeUser()} type="button">Delete account</button>}</div>}
          </form>
          <div className="mt-6 border-t border-coast-line pt-5">
            <div className="flex flex-wrap items-end justify-between gap-3"><div><h3 className="font-display text-xl">Assigned roles</h3><p className="mt-1 text-sm text-coast-muted">{selectedRoles.length ? selectedRoles.join(', ') : 'No roles assigned'}</p></div>{canAssignRoles && <button className={secondaryButton} disabled={busy || (selected.roles.some((role) => systemRoleNames.has(role)) && !canManageSystemRoles)} onClick={() => void saveUserRoles()} type="button">Save roles</button>}</div>
            {canAssignRoles ? <fieldset className="mt-4 grid gap-2 sm:grid-cols-2" disabled={busy || (selected.roles.some((role) => systemRoleNames.has(role)) && !canManageSystemRoles)}>
              <legend className="sr-only">Roles assigned to {selected.fullName}</legend>
              {roles.map((role) => <label className={`flex min-w-0 items-start gap-3 rounded-2xl border border-coast-line p-3 ${(role.isSystemRole && !canManageSystemRoles) || (selected.roles.some((current) => systemRoleNames.has(current)) && !canManageSystemRoles) ? 'opacity-70' : 'hover:bg-coast-paper'}`} key={role.id}>
                <input checked={selectedRoles.includes(role.name)} className="mt-1 h-4 w-4 shrink-0 accent-coast-blue" disabled={role.isSystemRole && !canManageSystemRoles} onChange={(event) => setSelectedRoles((current) => event.target.checked ? [...current, role.name] : current.filter((name) => name !== role.name))} type="checkbox" />
                <span className="min-w-0"><span className="block break-words text-sm font-bold text-coast-deep">{role.name}{role.isSystemRole ? ' · system role' : ''}</span><span className="mt-1 block text-xs leading-5 text-coast-muted">{role.description}</span></span>
              </label>)}
            </fieldset> : <p className="mt-4 rounded-2xl bg-coast-paper p-4 text-sm leading-6 text-coast-muted">Changing roles requires user read and update access together with role read access.</p>}
            {!canManageSystemRoles && selected.roles.some((role) => systemRoleNames.has(role)) && <p className="mt-4 rounded-2xl bg-coast-sage p-4 text-sm leading-6 text-coast-deep">System-role assignments are protected. An account with a system role cannot be deleted.</p>}
          </div>
        </section> : loading ? null : <section className={cardClass}><p className="text-sm text-coast-muted">Choose an account to review its details.</p></section>}
      </div>
      {!canCreate && !canUpdate && !canDelete && !canAssignRoles && !hasAnyPermission(user, ['auth.user.read']) && <ErrorMessage>Your account cannot read or manage user accounts.</ErrorMessage>}
    </div>
  </PageFrame>
}
