import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { AuthApiError, changePassword, deleteCurrentUser, getSessions, revokeSession } from '../features/auth/auth'
import type { AuthSession } from '../features/auth/auth'
import { authEntryHrefFor } from '../features/auth/authNavigation'
import { MAX_ACTIVE_SESSIONS, useAuthSession } from '../features/auth/authSession'
import type { AuthSessionsStatus } from '../features/auth/authSession'
import { preserveAuthLoadingContextForReload } from '../features/loading/backendLoading'
import AccountAreaNavigation from '../components/account/AccountAreaNavigation'
import SiteFooter from '../components/layout/SiteFooter'
import SiteHeader from '../components/layout/SiteHeader'

const inputClassName = 'mt-2 min-h-12 w-full rounded-2xl border border-coast-line bg-white px-4 py-3 text-[15px] text-coast-ink outline-none transition placeholder:text-coast-muted/70 focus:border-coast-blue focus:ring-2 focus:ring-coast-glass'
const actionClassName = 'inline-flex min-h-12 items-center justify-center rounded-full bg-coast-deep px-6 text-sm font-extrabold text-white shadow-sm transition duration-200 hover:-translate-y-0.5 hover:bg-coast-blue hover:shadow-md focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-blue active:scale-[0.98] disabled:cursor-wait disabled:opacity-60 motion-reduce:transition-none'
const sectionClassName = 'min-w-0 w-full scroll-mt-24 rounded-3xl border border-coast-line bg-coast-pearl p-5 shadow-[0_16px_45px_rgba(24,57,76,0.06)] sm:p-8'

