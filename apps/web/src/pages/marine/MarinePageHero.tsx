import conditionsImage from '../../assets/coastal/marine/coastal-conditions-hero.webp'
import evidenceImage from '../../assets/coastal/marine/coastal-evidence-hero.webp'
import safetyImage from '../../assets/coastal/marine/activity-safety-hero.webp'

export type MarineHeroArea = 'conditions' | 'history' | 'profiles'

const scenes = {
  conditions: {
    image: conditionsImage,
    eyebrow: 'READ THE COAST',
    detail: 'Wind, waves & swell',
    position: 'object-[60%_55%]',
  },
  history: {
    image: evidenceImage,
    eyebrow: 'FOLLOW THE EVIDENCE',
    detail: 'Readings, assessments & sources',
    position: 'object-[60%_70%]',
  },
  profiles: {
    image: safetyImage,
    eyebrow: 'PREPARE WITH PURPOSE',
    detail: 'Cited limits & independent review',
    position: 'object-[66%_65%]',
  },
} as const

export default function MarinePageHero({ area, title, intro }: { area: MarineHeroArea; title: string; intro: string }) {
  const scene = scenes[area]

  return <header aria-labelledby="marine-page-heading" className="mb-6 grid min-w-0 grid-cols-1 overflow-hidden rounded-[2rem] border border-coast-line bg-coast-pearl shadow-[0_16px_45px_rgba(24,57,76,0.07)]">
    <div className="relative h-44 min-w-0 overflow-hidden bg-coast-sage sm:h-56">
      <img alt="" aria-hidden="true" className={`absolute inset-0 h-full w-full object-cover ${scene.position}`} decoding="async" fetchPriority="high" height={1024} loading="eager" src={scene.image} width={1536} />
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 bg-linear-to-t from-coast-ink/10 via-transparent to-transparent" />
      <div aria-hidden="true" className="absolute right-4 top-4 flex h-10 w-10 items-center justify-center rounded-full border border-white/60 bg-coast-pearl/85 text-coast-deep backdrop-blur-sm sm:right-6 sm:top-6">
        <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24"><path d="M3 10c3 0 3-3 6-3s3 3 6 3 3-3 6-3M3 16c3 0 3-3 6-3s3 3 6 3 3-3 6-3" stroke="currentColor" strokeLinecap="round" strokeWidth="1.5" /></svg>
      </div>
    </div>
    <div className="relative flex min-w-0 flex-col justify-center p-6 sm:p-8">
      <div aria-hidden="true" className="mb-5 h-1 w-10 rounded-full bg-coast-teal" />
      <p className="text-[10px] font-extrabold tracking-[0.19em] text-coast-blue">{scene.eyebrow}</p>
      <h2 className="mt-3 break-words font-display text-3xl leading-[1.08] tracking-[-0.045em] text-coast-ink sm:text-4xl" id="marine-page-heading">{title}</h2>
      <p className="mt-4 max-w-3xl text-sm leading-6 text-coast-muted">{intro}</p>
      <p className="mt-6 border-t border-coast-line pt-4 text-xs font-semibold tracking-wide text-coast-deep">{scene.detail}</p>
    </div>
  </header>
}
