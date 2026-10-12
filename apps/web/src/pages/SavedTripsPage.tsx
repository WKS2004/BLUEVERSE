import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import PlannerLayout, { primaryButton, secondaryButton } from '../features/planner/PlannerLayout'
import PlannerSectionHeader from '../features/planner/PlannerSectionHeader'
import { listItineraries, type Itinerary } from '../features/planner/plannerApi'
import { dateTime, plannerError } from '../features/planner/plannerPresentation'
import { hasAllPermissions } from '../features/authorization/permissions'
import { useAuthSession } from '../features/auth/authSession'

type PlanFilter = 'All plans' | 'Drafts' | 'Confirmed' | 'Past'
const filters: PlanFilter[] = ['All plans', 'Drafts', 'Confirmed', 'Past']

function needsConditionsCheck(trip: Itinerary) {
  return trip.items.some(item => item.lastSuitabilityStatus !== 'SUITABLE' || item.lastAvailabilityStatus !== 'AVAILABLE' || item.lastOperationalStatus !== 'OPEN')
}

function HistoryIcon({ name }: { name: 'filter' | 'reset' | 'search' }) {
  return <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" className="h-5 w-5">
    {name === 'filter' ? <><path d="M4 7h16M4 17h16M9 4v6M15 14v6" /></> : name === 'reset' ? <><path d="M4 9a8 8 0 1 1 0 7M4 4v5h5" /></> : <><circle cx="10.5" cy="10.5" r="6.5" /><path d="m16 16 5 5" /></>}
  </svg>
}

export default function SavedTripsPage() {
  return <PlannerLayout permission="planner.itineraries.manage"><PlanHistory /></PlannerLayout>
}

function PlanHistory() {
  const { user } = useAuthSession()
  const [trips, setTrips] = useState<Itinerary[]>([])
  const [page, setPage] = useState(1), [pageSize, setPageSize] = useState(20), [retry, setRetry] = useState(0)
  const [loading, setLoading] = useState(true), [error, setError] = useState<string | null>(null)
  const [filter, setFilter] = useState<PlanFilter>('All plans'), [search, setSearch] = useState('')
  const [filterOptionsOpen, setFilterOptionsOpen] = useState(false), [reviewOnly, setReviewOnly] = useState(false)
  const [today] = useState(() => Date.now())
  const canCreate = hasAllPermissions(user, ['planner.recommendations.create'])
  useEffect(() => {
    let current = true
    listItineraries(page, pageSize, 'inline').then(value => { if (current) { setTrips(value); setError(null) } })
      .catch(cause => { if (current) setError(plannerError(cause, false)) }).finally(() => { if (current) setLoading(false) })
    return () => { current = false }
  }, [page, pageSize, retry])

  function resetFilters() { setFilter('All plans'); setSearch(''); setReviewOnly(false) }
  function move(next: number) { setLoading(true); setError(null); resetFilters(); setPage(next) }
  function changePageSize(value: number) { setLoading(true); setError(null); resetFilters(); setPage(1); setPageSize(value) }
  const visible = trips.filter(trip => {
    const past = Date.parse(trip.endsAt) < today
    return (filter === 'All plans' || filter === 'Drafts' && trip.status === 'Draft' || filter === 'Confirmed' && trip.status === 'Confirmed' || filter === 'Past' && past)
      && `${trip.title} ${trip.description ?? ''}`.toLowerCase().includes(search.trim().toLowerCase())
      && (!reviewOnly || needsConditionsCheck(trip))
  })
  const iconButton = 'inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-full text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue disabled:opacity-50'

  return <>
    <PlannerSectionHeader section="history" />
    <section aria-labelledby="find-plans-title" className="rounded-[2rem] border border-coast-line bg-coast-pearl p-5 sm:p-8">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h2 id="find-plans-title" className="font-display text-2xl">Find your coastal plans</h2>
        {trips.length > 0 && canCreate && <Link to="/planner/plan" className={secondaryButton}>Create a new plan <span aria-hidden="true">↗</span></Link>}
      </div>
      <div role="group" aria-label="Filter plans on this page" className="mt-4 flex flex-wrap gap-2">
        {filters.map(value => <button type="button" key={value} disabled={loading} aria-pressed={filter === value} className={`min-h-11 rounded-full border px-5 text-sm font-bold transition focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:opacity-50 ${filter === value ? 'border-coast-blue bg-coast-blue text-white' : 'border-coast-line text-coast-deep hover:bg-coast-sage'}`} onClick={() => setFilter(value)}>{value}</button>)}
      </div>
      <form role="search" aria-label="Search saved plans" className="mt-4 flex min-w-0 items-center gap-1 rounded-2xl border border-coast-line bg-white p-2 sm:gap-2" onSubmit={event => { event.preventDefault(); setSearch(search.trim()) }}>
        <label className="sr-only" htmlFor="plan-history-search">Find a plan on this page</label>
        <input id="plan-history-search" type="search" disabled={loading} value={search} onChange={event => setSearch(event.target.value)} placeholder="Search plans by name or notes" className="min-h-11 w-full min-w-0 rounded-xl bg-transparent px-2 text-sm text-coast-ink outline-none focus-visible:ring-2 focus-visible:ring-coast-glass sm:px-3" />
        <button type="button" aria-label="Filter options" aria-controls="plan-filter-options" aria-expanded={filterOptionsOpen} className={iconButton} onClick={() => setFilterOptionsOpen(open => !open)}><HistoryIcon name="filter" /></button>
        <button type="button" aria-label="Reset plan filters" className={iconButton} onClick={resetFilters}><HistoryIcon name="reset" /></button>
        <button type="submit" aria-label="Search plans" disabled={loading} className={`${iconButton} bg-coast-blue text-white hover:bg-coast-deep`}><HistoryIcon name="search" /></button>
      </form>
      {filterOptionsOpen && <div id="plan-filter-options" className="mt-3 rounded-2xl border border-coast-line bg-white p-4"><label className="flex items-center gap-3 text-sm text-coast-deep"><input type="checkbox" checked={reviewOnly} onChange={event => setReviewOnly(event.target.checked)} className="h-4 w-4 accent-coast-blue" />Only plans needing a conditions check</label></div>}
      <p className="mt-3 text-xs leading-6 text-coast-muted">Filters and search apply to this page of your history. Times are local to each trip.</p>
      <div className="py-8 sm:py-10">
        {error ? <section role="alert" className="rounded-2xl border border-coast-line bg-white p-6"><h3 className="font-display text-2xl">Your plans couldn’t be opened just yet.</h3><p className="mt-3 text-sm leading-7 text-coast-muted">{error}</p><button className={`${primaryButton} mt-5`} onClick={() => { setLoading(true); setError(null); setRetry(n => n + 1) }}>Try again</button></section>
          : loading ? <p aria-live="polite" className="text-sm text-coast-muted">Opening your saved trips…</p>
          : trips.length === 0 ? <section><h3 className="font-display text-xl">{page === 1 ? 'Your first coastal day awaits.' : 'You’ve reached the end of your trips.'}</h3><p className="mt-3 text-sm leading-7 text-coast-muted">{page === 1 ? 'Saved drafts and confirmed plans will appear here.' : 'Go back to see the plans you’ve already saved.'}</p>{page === 1 && canCreate && <Link className={`${primaryButton} mt-5`} to="/planner/plan">Create your first plan <span aria-hidden="true">↗</span></Link>}</section>
          : visible.length === 0 ? <section role="status"><h3 className="font-display text-xl">No plans match on this page.</h3><p className="mt-3 text-sm leading-7 text-coast-muted">Try another filter, clear your search, or browse another page.</p><button type="button" className={`${secondaryButton} mt-5`} onClick={resetFilters}>Show all plans on this page</button></section>
          : <div className="space-y-4">{visible.map(trip => <TripHistoryRow key={trip.itineraryId} trip={trip} today={today} />)}</div>}
      </div>
      <nav aria-label="Saved trip pages" className="flex flex-wrap items-center justify-between gap-5 border-t border-coast-line pt-5">
        <label htmlFor="plans-per-page" className="flex items-center gap-3 text-xs font-semibold text-coast-deep">Plans per page<select id="plans-per-page" value={pageSize} disabled={loading} onChange={event => changePageSize(Number(event.target.value))} className="min-h-11 rounded-xl border border-coast-line bg-white px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-coast-glass">{[10, 20, 25, 50].map(size => <option key={size} value={size}>{size}</option>)}</select></label>
        <div className="flex flex-wrap items-center gap-3"><span aria-live="polite" className="text-xs text-coast-deep">{loading || error ? `Page ${page}` : `${visible.length} of ${trips.length} plans on this page · Page ${page}`}</span><button className={secondaryButton} disabled={loading || page === 1} onClick={() => move(page - 1)}>Previous</button><button className={secondaryButton} disabled={loading || !!error || trips.length < pageSize} onClick={() => move(page + 1)}>Next</button></div>
      </nav>
    </section>
  </>
}

