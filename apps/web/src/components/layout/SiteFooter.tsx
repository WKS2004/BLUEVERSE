import { useEffect, useRef, useState } from 'react'
import { Link, useLocation } from 'react-router'
import { authEntryHref } from '../../features/auth/authNavigation'
import { useAuthSession } from '../../features/auth/authSession'

export default function SiteFooter({ compact = false, hideForShortScreens = false }: { compact?: boolean; hideForShortScreens?: boolean }) {
  const [showBackToTop, setShowBackToTop] = useState(false)
  const [animateArrow, setAnimateArrow] = useState(false)
  const animationTimer = useRef<number | null>(null)
  const { user } = useAuthSession()
  const location = useLocation()
  const homePrefix = location.pathname === '/' ? '' : '/'
  const loginHref = authEntryHref('/signin', location)
  const registrationHref = authEntryHref('/signup', location)

  useEffect(() => {
    function updateVisibility() {
      const maxScroll = document.documentElement.scrollHeight - window.innerHeight
      setShowBackToTop(maxScroll > 0 && window.scrollY >= maxScroll * 0.12)
    }

    updateVisibility()
    window.addEventListener('scroll', updateVisibility, { passive: true })
    window.addEventListener('resize', updateVisibility)

    return () => {
      window.removeEventListener('scroll', updateVisibility)
      window.removeEventListener('resize', updateVisibility)
      if (animationTimer.current !== null) window.clearTimeout(animationTimer.current)
    }
  }, [])

  function backToTop() {
    setAnimateArrow(true)
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    window.scrollTo({ top: 0, behavior: prefersReducedMotion ? 'instant' : 'smooth' })
    if (animationTimer.current !== null) window.clearTimeout(animationTimer.current)
    animationTimer.current = window.setTimeout(() => setAnimateArrow(false), 700)
  }

  const backToTopButton = (
    <button
      aria-hidden={!showBackToTop}
      aria-label="Back to top"
      className={`group fixed bottom-5 right-5 z-40 inline-flex h-12 w-12 items-center justify-center rounded-full border border-coast-line bg-coast-pearl text-coast-deep shadow-[0_8px_24px_rgba(24,57,76,0.16)] transition duration-300 hover:-translate-y-1 hover:bg-coast-glass hover:shadow-[0_12px_28px_rgba(24,57,76,0.2)] focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-blue motion-reduce:transition-none sm:bottom-7 sm:right-7 ${showBackToTop ? 'visible translate-y-0 opacity-100' : 'invisible translate-y-3 opacity-0 pointer-events-none'}`}
      onClick={(event) => { event.currentTarget.blur(); backToTop() }}
      tabIndex={showBackToTop ? 0 : -1}
      type="button"
    >
      <span aria-hidden="true" className={`text-xl leading-none transition-transform duration-200 group-hover:-translate-y-0.5 motion-reduce:transition-none ${animateArrow ? 'animate-bounce motion-reduce:animate-none' : ''}`}>↑</span>
    </button>
  )

  if (compact) {
    return (
      <>
        <footer className={`border-t border-coast-line/80 bg-coast-paper px-4 py-2.5 text-center text-[11px] text-coast-muted [@media(max-height:700px)]:py-1 ${hideForShortScreens ? '[@media(max-height:700px)]:hidden' : ''}`}>
          © {new Date().getFullYear()} BLUEVERSE <span aria-hidden="true">·</span> Made with care for the coast.
        </footer>
        {backToTopButton}
      </>
    )
  }

  return (
    <>
      <footer className="relative isolate overflow-hidden bg-coast-ink text-white">
        <div aria-hidden="true" className="absolute inset-0 -z-10 bg-[radial-gradient(ellipse_at_88%_100%,rgba(52,123,155,0.38),transparent_48%)]" />
        <div className="mx-auto grid max-w-7xl gap-10 px-5 py-12 sm:px-8 sm:py-14 md:grid-cols-[1.5fr_0.8fr_0.9fr_0.8fr] md:gap-8 lg:px-12 lg:py-16">
          <div className="max-w-sm">
            <Link className="group inline-flex items-center gap-2.5 rounded-sm text-white focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-glass" to="/" aria-label="BLUEVERSE home">
              <svg viewBox="0 0 44 44" fill="none" aria-hidden="true" className="h-10 w-10 shrink-0 text-coast-glass transition-transform duration-300 group-hover:-rotate-3 group-hover:scale-105 motion-reduce:transition-none">
                <circle cx="22" cy="22" r="20.5" stroke="currentColor" strokeWidth="1.5" />
                <path d="M7 25c5.2 0 5.2-4.2 10.4-4.2S22.6 25 27.8 25 33 20.8 38 20.8M7 31c5.2 0 5.2-4.2 10.4-4.2S22.6 31 27.8 31 33 26.8 38 26.8" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" />
                <circle cx="28.5" cy="13" r="3" fill="currentColor" />
              </svg>
              <span className="text-base font-extrabold tracking-[0.16em]">BLUEVERSE<span className="text-coast-glass">.</span></span>
            </Link>
            <p className="mt-5 text-sm leading-6 text-white/75">A clearer view of coastal places, marine life and the people who make the shore what it is.</p>
            <p className="mt-4 text-xs font-semibold tracking-wide text-coast-glass">For the coast, and everyone connected to it.</p>
          </div>

          <nav aria-label="Explore BLUEVERSE">
            <h2 className="text-xs font-extrabold tracking-[0.15em] text-coast-glass">EXPLORE</h2>
            <ul className="mt-4 grid gap-3 text-sm text-white/80">
              <li><Link className="transition-colors hover:text-white focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-glass" to={`${homePrefix}#our-story`}>Our story</Link></li>
              <li><Link className="transition-colors hover:text-white focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-glass" to={`${homePrefix}#what-matters`}>What matters</Link></li>
              <li><Link className="transition-colors hover:text-white focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-glass" to={`${homePrefix}#our-coast`}>Our coast</Link></li>
            </ul>
          </nav>

          <nav aria-label="Our coastal focus">
            <h2 className="text-xs font-extrabold tracking-[0.15em] text-coast-glass">OUR COASTAL FOCUS</h2>
            <ul className="mt-4 grid gap-3 text-sm text-white/80">
              <li><Link className="transition-colors hover:text-white focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-glass" to={`${homePrefix}#what-matters`}>Coastal discovery</Link></li>
              <li><Link className="transition-colors hover:text-white focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-glass" to={`${homePrefix}#what-matters`}>Marine awareness</Link></li>
              <li><Link className="transition-colors hover:text-white focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-glass" to={`${homePrefix}#our-coast`}>Shared stewardship</Link></li>
            </ul>
          </nav>

          <nav aria-label="Your BLUEVERSE account">
            <h2 className="text-xs font-extrabold tracking-[0.15em] text-coast-glass">YOUR BLUEVERSE</h2>
            <ul className="mt-4 grid gap-3 text-sm text-white/80">
              {user ? <>
                <li><Link className="transition-colors hover:text-white focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-glass" to="/profile">Profile</Link></li>
                <li><Link className="transition-colors hover:text-white focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-glass" to="/dashboard">Dashboard</Link></li>
              </> : <>
                <li><Link className="transition-colors hover:text-white focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-glass" to={loginHref}>Sign in</Link></li>
                <li><Link className="transition-colors hover:text-white focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-glass" to={registrationHref}>Create account</Link></li>
              </>}
            </ul>
          </nav>
        </div>

        <div className="mx-auto flex max-w-7xl flex-col gap-2 border-t border-white/15 px-5 py-5 text-xs text-white/60 sm:px-8 md:flex-row md:items-center md:justify-between lg:px-12">
          <p>© {new Date().getFullYear()} BLUEVERSE</p>
          <p>Made with care for the coast.</p>
        </div>
      </footer>

      {backToTopButton}
    </>
  )
}
