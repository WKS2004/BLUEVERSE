import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import PlannerLayout, { primaryButton, secondaryButton } from '../features/planner/PlannerLayout'
import AccountAreaNavigation from '../components/account/AccountAreaNavigation'
import { listItineraries, type Itinerary } from '../features/planner/plannerApi'
import { dateTime, plannerError } from '../features/planner/plannerPresentation'
import { hasAllPermissions } from '../features/authorization/permissions'
import { useAuthSession } from '../features/auth/authSession'
import lagoonImage from '../assets/coastal/mangrove-lagoon.jpg'

export default function SavedTripsPage() {
  return (
    <div className="flex min-h-screen flex-col bg-coast-paper font-sans text-coast-ink">
      <PlannerLayout permission="planner.itineraries.manage" publicRead={false}>
        <>
          <AccountAreaNavigation active="planner" />
          <main className="mx-auto min-h-[70vh] max-w-[90rem] flex-1 grid grid-cols-1 gap-6 px-4 py-6 sm:px-8 sm:py-10 lg:grid-cols-[15rem_minmax(0,1fr)] lg:items-start lg:gap-8 lg:px-6 lg:py-0">
            <SavedTrips />
          </main>
        </>
      </PlannerLayout>
    </div>
  )
}
function SavedTrips() {
  const { user } = useAuthSession()
  const [trips, setTrips] = useState<Itinerary[]>([])
  const [page, setPage] = useState(1)
  const [retry, setRetry] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [today] = useState(() => Date.now())
  useEffect(() => {
    let current = true
    listItineraries(page).then(value => { if (current) { setTrips(value); setError(null) } })
      .catch(cause => { if (current) setError(plannerError(cause)) }).finally(() => { if (current) setLoading(false) })
    return () => { current = false }
  }, [page, retry])
  function move(next: number) { setLoading(true); setPage(next) }
  return <>
    <div className="mb-9 flex flex-wrap items-end justify-between gap-6"><div><p className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">Good days, kept close</p><h1 className="mt-3 font-display text-4xl tracking-tight sm:text-5xl">Your saved coastal trips.</h1><p className="mt-4 max-w-xl leading-7 text-coast-muted">A place for plans you’re looking forward to. Shape the details and check conditions again before you go.</p></div>{hasAllPermissions(user, ['planner.recommendations.create']) && <Link to="/planner" className={primaryButton}>Plan a new day <span aria-hidden="true">↗</span></Link>}</div>
    {error ? <section role="alert" className="rounded-3xl border border-amber-200 bg-amber-50 p-7"><p>{error}</p><button className={`${secondaryButton} mt-5`} onClick={() => { setLoading(true); setRetry(n => n + 1) }}>Try again</button></section> : loading ? <p aria-live="polite" className="py-16 text-coast-muted">Opening your saved trips…</p> : trips.length === 0 ? <section className="grid overflow-hidden rounded-3xl border border-coast-line bg-white md:grid-cols-2"><img className="h-64 w-full object-cover md:h-full" src={lagoonImage} alt="Calm lagoon surrounded by mangroves" /><div className="p-8 sm:p-12"><p className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">Room for your next adventure</p><h2 className="mt-4 font-display text-3xl">{page === 1 ? 'Your first coastal day awaits.' : 'You’ve reached the end of your trips.'}</h2><p className="mt-4 leading-7 text-coast-muted">{page === 1 ? 'Find an experience you love, save it as a trip, and make the day your own.' : 'Go back to see the plans you’ve already saved.'}</p>{page === 1 && hasAllPermissions(user, ['planner.recommendations.create']) && <Link className={`${primaryButton} mt-6`} to="/planner">Find a coastal experience</Link>}</div></section> : <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">{trips.map(trip => {
      const review = trip.items.some(item => item.lastSuitabilityStatus !== 'SUITABLE' || item.lastAvailabilityStatus !== 'AVAILABLE' || item.lastOperationalStatus !== 'OPEN')
      return <article className="flex flex-col rounded-3xl border border-coast-line bg-white p-7" key={trip.itineraryId}><div className="flex items-center justify-between gap-2"><span className="text-xs font-bold uppercase tracking-[0.16em] text-coast-teal">{trip.items.length} {trip.items.length === 1 ? 'experience' : 'experiences'}</span><span className="rounded-full bg-coast-sage px-3 py-1 text-xs font-semibold text-coast-deep">{Date.parse(trip.endsAt) < today ? 'Past trip' : review ? 'Check before you go' : 'Conditions reviewed'}</span></div><h2 className="mt-5 font-display text-2xl tracking-tight"><Link className="rounded-lg hover:text-coast-blue focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-blue" to={`/planner/itineraries/${encodeURIComponent(trip.itineraryId)}`}>{trip.title}</Link></h2><p className="mt-3 text-sm text-coast-muted">{dateTime(trip.startsAt, trip.timeZone)}</p>{trip.description && <p className="mt-4 line-clamp-3 text-sm leading-6 text-coast-muted">{trip.description}</p>}<div className="mt-6 border-t border-coast-line pt-4"><Link className="inline-flex min-h-11 items-center gap-3 rounded-full text-sm font-bold text-coast-deep focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue" to={`/planner/itineraries/${encodeURIComponent(trip.itineraryId)}`}>Open this trip <span aria-hidden="true">↗</span></Link></div></article>
    })}</div>}
    {!loading && !error && <nav aria-label="Saved trip pages" className="mt-8 flex items-center justify-between gap-3"><button className={secondaryButton} disabled={page === 1} onClick={() => move(page - 1)}>Previous</button><span className="text-xs text-coast-muted">Page {page} · Times local to each trip</span><button className={secondaryButton} disabled={trips.length < 20} onClick={() => move(page + 1)}>Next</button></nav>}
  </>
}
