import { Link } from 'react-router'
import SiteFooter from '../components/layout/SiteFooter'
import SiteHeader from '../components/layout/SiteHeader'

type GlobalErrorPageProps = {
  kind: 'not-found' | 'server'
  onRetry?: () => void
}

function ErrorLandscape({ code }: { code: '404' | '500' }) {
  return (
    <div aria-hidden="true" className="relative mx-auto aspect-[1.12] w-full max-w-[34rem] overflow-hidden rounded-[2.5rem] border border-white/60 bg-[linear-gradient(150deg,#e4f0f1_0%,#f7f6f0_58%,#d6e8e9_100%)] shadow-[0_35px_90px_rgba(24,57,76,0.16)]">
      <div className="absolute -right-12 -top-14 h-52 w-52 rounded-full bg-white/45 blur-2xl" />
      <div className="absolute right-[17%] top-[13%] h-20 w-20 rounded-full bg-[#d3e6dc] shadow-[0_0_55px_rgba(211,230,220,0.9)] sm:h-24 sm:w-24" />
      <div className="absolute left-[12%] top-[14%] rounded-full border border-white/75 bg-white/55 px-4 py-2 text-[10px] font-extrabold tracking-[0.22em] text-coast-blue shadow-sm backdrop-blur-sm">
        BLUEVERSE · COASTAL NETWORK
      </div>
      <div className="absolute inset-x-0 bottom-0 h-[68%] bg-[linear-gradient(180deg,rgba(105,169,169,0.05),rgba(52,123,155,0.22))]" />
      <svg className="absolute inset-x-0 bottom-0 h-[76%] w-full" fill="none" preserveAspectRatio="none" viewBox="0 0 600 420">
        <path d="M0 112C85 79 143 145 226 114S371 73 451 111s108 25 149 4v305H0V112Z" fill="#8dbfc1" fillOpacity=".52" />
        <path d="M0 162c75-35 142 28 221 0s143-36 222-4 110 20 157-5v267H0V162Z" fill="#4d94a5" fillOpacity=".62" />
        <path d="M0 222c69-32 129 25 204 0s139-29 210-2 120 22 186-9v209H0V222Z" fill="#205c79" fillOpacity=".92" />
        <path d="M0 162c75-35 142 28 221 0s143-36 222-4 110 20 157-5" stroke="#f7f6f0" strokeOpacity=".8" strokeWidth="3" />
        <path d="M0 235c69-32 129 25 204 0s139-29 210-2 120 22 186-9" stroke="#c9e0e6" strokeOpacity=".8" strokeWidth="2" />
      </svg>
      <div className="absolute inset-x-0 top-[35%] flex items-center justify-center">
        <div className="grid h-40 w-40 rotate-[-5deg] place-items-center rounded-[2rem] border border-white/70 bg-white/65 shadow-[0_24px_60px_rgba(24,57,76,0.14)] backdrop-blur-md sm:h-48 sm:w-48">
          <span className="font-display text-6xl font-semibold tracking-[-0.09em] text-coast-deep sm:text-7xl">{code}</span>
        </div>
      </div>
      <div className="absolute bottom-[12%] left-[12%] h-2 w-2 rounded-full bg-white/90 shadow-[0_0_14px_5px_rgba(255,255,255,0.65)]" />
      <div className="absolute bottom-[19%] right-[15%] h-1.5 w-1.5 rounded-full bg-white/85 shadow-[0_0_12px_4px_rgba(255,255,255,0.6)]" />
    </div>
  )
}

export function NotFoundPage() {
  return <GlobalErrorPage kind="not-found" />
}

export function ServerErrorPage({ onRetry }: { onRetry?: () => void }) {
  return <GlobalErrorPage kind="server" onRetry={onRetry} />
}

function GlobalErrorPage({ kind, onRetry }: GlobalErrorPageProps) {
  const isNotFound = kind === 'not-found'

  return (
    <div className="flex min-h-screen flex-col bg-coast-paper text-coast-ink">
      <SiteHeader active="home" />
      <main className="relative isolate flex flex-1 flex-col overflow-hidden bg-coast-paper px-4 py-6 sm:px-8 sm:py-9">
        <div aria-hidden="true" className="pointer-events-none absolute -left-48 top-1/3 -z-10 h-[34rem] w-[34rem] rounded-full bg-coast-glass/45 blur-3xl" />
        <div aria-hidden="true" className="pointer-events-none absolute -bottom-64 -right-40 -z-10 h-[40rem] w-[40rem] rounded-full bg-coast-sage/75 blur-3xl" />

      <section className="mx-auto grid w-full max-w-7xl flex-1 items-center gap-10 py-8 md:grid-cols-[0.88fr_1.12fr] md:gap-12 md:py-12">
        <div className="order-2 max-w-xl md:order-1">
          <p className="inline-flex items-center gap-2 rounded-full border border-coast-line bg-white/75 px-3.5 py-2 text-[10px] font-extrabold tracking-[0.19em] text-coast-blue shadow-sm">
            <span aria-hidden="true" className="h-2 w-2 rounded-full bg-coast-teal" />
            {isNotFound ? 'UNCHARTED WATERS' : 'A TEMPORARY PAUSE'}
          </p>
          <h1 className="mt-6 max-w-lg font-display text-4xl font-semibold leading-[1.04] tracking-[-0.065em] text-coast-ink sm:text-5xl lg:text-6xl">
            {isNotFound ? 'This cove isn’t on our chart.' : 'A current interrupted the journey.'}
          </h1>
          <p className="mt-5 max-w-lg text-base leading-7 text-coast-muted sm:text-lg sm:leading-8">
            {isNotFound
              ? 'We couldn’t find the page you were looking for. The coast is still here, so let’s find another way in.'
              : 'Something went wrong while bringing this part of BLUEVERSE to shore. Your account and saved details are safe.'}
          </p>
          <div className="mt-8 flex flex-wrap gap-3">
            <Link className="inline-flex min-h-12 items-center justify-center gap-2 rounded-full bg-coast-deep px-6 text-sm font-extrabold text-white shadow-[0_12px_26px_rgba(32,92,121,0.2)] transition hover:-translate-y-0.5 hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue" to="/">
              <span aria-hidden="true">←</span> Back to the coast
            </Link>
            {isNotFound ? (
              <Link className="inline-flex min-h-12 items-center justify-center rounded-full border border-coast-line bg-white/75 px-6 text-sm font-bold text-coast-deep transition hover:border-coast-glass hover:bg-white focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue" to="/signin">Sign in</Link>
            ) : (
              <button className="inline-flex min-h-12 items-center justify-center rounded-full border border-coast-line bg-white/75 px-6 text-sm font-bold text-coast-deep transition hover:border-coast-glass hover:bg-white focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue" onClick={onRetry ?? (() => window.location.reload())} type="button">Try again</button>
            )}
          </div>
          <p className="mt-8 text-xs font-semibold tracking-wide text-coast-muted">{isNotFound ? '404 · Page not found' : '500 · Something went wrong'}</p>
        </div>

        <div className="order-1 md:order-2">
          <ErrorLandscape code={isNotFound ? '404' : '500'} />
          <p className="mt-4 text-center text-xs font-medium tracking-wide text-coast-muted">{isNotFound ? 'Sometimes the best discoveries start with a change of course.' : 'We’ll be ready when the tide settles.'}</p>
        </div>
      </section>
      </main>
      <SiteFooter />
    </div>
  )
}
