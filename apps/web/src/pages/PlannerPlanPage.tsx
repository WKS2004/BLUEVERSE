import PlannerSectionHeader from '../features/planner/PlannerSectionHeader'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import PlannerLayout, { field, primaryButton, secondaryButton } from '../features/planner/PlannerLayout'
import { useAuthSession } from '../features/auth/authSession'
import { hasAllPermissions } from '../features/authorization/permissions'
import { createItinerary, createRecommendations, getPlannerCatalogue, getItinerary, listItineraries, updateItinerary,
  type Itinerary, type ItineraryItemInput, type PlannerCatalogue, type RecommendationCandidate, type RecommendationResult } from '../features/planner/plannerApi'
import { dateTime, plannerError, statusLabel, utcTime, validatePlanningWindow } from '../features/planner/plannerPresentation'

const PACE_OPTIONS = [{ value: '2', label: 'Relaxed · 2 hours' }, { value: '4', label: 'Steady · 4 hours' }, { value: '8', label: 'Full day · 8 hours' }]
const EXPERIENCE_OPTIONS = [{ value: 'BEGINNER', label: 'Beginner' }, { value: 'INTERMEDIATE', label: 'Intermediate' }, { value: 'ADVANCED', label: 'Advanced' }]

export default function PlannerPlanPage() {
  return <PlannerLayout permission="planner.recommendations.create"><TripPlanContent /></PlannerLayout>
}

function PlanningSection({ number, title, children }: { number: string; title: string; children: ReactNode }) {
  return <section className="border-b border-coast-line pb-8 last:border-0 last:pb-0"><div className="mb-6 flex items-center gap-3"><span className="inline-flex h-8 w-8 items-center justify-center rounded-full bg-coast-sage text-xs font-bold text-coast-teal">{number}</span><h2 className="font-display text-xl tracking-tight">{title}</h2></div>{children}</section>
}

