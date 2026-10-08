import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import coastImage from '../assets/coastal/coast-hero-daylight.webp'
import PlannerLayout, { primaryButton, secondaryButton } from '../features/planner/PlannerLayout'
import { getRecommendationHighlights, type RecommendationHighlight } from '../features/planner/plannerApi'
import { plannerError, statusLabel, dateTime } from '../features/planner/plannerPresentation'
import PlannerWorkspace from '../features/planner/PlannerWorkspace'

export default function PlannerPage() {
	const { recommendationId } = useParams()
	return recommendationId ? <PlannerWorkspace /> : <RecommendationHome />
}

function RecommendationHome() {
	const [highlights, setHighlights] = useState<RecommendationHighlight[]>([])
	const [loading, setLoading] = useState(true)
	const [error, setError] = useState<string | null>(null)

	useEffect(() => {
		let current = true
		getRecommendationHighlights().then(value => {
			if (current) setHighlights(value)
		}).catch(cause => {
			if (current) setError(plannerError(cause))
		}).finally(() => {
			if (current) setLoading(false)
		})
		return () => { current = false }
	}, [])

	return <PlannerLayout permission="planner.recommendations.read" publicRead>
		<section className="mb-10 grid overflow-hidden rounded-[2rem] bg-coast-deep text-white md:grid-cols-[1.2fr_1fr]">
			<div className="px-7 py-10 sm:px-10 sm:py-14">
				<p className="text-xs font-bold uppercase tracking-[0.2em] text-coast-glass">A little planning. A lot of coast.</p>
				<h1 className="mt-4 max-w-lg font-display text-4xl leading-[1.12] tracking-[-0.04em] sm:text-5xl">Find your kind<br />of coastal day.</h1>
				<p className="mt-5 max-w-md leading-7 text-white/85">Start with an idea, then shape a trip around your time, interests and current coastal conditions.</p>
				<Link className={`${primaryButton} mt-7 bg-white text-coast-deep hover:bg-coast-glass`} to="/planner/plan">Plan a trip <span aria-hidden="true">↗</span></Link>
			</div>
			<img src={coastImage} alt="Sunlit Sri Lankan coastline and clear blue water" className="h-56 w-full object-cover md:h-full" />
		</section>
		<div className="mb-8 flex flex-wrap items-end justify-between gap-5">
			<div><p className="text-xs font-bold uppercase tracking-[0.2em] text-coast-teal">Worth making time for</p><h2 className="mt-3 font-display text-3xl tracking-tight sm:text-4xl">Coastal days to inspire you.</h2><p className="mt-3 max-w-xl leading-7 text-coast-muted">Explore current ideas, then make one your own in the planner.</p></div>
			<Link className={secondaryButton} to="/planner/plan">Build your own day</Link>
		</div>
		{error && <section role="alert" className="mb-6 rounded-2xl border border-amber-200 bg-amber-50 p-5 text-sm text-amber-950">{error}</section>}
		{loading ? <p aria-live="polite" className="py-12 text-coast-muted">Finding coastal ideas…</p> : highlights.length === 0 ? <section className="rounded-3xl border border-coast-line bg-white p-8"><h2 className="font-display text-2xl">No current recommendations.</h2><p className="mt-3 text-coast-muted">Plan a trip and we will find experiences that fit your day.</p><Link className={`${primaryButton} mt-6`} to="/planner/plan">Plan a trip</Link></section> : <div className="grid gap-5 md:grid-cols-2 lg:grid-cols-3">{highlights.map(highlight => <article key={highlight.id} className="flex flex-col rounded-3xl border border-coast-line bg-white p-6"><p className="text-xs font-bold uppercase tracking-[0.16em] text-coast-teal">{highlight.destinationName}</p><h3 className="mt-4 font-display text-2xl tracking-tight">{highlight.title}</h3><p className="mt-3 text-sm text-coast-muted">{highlight.activityName} · {dateTime(highlight.startsAt, highlight.timeZone)}</p><div className="mt-5 flex flex-wrap gap-2 text-xs font-semibold"><span className="rounded-lg bg-coast-paper px-3 py-2">{statusLabel(highlight.availabilityStatus)}</span><span className="rounded-lg bg-coast-paper px-3 py-2">{statusLabel(highlight.suitabilityStatus)}</span></div><Link className="mt-6 inline-flex min-h-11 items-center font-bold text-coast-deep underline decoration-coast-line underline-offset-4" to="/planner/plan">Plan around this idea <span aria-hidden="true" className="ml-2">↗</span></Link></article>)}</div>}
	</PlannerLayout>
}
