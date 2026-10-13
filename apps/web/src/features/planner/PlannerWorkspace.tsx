import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import coastImage from '../../assets/coastal/coast-hero-daylight.webp'
import PlannerLayout, { field, primaryButton, secondaryButton } from './PlannerLayout'
import { useAuthSession } from '../auth/authSession'
import { hasAllPermissions } from '../authorization/permissions'
import { createItinerary, createRecommendations, getBiodiversityPredictions, getPlannerCatalogue, getRecommendation, getWorkflow,
  getItinerary, listItineraries, updateItinerary,
  type Itinerary, type BiodiversityPrediction, type PlannerCatalogue, type RecommendationCandidate, type RecommendationResult, type WorkflowStatus } from './plannerApi'
import { dateTime, plannerError, statusLabel, utcTime, validatePlanningWindow } from './plannerPresentation'

export default function PlannerWorkspace() {
  const { recommendationId } = useParams()
  return <PlannerLayout permission="planner.recommendations.read"><Content key={recommendationId ?? 'new'} /></PlannerLayout>
}

function Content() {
  const { recommendationId } = useParams()
  const navigate = useNavigate()
  const { user } = useAuthSession()
  const [catalogue, setCatalogue] = useState<PlannerCatalogue | null>(null)
  const [loading, setLoading] = useState(true)
  const [reload, setReload] = useState(0)
  const [destinationId, setDestinationId] = useState('')
  const [activities, setActivities] = useState<string[]>([])
  const [starts, setStarts] = useState('')
  const [ends, setEnds] = useState('')
  const [duration, setDuration] = useState('4')
  const [experience, setExperience] = useState('INTERMEDIATE')
  const [biodiversity, setBiodiversity] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<RecommendationResult | null>(null)
  const [busy, setBusy] = useState(false)
  const [workflow, setWorkflow] = useState<WorkflowStatus | null>(null)
  const [selected, setSelected] = useState<RecommendationCandidate | null>(null)
  const [tripTitle, setTripTitle] = useState('')
  const saveDialog = useRef<HTMLDialogElement>(null)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [savedTrips, setSavedTrips] = useState<Itinerary[] | null>(null)
  const [appendTo, setAppendTo] = useState('')
  const [tripPage, setTripPage] = useState(1)
  const destination = catalogue?.destinations.find(d => d.destinationId === destinationId)

  useEffect(() => {
    let current = true
    const pending = recommendationId ? getRecommendation(recommendationId) : getPlannerCatalogue()
    pending.then(value => {
      if (!current) return
      if (recommendationId) setResult(value as RecommendationResult)
      else setCatalogue(value as PlannerCatalogue)
      setError(null)
    }).catch(cause => { if (current) setError(plannerError(cause)) }).finally(() => { if (current) setLoading(false) })
    return () => { current = false }
  }, [recommendationId, reload])

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (busy) return
    setError(null)
    if (!destination) { setError('Choose a destination to start planning.'); return }
    if (!starts || !ends) { setError('Choose both a start and an end date for the trip.'); return }
    let startsAt: string, endsAt: string
    try { startsAt = utcTime(starts, destination.timeZone); endsAt = utcTime(ends, destination.timeZone) }
    catch (cause) { setError(cause instanceof Error ? cause.message : 'Choose valid trip dates.'); return }
    const hours = Number(duration)
    const problem = validatePlanningWindow(startsAt, endsAt, hours)
    if (problem) { setError(problem); return }
    setBusy(true)
    try {
      const suggestions = await createRecommendations({ targetDestinationId: destination.destinationId, startsAt, endsAt,
        durationHours: hours, preferredActivityIds: activities, experienceLevel: experience, includeBiodiversityContext: biodiversity })
      navigate(`/planner/recommendations/${encodeURIComponent(suggestions.recommendationId)}`)
    } catch (cause) { setError(plannerError(cause)) } finally { setBusy(false) }
  }
  function choose(candidate: RecommendationCandidate) {
    setSelected(candidate); setTripTitle(candidate.title); setSaveError(null); setAppendTo(''); setSavedTrips(null); saveDialog.current?.showModal()
  }
  async function loadSavedTrips(page = 1) {
    setBusy(true)
    try { setSavedTrips(await listItineraries(page)); setTripPage(page); setSaveError(null) }
    catch (cause) { setSaveError(plannerError(cause)) } finally { setBusy(false) }
  }
  async function save(event: React.FormEvent) {
    event.preventDefault()
    if (!selected || !result || busy) return
    setBusy(true)
    try {
      if (appendTo) {
        const previous = await getItinerary(appendTo)
        const extra = { destinationId: selected.destinationId, activityId: selected.activityId, offeringId: selected.offeringId,
          title: selected.title, orderIndex: previous.items.length, scheduledStart: selected.scheduledStart,
          scheduledEnd: selected.scheduledEnd, timeZone: selected.timeZone ?? 'Asia/Colombo' }
        const trip = await updateItinerary(appendTo, { title: previous.title, description: previous.description,
          timeZone: previous.timeZone, concurrencyVersion: previous.concurrencyVersion,
          startsAt: new Date(Math.min(Date.parse(previous.startsAt), Date.parse(extra.scheduledStart))).toISOString(),
          endsAt: new Date(Math.max(Date.parse(previous.endsAt), Date.parse(extra.scheduledEnd))).toISOString(),
          items: [...previous.items.map((item, index) => ({ ...item, orderIndex: index })), extra], recommendationId: result.recommendationId })
        saveDialog.current?.close(); navigate(`/planner/itineraries/${encodeURIComponent(trip.itineraryId)}`)
        return
      }
      const trip = await createItinerary({ title: tripTitle.trim(), description: null, startsAt: selected.scheduledStart,
        endsAt: selected.scheduledEnd, recommendationId: result.recommendationId, timeZone: selected.timeZone ?? 'Asia/Colombo',
        items: [{ destinationId: selected.destinationId, activityId: selected.activityId, offeringId: selected.offeringId,
          title: selected.title, orderIndex: 0, scheduledStart: selected.scheduledStart, scheduledEnd: selected.scheduledEnd, timeZone: selected.timeZone ?? 'Asia/Colombo' }] })
      saveDialog.current?.close(); navigate(`/planner/itineraries/${encodeURIComponent(trip.itineraryId)}`)
    } catch (cause) { setSaveError(plannerError(cause)) } finally { setBusy(false) }
  }
  async function inspectWorkflow() {
    if (!result || busy) return
    setBusy(true)
    try { setWorkflow(await getWorkflow(result.workflowId)) } catch (cause) { setError(plannerError(cause)) } finally { setBusy(false) }
  }
  return <>
    {!recommendationId && <section className="mb-10 grid overflow-hidden rounded-[2rem] bg-coast-deep text-white md:grid-cols-[1.2fr_1fr]">
      <div className="px-7 py-10 sm:px-10 sm:py-14"><p className="text-xs font-bold uppercase tracking-[0.2em] text-coast-glass">A little planning. A lot of coast.</p>
        <h1 className="mt-4 max-w-lg font-display text-4xl leading-[1.12] tracking-[-0.04em] sm:text-5xl">Find your kind<br />of coastal day.</h1>
        <p className="mt-5 max-w-md leading-7 text-white/85">Choose a place and a pace. We’ll help you find experiences that fit your time, with conditions checked before you decide.</p>
        <div className="mt-7 flex flex-wrap gap-x-5 gap-y-2 text-xs font-semibold text-coast-glass"><span>Published experiences</span><span>Condition checks</span><span>Plans you can revisit</span></div>
      </div><img src={coastImage} alt="Sunlit Sri Lankan coastline and clear blue water" className="h-56 w-full object-cover md:h-full" />
    </section>}
    {error && <div role="alert" className="mb-6 rounded-2xl border border-amber-200 bg-amber-50 p-5 text-sm text-amber-950">{error}</div>}
    {loading ? <p aria-live="polite" className="py-16 text-coast-muted">Preparing your coastal planner…</p> : recommendationId ? <>
      <div className="mb-8 flex flex-wrap items-end justify-between gap-6 rounded-[2rem] border border-coast-line bg-white p-7 sm:p-10"><div><p className="text-xs font-bold uppercase tracking-[0.2em] text-coast-teal">Your coastal possibilities</p><h1 className="mt-4 font-display text-4xl tracking-[-0.04em] sm:text-5xl">A day worth making time for.</h1><p className="mt-3 max-w-xl text-coast-muted">Compare suggestions and review any cautions, then save the experience you’d like to plan around.</p></div><Link className={secondaryButton} to="/planner/plan">Create another plan</Link></div>
      {!result ? <button className={primaryButton} onClick={() => { setLoading(true); setReload(n => n + 1) }}>Try loading again</button> : <>
        <section aria-labelledby="planner-response-title" aria-live="polite" className="mb-7 grid gap-5 rounded-3xl border border-coast-line bg-coast-paper p-5 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center sm:p-7">
          <div><p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">PLANNING RESPONSE</p><h2 className="mt-2 font-display text-2xl" id="planner-response-title">Here’s what the checks found</h2><p className="mt-2 max-w-2xl text-sm leading-6 text-coast-muted">{recommendationOutcomeMessage(result)}</p></div>
          <dl className="grid grid-cols-2 gap-x-6 gap-y-3 text-sm sm:min-w-52"><div><dt className="text-xs font-bold text-coast-muted">Suggestions returned</dt><dd className="mt-1 font-display text-xl">{result.candidates.length}</dd></div><div><dt className="text-xs font-bold text-coast-muted">Other options not shown</dt><dd className="mt-1 font-display text-xl">{result.excludedCandidatesCount}</dd></div></dl>
        </section>
        {result.uncertaintyNotes.length > 0 && <aside className="mb-7 rounded-2xl border border-amber-200 bg-amber-50 px-6 py-5"><h2 className="font-bold text-amber-950">Information to review</h2><p className="mt-2 text-sm leading-6 text-amber-950">Some information could not be verified. Suggestions are planning guidance, not a booking or a guarantee of safety.</p><details className="mt-3 text-sm text-amber-950"><summary className="cursor-pointer font-semibold focus-visible:outline-2">See what could not be checked</summary><ul className="mt-3 space-y-2">{result.uncertaintyNotes.map(note => <li key={note}>{note.replace(/[0-9a-f]{8}-[0-9a-f-]{27,}/gi, 'this experience')}</li>)}</ul></details></aside>}
        {result.candidates.length ? <div className="grid gap-6 lg:grid-cols-2">{result.candidates.map((candidate, index) => <Candidate key={`${candidate.offeringId}-${candidate.scheduledStart}`} candidate={candidate} index={index} canSave={hasAllPermissions(user, ['planner.itineraries.manage'])} canInspect={hasAllPermissions(user, ['planner.biodiversity.read'])} onChoose={choose} />)}</div> : <section className="rounded-3xl border border-coast-line bg-white px-8 py-14 text-center"><div aria-hidden="true" className="mb-5 text-4xl text-coast-teal">≈</div><h2 className="font-display text-2xl">No verified matches for this trip.</h2><p className="mx-auto mt-3 max-w-lg leading-7 text-coast-muted">Try another time, widen your trip window, or choose a different activity. When condition information is unavailable, we leave an experience out.</p><Link className={`${primaryButton} mt-6`} to="/planner/plan">Adjust your trip</Link></section>}
        <div className="mt-8 flex flex-wrap items-center justify-between gap-4 text-xs text-coast-muted"><p>Search completed {dateTime(result.generatedAt)} · Experience times shown in the destination’s local time zone</p>{hasAllPermissions(user, ['planner.workflows.read']) && <button disabled={busy} className="min-h-11 rounded-full px-4 font-semibold underline decoration-coast-line underline-offset-4 focus-visible:outline-2 focus-visible:outline-coast-blue disabled:cursor-wait" onClick={inspectWorkflow}>{busy ? 'Checking coordination…' : 'View coordination status'}</button>}</div>
        {workflow && <section aria-labelledby="planner-coordination-title" aria-live="polite" className="mt-3 rounded-2xl border border-coast-line bg-white p-5 text-sm">
          <h2 className="font-bold" id="planner-coordination-title">Planning agent coordination</h2>
          <dl className="mt-3 grid gap-3 sm:grid-cols-2"><div><dt className="text-xs font-bold text-coast-muted">Coordinator availability</dt><dd className="mt-1 font-semibold">{humanWorkflowStatus(workflow.aiDependencyStatus)}</dd></div><div><dt className="text-xs font-bold text-coast-muted">Agent execution</dt><dd className="mt-1 font-semibold">{humanWorkflowStatus(workflow.aiExecutionStatus)}</dd></div></dl>
          <p className="mt-3 leading-6 text-coast-muted">{agentExecutionDescription(workflow.aiExecutionStatus)}</p>
          {workflow.status === 'FAILED' && workflow.failureReason && <p className="mt-3 rounded-xl bg-red-50 p-3 leading-6 text-red-900" role="alert">{workflow.failureReason}</p>}
          {workflow.status !== 'FAILED' && workflow.resultSummary && <p className="mt-3 leading-6">{workflow.resultSummary}</p>}
          {workflow.aiExecutionSummary ? <div className="mt-4 border-t border-coast-line pt-4">
            <h3 className="font-bold">What the coastal checks found</h3>
            <p className="mt-2 leading-6 text-coast-muted">{workflow.aiExecutionSummary.objectiveSummary}</p>
            {workflow.aiExecutionSummary.steps.length > 0 && <ul className="mt-3 grid gap-3">
              {workflow.aiExecutionSummary.steps.map((step, index) => <li className="rounded-2xl bg-coast-paper p-4" key={`${step.agentId}-${index}`}>
                <div className="flex flex-wrap items-center justify-between gap-2"><h4 className="font-semibold">{agentDisplayName(step.agentId)}</h4><span className="rounded-full bg-white px-3 py-1 text-xs font-bold">{agentStepStatus(step.status)}</span></div>
                {step.summary && <p className="mt-2 text-sm leading-6">{step.summary}</p>}
                {step.sourceReferences.length > 0 && <p className="mt-2 text-xs text-coast-muted">Evidence sources considered: {step.sourceReferences.length}</p>}
                {step.uncertaintyNotes.length > 0 && <div className="mt-3 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-950"><p className="font-semibold">Information to review</p><ul className="mt-1 list-disc space-y-1 pl-5">{step.uncertaintyNotes.map((note, noteIndex) => <li key={`${noteIndex}-${note}`}>{note}</li>)}</ul></div>}
              </li>)}
            </ul>}
            {workflow.aiExecutionSummary.safetyProposal && <div className="mt-3 rounded-2xl border border-coast-glass bg-coast-sage p-4">
              <h4 className="font-bold">Safety recommendation</h4>
              <p className="mt-2 text-sm font-semibold">{humanAgentValue(workflow.aiExecutionSummary.safetyProposal.recommendation)}</p>
              <p className="mt-2 text-sm leading-6">{workflow.aiExecutionSummary.safetyProposal.summary}</p>
              {workflow.aiExecutionSummary.safetyProposal.proposedAction && <p className="mt-2 text-sm">Suggested operational action: <span className="font-semibold">{humanAgentValue(workflow.aiExecutionSummary.safetyProposal.proposedAction)}</span></p>}
              {workflow.aiExecutionSummary.safetyProposal.evidenceReferences.length > 0 && <p className="mt-2 text-xs text-coast-muted">Evidence sources considered: {workflow.aiExecutionSummary.safetyProposal.evidenceReferences.length}</p>}
              {workflow.aiExecutionSummary.safetyProposal.uncertainties.length > 0 && <ul className="mt-2 list-disc space-y-1 pl-5 text-sm">{workflow.aiExecutionSummary.safetyProposal.uncertainties.map((note, index) => <li key={`${index}-${note}`}>{note}</li>)}</ul>}
              <p className="mt-3 border-t border-coast-glass pt-3 text-xs leading-5 text-coast-muted">This is a recommendation for planning. It does not reserve an experience or change coastal access.</p>
            </div>}
          </div> : <p className="mt-3 border-t border-coast-line pt-3 text-xs leading-5 text-coast-muted">This status response reports coordinator availability and execution state. It does not include separate responses from each delegated agent.</p>}
        </section>}
      </>}
    </> : <div className="grid items-start gap-8 lg:grid-cols-[minmax(0,1fr)_300px]">
      <section className="rounded-3xl border border-coast-line bg-white p-6 sm:p-9"><div className="mb-8"><p className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">Start with what suits you</p><h2 className="mt-2 font-display text-3xl tracking-tight">Where shall we go?</h2><p className="mt-3 leading-6 text-coast-muted">Your next coastal day starts here. Plan up to 30 days ahead.</p></div>
        {!catalogue || catalogue.status === 'UNAVAILABLE' || !catalogue.destinations.length ? <div className="rounded-2xl bg-coast-sage p-6"><h3 className="text-lg font-bold">Destinations aren’t available right now.</h3><p className="mt-2 text-sm leading-6 text-coast-muted">{catalogue?.message ?? 'Please try again shortly. You can still revisit your saved trips.'}</p><div className="mt-5 flex flex-wrap gap-3"><button className={secondaryButton} onClick={() => { setLoading(true); setReload(n => n + 1) }}>Try again</button>{hasAllPermissions(user, ['planner.itineraries.manage']) && <Link className={secondaryButton} to="/planner/saved">View saved trips</Link>}</div></div> : <form onSubmit={submit} noValidate>
          <label htmlFor="planner-destination" className="block text-sm font-bold">Destination<select id="planner-destination" className={field} value={destinationId} onChange={e => { setDestinationId(e.target.value); setActivities([]) }}><option value="">Choose your stretch of coast</option>{catalogue.destinations.map(d => <option key={d.destinationId} value={d.destinationId}>{d.name} · {d.region}</option>)}</select></label>
          {destination && <fieldset className="mt-7"><legend className="text-sm font-bold">What would you like to do? <span className="font-normal text-coast-muted">Optional</span></legend><p className="mt-2 text-xs text-coast-muted">Leave these open to discover everything that fits.</p><div className="mt-3 flex flex-wrap gap-2">{destination.activities.map(activity => <label key={activity.activityId} className={`flex min-h-11 cursor-pointer items-center gap-2 rounded-full border px-4 py-2 text-sm transition ${activities.includes(activity.activityId) ? 'border-coast-deep bg-coast-sage font-bold text-coast-deep' : 'border-coast-line hover:border-coast-teal'}`}><input type="checkbox" className="accent-coast-deep" checked={activities.includes(activity.activityId)} onChange={e => setActivities(a => e.target.checked ? [...a, activity.activityId] : a.filter(id => id !== activity.activityId))} />{activity.name}</label>)}</div></fieldset>}
          <fieldset className="mt-8"><legend className="text-sm font-bold">When do you have time?</legend><p id="planner-zone" className="mt-2 text-xs text-coast-muted">{destination ? `All times here are local to your destination (${destination.timeZone}).` : 'Choose a destination first. Times will use its local time zone.'}</p><div className="mt-4 grid gap-4 sm:grid-cols-2"><label className="text-sm font-semibold" htmlFor="planner-starts">Trip starts<input aria-describedby="planner-zone" disabled={!destination} id="planner-starts" className={field} type="datetime-local" value={starts} onChange={e => setStarts(e.target.value)} /></label><label className="text-sm font-semibold" htmlFor="planner-ends">Trip ends<input aria-describedby="planner-zone" disabled={!destination} id="planner-ends" className={field} type="datetime-local" value={ends} onChange={e => setEnds(e.target.value)} /></label></div></fieldset>
          <div className="mt-7 grid gap-4 sm:grid-cols-2"><label className="text-sm font-bold" htmlFor="planner-duration">Hours for an experience<input id="planner-duration" className={field} type="number" min="1" max="720" step="1" value={duration} onChange={e => setDuration(e.target.value)} /></label><label className="text-sm font-bold" htmlFor="planner-experience">Your experience level<select id="planner-experience" className={field} value={experience} onChange={e => setExperience(e.target.value)}><option value="BEGINNER">Beginner</option><option value="INTERMEDIATE">Intermediate</option><option value="ADVANCED">Advanced</option></select></label></div>
          <label className="mt-7 flex cursor-pointer items-start gap-3 rounded-2xl bg-coast-paper p-4 text-sm"><input className="mt-1 h-4 w-4 accent-coast-deep" type="checkbox" checked={biodiversity} onChange={e => setBiodiversity(e.target.checked)} /><span><span className="block font-bold">Include wildlife context</span><span className="mt-1 block leading-6 text-coast-muted">Optional sourced predictions, when available. Wildlife sightings are never guaranteed.</span></span></label>
          <button disabled={busy} className={`${primaryButton} mt-8 w-full sm:w-auto`} type="submit">{busy ? 'Finding experiences…' : 'Find my coastal day'}<span aria-hidden="true">↗</span></button>
        </form>}
      </section><aside className="space-y-7 p-3 lg:pt-8"><div><span className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">Thoughtfully planned</span><h2 className="mt-3 font-display text-2xl leading-tight">More time enjoying.<br />Less time guessing.</h2></div>{[['01', 'Make it yours', 'Choose your destination, available time and the activities you enjoy.'], ['02', 'Know what to expect', 'We check availability, marine suitability and operating restrictions. Missing evidence is made clear.'], ['03', 'Keep a plan you love', 'Save an experience, shape your itinerary, and review conditions again before you go.']].map(([step, title, copy]) => <div key={step} className="border-t border-coast-line pt-5"><p className="text-xs font-bold text-coast-teal">{step}</p><h3 className="mt-2 font-bold">{title}</h3><p className="mt-2 text-sm leading-6 text-coast-muted">{copy}</p></div>)}</aside>
    </div>}
    <dialog ref={saveDialog} aria-labelledby="save-trip-title" className="m-auto w-[calc(100%-2rem)] max-w-lg rounded-3xl border-0 bg-white p-7 text-coast-ink shadow-xl backdrop:bg-coast-ink/40"><form onSubmit={save}><h2 id="save-trip-title" className="font-display text-2xl">Make this day yours.</h2><p className="mt-3 text-sm leading-6 text-coast-muted">Save a private trip to edit and review later. Saving a plan doesn’t book the experience.</p><label htmlFor="save-trip-name" className="mt-6 block text-sm font-bold">Trip name<input id="save-trip-name" required disabled={Boolean(appendTo)} maxLength={150} className={field} value={tripTitle} onChange={e => setTripTitle(e.target.value)} /></label>{savedTrips === null ? <button disabled={busy} className={secondaryButton + " mt-5"} type="button" onClick={() => loadSavedTrips()}>Add to a saved trip instead</button> : <div className="mt-5"><label className="text-sm font-bold" htmlFor="append-trip">Save to<select id="append-trip" className={field} value={appendTo} onChange={e => setAppendTo(e.target.value)}><option value="">A new trip</option>{savedTrips.map(trip => <option key={trip.itineraryId} value={trip.itineraryId}>{trip.title}</option>)}</select></label><div className="mt-3 flex gap-3"><button className={secondaryButton} disabled={busy || tripPage === 1} type="button" onClick={() => loadSavedTrips(tripPage - 1)}>Previous trips</button><button className={secondaryButton} disabled={busy || savedTrips.length < 20} type="button" onClick={() => loadSavedTrips(tripPage + 1)}>More trips</button></div></div>}{saveError && <p role="alert" className="mt-4 text-sm text-amber-900">{saveError}</p>}<div className="mt-6 flex flex-wrap gap-3"><button disabled={busy || !tripTitle.trim()} className={primaryButton} type="submit">{busy ? 'Saving…' : 'Save my trip'}</button><button disabled={busy} className={secondaryButton} type="button" onClick={() => { saveDialog.current?.close(); setSelected(null) }}>Cancel</button></div></form></dialog>
  </>
}