function TripPlanContent() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const { user } = useAuthSession()
  const canSave = hasAllPermissions(user, ['planner.itineraries.manage'])
  const [catalogue, setCatalogue] = useState<PlannerCatalogue | null>(null)
  const [catalogueLoading, setCatalogueLoading] = useState(true)
  const [catalogueError, setCatalogueError] = useState<string | null>(null)
  const [reload, setReload] = useState(0)
  const [destinationId, setDestinationId] = useState(searchParams.get('destinationId') ?? '')
  const [activities, setActivities] = useState<string[]>(searchParams.get('activityId') ? [searchParams.get('activityId')!] : [])
  const [starts, setStarts] = useState(''), [ends, setEnds] = useState('')
  const [duration, setDuration] = useState('4'), [experience, setExperience] = useState('INTERMEDIATE')
  const [biodiversity, setBiodiversity] = useState(true)
  const [tripTitle, setTripTitle] = useState(''), [tripNotes, setTripNotes] = useState('')
  const [error, setError] = useState<string | null>(null), [busy, setBusy] = useState(false)
  const [result, setResult] = useState<RecommendationResult | null>(null)
  const [selected, setSelected] = useState<RecommendationCandidate | null>(null)
  const [savedTrips, setSavedTrips] = useState<Itinerary[] | null>(null)
  const [appendTo, setAppendTo] = useState(''), [tripPage, setTripPage] = useState(1)
  const [hasMoreTrips, setHasMoreTrips] = useState(false), [loadingTrips, setLoadingTrips] = useState(false)
  const [saving, setSaving] = useState(false), [saveError, setSaveError] = useState<string | null>(null)
  const saveDialog = useRef<HTMLDialogElement>(null)
  const resultSection = useRef<HTMLElement>(null)
  useEffect(() => {
    let current = true
    getPlannerCatalogue('inline').then(value => { if (current) { setCatalogue(value); setCatalogueError(null) } })
      .catch(cause => { if (current) setCatalogueError(plannerError(cause, false)) })
      .finally(() => { if (current) setCatalogueLoading(false) })
    return () => { current = false }
  }, [reload])
  const destination = catalogue?.destinations.find(d => d.destinationId === destinationId)
  function invalidate() { setResult(null); setSelected(null); setError(null); setSaveError(null) }
  function retryCatalogue() { setCatalogueLoading(true); setCatalogueError(null); setReload(n => n + 1) }
  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (busy) return
    setError(null)
    if (!destination) { setError('Choose a destination to start planning.'); return }
    if (!starts || !ends) { setError('Choose both a start and end for your trip.'); return }
    let startsAt: string, endsAt: string
    try { startsAt = utcTime(starts, destination.timeZone); endsAt = utcTime(ends, destination.timeZone) }
    catch { setError('Choose complete, unambiguous local dates and times for this destination.'); return }
    const hours = Number(duration)
    const invalid = validatePlanningWindow(startsAt, endsAt, hours)
    if (invalid) { setError(invalid); return }
    setBusy(true)
    try {
      const suggestions = await createRecommendations({ targetDestinationId: destination.destinationId, startsAt, endsAt,
        durationHours: hours, preferredActivityIds: activities.filter(id => destination.activities.some(a => a.activityId === id)),
        experienceLevel: experience, includeBiodiversityContext: biodiversity, timeZone: destination.timeZone })
      setResult(suggestions); setSelected(suggestions.candidates[0] ?? null)
      setTripTitle(suggestions.candidates[0]?.title ?? '')
      requestAnimationFrame(() => resultSection.current?.scrollIntoView({ behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth', block: 'start' }))
    } catch (cause) { setError(plannerError(cause)) } finally { setBusy(false) }
  }
  async function loadSavedTrips(page = 1) {
    if (loadingTrips) return
    setLoadingTrips(true); setSaveError(null)
    try { const rows = await listItineraries(page); setSavedTrips(rows.filter(t => t.status === 'Draft')); setHasMoreTrips(rows.length === 20); setTripPage(page) }
    catch (cause) { setSaveError(plannerError(cause)) } finally { setLoadingTrips(false) }
  }
  async function save(event: React.FormEvent) {
    event.preventDefault()
    if (!selected || !result || saving || loadingTrips || !canSave) return
    if (!appendTo && !tripTitle.trim()) { setSaveError('Give your coastal day a name.'); return }
    setSaving(true); setSaveError(null)
    const extra: ItineraryItemInput = { destinationId: selected.destinationId, activityId: selected.activityId, offeringId: selected.offeringId,
      title: selected.title, orderIndex: 0, scheduledStart: selected.scheduledStart, scheduledEnd: selected.scheduledEnd, timeZone: selected.timeZone ?? 'Asia/Colombo' }
    try {
      const previous = appendTo ? await getItinerary(appendTo) : null
      const saved = previous ? await updateItinerary(previous.itineraryId, { title: previous.title, description: previous.description,
        timeZone: previous.timeZone, concurrencyVersion: previous.concurrencyVersion,
        startsAt: new Date(Math.min(Date.parse(previous.startsAt), Date.parse(extra.scheduledStart))).toISOString(),
        endsAt: new Date(Math.max(Date.parse(previous.endsAt), Date.parse(extra.scheduledEnd))).toISOString(),
        items: [...previous.items.map((item, i) => ({ ...item, orderIndex: i })), { ...extra, orderIndex: previous.items.length }], recommendationId: result.recommendationId })
        : await createItinerary({ title: tripTitle.trim(), description: tripNotes.trim() || null, startsAt: selected.scheduledStart,
          endsAt: selected.scheduledEnd, timeZone: extra.timeZone, recommendationId: result.recommendationId, items: [extra] })
      saveDialog.current?.close(); navigate(`/planner/itineraries/${encodeURIComponent(saved.itineraryId)}`)
    } catch (cause) { setSaveError(plannerError(cause)) } finally { setSaving(false) }
  }
  return <>
    <PlannerSectionHeader section="create" />
    <div className="grid items-start gap-7 lg:grid-cols-[minmax(0,1fr)_20rem]">
      <div>
        {catalogueLoading ? <div aria-live="polite" className="rounded-[2rem] border border-coast-line bg-white p-9 text-coast-muted">Preparing your coastal planner…</div> : catalogueError || !destination && (!catalogue || catalogue.status !== 'AVAILABLE' || !catalogue.destinations.length) ? <section className="rounded-[2rem] border border-coast-line bg-white p-8 sm:p-10" role={catalogueError ? 'alert' : undefined}><span aria-hidden="true" className="inline-flex h-12 w-12 items-center justify-center rounded-2xl bg-coast-sage text-xl text-coast-teal">≈</span><h2 className="mt-5 font-display text-2xl">{catalogueError ? 'We couldn’t load destinations.' : 'Destinations aren’t available right now.'}</h2><p className="mt-3 max-w-lg text-sm leading-7 text-coast-muted">{catalogueError ?? catalogue?.message ?? 'Current experience information is unavailable. Please try again shortly.'}</p><div className="mt-6 flex flex-wrap gap-3"><button className={primaryButton} onClick={retryCatalogue}>Try again</button>{canSave && <Link className={secondaryButton} to="/planner/saved">View saved trips</Link>}</div><p className="mt-5 text-xs leading-6 text-coast-muted">Your existing plans are still yours. You can revisit them while fresh destination information returns.</p></section> : <form onSubmit={submit} noValidate className="rounded-[2rem] border border-coast-line bg-white p-6 sm:p-9">
          <fieldset disabled={busy} className="min-w-0 space-y-8">
            <PlanningSection number="01" title="Choose your stretch of coast">
              <label htmlFor="planner-destination" className="block text-sm font-bold">Destination<select id="planner-destination" required className={field} value={destinationId} onChange={e => { setDestinationId(e.target.value); setActivities([]); invalidate() }}><option value="">Choose your stretch of coast</option>{catalogue?.destinations.map(d => <option key={d.destinationId} value={d.destinationId}>{d.name} · {d.region}</option>)}</select></label>
              {destination && <fieldset className="mt-6"><legend className="text-sm font-bold">What would you like to do? <span className="font-normal text-coast-muted">Optional</span></legend><p className="mt-2 text-xs leading-6 text-coast-muted">Choose a few favourites, or leave this open to discover what fits.</p><div className="mt-3 flex flex-wrap gap-2">{destination.activities.map(a => <label key={a.activityId} className={`inline-flex min-h-11 cursor-pointer items-center gap-2 rounded-full border px-4 py-2 text-sm ${activities.includes(a.activityId) ? 'border-coast-teal bg-coast-sage font-semibold text-coast-deep' : 'border-coast-line text-coast-muted hover:border-coast-teal'}`}><input className="accent-coast-deep" type="checkbox" checked={activities.includes(a.activityId)} onChange={e => { setActivities(values => e.target.checked ? [...values, a.activityId] : values.filter(id => id !== a.activityId)); invalidate() }} />{a.name}</label>)}</div></fieldset>}
            </PlanningSection>
            <PlanningSection number="02" title="Make a little time for it">
              <p id="planner-zone" className="mb-4 text-xs leading-6 text-coast-muted">{destination ? `All times here are local to your destination (${destination.timeZone}).` : 'Choose a destination first. Times will use its local time zone.'} Plan up to 30 days ahead.</p>
              <div className="grid gap-4 sm:grid-cols-2"><label className="text-sm font-bold" htmlFor="planner-starts">Trip starts<input id="planner-starts" type="datetime-local" required disabled={!destination} aria-describedby="planner-zone" className={field} value={starts} onChange={e => { setStarts(e.target.value); invalidate() }} /></label><label className="text-sm font-bold" htmlFor="planner-ends">Trip ends<input id="planner-ends" type="datetime-local" required disabled={!destination} aria-describedby="planner-zone" className={field} value={ends} onChange={e => { setEnds(e.target.value); invalidate() }} /></label></div>
            </PlanningSection>
            <PlanningSection number="03" title="Set the pace of your day">
              <div className="grid gap-4 sm:grid-cols-2"><label className="text-sm font-bold" htmlFor="planner-duration">Time for an experience<select id="planner-duration" className={field} value={duration} onChange={e => { setDuration(e.target.value); invalidate() }}>{PACE_OPTIONS.map(p => <option key={p.value} value={p.value}>{p.label}</option>)}</select></label><label className="text-sm font-bold" htmlFor="planner-experience">Your experience level<select id="planner-experience" className={field} value={experience} onChange={e => { setExperience(e.target.value); invalidate() }}>{EXPERIENCE_OPTIONS.map(p => <option key={p.value} value={p.value}>{p.label}</option>)}</select></label></div>
              <label className="mt-6 flex cursor-pointer items-start gap-3 rounded-2xl bg-coast-paper p-5"><input type="checkbox" className="mt-1 h-4 w-4 accent-coast-deep" checked={biodiversity} onChange={e => { setBiodiversity(e.target.checked); invalidate() }} /><span><span className="block text-sm font-bold">Include wildlife context</span><span className="mt-1 block text-xs leading-6 text-coast-muted">Optional sourced context, when available. Wildlife sightings are never guaranteed.</span></span></label>
            </PlanningSection>
          </fieldset>
          {error && <p role="alert" className="mt-6 rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm leading-6 text-amber-950">{error}</p>}
          <div className="mt-8 flex flex-wrap items-center justify-between gap-4 border-t border-coast-line pt-6"><p className="max-w-xs text-xs leading-6 text-coast-muted">We’ll check what fits your choices. You decide what to keep.</p><button type="submit" disabled={busy} className={primaryButton}>{busy ? 'Finding experiences…' : 'Find my coastal day'}<span aria-hidden="true">↗</span></button></div>
        </form>}
      </div>
      <aside aria-label="Your planning guide" className="rounded-[2rem] bg-coast-sage p-7 lg:sticky lg:top-28"><p className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">Your day at a glance</p><h2 className="mt-4 font-display text-2xl leading-tight">A little space<br />for a great day.</h2><dl className="mt-6 space-y-4 border-y border-coast-line py-5"><div><dt className="text-xs text-coast-muted">Your coast</dt><dd className="mt-1 text-sm font-bold">{destination?.name ?? 'Somewhere you’ll love'}</dd></div><div><dt className="text-xs text-coast-muted">Your time</dt><dd className="mt-1 text-sm font-bold">{starts && ends ? `${starts.replace('T', ' ')} → ${ends.replace('T', ' ')}` : 'A window that works for you'}</dd></div><div><dt className="text-xs text-coast-muted">Your pace</dt><dd className="mt-1 text-sm font-bold">{PACE_OPTIONS.find(p => p.value === duration)?.label}</dd></div></dl><p className="mt-5 text-xs leading-6 text-coast-muted">Suggestions use current availability, marine suitability, and operating information. Missing evidence is made clear.</p><Link to="/planner" className="mt-4 inline-flex min-h-10 items-center text-xs font-bold text-coast-blue underline underline-offset-4">How the planner works ↗</Link><p className="mt-3 text-xs leading-6 text-coast-muted">Save a draft first. Arrange your itinerary and confirm it when you’re ready. Saving does not make a booking.</p>{canSave && <Link to="/planner/saved" className="mt-5 inline-flex min-h-11 items-center gap-3 text-sm font-bold text-coast-deep underline decoration-coast-line underline-offset-4">Open your plan history <span aria-hidden="true">↗</span></Link>}</aside>
    </div>
    {result && <section ref={resultSection} aria-labelledby="your-experiences-title" className="mt-12 scroll-mt-28"><div className="mb-6 flex flex-wrap items-end justify-between gap-4"><div><p className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">The possibilities for your day</p><h2 id="your-experiences-title" className="mt-3 font-display text-3xl tracking-tight">{result.candidates.length ? 'Find a moment you’d like to keep.' : 'No verified matches for this trip.'}</h2></div>{hasAllPermissions(user, ['planner.recommendations.read']) && <Link to={`/planner/recommendations/${encodeURIComponent(result.recommendationId)}`} className={secondaryButton}>Open these suggestions <span aria-hidden="true">↗</span></Link>}</div>
      {result.uncertaintyNotes.map(note => <p key={note} className="mb-4 rounded-2xl bg-amber-50 p-4 text-sm leading-6 text-amber-950">{note}</p>)}
      {!result.candidates.length ? <p className="rounded-2xl border border-coast-line bg-white p-7 leading-7 text-coast-muted">{result.outcome === 'DEPENDENCIES_UNAVAILABLE' ? 'Current experience or condition information is unavailable. Please try again later.' : 'Try a different window of time or keep your interests open. Your choices are still here.'}</p> : <fieldset><legend className="sr-only">Choose an experience for your plan</legend><div className="grid gap-5 md:grid-cols-2">{result.candidates.map((candidate, index) => <label key={`${candidate.offeringId}:${candidate.scheduledStart}:${candidate.activityId}`} className={`cursor-pointer rounded-3xl border-2 p-6 transition ${selected === candidate ? 'border-coast-teal bg-white shadow-sm' : 'border-coast-line bg-coast-pearl hover:border-coast-teal'}`}><div className="flex items-start justify-between gap-4"><div><p className="text-xs font-bold text-coast-teal">EXPERIENCE {String(index + 1).padStart(2, '0')}</p><h3 className="mt-3 font-display text-2xl tracking-tight">{candidate.title}</h3></div><input type="radio" name="selected-experience" aria-label={`Choose ${candidate.title}`} className="mt-1 h-5 w-5 shrink-0 accent-coast-teal" checked={selected === candidate} onChange={() => { setSelected(candidate); setTripTitle(candidate.title) }} /></div><p className="mt-4 text-sm leading-6 text-coast-muted">{dateTime(candidate.scheduledStart, candidate.timeZone)} – {dateTime(candidate.scheduledEnd, candidate.timeZone)}</p><div className="mt-4 flex flex-wrap gap-2 text-xs"><span className="rounded-lg bg-coast-sage px-3 py-2">{statusLabel(candidate.availabilityStatus)}</span><span className="rounded-lg bg-coast-sage px-3 py-2">{statusLabel(candidate.suitability.status)}</span><span className="rounded-lg bg-coast-sage px-3 py-2">Operations: {statusLabel(candidate.operationalStatus)}</span></div><ul className="mt-4 space-y-2 text-sm leading-6 text-coast-muted">{candidate.reasons.map(reason => <li key={reason}>• {reason}</li>)}</ul>{candidate.suitability.marineConditionTime && <p className="mt-4 text-xs text-coast-muted">Conditions checked {dateTime(candidate.suitability.marineConditionTime, candidate.timeZone)}</p>}</label>)}</div></fieldset>}
      {selected && canSave && <div className="mt-6 flex flex-wrap items-center justify-between gap-5 rounded-3xl bg-coast-deep p-6 text-white"><div><p className="text-xs text-coast-glass">Your chosen experience</p><p className="mt-2 text-lg font-bold">{selected.title}</p></div><button className="inline-flex min-h-12 items-center gap-3 rounded-full bg-white px-6 py-3 text-sm font-bold text-coast-deep hover:bg-coast-glass focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-white" onClick={() => saveDialog.current?.showModal()}>Save a draft plan <span aria-hidden="true">↗</span></button></div>}
    </section>}
    <dialog ref={saveDialog} aria-labelledby="save-trip-title" onCancel={event => { if (saving) event.preventDefault() }} className="m-auto max-h-[85dvh] w-[calc(100%-2rem)] max-w-lg overflow-y-auto rounded-[2rem] border border-coast-line bg-white p-7 text-coast-ink shadow-xl backdrop:bg-coast-ink/50 sm:p-9"><form onSubmit={save}><p className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">Keep a day to look forward to</p><h2 id="save-trip-title" className="mt-3 font-display text-2xl">Make this day yours.</h2><p className="mt-3 text-sm leading-7 text-coast-muted">Save a private draft to arrange and review later. You can confirm it from your plan’s details.</p>
      <fieldset disabled={saving || loadingTrips} className="mt-5 min-w-0 space-y-5"><label htmlFor="plan-trip-title" className="block text-sm font-bold">Trip name<input id="plan-trip-title" className={field} maxLength={150} value={tripTitle} onChange={e => setTripTitle(e.target.value)} /></label><label htmlFor="plan-trip-notes" className="block text-sm font-bold">Your notes<textarea id="plan-trip-notes" className={field} maxLength={500} rows={3} value={tripNotes} onChange={e => setTripNotes(e.target.value)} /></label>
      {savedTrips === null ? <button type="button" className={secondaryButton} onClick={() => loadSavedTrips()}>Add to a saved trip instead</button> : <div><label htmlFor="append-trip" className="block text-sm font-bold">Save to<select id="append-trip" className={field} value={appendTo} onChange={e => setAppendTo(e.target.value)}><option value="">A new trip</option>{savedTrips.map(t => <option key={t.itineraryId} value={t.itineraryId}>{t.title}</option>)}</select></label>{!savedTrips.length && <p className="mt-3 text-xs leading-6 text-coast-muted">No editable drafts on this page. Create a new plan or browse another page.</p>}<div className="mt-3 flex gap-3"><button type="button" className={secondaryButton} disabled={tripPage === 1 || loadingTrips} onClick={() => loadSavedTrips(tripPage - 1)}>Previous trips</button><button type="button" className={secondaryButton} disabled={!hasMoreTrips || loadingTrips} onClick={() => loadSavedTrips(tripPage + 1)}>More trips</button></div></div>}
      </fieldset>{saveError && <p role="alert" className="mt-4 rounded-xl bg-amber-50 p-4 text-sm text-amber-950">{saveError}</p>}<div className="mt-7 flex flex-wrap gap-3"><button type="submit" className={primaryButton} disabled={saving || loadingTrips || (!appendTo && !tripTitle.trim())}>{saving ? 'Saving…' : 'Save my trip'}</button><button type="button" className={secondaryButton} disabled={saving} onClick={() => { saveDialog.current?.close(); setSaveError(null) }}>Keep exploring</button></div></form></dialog>
  </>
}
