import type { ReactNode } from 'react'
import { Link, useLocation } from 'react-router'
import SiteHeader from '../../components/layout/SiteHeader'
import SiteFooter from '../../components/layout/SiteFooter'
import { useAuthSession } from '../auth/authSession'
import { hasAllPermissions } from '../authorization/permissions'

export const primaryButton = 'inline-flex min-h-12 items-center justify-center gap-2 rounded-full bg-coast-deep px-6 py-3 text-sm font-bold text-white transition hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-blue disabled:cursor-not-allowed disabled:opacity-50'
export const secondaryButton = 'inline-flex min-h-11 items-center justify-center rounded-full border border-coast-line bg-white px-5 py-2 text-sm font-bold text-coast-deep transition hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:opacity-50'
export const field = 'mt-2 min-h-12 w-full rounded-xl border border-coast-line bg-white px-4 py-3 text-sm text-coast-ink outline-none focus:border-coast-blue focus:ring-2 focus:ring-coast-glass disabled:bg-coast-sage disabled:text-coast-muted'

export default function PlannerLayout({ children, permission = 'planner.recommendations.create', publicRead = false }: { children: ReactNode; permission?: string; publicRead?: boolean }) {
  const { status, user } = useAuthSession()
  const { pathname } = useLocation()
  return <><SiteHeader /><main id="main-content" className="min-h-[70vh] bg-coast-paper text-coast-ink">
    <div className="mx-auto max-w-7xl px-5 pb-16 pt-6 sm:px-8 lg:px-12">
      <nav aria-label="Coastal planning" className="mb-8 flex flex-wrap items-center justify-between gap-4 border-b border-coast-line pb-5">
        <Link to="/planner/plan" className="font-display text-lg font-bold tracking-tight">The coastal planner<span className="ml-2 text-coast-teal">↗</span></Link>
        <div className="flex rounded-full border border-coast-line bg-white p-1 text-sm font-bold">
          {hasAllPermissions(user, ['planner.recommendations.create']) && <Link to="/planner/plan" aria-current={pathname === '/planner/plan' ? 'page' : undefined} className={`rounded-full px-5 py-2 focus-visible:outline-2 focus-visible:outline-coast-blue ${pathname === '/planner/plan' ? 'bg-coast-deep text-white' : 'text-coast-muted hover:text-coast-deep'}`}>Plan a trip</Link>}
          {hasAllPermissions(user, ['planner.itineraries.manage']) && <Link to="/planner/saved" aria-current={pathname.includes('/saved') || pathname.includes('/itineraries') ? 'page' : undefined} className={`rounded-full px-5 py-2 focus-visible:outline-2 focus-visible:outline-coast-blue ${pathname.includes('/saved') || pathname.includes('/itineraries') ? 'bg-coast-deep text-white' : 'text-coast-muted hover:text-coast-deep'}`}>Saved trips</Link>}
        </div>
      </nav>
      {publicRead ? (status === 'checking' ? null : !hasAllPermissions(user, [permission]) ? <section className="rounded-3xl border border-coast-line bg-white p-10"><h1 className="font-display text-3xl">Coastal planning access</h1><p className="mt-3 max-w-xl text-coast-muted">Ask an administrator to give your account coastal planning access. Your account and profile are still available.</p><Link to="/profile" className={`${secondaryButton} mt-6`}>Back to your profile</Link></section> : children) : (status === 'checking' ? null : status !== 'signed-in' ? <section className="rounded-3xl bg-white p-10"><h1 className="font-display text-3xl">Sign in to plan with the coast.</h1><p className="mt-3 text-coast-muted">Recommendations are generated for your account so saved plans stay yours.</p><Link to="/signin" className={`${primaryButton} mt-6`}>Sign in</Link></section> : !hasAllPermissions(user, [permission]) ? <section className="rounded-3xl border border-coast-line bg-white p-10"><h1 className="font-display text-3xl">Coastal planning access</h1><p className="mt-3 max-w-xl text-coast-muted">Ask an administrator to give your account coastal planning access. Your account and profile are still available.</p><Link to="/profile" className={`${secondaryButton} mt-6`}>Back to your profile</Link></section> : children)}
    </div></main><SiteFooter /></>
}
