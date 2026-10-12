import type { ReactNode } from 'react'
import { Link, useLocation } from 'react-router'
import SiteHeader from '../../components/layout/SiteHeader'
import SiteFooter from '../../components/layout/SiteFooter'
import AccountAreaNavigation from '../../components/account/AccountAreaNavigation'
import { useAuthSession } from '../auth/authSession'
import { authEntryHrefFor } from '../auth/authNavigation'
import { hasAllPermissions } from '../authorization/permissions'

export const primaryButton = 'inline-flex min-h-12 items-center justify-center gap-3 rounded-full bg-coast-deep px-6 py-3 text-sm font-bold text-white transition hover:-translate-y-0.5 hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-blue disabled:cursor-not-allowed disabled:opacity-50 motion-reduce:transform-none'
export const secondaryButton = 'inline-flex min-h-11 items-center justify-center gap-2 rounded-full border border-coast-line bg-white px-5 py-2 text-sm font-bold text-coast-deep transition hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:opacity-50'
export const field = 'mt-2 min-h-12 w-full rounded-xl border border-coast-line bg-coast-pearl px-4 py-3 text-sm text-coast-ink outline-none transition focus:border-coast-blue focus:ring-2 focus:ring-coast-glass disabled:bg-coast-sage disabled:text-coast-muted'

export default function PlannerLayout({ children, permission = 'planner.recommendations.create', publicRead = false }: { children: ReactNode; permission?: string; publicRead?: boolean }) {
  const { status, user } = useAuthSession()
  const location = useLocation()
  const pageAnimation = location.pathname === '/planner/plan' ? 'motion-safe:animate-planner-create' : location.pathname === '/planner/saved' ? 'motion-safe:animate-planner-history' : ''
  const content = publicRead ? children : status === 'checking' ? null : status !== 'signed-in' ? (
    <section className="mx-auto my-12 max-w-2xl rounded-[2rem] border border-coast-line bg-white p-8 text-center sm:p-14">
      <p className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">Your own coastal day</p>
      <h1 className="mt-4 font-display text-3xl tracking-tight sm:text-4xl">Sign in to plan with the coast.</h1>
      <p className="mt-4 leading-7 text-coast-muted">Keep your plans in one place, shape the details, and come back whenever you need them.</p>
      <Link to={authEntryHrefFor('/signin', `${location.pathname}${location.search}${location.hash}`)} className={`${primaryButton} mt-7`}>Sign in and continue <span aria-hidden="true">↗</span></Link>
    </section>
  ) : !hasAllPermissions(user, [permission]) ? (
    <section className="mx-auto my-12 max-w-2xl rounded-[2rem] border border-coast-line bg-white p-8 sm:p-14">
      <h1 className="font-display text-3xl">Coastal planning access</h1>
      <p className="mt-4 leading-7 text-coast-muted">Ask an administrator to give your account coastal planning access. You can still explore how the planner works and use your profile.</p>
      <div className="mt-7 flex flex-wrap gap-3"><Link to="/planner" className={primaryButton}>Explore the planner</Link><Link to="/profile" className={secondaryButton}>Back to your profile</Link></div>
    </section>
  ) : children
  return <div className="flex min-h-screen flex-col bg-coast-paper font-sans text-coast-ink">
    <SiteHeader active="planner" />
    {publicRead ? <main id="main-content" className="mx-auto w-full max-w-7xl flex-1 px-5 pb-20 pt-8 sm:px-8 sm:pt-10 lg:px-12">{content}</main> : <main id="main-content" className="mx-auto grid w-full max-w-[90rem] flex-1 grid-cols-1 content-start gap-6 px-4 py-6 sm:px-8 sm:py-10 lg:grid-cols-[15rem_minmax(0,1fr)] lg:items-start lg:gap-8 lg:px-6 lg:py-0">
      <AccountAreaNavigation active="planner" />
      <div key={location.pathname} className={`min-w-0 pb-20 lg:pt-10 ${pageAnimation}`}>{content}</div>
    </main>}
    <SiteFooter />
  </div>
}
