import { useEffect, useRef, useState } from 'react'
import { Link, useLocation } from 'react-router'
import { authEntryHref } from '../../features/auth/authNavigation'
import { MAX_DEVICE_ACCOUNTS, useAuthSession } from '../../features/auth/authSession'
import { preserveAuthLoadingContextForReload } from '../../features/loading/backendLoading'

type SiteHeaderProps = {
  active?: 'home' | 'login' | 'register' | 'profile' | 'dashboard'
  compactMobile?: boolean
}

function WaveMark() {
  return (
    <svg viewBox="0 0 44 44" fill="none" aria-hidden="true" className="h-10 w-10 shrink-0 text-coast-deep">
      <circle cx="22" cy="22" r="20.5" stroke="currentColor" strokeWidth="1.5" />
      <path d="M7 25c5.2 0 5.2-4.2 10.4-4.2S22.6 25 27.8 25 33 20.8 38 20.8M7 31c5.2 0 5.2-4.2 10.4-4.2S22.6 31 27.8 31 33 26.8 38 26.8" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" />
      <circle cx="28.5" cy="13" r="3" fill="currentColor" />
    </svg>
  )
}

function navClass(active: boolean) {
  return `rounded-full px-3 py-2 text-sm font-semibold transition-colors hover:bg-white/75 hover:text-coast-blue focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-blue ${active ? 'text-coast-deep' : 'text-coast-muted'}`
}

const accountMenuLinkClass = 'flex min-h-11 items-center justify-between gap-4 rounded-2xl px-4 text-sm font-semibold text-coast-ink transition duration-200 hover:translate-x-0.5 hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue motion-reduce:transition-none'

