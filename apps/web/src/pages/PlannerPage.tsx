import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import coastImage from '../assets/planner/coastal-discovery.webp'
import experienceImage from '../assets/planner/guided-experiences.webp'
import PlannerLayout, { primaryButton, secondaryButton } from '../features/planner/PlannerLayout'
import { getRecommendationHighlights, type RecommendationHighlight } from '../features/planner/plannerApi'
import { plannerError, statusLabel, dateTime } from '../features/planner/plannerPresentation'
import PlannerWorkspace from '../features/planner/PlannerWorkspace'

export default function PlannerPage() {
  const { recommendationId } = useParams()
  return recommendationId ? <PlannerWorkspace /> : <PlannerIntroduction />
}

function PlannerIntroduction() {
  return <PlannerLayout publicRead>
    <section className="grid items-center gap-8 pb-10 lg:grid-cols-[1.05fr_1fr] lg:gap-14 lg:pb-16">
      <div className="py-4 sm:py-8">
        <p className="flex items-center gap-3 text-xs font-bold uppercase tracking-[0.2em] text-coast-teal"><span className="h-px w-9 bg-coast-teal" />The smart coastal planner</p>
        <h1 className="mt-6 max-w-xl font-display text-[clamp(2.7rem,5.4vw,4.8rem)] leading-[1.08] tracking-[-0.055em]">A coastal day, <br /><span className="text-coast-blue">made for you.</span></h1>
        <p className="mt-6 max-w-lg text-base leading-8 text-coast-muted sm:text-lg">From a quiet stretch of shore to a little adventure on the water. Find experiences that fit your time, your interests, and the conditions along the coast.</p>
        <div className="mt-8 flex flex-wrap items-center gap-4"><Link to="/planner/plan" className={primaryButton}>Let’s plan your trip <span aria-hidden="true">↗</span></Link><a href="#how-planning-works" className="inline-flex min-h-12 items-center gap-3 rounded-full px-2 text-sm font-bold text-coast-deep underline decoration-coast-line underline-offset-8 focus-visible:outline-2 focus-visible:outline-coast-blue">How it works <span aria-hidden="true">↓</span></a></div>
        <p className="mt-5 text-xs leading-6 text-coast-muted">Explore freely. Sign in when you’re ready to create your own plan.</p>
      </div>
      <figure className="relative min-h-96 overflow-hidden rounded-t-[8rem] rounded-b-[2rem] bg-coast-glass sm:min-h-[31rem] lg:min-h-[35rem]">
        <img src={coastImage} alt="Travellers exploring a palm-lined beach beside turquoise water" width={1024} height={1536} className="absolute inset-0 h-full w-full object-cover" fetchPriority="high" />
        <div className="absolute inset-0 bg-linear-to-t from-coast-ink/80 via-transparent to-transparent" />
        <figcaption className="absolute inset-x-0 bottom-0 p-7 text-white sm:p-9"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-coast-glass">A little planning. A lot of coast.</p><p className="mt-3 max-w-xs font-display text-2xl leading-snug">Leave room for the moments<br />you came here for.</p></figcaption>
      </figure>
    </section>
    <section id="how-planning-works" aria-labelledby="planning-steps-title" className="scroll-mt-28 border-y border-coast-line py-10 sm:py-14">
      <div className="flex flex-wrap items-end justify-between gap-5"><div><p className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">From an idea to a day out</p><h2 id="planning-steps-title" className="mt-3 font-display text-3xl tracking-tight sm:text-4xl">Your plan, in three simple steps.</h2></div><p className="max-w-sm text-sm leading-7 text-coast-muted">You choose the kind of day. We bring the useful details together.</p></div>
      <ol className="mt-9 grid gap-8 md:grid-cols-3 md:gap-10">{[
        ['Choose your coast', 'Pick a destination, your dates, and what you would love to do. Keep your interests open or choose a few favourites.'],
        ['Find what fits', 'Explore suggestions checked against available experiences, marine suitability, and operating restrictions.'],
        ['Make the day yours', 'Save a draft, arrange your experiences, and confirm when you’re ready. Revisit your plan and review conditions before you go.'],
      ].map(([title, copy], index) => <li key={title}><span className="inline-flex h-10 w-10 items-center justify-center rounded-full border border-coast-line text-sm font-bold text-coast-teal">0{index + 1}</span><h3 className="mt-4 font-display text-xl tracking-tight">{title}</h3><p className="mt-3 text-sm leading-7 text-coast-muted">{copy}</p></li>)}</ol>
    </section>
    <section className="grid items-center gap-8 py-14 sm:py-20 md:grid-cols-[0.85fr_1fr] lg:gap-16">
      <img src={experienceImage} alt="Visitors in life jackets preparing a kayaking outing with a local guide" width={1536} height={1024} loading="lazy" className="h-72 w-full rounded-[2rem] object-cover sm:h-96" />
      <div><p className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">Thoughtful details. Better days.</p><h2 className="mt-4 max-w-lg font-display text-3xl leading-tight tracking-tight sm:text-4xl">More time enjoying.<br />Less time guessing.</h2><div className="mt-7 space-y-5">{[
        ['A day that suits you', 'Plan around your destination, available time, interests, and experience level.'],
        ['Context you can understand', 'See availability, marine conditions, and practical cautions. Optional wildlife context never promises a sighting.'],
        ['Your plans, kept together', 'Return to drafts, confirmed trips, and past days in your own plan history.'],
      ].map(([title, copy]) => <div className="flex gap-4" key={title}><span aria-hidden="true" className="mt-1 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-coast-glass text-xs font-bold text-coast-deep">✓</span><div><h3 className="text-sm font-bold">{title}</h3><p className="mt-1 text-sm leading-7 text-coast-muted">{copy}</p></div></div>)}</div><p className="mt-6 border-t border-coast-line pt-5 text-xs leading-6 text-coast-muted">A saved plan is your itinerary, not a booking. Conditions can change; check again before travelling.</p></div>
    </section>
    <CoastalHighlights />
    <section className="mt-14 flex flex-wrap items-center justify-between gap-7 rounded-[2rem] bg-coast-deep px-7 py-10 text-white sm:px-10"><div><p className="text-xs font-semibold uppercase tracking-[0.18em] text-coast-glass">Your next day by the water</p><h2 className="mt-3 font-display text-3xl tracking-tight">A good day starts with a little plan.</h2></div><Link to="/planner/plan" className="inline-flex min-h-12 items-center gap-4 rounded-full bg-white px-6 py-3 text-sm font-bold text-coast-deep transition hover:bg-coast-glass focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-white">Create a plan <span aria-hidden="true">↗</span></Link></section>
  </PlannerLayout>
}