function recommendationOutcomeMessage(result: RecommendationResult): string {
  if (result.outcome === 'DEPENDENCIES_UNAVAILABLE') return 'Some live coastal checks were unavailable, so not every option could be verified. Review the information below before making plans.'
  if (result.outcome === 'NO_MATCHES') return 'No experience passed the available checks for this search. Adjust the time or preferences and try again.'
  if (result.outcome === 'MATCHES_FOUND') return `The planner returned ${result.candidates.length} option${result.candidates.length === 1 ? '' : 's'}. Each card shows the returned reasons and current availability, marine suitability and operational status.`
  if (result.candidates.length > 0) return `The planner returned ${result.candidates.length} option${result.candidates.length === 1 ? '' : 's'}. Review the reasons and coastal checks on each card.`
  return 'The planner returned no options for this search. Review any unavailable information below, then adjust your time or preferences and try again.'
}

function humanWorkflowStatus(value: string): string {
  const labels: Record<string, string> = {
    AVAILABLE: 'Available',
    NOT_CONNECTED: 'Not connected',
    UNAVAILABLE: 'Unavailable',
    NOT_STARTED: 'No agent run recorded',
    PLANNING: 'Planning',
    DELEGATING: 'Gathering delegated results',
    BLOCKED: 'Blocked',
    COMPLETED: 'Completed',
    SAFE_FAILURE: 'Stopped safely',
  }
  return labels[value] ?? 'Status not recognized'
}