function ProfilePage() {
  const { user, status, updateUser, forgetAccount, removeAccount, signOutEverywhere } = useAuthSession()
  const navigate = useNavigate()
  const [isSaving, setIsSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  const [sessionData, setSessionData] = useState<{
    userId: string
    status: AuthSessionsStatus
    items: AuthSession[]
    error: string | null
  }>({ userId: '', status: 'checking', items: [], error: null })
  const userId = user?.id ?? null
  const sessions = userId && sessionData.userId === userId ? sessionData.items : []
  const sessionsStatus = !userId
    ? 'none'
    : sessionData.userId === userId ? sessionData.status : 'checking'
  const sessionError = userId && sessionData.userId === userId ? sessionData.error : null
  const [revokingSessionId, setRevokingSessionId] = useState<string | null>(null)
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [passwordError, setPasswordError] = useState<string | null>(null)
  const [isChangingPassword, setIsChangingPassword] = useState(false)
  const [securityAction, setSecurityAction] = useState<{ type: 'session'; session: AuthSession } | { type: 'everywhere' } | null>(null)
  const [verificationPassword, setVerificationPassword] = useState('')
  const [verificationError, setVerificationError] = useState<string | null>(null)
  const [isVerifying, setIsVerifying] = useState(false)
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false)
  const [deleteError, setDeleteError] = useState<string | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)
  const passwordsMatch = confirmPassword.length > 0 && newPassword === confirmPassword

  useEffect(() => {
    if (!userId) return

    let active = true
    void getSessions().then((items) => {
      if (!active) return
      setSessionData({ userId, items, status: items.length ? 'loaded' : 'none', error: null })
    }).catch(() => {
      if (!active) return
      setSessionData({ userId, items: [], status: 'unavailable', error: 'Your signed-in devices could not be loaded. Try again in a moment.' })
    })
    return () => { active = false }
  }, [userId])

  async function saveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!user) return

    const formData = new FormData(event.currentTarget)
    const fullName = String(formData.get('fullName') ?? '').trim()
    if (!fullName || fullName.length > 100) {
      setSaveError('Enter a name between 1 and 100 characters.')
      setSaved(false)
      return
    }

    setIsSaving(true)
    setSaveError(null)
    setSaved(false)
    try {
      await updateUser(fullName)
      setSaved(true)
    } catch (requestError) {
      setSaveError(requestError instanceof AuthApiError && requestError.status === 400
        ? 'Check your name and try again.'
        : 'We couldn’t save your changes just now. Please try again.')
    } finally {
      setIsSaving(false)
    }
  }

  async function submitPasswordChange(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!user) return
    setPasswordError(null)
    if (newPassword.length < 8) {
      setPasswordError('Use at least 8 characters for your new password.')
      return
    }
    if (!passwordsMatch) {
      setPasswordError('Your new passwords do not match yet.')
      return
    }

    setIsChangingPassword(true)
    try {
      await changePassword(currentPassword, newPassword)
      forgetAccount(user.id)
      try {
        window.sessionStorage.setItem(
          'blueverse.auth-transition-notice',
          'Your password has changed. The account you were using was signed out for security.'
        )
      } catch {
        // The account change still completes if session storage is unavailable.
      }
      preserveAuthLoadingContextForReload('password')
      window.location.reload()
    } catch (requestError) {
      setPasswordError(requestError instanceof AuthApiError && requestError.status === 400
        ? requestError.message
        : 'We couldn’t change your password just now. Check your current password and try again.')
    } finally {
      setIsChangingPassword(false)
    }
  }

  async function endSession(session: AuthSession) {
    setRevokingSessionId(session.id)
    try {
      if (session.isCurrent && user) {
        await removeAccount(user.id)
        preserveAuthLoadingContextForReload('signOut')
        window.location.reload()
        return
      }
      await revokeSession(session.id)
      const remainingSessions = sessions.filter((item) => item.id !== session.id)
      setSessionData({ userId: user?.id ?? '', items: remainingSessions, status: remainingSessions.length ? 'loaded' : 'none', error: null })
    } catch {
      if (user) setSessionData({ userId: user.id, items: sessions, status: sessionsStatus, error: 'That session could not be ended. It may have already expired.' })
    } finally {
      setRevokingSessionId(null)
    }
  }

  async function verifyAndApplySessionAction(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!user || !securityAction) return
    setIsVerifying(true)
    setVerificationError(null)
    try {
      if (securityAction.type === 'everywhere') {
        await signOutEverywhere(verificationPassword)
        preserveAuthLoadingContextForReload('signOut')
        window.location.reload()
        return
      } else {
        await revokeSession(securityAction.session.id, verificationPassword)
        const remainingSessions = sessions.filter((item) => item.id !== securityAction.session.id)
        setSessionData({ userId: user.id, items: remainingSessions, status: remainingSessions.length ? 'loaded' : 'none', error: null })
      }
      setSecurityAction(null)
      setVerificationPassword('')
    } catch (requestError) {
      setVerificationError(requestError instanceof AuthApiError
        ? requestError.message
        : 'We couldn’t verify your password. Please try again.')
    } finally {
      setIsVerifying(false)
    }
  }

  async function deleteAccount() {
    if (!user) return
    setIsDeleting(true)
    setDeleteError(null)
    try {
      await deleteCurrentUser()
      forgetAccount(user.id)
      navigate('/', { replace: true })
      preserveAuthLoadingContextForReload('deleteAccount')
      window.location.reload()
    } catch (requestError) {
      setDeleteError(requestError instanceof AuthApiError
        ? requestError.message
        : 'We couldn’t delete this account just now. Please try again.')
    } finally {
      setIsDeleting(false)
    }
  }

  return (
    <div className="flex min-h-screen flex-col bg-coast-paper font-sans text-coast-ink" id="top">
      <SiteHeader active="profile" />
      <main className="mx-auto grid w-full max-w-[90rem] flex-1 grid-cols-1 content-start gap-6 px-4 py-6 sm:px-8 sm:py-10 lg:grid-cols-[15rem_minmax(0,1fr)] lg:items-start lg:gap-8 lg:px-6 lg:py-0">
        <AccountAreaNavigation active="profile" />
        <div className="min-w-0 lg:py-12">
          <header className="mb-7 sm:mb-9">
            <p className="text-[11px] font-extrabold tracking-[0.18em] text-coast-blue">YOUR BLUEVERSE</p>
            <h1 className="mt-2 font-display text-4xl leading-tight tracking-[-0.05em] sm:text-5xl">Your profile, in your hands.</h1>
            <p className="mt-3 max-w-2xl text-sm leading-6 text-coast-muted sm:text-base sm:leading-7">Keep the details connected to your account current, manage your password and review where it is signed in.</p>
          </header>

          {status === 'checking' ? null : !user ? (
            <section className={sectionClassName}>
              <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">ACCOUNT ACCESS</p>
              <h2 className="mt-3 font-display text-3xl tracking-[-0.04em]">Sign in to view your profile.</h2>
              <p className="mt-3 max-w-xl text-sm leading-6 text-coast-muted">{status === 'unavailable' ? 'We couldn’t load your profile just now. Sign in again or try again in a little while.' : 'Your profile is available after you sign in to BLUEVERSE.'}</p>
              <Link className={`${actionClassName} mt-6`} to={authEntryHrefFor('/signin', '/profile')}>Sign in</Link>
            </section>
          ) : (
            <div className="grid min-w-0 gap-5">
              <section aria-labelledby="profile-details-title" className={sectionClassName} id="personal-details">
                <div className="flex items-center gap-4 border-b border-coast-line pb-5 sm:pb-6">
                  <span aria-hidden="true" className="inline-flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-coast-sage font-display text-xl font-bold text-coast-deep sm:h-14 sm:w-14">{user.fullName.trim().charAt(0).toUpperCase() || 'B'}</span>
                  <div className="min-w-0 flex-1">
                    <p className="text-xs font-extrabold tracking-[0.14em] text-coast-blue">PERSONAL DETAILS</p>
                    <h2 className="mt-1 break-words font-display text-2xl tracking-[-0.03em] [overflow-wrap:anywhere]" id="profile-details-title">{user.fullName}</h2>
                  </div>
                </div>

                <form aria-busy={isSaving} className="mt-6 grid gap-5" onSubmit={saveProfile}>
                  <label className="block text-sm font-bold text-coast-ink" htmlFor="profile-full-name">
                    Full name
                    <input autoComplete="name" className={inputClassName} defaultValue={user.fullName} id="profile-full-name" maxLength={100} name="fullName" required />
                    <span className="mt-2 block text-xs font-normal text-coast-muted">This is the name shown with your BLUEVERSE account.</span>
                  </label>
                  <label className="block text-sm font-bold text-coast-ink" htmlFor="profile-email">
                    Email address
                    <input className={`${inputClassName} cursor-not-allowed bg-coast-sand/70`} id="profile-email" readOnly value={user.email} />
                    <span className="mt-2 block text-xs font-normal text-coast-muted">Email changes are not available from this profile yet.</span>
                  </label>
                  {saveError && <p className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm leading-5 text-red-900" role="alert">{saveError}</p>}
                  {saved && <p className="rounded-2xl bg-coast-sage px-4 py-3 text-sm font-semibold text-coast-deep" role="status">Your profile has been updated.</p>}
                  <div className="flex flex-wrap items-center gap-4 pt-1">
                    <button className={actionClassName} disabled={isSaving} type="submit">Save changes</button>
                    <span className="text-xs leading-5 text-coast-muted">Only the details you can edit are sent to BLUEVERSE.</span>
                  </div>
                </form>
                <div aria-labelledby="allocated-roles-title" className="mt-7 border-t border-coast-line pt-5">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div><p className="text-xs font-extrabold tracking-[0.14em] text-coast-blue">ACCOUNT ACCESS</p><h3 className="mt-1 font-display text-xl" id="allocated-roles-title">Roles allocated to you</h3></div>
                    <span className="rounded-full bg-coast-sage px-3 py-1.5 text-xs font-extrabold text-coast-deep">{user.roles.length} {user.roles.length === 1 ? 'role' : 'roles'}</span>
                  </div>
                  {user.roles.length ? <ul aria-label="Your assigned roles" className="mt-4 flex flex-wrap gap-2">
                    {user.roles.map((role) => <li className="rounded-full border border-coast-line bg-white px-4 py-2 text-sm font-bold text-coast-deep" key={role}>{role}</li>)}
                  </ul> : <p className="mt-3 text-sm leading-6 text-coast-muted">No roles have been allocated to this account yet.</p>}
                </div>
              </section>

              <section aria-labelledby="change-password-title" className={sectionClassName} id="change-password">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0 flex-1">
                    <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">ACCOUNT SECURITY</p>
                    <h2 className="mt-2 break-words font-display text-2xl tracking-[-0.035em]" id="change-password-title">Change your password</h2>
                    <p className="mt-2 max-w-xl text-sm leading-6 text-coast-muted">Choose a password you have not used before. You’ll need to sign in again after it changes.</p>
                  </div>
                  <span aria-hidden="true" className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-coast-sage text-coast-blue">
                    <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24"><rect x="5" y="10" width="14" height="11" rx="2.5" stroke="currentColor" strokeWidth="1.8"/><path d="M8 10V7a4 4 0 0 1 8 0v3" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round"/><circle cx="12" cy="15" r="1.2" fill="currentColor"/><path d="M12 16v2" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round"/></svg>
                  </span>
                </div>
                <form aria-busy={isChangingPassword} className="mt-5 grid gap-4 md:grid-cols-2" onSubmit={submitPasswordChange}>
                  <label className="block text-sm font-bold text-coast-ink md:col-span-2" htmlFor="current-password">Current password
                    <input autoComplete="current-password" className={inputClassName} id="current-password" onChange={(event) => setCurrentPassword(event.target.value)} required type="password" value={currentPassword} />
                  </label>
                  <label className="block text-sm font-bold text-coast-ink" htmlFor="new-password">New password
                    <input autoComplete="new-password" className={inputClassName} id="new-password" minLength={8} onChange={(event) => setNewPassword(event.target.value)} required type="password" value={newPassword} />
                    <span className="mt-2 block text-xs font-normal text-coast-muted">At least 8 characters.</span>
                  </label>
                  <label className="block text-sm font-bold text-coast-ink" htmlFor="confirm-new-password">Confirm new password
                    <input aria-invalid={confirmPassword.length > 0 && !passwordsMatch} autoComplete="new-password" className={inputClassName} id="confirm-new-password" onChange={(event) => setConfirmPassword(event.target.value)} required type="password" value={confirmPassword} />
                    {confirmPassword && <span aria-live="polite" className={`mt-2 block text-xs font-semibold ${passwordsMatch ? 'text-coast-teal' : 'text-red-800'}`}>{passwordsMatch ? 'Passwords match.' : 'Passwords do not match yet.'}</span>}
                  </label>
                  {passwordError && <p className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm leading-5 text-red-900 md:col-span-2" role="alert">{passwordError}</p>}
                  <div className="md:col-span-2"><button className={actionClassName} disabled={isChangingPassword} type="submit">Update password</button></div>
                </form>
              </section>

              <section aria-labelledby="sessions-title" className={sectionClassName} id="sessions">
                <div className="flex flex-wrap items-start justify-between gap-4 border-b border-coast-line pb-5">
                  <div className="min-w-0 flex-1">
                    <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">LOGIN SESSIONS</p>
                    <h2 className="mt-2 break-words font-display text-2xl tracking-[-0.035em]" id="sessions-title">Devices signed in to this account</h2>
                    <p className="mt-2 text-sm leading-6 text-coast-muted">Review recent sign-ins and end a session you no longer recognize or use.</p>
                  </div>
                  <span className="rounded-full bg-coast-sage px-3 py-1.5 text-xs font-extrabold text-coast-deep">{sessionsStatus === 'loaded' ? `${sessions.length} active` : '—'}</span>
                </div>
                {sessionsStatus === 'checking' ? null
                  : sessionsStatus === 'unavailable' ? <p className="pt-5 text-sm leading-6 text-coast-muted">{sessionError}</p>
                    : sessions.length ? <ul className="divide-y divide-coast-line">
                      {sessions.map((session) => (
                        <li className="flex flex-wrap items-center justify-between gap-3 py-4" key={session.id}>
                          <div className="flex min-w-0 items-start gap-3">
                            <span aria-hidden="true" className={`mt-0.5 inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-full ${session.isCurrent ? 'bg-coast-deep text-white' : 'bg-coast-sage text-coast-blue'}`}>{session.isCurrent ? '✓' : '↗'}</span>
                            <div className="min-w-0">
                              <p className="text-sm font-extrabold text-coast-ink">{session.isCurrent ? 'This browser' : 'Signed-in device'}{session.isCurrent && <span className="ml-2 rounded-full bg-coast-sand px-2.5 py-1 text-[10px] font-bold text-coast-deep">Current</span>}</p>
                              <p className="mt-1 text-xs leading-5 text-coast-muted">Last active {new Date(session.lastSeenAt).toLocaleString()} · expires {new Date(session.expiresAt).toLocaleDateString()}</p>
                              <p className="text-[11px] text-coast-muted">{session.rememberMe ? '30-day sign-in' : '1-day sign-in'}</p>
                            </div>
                          </div>
                          <button aria-busy={revokingSessionId === session.id} className="min-h-10 rounded-full border border-coast-line px-4 text-xs font-bold text-coast-deep transition-colors hover:border-red-200 hover:bg-red-50 hover:text-red-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue disabled:cursor-wait disabled:opacity-60" disabled={revokingSessionId !== null} onClick={() => session.isCurrent ? void endSession(session) : (setVerificationError(null), setSecurityAction({ type: 'session', session }))} type="button">{session.isCurrent ? 'Sign out here' : 'End session'}</button>
                        </li>
                      ))}
                    </ul> : <p className="pt-5 text-sm text-coast-muted">No active login sessions to show.</p>}
                {sessionError && sessionsStatus === 'loaded' && <p className="mt-3 rounded-2xl bg-red-50 px-4 py-3 text-sm text-red-900" role="alert">{sessionError}</p>}
                {sessionsStatus === 'loaded' && sessions.length >= MAX_ACTIVE_SESSIONS && <p className="mt-4 rounded-2xl bg-coast-sand px-4 py-3 text-sm leading-6 text-coast-deep">This account has reached its active-session limit. End an older session before signing in on another device.</p>}
                <div className="mt-5 border-t border-coast-line pt-5">
                  <p className="text-sm leading-6 text-coast-muted">End every active session for this account after verifying your password.</p>
                  <button className="mt-3 min-h-10 rounded-full border border-coast-line px-4 text-xs font-extrabold text-coast-deep transition-colors hover:border-red-200 hover:bg-red-50 hover:text-red-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue" onClick={() => { setVerificationError(null); setSecurityAction({ type: 'everywhere' }) }} type="button">Sign out this account everywhere</button>
                </div>
              </section>

              <section aria-labelledby="delete-account-title" className="min-w-0 rounded-3xl border border-red-200 bg-red-50 p-5 shadow-[0_16px_45px_rgba(127,29,29,0.06)] sm:p-7">
                <p className="text-xs font-extrabold tracking-[0.15em] text-red-800">DANGER ZONE</p>
                <h2 className="mt-2 break-words font-display text-2xl tracking-[-0.035em] text-red-950" id="delete-account-title">Delete your account</h2>
                <p className="mt-2 max-w-2xl text-sm leading-6 text-red-900">This permanently removes your BLUEVERSE account and its active sessions. If an assigned system role protects this account, the confirmation will name it and keep the account active.</p>
                {deleteError && !deleteDialogOpen && <p className="mt-4 rounded-2xl border border-red-300 bg-white px-4 py-3 text-sm leading-6 text-red-900" role="alert">{deleteError}</p>}
                <button className="mt-5 min-h-11 rounded-full bg-red-800 px-5 text-sm font-extrabold text-white transition duration-200 hover:-translate-y-0.5 hover:bg-red-950 hover:shadow-md focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-red-800 disabled:cursor-wait disabled:opacity-60 motion-reduce:transition-none" disabled={isDeleting} onClick={() => { setDeleteError(null); setDeleteDialogOpen(true) }} type="button">Delete account</button>
              </section>

              <aside className="rounded-3xl bg-coast-deep p-6 text-white shadow-sm sm:p-8">
                <p className="text-xs font-extrabold tracking-[0.15em] text-coast-glass">ACCOUNT SINCE</p>
                <p className="mt-3 font-display text-3xl tracking-[-0.04em]">{new Date(user.createdAt).toLocaleDateString(undefined, { year: 'numeric', month: 'long' })}</p>
                <p className="mt-3 text-sm leading-6 text-white/75">Your profile belongs to the coastal community growing around BLUEVERSE.</p>
              </aside>
            </div>
          )}
        </div>
      </main>
      {securityAction && <div className="fixed inset-0 z-[70] flex items-center justify-center bg-coast-ink/55 px-4 py-8 backdrop-blur-sm">
        <section aria-labelledby="password-confirmation-title" aria-modal="true" className="w-full max-w-md rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-[0_24px_70px_rgba(24,57,76,0.3)] sm:p-8" role="dialog">
          <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">PASSWORD VERIFICATION</p>
          <h2 className="mt-2 font-display text-2xl tracking-[-0.035em]" id="password-confirmation-title">{securityAction.type === 'everywhere' ? 'Sign out this account everywhere?' : 'End this device session?'}</h2>
          <p className="mt-3 text-sm leading-6 text-coast-muted">Enter your current password to confirm this security change.</p>
          <form className="mt-5 grid gap-4" onSubmit={verifyAndApplySessionAction}>
            <label className="text-sm font-bold text-coast-ink" htmlFor="verify-current-password">Current password
              <input autoComplete="current-password" autoFocus className={inputClassName} id="verify-current-password" onChange={(event) => setVerificationPassword(event.target.value)} required type="password" value={verificationPassword} />
            </label>
            {verificationError && <p className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm leading-5 text-red-900" role="alert">{verificationError}</p>}
            <div className="flex flex-wrap justify-end gap-3 pt-1">
              <button className="min-h-11 rounded-full border border-coast-line px-5 text-sm font-bold text-coast-deep transition-colors hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:opacity-60" disabled={isVerifying} onClick={() => { setSecurityAction(null); setVerificationPassword('') }} type="button">Cancel</button>
              <button className={actionClassName} disabled={isVerifying} type="submit">Verify and continue</button>
            </div>
          </form>
        </section>
      </div>}
      {deleteDialogOpen && <div className="fixed inset-0 z-[70] flex items-center justify-center bg-red-950/55 px-4 py-8 backdrop-blur-sm">
        <section aria-labelledby="delete-confirmation-title" aria-modal="true" className="w-full max-w-md rounded-3xl border border-red-300 bg-white p-6 shadow-[0_24px_70px_rgba(69,10,10,0.35)] sm:p-8" role="alertdialog">
          <p className="text-xs font-extrabold tracking-[0.15em] text-red-800">FINAL CONFIRMATION</p>
          <h2 className="mt-2 font-display text-2xl tracking-[-0.035em] text-red-950" id="delete-confirmation-title">Delete this account permanently?</h2>
          <p className="mt-3 text-sm leading-6 text-red-900">This cannot be undone. Choose cancel to keep your account, or confirm deletion to remove it and end its sessions.</p>
          {deleteError && <p className="mt-4 rounded-2xl border border-red-300 bg-red-50 px-4 py-3 text-sm leading-6 text-red-900" role="alert">{deleteError}</p>}
          <div className="mt-6 flex flex-wrap justify-end gap-3">
            <button autoFocus className="min-h-11 rounded-full border border-coast-line px-5 text-sm font-bold text-coast-deep transition-colors hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:opacity-60" disabled={isDeleting} onClick={() => setDeleteDialogOpen(false)} type="button">Cancel</button>
            <button className="min-h-11 rounded-full bg-red-800 px-5 text-sm font-extrabold text-white transition duration-200 hover:-translate-y-0.5 hover:bg-red-950 focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-red-800 disabled:cursor-wait disabled:opacity-60" disabled={isDeleting} onClick={() => void deleteAccount()} type="button">Yes, delete account</button>
          </div>
        </section>
      </div>}
      <SiteFooter />
    </div>
  )
}

export default ProfilePage