function CoastalHighlights() {
  const [highlights, setHighlights] = useState<RecommendationHighlight[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [reload, setReload] = useState(0)
  useEffect(() => {
    let current = true
    getRecommendationHighlights().then(value => { if (current) setHighlights(value) })
      .catch(cause => { if (current) setError(plannerError(cause, false)) })
      .finally(() => { if (current) setLoading(false) })
    return () => { current = false }
  }, [reload])
  function retry() { setLoading(true); setError(null); setReload(n => n + 1) }
  return <section aria-labelledby="coastal-highlights-title"><div className="mb-7"><p className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">A little inspiration</p><h2 id="coastal-highlights-title" className="mt-3 font-display text-3xl tracking-tight sm:text-4xl">Coastal days to inspire you.</h2><p className="mt-3 max-w-2xl leading-7 text-coast-muted">Current experiences to spark an idea. Your own suggestions will be tailored to the dates and interests you choose.</p></div>
    {error ? <div role="alert" className="flex flex-wrap items-center justify-between gap-5 rounded-2xl border border-coast-line bg-coast-sage p-6"><div><p className="font-bold">Fresh coastal ideas are taking a little longer.</p><p className="mt-2 max-w-xl text-sm leading-6 text-coast-muted">{error} You can still explore the planner and return to your plans.</p></div><button className={secondaryButton} onClick={retry}>Try again</button></div> : loading ? <p aria-live="polite" className="py-8 text-sm text-coast-muted">Finding coastal ideas…</p> : highlights.length === 0 ? <div className="flex flex-wrap items-center justify-between gap-5 rounded-2xl border border-coast-line bg-white p-6"><div><h3 className="font-bold">No current coastal ideas.</h3><p className="mt-2 text-sm leading-6 text-coast-muted">Verified experiences are unavailable right now. Please try again later.</p></div><button className={secondaryButton} onClick={retry}>Try again</button></div> : <div className="grid gap-5 md:grid-cols-2 lg:grid-cols-3">{highlights.map(highlight => <HighlightCard key={highlight.id} highlight={highlight} />)}</div>}
  </section>
}

function HighlightCard({ highlight }: { highlight: RecommendationHighlight }) {
  const search = new URLSearchParams({ destinationId: highlight.destinationId, activityId: highlight.activityId }).toString()
  return <article className="flex flex-col rounded-3xl border border-coast-line bg-white p-7 transition hover:border-coast-teal">
    <p className="text-xs font-bold uppercase tracking-[0.16em] text-coast-teal">{highlight.destinationName}</p><h3 className="mt-4 font-display text-2xl tracking-tight">{highlight.title}</h3><p className="mt-3 text-sm text-coast-muted">{highlight.activityName} · {dateTime(highlight.startsAt, highlight.timeZone)}</p>
    <div className="mt-5 flex flex-wrap gap-2 text-xs font-semibold"><span className="rounded-lg bg-coast-paper px-3 py-2">Availability: {statusLabel(highlight.availabilityStatus)}</span><span className="rounded-lg bg-coast-paper px-3 py-2">Marine: {statusLabel(highlight.suitabilityStatus)}</span><span className="rounded-lg bg-coast-paper px-3 py-2">Operations: {statusLabel(highlight.operationalStatus)}</span></div>
    {highlight.evidenceCheckedAt && <p className="mt-3 text-xs text-coast-muted">Availability checked {dateTime(highlight.evidenceCheckedAt, highlight.timeZone)}</p>}{highlight.uncertaintyNotes.map(note => <p key={note} className="mt-3 text-sm text-amber-900">{note}</p>)}
    <Link className="mt-6 inline-flex min-h-11 items-center gap-3 font-bold text-coast-deep underline decoration-coast-line underline-offset-4 focus-visible:outline-2 focus-visible:outline-coast-blue" to={{ pathname: '/planner/plan', search: `?${search}` }}>Start with this idea <span aria-hidden="true">↗</span></Link>
  </article>
}