function TripHistoryRow({ trip, today }: { trip: Itinerary; today: number }) {
  const past = Date.parse(trip.endsAt) < today
  const clock = new Intl.DateTimeFormat('en-GB', { timeZone: trip.timeZone, day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(trip.startsAt))
  const href = `/planner/itineraries/${encodeURIComponent(trip.itineraryId)}`
  return <article className="grid items-center gap-5 rounded-3xl border border-coast-line bg-white p-5 transition hover:border-coast-teal sm:grid-cols-[7.5rem_minmax(0,1fr)] xl:grid-cols-[7.5rem_minmax(0,1fr)_auto] sm:p-6">
    <div className="rounded-2xl bg-coast-sage px-4 py-5 text-center"><p className="text-sm font-bold text-coast-deep">{clock}</p><p className="mt-2 text-xs text-coast-muted">{past ? 'A past coastal day' : 'A day to look forward to'}</p></div>
    <div className="min-w-0"><div className="flex flex-wrap items-center gap-3"><span className="rounded-full border border-coast-line px-3 py-1 text-xs font-semibold text-coast-deep">{trip.status}</span><span className="text-xs text-coast-muted">{trip.items.length} {trip.items.length === 1 ? 'experience' : 'experiences'}</span>{!past && <span className="text-xs text-coast-muted">{needsConditionsCheck(trip) ? 'Check before you go' : 'Conditions reviewed'}</span>}</div><h3 className="mt-3 break-words font-display text-2xl tracking-tight"><Link to={href} className="rounded-md hover:text-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue">{trip.title}</Link></h3><p className="mt-2 text-xs leading-6 text-coast-muted">{dateTime(trip.startsAt, trip.timeZone)} – {dateTime(trip.endsAt, trip.timeZone)}</p>{trip.description && <p className="mt-3 line-clamp-2 break-words text-sm leading-6 text-coast-muted">{trip.description}</p>}</div>
    <Link className={`${secondaryButton} w-fit sm:col-start-2 xl:col-start-auto`} to={href}>Open this trip <span aria-hidden="true">↗</span></Link>
  </article>
}
