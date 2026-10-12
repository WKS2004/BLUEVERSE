import { useCallback, useLayoutEffect, useRef } from 'react'
import type { ReactNode } from 'react'
import { Outlet, useLocation } from 'react-router'
import AccountAreaNavigation from '../../components/account/AccountAreaNavigation'
import SiteFooter from '../../components/layout/SiteFooter'
import SiteHeader from '../../components/layout/SiteHeader'
import { MarineInitialLoadingContext } from '../../features/marine/marineWorkspaceContext'
import MarineWorkflowNavigation from './MarineWorkflowNavigation'

export default function MarineWorkspaceLayout({ children }: { children?: ReactNode }) {
  const location = useLocation()
  const visit = useRef<{ key: string | null; switched: boolean }>({ key: null, switched: false })
  useLayoutEffect(() => {
    if (visit.current.key !== null && visit.current.key !== location.key) visit.current.switched = true
    visit.current.key = location.key
  }, [location.key])
  const showInitialLoadingScreen = useCallback(() => !visit.current.switched, [])

  return <MarineInitialLoadingContext.Provider value={showInitialLoadingScreen}>
    <div className="flex min-h-screen flex-col bg-coast-paper font-sans text-coast-ink">
      <SiteHeader />
      <main className="mx-auto grid w-full max-w-[90rem] flex-1 grid-cols-1 content-start gap-6 px-4 py-6 sm:px-8 sm:py-10 lg:grid-cols-[15rem_minmax(0,1fr)] lg:items-start lg:gap-8 lg:px-6 lg:py-0">
        <AccountAreaNavigation active="marine" />
        <div className="min-w-0 lg:py-12">
          <header className="mb-5">
            <p className="text-[11px] font-extrabold tracking-[0.18em] text-coast-blue">MARINE CONDITIONS &amp; SAFETY</p>
            <h1 className="mt-2 break-words font-display text-4xl leading-tight tracking-[-0.05em] sm:text-5xl">Marine &amp; safety</h1>
            <p className="mt-3 max-w-3xl text-sm leading-6 text-coast-muted">Read coastal conditions, revisit saved evidence, and manage reviewed activity safety limits.</p>
          </header>
          <div className="scroll-mt-[76px]" id="marine-workflow-start" />
          <MarineWorkflowNavigation />
          <div className="min-w-0 motion-safe:animate-coast-tab-change" data-marine-workflow={location.pathname} key={location.pathname}>
            {children ?? <Outlet />}
          </div>
        </div>
      </main>
      <SiteFooter />
    </div>
  </MarineInitialLoadingContext.Provider>
}
