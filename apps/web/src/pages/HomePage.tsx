import coastalLivelihood from '../assets/coastal/coastal-livelihood.webp'
import coastHero from '../assets/coastal/coast-hero-daylight.webp'
import mangroveLagoon from '../assets/coastal/mangrove-lagoon.jpg'
import { Link, useLocation } from 'react-router'
import { authEntryHref } from '../features/auth/authNavigation'
import { useAuthSession } from '../features/auth/authSession'
import SiteFooter from '../components/layout/SiteFooter'
import SiteHeader from '../components/layout/SiteHeader'

function HomePage() {
  const { user } = useAuthSession()
  const location = useLocation()
  const loginHref = authEntryHref('/signin', location)
  const registrationHref = authEntryHref('/signup', location)

  return (
    <div className="min-h-screen overflow-clip bg-coast-paper font-sans text-coast-ink" id="top">
      <SiteHeader active="home" />
      <main>
        <section aria-labelledby="home-title" className="relative isolate flex h-[calc(100svh-76px)] min-h-0 items-center overflow-hidden bg-coast-deep text-white">
          <img alt="A quiet palm-lined tropical cove in soft daylight" className="absolute inset-0 -z-20 h-full w-full object-cover object-[56%_center]" fetchPriority="high" src={coastHero} />
          <div aria-hidden="true" className="absolute inset-0 -z-10 bg-gradient-to-r from-coast-ink/85 via-coast-ink/60 to-coast-ink/10" />
          <div aria-hidden="true" className="absolute inset-x-0 bottom-0 -z-10 h-44 bg-gradient-to-t from-coast-ink/35 to-transparent" />
          <div className="mx-auto w-full max-w-7xl px-5 py-4 sm:px-8 sm:py-7 lg:px-12 [@media(max-height:480px)]:py-1">
            <div className="max-w-4xl">
              <p className="flex items-center gap-3 text-[10px] font-extrabold tracking-[0.2em] text-coast-glass sm:text-xs [@media(max-height:480px)]:text-[9px]"><span aria-hidden="true" className="h-px w-7 bg-current" /> THE COAST, MORE CONNECTED</p>
              <h1 className="mt-3 font-display text-[clamp(2rem,calc(6.2svh_+_2.4vw),5.25rem)] leading-[1.02] tracking-[-0.055em] sm:mt-5 sm:leading-[1.04] [@media(max-height:480px)]:mt-2 [@media(max-height:480px)]:text-[clamp(1.75rem,calc(5.4svh_+_1vw),4rem)]" id="home-title">Closer to the coast.<span className="mt-1 block text-coast-glass">Closer to what matters.</span></h1>
              <p className="mt-4 max-w-[38rem] text-sm leading-6 text-white/90 sm:mt-5 sm:text-base sm:leading-7 lg:text-lg lg:leading-8 [@media(max-height:480px)]:mt-2 [@media(max-height:480px)]:text-xs [@media(max-height:480px)]:leading-4">Discover coastal experiences with a deeper understanding of the sea, the people and the living world around them.</p>
              <Link className="mt-5 inline-flex min-h-11 items-center gap-6 rounded-full bg-coast-paper px-5 py-2.5 text-sm font-extrabold text-coast-deep shadow-sm transition duration-200 hover:-translate-y-0.5 hover:bg-white hover:shadow-lg focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-white active:scale-[0.98] motion-reduce:transition-none sm:mt-7 sm:min-h-12 sm:gap-8 sm:px-6 sm:py-3 [@media(max-height:480px)]:mt-3 [@media(max-height:480px)]:min-h-9 [@media(max-height:480px)]:py-1.5" to="#our-story">Discover BLUEVERSE <span aria-hidden="true" className="text-lg">↗</span></Link>
            </div>
          </div>
          <span className="absolute bottom-5 right-6 hidden text-[10px] font-bold tracking-[0.18em] text-white/80 sm:block lg:right-12">BLUEVERSE · SRI LANKA</span>
        </section>

        <section className="mx-auto grid max-w-7xl gap-8 px-5 py-20 sm:px-8 sm:py-24 md:grid-cols-[0.7fr_1.6fr_1fr] md:gap-12 md:py-32 lg:px-12" id="our-story">
          <div className="flex items-start justify-between md:block">
            <span className="text-[11px] font-extrabold tracking-[0.16em] text-coast-muted">01 / THE IDEA</span>
            <span aria-hidden="true" className="font-display text-5xl leading-none text-coast-glass md:mt-10 md:block">〜</span>
          </div>
          <div>
            <p className="text-[11px] font-extrabold tracking-[0.18em] text-coast-blue">A FRESH PERSPECTIVE ON THE SHORE</p>
            <h2 className="mt-4 max-w-3xl font-display text-4xl leading-[1.14] tracking-[-0.045em] sm:text-5xl lg:text-[3.5rem]">The best coastal days begin with <span className="text-coast-blue">a little more understanding.</span></h2>
          </div>
          <div className="border-l border-coast-line pl-5 text-[15px] leading-7 text-coast-muted sm:pl-7 md:mt-8">
            <p>BLUEVERSE brings coastal discovery, marine awareness and care for the environment into one shared experience.</p>
            <p className="mt-4">It is being built for everyone who visits, works along, or helps look after our coast.</p>
          </div>
        </section>

        <section className="bg-coast-sage py-20 sm:py-24 md:py-28" id="what-matters">
          <div className="mx-auto max-w-7xl px-5 sm:px-8 lg:px-12">
            <div className="mb-10 flex flex-col gap-5 md:mb-12 md:flex-row md:items-end md:justify-between">
              <div>
                <p className="text-[11px] font-extrabold tracking-[0.18em] text-coast-blue">THREE WAYS TO SEE THE COAST DIFFERENTLY</p>
                <h2 className="mt-3 font-display text-4xl tracking-[-0.045em] sm:text-5xl">More than a destination.</h2>
              </div>
              <p className="max-w-sm text-sm leading-6 text-coast-muted">A connected view of the coast helps every decision feel a little more considered.</p>
            </div>

            <div className="grid gap-4 md:grid-cols-2 md:gap-5">
              <article className="group relative isolate flex min-h-[410px] flex-col justify-end overflow-hidden rounded-[1.75rem] bg-coast-deep p-7 text-white shadow-[0_14px_38px_rgba(24,57,76,0.1)] transition duration-300 hover:-translate-y-1 hover:shadow-[0_22px_48px_rgba(24,57,76,0.18)] motion-reduce:transition-none sm:p-9 md:row-span-2 md:min-h-[540px]">
                <img alt="A small wooden fishing boat resting in clear coastal water" className="absolute inset-0 -z-20 h-full w-full object-cover transition duration-700 group-hover:scale-[1.035] motion-reduce:transition-none" loading="lazy" src={coastalLivelihood} />
                <div aria-hidden="true" className="absolute inset-0 -z-10 bg-gradient-to-t from-coast-ink/90 via-coast-ink/40 to-coast-ink/5" />
                <p className="text-[11px] font-extrabold tracking-[0.16em] text-coast-glass">01 / COASTAL DISCOVERY</p>
                <h3 className="mt-4 max-w-lg font-display text-3xl leading-tight tracking-[-0.04em] sm:text-4xl">Find more than a place to go.</h3>
                <p className="mt-3 max-w-lg text-sm leading-6 text-white/90">Explore destinations and experiences alongside the marine life and local context that make each stretch of coast distinct.</p>
              </article>

              <article className="group flex min-h-[250px] flex-col justify-between rounded-[1.75rem] border border-coast-line/80 bg-coast-paper p-7 shadow-[0_8px_24px_rgba(24,57,76,0.035)] transition duration-300 hover:-translate-y-1 hover:border-coast-glass hover:shadow-[0_18px_38px_rgba(24,57,76,0.08)] motion-reduce:transition-none sm:p-9">
                <div className="flex items-start justify-between gap-4">
                  <p className="text-[11px] font-extrabold tracking-[0.16em] text-coast-blue">02 / MARINE AWARENESS</p>
                  <span className="inline-flex h-12 w-12 items-center justify-center rounded-full bg-coast-sage text-coast-blue transition-transform duration-300 group-hover:-rotate-6 group-hover:scale-105 motion-reduce:transition-none"><svg aria-hidden="true" className="h-8 w-8" viewBox="0 0 40 40" fill="none"><path d="M4 14c5 0 5-5 10-5s5 5 10 5 5-5 12-5M4 23c5 0 5-5 10-5s5 5 10 5 5-5 12-5M4 32c5 0 5-5 10-5s5 5 10 5 5-5 12-5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" /></svg></span>
                </div>
                <div>
                  <h3 className="font-display text-3xl leading-tight tracking-[-0.04em]">Know the coast before you go.</h3>
                  <p className="mt-3 max-w-lg text-sm leading-6 text-coast-muted">Bring marine conditions and practical safety context into the same decision as the activity you have in mind.</p>
                </div>
              </article>

              <article className="group flex min-h-[250px] flex-col justify-between rounded-[1.75rem] bg-coast-deep p-7 text-white shadow-[0_12px_32px_rgba(24,57,76,0.09)] transition duration-300 hover:-translate-y-1 hover:shadow-[0_20px_42px_rgba(24,57,76,0.16)] motion-reduce:transition-none sm:p-9">
                <div className="flex items-start justify-between gap-4">
                  <p className="text-[11px] font-extrabold tracking-[0.16em] text-coast-glass">03 / SHARED STEWARDSHIP</p>
                  <span className="inline-flex h-12 w-12 items-center justify-center rounded-full bg-white/10 text-coast-glass transition-transform duration-300 group-hover:rotate-6 group-hover:scale-105 motion-reduce:transition-none"><svg aria-hidden="true" className="h-8 w-8" viewBox="0 0 40 40" fill="none"><path d="M31 9c-12 0-21 5-22 17 8 0 15-2 19-7M12 31c2-6 7-12 15-18" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" /><path d="M9 27c0 2 1 4 3 5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" /></svg></span>
                </div>
                <div>
                  <h3 className="font-display text-3xl leading-tight tracking-[-0.04em]">Make room for a healthier coast.</h3>
                  <p className="mt-3 max-w-lg text-sm leading-6 text-white/80">Connect visitors, coastal operators and reviewers around more thoughtful decisions for people and the shore.</p>
                </div>
              </article>
            </div>
          </div>
        </section>

        <section className="mx-auto grid max-w-7xl gap-10 px-5 py-20 sm:px-8 sm:py-24 md:grid-cols-2 md:items-center md:gap-16 md:py-32 lg:px-12" id="our-coast">
          <figure className="group relative m-0 overflow-hidden rounded-[1.75rem] bg-coast-glass shadow-[0_18px_42px_rgba(24,57,76,0.1)]">
            <img alt="Mangrove roots meeting clear blue-green water along a tropical coastal lagoon" className="aspect-[4/5] w-full object-cover transition-transform duration-700 group-hover:scale-[1.025] motion-reduce:transition-none sm:aspect-[5/4] md:aspect-[4/5]" loading="lazy" src={mangroveLagoon} />
            <figcaption className="absolute bottom-4 left-4 rounded-sm bg-coast-paper/95 px-3 py-2 text-[10px] font-extrabold tracking-[0.14em] text-coast-deep">LIFE ALONG THE SHORE</figcaption>
          </figure>
          <div className="max-w-xl">
            <span className="text-[11px] font-extrabold tracking-[0.16em] text-coast-muted">02 / OUR COAST</span>
            <p className="mt-9 text-[11px] font-extrabold tracking-[0.18em] text-coast-blue">ROOTED IN REAL PLACES</p>
            <h2 className="mt-3 font-display text-4xl leading-[1.12] tracking-[-0.045em] sm:text-5xl lg:text-[3.5rem]">The sea is never <span className="text-coast-blue">just the view.</span></h2>
            <p className="mt-6 text-[15px] leading-7 text-coast-muted">Conditions change. Marine life matters. Coastal livelihoods shape each place. BLUEVERSE is designed to bring these stories together, so the next journey or operational choice starts with a fuller picture.</p>
            <div aria-hidden="true" className="my-7 h-px w-14 bg-coast-blue/60" />
            <p className="max-w-sm font-display text-xl leading-7 text-coast-deep">Made for coastal visitors, local teams and the people caring for what comes next.</p>
          </div>
        </section>

        <section aria-labelledby="continue-title" className="relative isolate scroll-mt-20 overflow-hidden bg-coast-deep py-16 text-white sm:py-20 md:py-24" id="join-the-coast">
          <div aria-hidden="true" className="absolute inset-0 -z-10 bg-[radial-gradient(ellipse_at_85%_10%,rgba(201,224,230,0.26),transparent_46%)]" />
          <div className="mx-auto grid max-w-7xl gap-8 px-5 sm:px-8 md:grid-cols-[1.1fr_0.9fr] md:items-center md:gap-14 lg:px-12">
            <div>
              <p className="text-[11px] font-extrabold tracking-[0.18em] text-coast-glass">A CLEARER CONNECTION TO THE SHORE</p>
              <h2 className="mt-4 max-w-2xl font-display text-4xl leading-tight tracking-[-0.045em] sm:text-5xl" id="continue-title">A more thoughtful way forward.</h2>
              <p className="mt-5 max-w-xl text-[15px] leading-7 text-white/80">Come closer to the coast with a platform built for informed experiences and shared responsibility.</p>
              {!user && <Link className="mt-7 inline-flex min-h-12 items-center gap-5 rounded-full bg-coast-paper px-6 py-3 text-sm font-extrabold text-coast-deep shadow-sm transition duration-200 hover:-translate-y-0.5 hover:bg-white hover:shadow-lg focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-white active:scale-[0.98] motion-reduce:transition-none" to={loginHref}>Sign in to your account <span aria-hidden="true">↗</span></Link>}
              {!user && <p className="mt-4 text-sm text-white/70">New to BLUEVERSE? <Link className="font-bold text-white underline decoration-coast-glass underline-offset-4 hover:text-coast-glass" to={registrationHref}>Create an account</Link></p>}
            </div>

            {user ? (
              <div className="rounded-3xl border border-white/15 bg-white/[0.08] p-7 backdrop-blur-sm sm:p-9">
                <p className="text-[11px] font-extrabold tracking-[0.16em] text-coast-glass">WELCOME BACK</p>
                <h3 className="mt-3 font-display text-3xl leading-tight tracking-[-0.04em]">Good to have you here, {user.fullName.split(' ')[0]}.</h3>
                <p className="mt-3 text-sm leading-6 text-white/75">Your coastal overview is ready whenever you are.</p>
                <div className="mt-6 flex flex-wrap gap-3">
                  <Link className="inline-flex min-h-11 items-center rounded-full bg-coast-paper px-5 text-sm font-extrabold text-coast-deep transition duration-200 hover:-translate-y-0.5 hover:bg-white hover:shadow-md focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-white" to="/dashboard">Open your overview</Link>
                  <Link className="inline-flex min-h-11 items-center rounded-full border border-white/25 px-5 text-sm font-bold text-white transition duration-200 hover:-translate-y-0.5 hover:bg-white/10 focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-white" to="/profile">View your profile</Link>
                </div>
              </div>
            ) : (
              <div className="flex min-h-64 flex-col justify-center rounded-sm border border-white/15 bg-white/[0.06] p-7 sm:p-9">
                <span aria-hidden="true" className="font-display text-6xl leading-none text-coast-glass">〜</span>
                <p className="mt-5 max-w-sm font-display text-2xl leading-tight">Good journeys begin with curiosity.</p>
                <span className="mt-2 text-sm leading-6 text-white/70">Explore the idea. Stay for the coast.</span>
              </div>
            )}
          </div>
        </section>
      </main>
      <SiteFooter />
    </div>
  )
}

export default HomePage