function agentExecutionDescription(value: string): string {
  const descriptions: Record<string, string> = {
    NOT_STARTED: 'No agent execution is recorded for this workflow. The suggestions above show the planner’s checked results.',
    PLANNING: 'The planning agent has begun working on this workflow. Check the coordination status again for updates.',
    DELEGATING: 'The planning agent is gathering delegated coastal information. Check the coordination status again for updates.',
    BLOCKED: 'The coordination workflow is blocked. Review the dependency availability and check again for an updated status.',
    COMPLETED: 'Agent coordination is complete. Review the coastal findings and any uncertainties below.',
    UNAVAILABLE: 'Agent coordination is unavailable. The recommendation cards still show the data returned by the planner.',
    SAFE_FAILURE: 'The agent workflow stopped safely. No agent proposal or action is included in this response.',
  }
  return descriptions[value] ?? 'The service returned an execution state this screen does not recognize.'
}

function agentDisplayName(agentId: string): string {
  const names: Record<string, string> = {
    'experience-biodiversity': 'Coastal experiences & wildlife',
    'marine-safety': 'Marine conditions',
    'safety-operations': 'Safety & Operations',
    'planning-coordination': 'Planning coordination',
  }
  return names[agentId] ?? 'Coastal check'
}

function agentStepStatus(value: string): string {
  const statuses: Record<string, string> = {
    COMPLETED: 'Complete', SUCCEEDED: 'Complete', AVAILABLE: 'Complete',
    RUNNING: 'In progress', IN_PROGRESS: 'In progress', PLANNING: 'In progress',
    UNAVAILABLE: 'Unavailable', FAILED: 'Could not complete', SAFE_FAILURE: 'Stopped safely',
  }
  return statuses[value.toUpperCase()] ?? 'Status not verified'
}

