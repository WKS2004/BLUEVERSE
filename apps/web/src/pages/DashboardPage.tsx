import { Link } from 'react-router'
import { authEntryHrefFor } from '../features/auth/authNavigation'
import { useAuthSession } from '../features/auth/authSession'
import AccountAreaNavigation from '../components/account/AccountAreaNavigation'
import SiteFooter from '../components/layout/SiteFooter'
import SiteHeader from '../components/layout/SiteHeader'

const coastalFocus = [
  { number: '01', title: 'Coastal discovery', copy: 'Places and experiences understood through the communities and marine life around them.', icon: '◌' },
  { number: '02', title: 'Marine awareness', copy: 'Clearer context for changing conditions and more considered days by the water.', icon: '≈' },
  { number: '03', title: 'Shared stewardship', copy: 'A connected view for visitors, local teams and people caring for the shore.', icon: '⌁' },
]

function DashboardPage() {
  const { user, status } = useAuthSession()

  return (
    <div className="flex min-h-screen flex-col bg-coast-paper font-sans text-coast-ink" id="top">
      <SiteHeader active="dashboard" />
      <main className="mx-auto grid w-full max-w-[90rem] flex-1 grid-cols-1 content-start gap-6 px-4 py-6 sm:px-8 sm:py-10 lg:grid-cols-[15rem_minmax(0,1fr)] lg:items-start lg:gap-8 lg:px-6 lg:py-0">
        <AccountAreaNavigation active="dashboard" />
        <div className="min-w-0 lg:py-12">
          <section aria-labelledby="dashboard-title" className="relative isolate overflow-hidden rounded-[2rem] bg-coast-deep text-white shadow-sm" id="overview">
            <div aria-hidden="true" className="absolute inset-0 -z-10 bg-[radial-gradient(ellipse_at_85%_10%,rgba(201,224,230,0.32),transparent_42%)]" />
            <div className="px-5 py-8 sm:px-8 sm:py-10 lg:px-10 lg:py-12">
              <p className="text-[11px] font-extrabold tracking-[0.18em] text-coast-glass">YOUR BLUEVERSE</p>
              <h1 className="mt-3 max-w-3xl font-display text-4xl leading-tight tracking-[-0.05em] sm:text-5xl" id="dashboard-title">A clearer view of your coastal journey.</h1>
              <p className="mt-4 max-w-2xl text-sm leading-6 text-white/80 sm:text-base sm:leading-7">Your home for account details today, with room for coastal experiences as they become available.</p>
            </div>
          </section>

          {status === 'checking' ? null : !user ? (
            <section className="mt-5 rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-sm sm:p-9">
              <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">ACCOUNT OVERVIEW</p>
              <h2 className="mt-3 font-display text-3xl tracking-[-0.04em]">Sign in to make this space yours.</h2>
              <p className="mt-3 max-w-xl text-sm leading-6 text-coast-muted">{status === 'unavailable' ? 'We couldn’t check your account just now. Sign in again or try in a little while.' : 'Your account summary and coastal areas of focus will appear here after sign-in.'}</p>
              <div className="mt-6 flex flex-wrap gap-3">
                <Link className="inline-flex min-h-11 items-center justify-center rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white transition duration-200 hover:-translate-y-0.5 hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue" to={authEntryHrefFor('/signin', '/dashboard')}>Sign in</Link>
                <Link className="inline-flex min-h-11 items-center justify-center rounded-full border border-coast-line px-5 text-sm font-bold text-coast-deep transition duration-200 hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue" to="/signup">Create account</Link>
              </div>
            </section>
          ) : (
            <>
              <section aria-label="Account and quick links" className="mt-5 grid gap-4 md:grid-cols-2">
                <article className="flex min-h-48 flex-col justify-between rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-[0_12px_36px_rgba(24,57,76,0.05)] transition duration-300 hover:-translate-y-1 hover:shadow-[0_18px_42px_rgba(24,57,76,0.09)] motion-reduce:transition-none sm:p-7">
                  <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">YOUR ACCOUNT</p>
                  <div className="mt-5">
                    <h2 className="break-words font-display text-2xl tracking-[-0.035em] [overflow-wrap:anywhere]">Welcome, {user.fullName.split(' ')[0]}.</h2>
                    <p className="mt-2 truncate text-sm text-coast-muted">{user.email}</p>
                  </div>
                  <Link className="mt-5 inline-flex min-h-10 w-fit items-center gap-2 rounded-full px-3 text-sm font-bold text-coast-deep transition-colors hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue" to="/profile#personal-details">Review your profile <span aria-hidden="true">→</span></Link>
                </article>

                <article className="flex min-h-48 flex-col justify-between rounded-3xl bg-coast-sage p-6 sm:p-7">
                  <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">QUICK ACCESS</p>
                  <div className="mt-5">
                    <p className="font-display text-2xl tracking-[-0.035em]">A little closer to the coast.</p>
                    <p className="mt-2 text-sm leading-6 text-coast-muted">Explore BLUEVERSE’s shared coastal perspective and the ideas behind it.</p>
                  </div>
                  <Link className="mt-5 inline-flex min-h-10 w-fit items-center gap-2 rounded-full px-3 text-sm font-bold text-coast-deep transition-colors hover:bg-white/75 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue" to="/#our-coast">Explore the coast <span aria-hidden="true">→</span></Link>
                </article>
              </section>

              <section aria-labelledby="dashboard-focus-title" className="mt-10 scroll-mt-24 sm:mt-14" id="coastal-focus">
                <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
                  <div>
                    <p className="text-[11px] font-extrabold tracking-[0.18em] text-coast-blue">WHAT’S AHEAD</p>
                    <h2 className="mt-2 font-display text-3xl tracking-[-0.045em] sm:text-4xl" id="dashboard-focus-title">A coast with more context.</h2>
                  </div>
                  <p className="max-w-md text-sm leading-6 text-coast-muted">These are BLUEVERSE’s areas of focus. Coastal service updates will appear here when they are ready.</p>
                </div>
                <div className="grid gap-4 md:grid-cols-3">
                  {coastalFocus.map((item) => (
                    <article className="group rounded-3xl border border-coast-line bg-coast-pearl p-6 transition duration-300 hover:-translate-y-1 hover:border-coast-glass hover:shadow-[0_18px_42px_rgba(24,57,76,0.08)] motion-reduce:transition-none sm:p-7" key={item.number}>
                      <div className="flex items-center justify-between gap-4">
                        <span aria-hidden="true" className="inline-flex h-11 w-11 items-center justify-center rounded-full bg-coast-sage font-display text-2xl text-coast-blue transition-transform duration-300 group-hover:scale-110 motion-reduce:transition-none">{item.icon}</span>
                        <span className="text-[11px] font-extrabold tracking-[0.15em] text-coast-muted">{item.number}</span>
                      </div>
                      <h3 className="mt-6 font-display text-2xl tracking-[-0.035em]">{item.title}</h3>
                      <p className="mt-3 min-h-12 text-sm leading-6 text-coast-muted">{item.copy}</p>
                      <p className="mt-5 inline-flex items-center gap-2 rounded-full bg-coast-sand px-3 py-2 text-xs font-bold text-coast-deep"><span aria-hidden="true" className="h-1.5 w-1.5 rounded-full bg-coast-teal" /> Coming into view</p>
                    </article>
                  ))}
                </div>
              </section>
            </>
          )}
        </div>
      </main>
      <SiteFooter />
    </div>
  )
}

export default DashboardPage