function SiteHeader({ active = 'home', compactMobile = false }: SiteHeaderProps) {
  const [menuOpen, setMenuOpen] = useState(false)
  const [accountMenuOpen, setAccountMenuOpen] = useState(false)
  const [accountError, setAccountError] = useState<string | null>(null)
  const [isSigningOut, setIsSigningOut] = useState(false)
  const headerRef = useRef<HTMLElement>(null)
  const location = useLocation()
  const { user, accounts, status, switchingAccountId, switchAccount, signOut } = useAuthSession()
  const accountLimitReached = accounts.length >= MAX_DEVICE_ACCOUNTS
  const loginHref = authEntryHref('/signin', location)
  const registrationHref = authEntryHref('/signup', location)

  useEffect(() => {
    if (!accountMenuOpen) return

    function closeOnOutsidePointer(event: PointerEvent) {
      if (!headerRef.current?.contains(event.target as Node)) setAccountMenuOpen(false)
    }
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key !== 'Escape') return
      setAccountMenuOpen(false)
      const triggerId = window.matchMedia('(min-width: 640px)').matches ? 'desktop-account-trigger' : 'mobile-account-trigger'
      document.getElementById(triggerId)?.focus()
    }

    document.addEventListener('pointerdown', closeOnOutsidePointer)
    document.addEventListener('keydown', closeOnEscape)
    return () => {
      document.removeEventListener('pointerdown', closeOnOutsidePointer)
      document.removeEventListener('keydown', closeOnEscape)
    }
  }, [accountMenuOpen])

  async function handleSwitchAccount(accountId: string) {
    setAccountError(null)
    try {
      await switchAccount(accountId)
      setAccountMenuOpen(false)
      setMenuOpen(false)
      preserveAuthLoadingContextForReload('switchAccount')
      window.location.reload()
    } catch {
      setAccountError('We could not switch to that account. It may need you to sign in again.')
    }
  }

  async function handleSignOut() {
    setAccountError(null)
    setIsSigningOut(true)
    try {
      await signOut()
      setAccountMenuOpen(false)
      setMenuOpen(false)
      preserveAuthLoadingContextForReload('signOut')
      window.location.reload()
    } catch {
      setAccountError('We could not sign this account out from this device just now. Please try again.')
    } finally {
      setIsSigningOut(false)
    }
  }

  function accountMenu(mobile: boolean) {
    const menuId = mobile ? 'mobile-account-menu' : 'desktop-account-menu'
    const avatar = user?.fullName.trim().charAt(0).toUpperCase() || 'B'

    return (
      <div className="relative shrink-0">
        <button
          aria-controls={accountMenuOpen ? menuId : undefined}
          aria-expanded={accountMenuOpen}
          aria-label={mobile ? 'Account menu' : undefined}
          className={`group inline-flex min-h-11 items-center gap-2.5 rounded-full border border-coast-line bg-white/80 px-2.5 pr-4 text-sm font-bold text-coast-deep shadow-sm transition duration-200 hover:-translate-y-0.5 hover:border-coast-glass hover:bg-white hover:shadow-md focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue active:scale-[0.98] motion-reduce:transition-none ${mobile ? 'h-10 min-h-10 gap-1.5 px-2.5 pr-2.5' : ''}`}
          id={mobile ? 'mobile-account-trigger' : 'desktop-account-trigger'}
          onClick={() => { setAccountError(null); setAccountMenuOpen((open) => !open) }}
          type="button"
        >
          <span aria-hidden="true" className="inline-flex h-7 w-7 items-center justify-center rounded-full bg-coast-sage text-xs font-extrabold text-coast-deep transition-colors group-hover:bg-coast-glass">{avatar}</span>
          <span className="hidden text-sm sm:inline">Account</span>
          <svg aria-hidden="true" className={`h-3.5 w-3.5 shrink-0 transition-transform duration-200 ${accountMenuOpen ? 'rotate-180' : ''}`} viewBox="0 0 16 16" fill="none"><path d="m3.5 6 4.5 4 4.5-4" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" /></svg>
        </button>

        {accountMenuOpen && (
          <div className={`z-[60] max-h-[calc(100dvh-5rem)] origin-top-right overflow-y-auto overscroll-contain rounded-3xl border border-coast-line bg-coast-pearl p-3 text-coast-ink shadow-[0_20px_55px_rgba(24,57,76,0.18)] motion-safe:animate-coast-menu ${mobile ? 'fixed left-2 right-2 top-[4.25rem] w-auto' : 'absolute right-0 top-full mt-2 w-[min(22rem,calc(100vw-1rem))]'}`} id={menuId}>
            <div className="flex items-center gap-3 rounded-[1.25rem] bg-coast-sand p-4">
              <span aria-hidden="true" className="inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-coast-deep text-sm font-extrabold text-white">{avatar}</span>
              <div className="min-w-0">
                <p className="break-words text-sm font-extrabold text-coast-ink [overflow-wrap:anywhere]">{user?.fullName}</p>
                <p className="truncate text-xs text-coast-muted">{user?.email}</p>
              </div>
            </div>

            <div className="mx-2 mb-1 mt-4 flex items-center justify-between gap-2 px-2 text-[11px] font-bold text-coast-muted">
              <span>Accounts on this browser</span><span className="text-coast-deep">{accounts.length} / {MAX_DEVICE_ACCOUNTS}</span>
            </div>
            <div className="grid gap-1">
              {accounts.map((account) => {
                const isCurrent = account.id === user?.id
                return (
                  <div className={`flex items-center gap-1 rounded-2xl transition-colors ${isCurrent ? 'bg-coast-sand' : 'hover:bg-coast-sage/70'}`} key={account.id}>
                    <button aria-current={isCurrent ? 'true' : undefined} className={`flex min-h-12 min-w-0 flex-1 items-center gap-3 rounded-2xl px-3 text-left focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue ${isCurrent ? 'cursor-default disabled:cursor-default' : 'cursor-pointer disabled:cursor-wait'}`} disabled={isCurrent || switchingAccountId !== null} onClick={() => void handleSwitchAccount(account.id)} type="button">
                      <span aria-hidden="true" className={`inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-xs font-extrabold ${isCurrent ? 'bg-coast-deep text-white' : 'bg-white text-coast-deep'}`}>{account.fullName.trim().charAt(0).toUpperCase() || 'B'}</span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-extrabold text-coast-ink">{account.fullName}</span>
                        <span className="block truncate text-[11px] text-coast-muted">{account.email}</span>
                      </span>
                      <span className="shrink-0 text-[10px] font-bold text-coast-blue">{isCurrent ? 'Active' : 'Switch'}</span>
                    </button>
                  </div>
                )
              })}
            </div>

            <div aria-hidden="true" className="mx-2 my-2 h-px bg-coast-line" />
            <nav aria-label="Account menu" className="grid gap-1">
              <Link className={accountMenuLinkClass} to="/profile" onClick={() => { setAccountMenuOpen(false); setMenuOpen(false) }}>Profile <span aria-hidden="true">↗</span></Link>
              <Link className={accountMenuLinkClass} to="/dashboard" onClick={() => { setAccountMenuOpen(false); setMenuOpen(false) }}>Dashboard <span aria-hidden="true">↗</span></Link>
              {accountLimitReached ? (
                <p className="mx-4 my-2 text-[11px] leading-5 text-coast-muted">Remove an account from this browser before adding another. The limit is five.</p>
              ) : (
                <>
                  <Link className={accountMenuLinkClass} to={loginHref} onClick={() => { setAccountMenuOpen(false); setMenuOpen(false) }}><span>Add another account</span><span aria-hidden="true" className="rounded-full bg-coast-sage px-2.5 py-1 text-[10px] font-bold text-coast-deep">＋</span></Link>
                  <Link className={accountMenuLinkClass} to={registrationHref} onClick={() => { setAccountMenuOpen(false); setMenuOpen(false) }}><span>New registration</span><span aria-hidden="true" className="rounded-full bg-coast-sage px-2.5 py-1 text-[10px] font-bold text-coast-deep">＋</span></Link>
                </>
              )}
            </nav>

            <div className="my-2 h-px bg-coast-line" />
            {accountError && <p className="mx-2 mb-2 rounded-2xl bg-red-50 px-3 py-2.5 text-xs leading-5 text-red-900" role="alert">{accountError}</p>}
            <button className="flex min-h-11 w-full items-center justify-between rounded-2xl px-4 text-sm font-bold text-coast-muted transition duration-200 hover:bg-red-50 hover:text-red-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue disabled:cursor-wait disabled:opacity-60 motion-reduce:transition-none" disabled={isSigningOut} onClick={() => void handleSignOut()} type="button">
              <span>Sign out from this device</span><span aria-hidden="true" className="text-lg">↗</span>
            </button>
          </div>
        )}
      </div>
    )
  }

  function authLinks(mobile = false) {
    if (status === 'checking') return null
    if (user) return mobile ? null : accountMenu(false)

    return (
      <>
        <Link aria-current={active === 'login' ? 'page' : undefined} className={`${mobile ? 'inline-flex min-h-12 flex-1 items-center justify-center px-4' : 'inline-flex min-h-11 items-center px-4'} rounded-full border border-coast-line bg-white/70 text-sm font-bold text-coast-deep transition duration-200 hover:-translate-y-0.5 hover:border-coast-glass hover:bg-white hover:shadow-sm focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue active:scale-[0.98] motion-reduce:transition-none`} to={loginHref} onClick={() => setMenuOpen(false)}>Sign in</Link>
        <Link aria-current={active === 'register' ? 'page' : undefined} className={`${mobile ? 'inline-flex min-h-12 flex-1 items-center justify-center px-4' : 'inline-flex min-h-11 items-center justify-center px-5'} rounded-full bg-coast-deep text-sm font-bold text-white shadow-sm transition duration-200 hover:-translate-y-0.5 hover:bg-coast-blue hover:shadow-md focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue active:scale-[0.98] motion-reduce:transition-none ${active === 'register' ? 'ring-2 ring-coast-blue ring-offset-2 ring-offset-coast-paper' : ''}`} to={registrationHref} onClick={() => setMenuOpen(false)}>Create account</Link>
      </>
    )
  }

  return (
    <header className={`sticky top-0 z-50 border-b border-coast-line/80 bg-coast-paper/95 text-coast-ink shadow-[0_1px_0_rgba(24,57,76,0.02)] backdrop-blur-md ${compactMobile ? 'h-16 sm:h-[76px]' : 'h-[76px]'}`} ref={headerRef}>
      <div className="mx-auto flex h-full max-w-7xl items-center justify-between gap-3 px-3 sm:gap-5 sm:px-8 lg:px-12">
        <Link className="flex shrink-0 items-center gap-2 rounded-sm text-coast-deep focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-blue sm:gap-2.5" to="/" aria-label="BLUEVERSE home">
          <span className="[&_svg]:h-9 [&_svg]:w-9 sm:[&_svg]:h-10 sm:[&_svg]:w-10"><WaveMark /></span>
          <span className="text-[15px] font-extrabold tracking-[0.13em] sm:text-[17px] sm:tracking-[0.16em]">BLUEVERSE<span className="text-coast-teal">.</span></span>
        </Link>

        {active === 'home' && (
          <nav aria-label="Main navigation" className="hidden items-center gap-2 md:flex lg:gap-4">
            <Link className={navClass(false)} to="#our-story">Our story</Link>
            <Link className={navClass(false)} to="#what-matters">What matters</Link>
            <Link className={navClass(false)} to="#our-coast">Our coast</Link>
          </nav>
        )}

        <div className="hidden shrink-0 items-center gap-2 sm:flex">{authLinks()}</div>
        <div className="flex shrink-0 items-center gap-2 sm:hidden">
          {user && status === 'signed-in' && accountMenu(true)}
          <button aria-controls="mobile-site-menu" aria-expanded={menuOpen} aria-label={menuOpen ? 'Close menu' : 'Open menu'} className="inline-flex h-10 w-10 items-center justify-center rounded-full border border-coast-line bg-white/70 text-coast-deep transition duration-200 hover:-translate-y-0.5 hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue active:scale-95" onClick={() => { setMenuOpen((open) => !open); setAccountMenuOpen(false) }} type="button">
            <span aria-hidden="true" className="text-2xl leading-none">{menuOpen ? '×' : '≡'}</span>
          </button>
        </div>
      </div>

      <nav aria-label="Mobile navigation" className={`absolute left-0 right-0 top-full border-b border-coast-line bg-coast-paper px-5 pb-5 pt-2 shadow-lg transition duration-200 sm:hidden ${menuOpen ? 'visible translate-y-0 opacity-100' : 'invisible -translate-y-2 opacity-0 pointer-events-none'}`} id="mobile-site-menu">
        {active === 'home' ? <>
          <Link className="block border-b border-coast-line py-3 text-sm font-semibold text-coast-ink transition-colors hover:text-coast-blue" to="#our-story" onClick={() => setMenuOpen(false)}>Our story</Link>
          <Link className="block border-b border-coast-line py-3 text-sm font-semibold text-coast-ink transition-colors hover:text-coast-blue" to="#what-matters" onClick={() => setMenuOpen(false)}>What matters</Link>
          <Link className="block border-b border-coast-line py-3 text-sm font-semibold text-coast-ink transition-colors hover:text-coast-blue" to="#our-coast" onClick={() => setMenuOpen(false)}>Our coast</Link>
        </> : <Link className="block border-b border-coast-line py-3 text-sm font-semibold text-coast-ink transition-colors hover:text-coast-blue" to="/" onClick={() => setMenuOpen(false)}>Explore BLUEVERSE</Link>}
        {!user && status !== 'checking' && <div className="mt-4 flex flex-wrap gap-3">{authLinks(true)}</div>}
      </nav>
    </header>
  )
}

export default SiteHeader
