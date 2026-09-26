import type { ReactNode } from 'react'
import SiteFooter from './SiteFooter'
import SiteHeader from './SiteHeader'

type AuthPageLayoutProps = {
  active: 'login' | 'register'
  eyebrow: string
  title: string
  description: string
  storyTitle: string
  story: string
  photoSrc: string
  photoAlt: string
  mobilePhotoSrc: string
  mobilePhotoAlt: string
  children: ReactNode
}

export default function AuthPageLayout({
  active,
  eyebrow,
  title,
  description,
  storyTitle,
  story,
  photoSrc,
  photoAlt,
  mobilePhotoSrc,
  mobilePhotoAlt,
  children,
}: AuthPageLayoutProps) {
  const isRegistration = active === 'register'
  const desktopPhotoOrder = isRegistration ? 'md:order-1' : 'md:order-2'
  const desktopFormOrder = isRegistration ? 'md:order-2' : 'md:order-1'
  const desktopColumns = isRegistration ? 'md:grid-cols-[1.04fr_0.96fr]' : 'md:grid-cols-[0.96fr_1.04fr]'

  return (
    <div className="flex min-h-screen flex-col bg-coast-sand font-sans text-coast-ink" id="top">
      <SiteHeader active={active} compactMobile />
      <main className="flex flex-1 items-start justify-center px-4 py-8 sm:px-6 sm:py-10 md:items-center lg:px-8 lg:py-14">
        <div className={`mx-auto grid w-full max-w-6xl grid-cols-1 overflow-hidden rounded-[2rem] border border-coast-line bg-coast-pearl shadow-[0_20px_70px_rgba(24,57,76,0.11)] sm:rounded-[2.5rem] md:min-h-[38rem] ${desktopColumns}`}>
          <aside aria-label={mobilePhotoAlt} className={`group relative isolate order-1 aspect-[16/9] w-full overflow-hidden bg-coast-deep text-white motion-safe:animate-coast-arrive sm:aspect-[2/1] md:aspect-auto md:min-h-[38rem] ${desktopPhotoOrder}`}>
            <picture>
              <source media="(max-width: 767px)" srcSet={mobilePhotoSrc} />
              <img alt={photoAlt} className={`absolute inset-0 -z-20 h-full w-full object-cover transition-transform duration-700 group-hover:scale-[1.025] motion-reduce:transition-none ${isRegistration ? 'object-[center_36%] md:object-center' : 'object-center'}`} src={photoSrc} />
            </picture>
            <div aria-hidden="true" className="absolute inset-0 -z-10 bg-gradient-to-r from-coast-ink/65 via-coast-ink/30 to-coast-ink/10 md:bg-gradient-to-t md:from-coast-ink/90 md:via-coast-ink/25 md:to-coast-ink/5" />
            <div className="absolute inset-x-0 bottom-0 px-5 py-5 sm:px-7 sm:py-7 md:p-8 lg:p-10">
              <p className="text-[10px] font-extrabold tracking-[0.18em] text-coast-glass sm:text-[11px]">{isRegistration ? 'A SHARED PLACE BY THE SEA' : 'A FULLER VIEW OF THE SHORE'}</p>
              <h2 className="mt-2 max-w-md font-display text-2xl leading-tight tracking-[-0.04em] sm:text-3xl md:text-3xl lg:text-4xl">{storyTitle}</h2>
              <p className="mt-3 hidden max-w-md text-sm leading-6 text-white/85 md:block">{story}</p>
              <div aria-hidden="true" className="mt-5 hidden items-center gap-3 text-[10px] font-extrabold tracking-[0.16em] text-white/75 md:flex">
                <span className="h-px w-8 bg-coast-glass/80" />
                BLUEVERSE · SRI LANKA
              </div>
            </div>
          </aside>

          <section aria-labelledby="auth-page-title" className={`order-2 min-w-0 p-6 motion-safe:animate-coast-arrive sm:p-8 md:flex md:flex-col md:justify-center md:p-10 lg:p-12 xl:p-14 ${desktopFormOrder}`}>
            <div className="mb-5 flex items-center gap-3 sm:mb-7">
              <span aria-hidden="true" className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-coast-sage text-coast-deep sm:h-10 sm:w-10">
                {isRegistration ? (
                  <svg viewBox="0 0 24 24" fill="none" className="h-5 w-5"><path d="M12 20s-7-3.8-7-9a4 4 0 0 1 7-2.6A4 4 0 0 1 19 11c0 5.2-7 9-7 9Z" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" /></svg>
                ) : (
                  <svg viewBox="0 0 24 24" fill="none" className="h-5 w-5"><path d="M4 15c2.7 0 2.7-2 5.3-2s2.7 2 5.4 2 2.7-2 5.3-2M4 19c2.7 0 2.7-2 5.3-2s2.7 2 5.4 2 2.7-2 5.3-2M12 3v6m-3-3 3 3 3-3" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" /></svg>
                )}
              </span>
              <p className="text-[10px] font-extrabold tracking-[0.16em] text-coast-blue sm:text-[11px] sm:tracking-[0.18em]">{eyebrow}</p>
            </div>
            <h1 className="font-display text-3xl leading-[1.08] tracking-[-0.05em] sm:text-4xl lg:text-5xl" id="auth-page-title">{title}</h1>
            <p className="mt-4 max-w-md text-sm leading-6 text-coast-muted sm:text-base sm:leading-7">{description}</p>
            {isRegistration && <p className="mt-5 flex items-center gap-2 text-xs font-semibold text-coast-muted sm:text-sm"><span aria-hidden="true" className="h-2 w-2 shrink-0 rounded-full bg-coast-teal" /> A simple starting point for your next coastal discovery.</p>}
            {children}
          </section>
        </div>
      </main>
      <SiteFooter compact />
    </div>
  )
}