function humanAgentValue(value: string): string {
  return value.toLowerCase().split('_').map(part => part.charAt(0).toUpperCase() + part.slice(1)).join(' ')
}

function Candidate({ candidate, index, canSave, canInspect, onChoose }: { candidate: RecommendationCandidate; index: number; canSave: boolean; canInspect: boolean; onChoose: (candidate: RecommendationCandidate) => void }) {
  const [prediction, setPrediction] = useState<BiodiversityPrediction | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const cautious = candidate.suitability.status === 'CAUTION' || candidate.operationalStatus === 'CAUTION'
  async function inspect() {
    setBusy(true)
    try { setPrediction(await getBiodiversityPredictions(candidate.destinationId, candidate.activityId)); setError(null) }
    catch (cause) { setError(plannerError(cause)) } finally { setBusy(false) }
  }
  return <article className="flex flex-col rounded-[2rem] border border-coast-line bg-white p-6 transition hover:border-coast-teal sm:p-8"><div className="flex flex-wrap items-center justify-between gap-3"><p className="text-xs font-bold uppercase tracking-[0.15em] text-coast-teal">Option {String(index + 1).padStart(2, '0')}</p><span className={`rounded-full px-3 py-1.5 text-xs font-bold ${cautious ? 'bg-amber-50 text-amber-900' : 'bg-coast-sage text-coast-deep'}`}>{cautious ? 'A caution to review' : 'Matches your trip'}</span></div><h2 className="mt-5 font-display text-2xl tracking-tight">{candidate.title}</h2><p className="mt-3 text-sm text-coast-muted">{dateTime(candidate.scheduledStart, candidate.timeZone ?? 'Asia/Colombo')} – {dateTime(candidate.scheduledEnd, candidate.timeZone ?? 'Asia/Colombo')}</p><div className="my-6 flex flex-wrap gap-2 text-xs font-semibold"><span className="rounded-lg bg-coast-paper px-3 py-2">{statusLabel(candidate.availabilityStatus)}</span><span className="rounded-lg bg-coast-paper px-3 py-2">{statusLabel(candidate.suitability.status)}</span><span className="rounded-lg bg-coast-paper px-3 py-2">Operations: {statusLabel(candidate.operationalStatus)}</span></div><h3 className="text-sm font-bold">Why this fits</h3><ul className="mt-3 space-y-2 text-sm leading-6 text-coast-muted">{candidate.reasons.map(reason => <li className="flex gap-3" key={reason}><span aria-hidden="true" className="text-coast-teal">✓</span>{reason}</li>)}</ul><details className="mt-6 rounded-2xl border border-coast-line p-4 text-sm"><summary className="cursor-pointer font-bold focus-visible:outline-2 focus-visible:outline-coast-blue">Conditions & wildlife context</summary><p className="mt-3 text-xs leading-6 text-coast-muted">{candidate.suitability.marineConditionTime ? `Marine evidence for ${dateTime(candidate.suitability.marineConditionTime)}. Review again before departure.` : 'Marine evidence could not be verified.'}</p><p className="mt-3 leading-6 text-coast-muted">{candidate.biodiversityContext ? `${candidate.biodiversityContext.speciesName}. This is predicted context, not a promised sighting.` : 'No verified wildlife context was attached to this suggestion.'}</p>{canInspect && <button disabled={busy} className="mt-3 min-h-11 font-bold text-coast-deep underline underline-offset-4 focus-visible:outline-2" onClick={inspect}>{busy ? 'Checking wildlife context…' : 'Check wildlife context'}</button>}{error && <p role="alert" className="mt-3 text-amber-900">{error}</p>}{prediction && <div aria-live="polite" className="mt-3 text-sm leading-6 text-coast-muted"><p>{prediction.status === 'available' ? prediction.predictedSpecies.length ? prediction.predictedSpecies.map(s => `${s.commonName} (${s.scientificName})`).join(' · ') : 'No species were predicted for this location.' : prediction.message}</p>{prediction.uncertaintyText && <p className="mt-2">{prediction.uncertaintyText}</p>}{prediction.status === 'available' && <p className="mt-2">{prediction.limitations}</p>}{prediction.modelMetadata?.inferenceTimestamp && <p className="mt-2 text-xs">Prediction issued {dateTime(prediction.modelMetadata.inferenceTimestamp)} · Model {prediction.modelMetadata.modelVersion}</p>}</div>}</details>{canSave && <button className={`${primaryButton} mt-7 w-full`} onClick={() => onChoose(candidate)}>Plan around this experience<span aria-hidden="true">↗</span></button>}</article>
}
