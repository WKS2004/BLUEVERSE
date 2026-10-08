import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import PlannerLayout, { field, primaryButton, secondaryButton } from '../features/planner/PlannerLayout'
import AccountAreaNavigation from '../components/account/AccountAreaNavigation'
import { useAuthSession } from '../features/auth/authSession'
import { hasAllPermissions } from '../features/authorization/permissions'

import {
  createItinerary,
  createRecommendations,
  getPlannerCatalogue,
  type Itinerary,
  type ItineraryItemInput,
  type PlannerCatalogue,
  type RecommendationCandidate,
  type RecommendationResult,
} from '../features/planner/plannerApi'
import { dateTime, plannerError, utcTime, validatePlanningWindow } from '../features/planner/plannerPresentation'
import { authEntryHrefFor } from '../features/auth/authNavigation'


export default function PlannerPlanPage() {
  return (
    <div className="flex min-h-screen flex-col bg-coast-paper font-sans text-coast-ink">
      <PlannerLayout permission="planner.recommendations.create">
        <PlannerPlan />
      </PlannerLayout>
    </div>
  )
}
function PlannerPlan() {
  return (
    <>
      <AccountAreaNavigation active="planner" />
      <main className="mx-auto min-h-[70vh] max-w-[90rem] flex-1 grid grid-cols-1 gap-6 px-4 py-6 sm:px-8 sm:py-10 lg:grid-cols-[15rem_minmax(0,1fr)] lg:items-start lg:gap-8 lg:px-6 lg:py-0">
        <TripPlanContent />
      </main>
    </>
  )
}

function TripPlanContent() {
  const navigate = useNavigate()
  const { user, status } = useAuthSession()
  const [catalogue, setCatalogue] = useState<PlannerCatalogue | null>(null)
  const [reload, setReload] = useState(0)
  const [destinationId, setDestinationId] = useState('')
  const [activities, setActivities] = useState<string[]>([])
  const [starts, setStarts] = useState('')
  const [ends, setEnds] = useState('')
  const [duration, setDuration] = useState('4')
  const [experience, setExperience] = useState('INTERMEDIATE')
  const [biodiversity, setBiodiversity] = useState(true)
  const [tripTitle, setTripTitle] = useState('')
  const [tripNotes, setTripNotes] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [result, setResult] = useState<RecommendationResult | null>(null)
  const [selected, setSelected] = useState<RecommendationCandidate | null>(null)
  const [savedTrips, setSavedTrips] = useState<Itinerary[] | null>(null)
  const [appendTo, setAppendTo] = useState('')
  const [tripPage, setTripPage] = useState(1)
  const [saving, setSaving] = useState(false)
  const [draftError, setDraftError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const saveDialog = useRef<HTMLDialogElement>(null)
  const [saveError, setSaveError] = useState<string | null>(null)

  useEffect(() => {
    let current = true
    getPlannerCatalogue().then(value => {
      if (!current) return
      setCatalogue(value as PlannerCatalogue)
      setError(null)
    }).catch(cause => { if (current) setError(plannerError(cause)) })
    return () => { current = false }
  }, [reload])

  const destination = catalogue?.destinations.find(d => d.destinationId === destinationId)
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
      const suggestions = await createRecommendations({
        targetDestinationId: destination.destinationId,
        startsAt,
        endsAt,
        durationHours: hours,
        preferredActivityIds: activities,
        experienceLevel: experience,
        includeBiodiversityContext: biodiversity,
      })
      setResult(suggestions)
      const firstCandidate = suggestions.candidates[0] ?? null
      setSelected(firstCandidate)
      setTripTitle(firstCandidate?.title ?? '')
      setNotice(null)
    } catch (cause) { setError(plannerError(cause)) } finally { setBusy(false) }
  }

  async function loadSavedTrips(page = 1) {
    setBusy(true)
    try {
      setSavedTrips(await listItineraries(page))
      setTripPage(page)
      setSaveError(null)
    } catch (cause) { setSaveError(plannerError(cause)) } finally { setBusy(false) }
  }

  async function save(event: React.FormEvent) {
    event.preventDefault()
    if (!selected || !result || busy) return
    setSaving(true)
    setDraftError(null)
    setNotice(null)
    try {
      if (appendTo) {
        const previous = await getItinerary(appendTo)
        const extra: ItineraryItemInput = {
          destinationId: selected.destinationId,
          activityId: selected.activityId,
          offeringId: selected.offeringId,
          title: selected.title,
          orderIndex: previous.items.length,
          scheduledStart: selected.scheduledStart,
          scheduledEnd: selected.scheduledEnd,
          timeZone: selected.timeZone ?? 'Asia/Colombo',
        }
        const trip = await updateItinerary(appendTo, {
          title: previous.title,
          description: previous.description,
          timeZone: previous.timeZone,
          concurrencyVersion: previous.concurrencyVersion,
          startsAt: new Date(Math.min(Date.parse(previous.startsAt), Date.parse(extra.scheduledStart))).toISOString(),
          endsAt: new Date(Math.max(Date.parse(previous.endsAt), Date.parse(extra.scheduledEnd))).toISOString(),
          items: [...previous.items.map((item, index) => ({ ...item, orderIndex: index })), extra],
          recommendationId: result.recommendationId,
        })
        saveDialog.current?.close()
        navigate(`/planner/itineraries/${encodeURIComponent(trip.itineraryId)}`)
        return
      }
      const trip = await createItinerary({
        title: tripTitle.trim(),
        description: tripNotes.trim() || null,
        startsAt: selected.scheduledStart,
        endsAt: selected.scheduledEnd,
        recommendationId: result.recommendationId,
        timeZone: selected.timeZone ?? 'Asia/Colombo',
        items: [{
          destinationId: selected.destinationId,
          activityId: selected.activityId,
          offeringId: selected.offeringId,
          title: selected.title,
          orderIndex: 0,
          scheduledStart: selected.scheduledStart,
          scheduledEnd: selected.scheduledEnd,
          timeZone: selected.timeZone ?? 'Asia/Colombo',
        }],
      })
      saveDialog.current?.close()
      navigate(`/planner/itineraries/${encodeURIComponent(trip.itineraryId)}`)
    } catch (cause) {
      setDraftError(plannerError(cause))
      setSaveError(plannerError(cause))
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <div className="mb-9 flex flex-wrap items-end justify-between gap-6">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">Plan your coastal day</p>
          <h1 className="mt-3 font-display text-4xl tracking-tight sm:text-5xl">Build your trip.</h1>
          <p className="mt-4 max-w-xl leading-7 text-coast-muted">Choose a place, a window of time, and the experiences you want. Draft your plan first, then confirm it when it is ready to be processed.</p>
        </div>
        <div className="flex flex-wrap gap-3">
          {hasAllPermissions(user, ['planner.itineraries.manage']) && (
            <Link to="/planner/saved" className={secondaryButton}>Saved trips</Link>
          )}
          {hasAllPermissions(user, ['planner.recommendations.create']) && (
            <Link to="/planner" className={primaryButton}>Back to recommendations</Link>
          )}
        </div>
      </div>

      {error && <div role="alert" className="mb-6 rounded-2xl border border-amber-200 bg-amber-50 p-5 text-sm text-amber-950">{error}</div>}
      {notice && <p role="status" className="mb-6 rounded-2xl bg-coast-sage p-5 text-sm">{notice}</p>}
      {draftError && <div role="alert" className="mb-6 rounded-2xl border border-amber-200 bg-amber-50 p-5 text-sm text-amber-950">{draftError}</div>}

      {status === 'checking' ? (
        <p aria-live="polite" className="py-16 text-coast-muted">Preparing your coastal planner…</p>
      ) : status !== 'signed-in' ? (
        <section className="rounded-3xl bg-white p-10">
          <h1 className="font-display text-3xl">Sign in to plan a trip.</h1>
          <p className="mt-3 text-coast-muted">You can browse recommendations without signing in. Planning and saving trips needs an account.</p>
          <div className="mt-6 flex flex-wrap gap-3">
            <Link to={authEntryHrefFor('/signin', `/planner/plan`)} className={`${primaryButton}`}>Sign in</Link>
            <Link to="/signup" className={secondaryButton}>Create account</Link>
          </div>
        </section>
      ) : !hasAllPermissions(user, ['planner.recommendations.create']) ? (
        <section className="rounded-3xl border border-coast-line bg-white p-10">
          <h1 className="font-display text-3xl">Coastal planning access</h1>
          <p className="mt-3 max-w-xl text-coast-muted">Ask an administrator to give your account coastal planning access. Your account and profile are still available.</p>
          <Link to="/profile" className={`${secondaryButton} mt-6`}>Back to your profile</Link>
        </section>
      ) : catalogue && catalogue.status === 'AVAILABLE' && catalogue.destinations.length > 0 && destination ? (
        <div className="grid items-start gap-8 lg:grid-cols-[minmax(0,1fr)_300px]">
          <section className="rounded-3xl border border-coast-line bg-white p-6 sm:p-9">
            <div className="mb-8">
              <p className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">Your trip, step by step</p>
              <h2 className="mt-2 font-display text-3xl tracking-tight">Where shall we go?</h2>
              <p className="mt-3 leading-6 text-coast-muted">Your next coastal day starts here. Plan up to 30 days ahead.</p>
            </div>

            <form onSubmit={submit} noValidate>
              <label htmlFor="planner-destination" className="block text-sm font-bold">
                Destination
                <select
                  id="planner-destination"
                  className={field}
                  value={destinationId}
                  onChange={e => { setDestinationId(e.target.value); setActivities([]); setResult(null); setSelected(null) }}
                >
                  <option value="">Choose your stretch of coast</option>
                  {catalogue.destinations.map(d => (
                    <option key={d.destinationId} value={d.destinationId}>
                      {d.name} · {d.region}
                    </option>
                  ))}
                </select>
              </label>

              {destination && (
                <fieldset className="mt-7">
                  <legend className="text-sm font-bold">
                    What would you like to do? <span className="font-normal text-coast-muted">Optional</span>
                  </legend>
                  <p className="mt-2 text-xs text-coast-muted">Leave these open to discover everything that fits.</p>
                  <div className="mt-3 flex flex-wrap gap-2">
                    {destination.activities.map(activity => (
                      <label
                        key={activity.activityId}
                        className={`flex min-h-11 cursor-pointer items-center gap-2 rounded-full border px-4 py-2 text-sm transition ${
                          activities.includes(activity.activityId)
                            ? 'border-coast-deep bg-coast-sage font-bold text-coast-deep'
                            : 'border-coast-line hover:border-coast-teal'
                        }`}
                      >
                        <input
                          type="checkbox"
                          className="accent-coast-deep"
                          checked={activities.includes(activity.activityId)}
                          onChange={e =>
                            setActivities(a =>
                              e.target.checked
                                ? [...a, activity.activityId]
                                : a.filter(id => id !== activity.activityId)
                            )
                          }
                        />
                        {activity.name}
                      </label>
                    ))}
                  </div>
                </fieldset>
              )}

              <fieldset className="mt-8">
                <legend className="text-sm font-bold">When do you have time?</legend>
                <p id="planner-zone" className="mt-2 text-xs text-coast-muted">
                  {destination ? `All times here are local to your destination (${destination.timeZone}).` : 'Choose a destination first. Times will use its local time zone.'}
                </p>
                <div className="mt-4 grid gap-4 sm:grid-cols-2">
                  <label className="text-sm font-semibold" htmlFor="planner-starts">
                    Trip starts
                    <input
                      aria-describedby="planner-zone"
                      disabled={!destination}
                      id="planner-starts"
                      className={field}
                      type="datetime-local"
                      value={starts}
                      onChange={e => setStarts(e.target.value)}
                    />
                  </label>
                  <label className="text-sm font-semibold" htmlFor="planner-ends">
                    Trip ends
                    <input
                      aria-describedby="planner-zone"
                      disabled={!destination}
                      id="planner-ends"
                      className={field}
                      type="datetime-local"
                      value={ends}
                      onChange={e => setEnds(e.target.value)}
                    />
                  </label>
                </div>
              </fieldset>

              <div className="mt-7 grid gap-4 sm:grid-cols-2">
                <label className="text-sm font-bold" htmlFor="planner-duration">
                  Hours for an experience
                  <input
                    id="planner-duration"
                    className={field}
                    type="number"
                    min="1"
                    max="720"
                    step="1"
                    value={duration}
                    onChange={e => setDuration(e.target.value)}
                  />
                </label>
                <label className="text-sm font-bold" htmlFor="planner-experience">
                  Your experience level
                  <select
                    id="planner-experience"
                    className={field}
                    value={experience}
                    onChange={e => setExperience(e.target.value)}
                  >
                    <option value="BEGINNER">Beginner</option>
                    <option value="INTERMEDIATE">Intermediate</option>
                    <option value="ADVANCED">Advanced</option>
                  </select>
                </label>
              </div>

              <label className="mt-7 flex cursor-pointer items-start gap-3 rounded-2xl bg-coast-paper p-4 text-sm">
                <input
                  className="mt-1 h-4 w-4 accent-coast-deep"
                  type="checkbox"
                  checked={biodiversity}
                  onChange={e => setBiodiversity(e.target.checked)}
                />
                <span>
                  <span className="block font-bold">Include wildlife context</span>
                  <span className="mt-1 block leading-6 text-coast-muted">Optional sourced predictions, when available. Wildlife sightings are never guaranteed.</span>
                </span>
              </label>

              <button disabled={busy} className={`${primaryButton} mt-8 w-full sm:w-auto`} type="submit">
                {busy ? 'Finding experiences…' : 'Find my coastal day'}
                <span aria-hidden="true">↗</span>
              </button>
            </form>
          </section>

          {result && selected && (
            <div className="mt-8 rounded-3xl border border-coast-line bg-white p-6 sm:p-9">
              <p className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">Selected experience</p>
              <h2 className="mt-3 font-display text-2xl tracking-tight">{selected.title}</h2>
              <p className="mt-2 text-sm text-coast-muted">{dateTime(selected.scheduledStart, selected.timeZone ?? 'Asia/Colombo')} – {dateTime(selected.scheduledEnd, selected.timeZone ?? 'Asia/Colombo')}</p>
              <div className="mt-4 flex flex-wrap gap-2 text-xs font-semibold">
                <span className="rounded-lg bg-coast-paper px-3 py-2">{statusLabel(selected.availabilityStatus)}</span>
                <span className="rounded-lg bg-coast-paper px-3 py-2">{statusLabel(selected.suitability.status)}</span>
                <span className="rounded-lg bg-coast-paper px-3 py-2">Operations: {statusLabel(selected.operationalStatus)}</span>
              </div>
              <div className="mt-5 flex flex-wrap gap-3">
                <button
                  disabled={saving}
                  className={primaryButton}
                  type="button"
                  onClick={() => saveDialog.current?.showModal()}
                >
                  Save or confirm this trip
                  <span aria-hidden="true">↗</span>
                </button>
              </div>
            </div>
          )}

          {!result && !selected && (
            <aside className="space-y-7 p-3 lg:pt-8">
              <div>
                <span className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">Thoughtfully planned</span>
                <h2 className="mt-3 font-display text-2xl leading-tight">More time enjoying.<br />Less time guessing.</h2>
              </div>
              {[
                ['01', 'Make it yours', 'Choose your destination, available time and the activities you enjoy.'],
                ['02', 'Know what to expect', 'We check availability, marine suitability and operating restrictions. Missing evidence is made clear.'],
                ['03', 'Keep a plan you love', 'Save a plan as a draft, confirm it when it is ready, and review conditions again before you go.'],
              ].map(([step, title, copy]) => (
                <div key={step} className="border-t border-coast-line pt-5">
                  <p className="text-xs font-bold text-coast-teal">{step}</p>
                  <h3 className="mt-2 font-bold">{title}</h3>
                  <p className="mt-2 text-sm leading-6 text-coast-muted">{copy}</p>
                </div>
              ))}
            </aside>
          )}
        </div>
      ) : catalogue && catalogue.status === 'UNAVAILABLE' ? (
        <div className="rounded-2xl bg-coast-sage p-6">
          <h3 className="text-lg font-bold">Destinations aren’t available right now.</h3>
          <p className="mt-2 text-sm leading-6 text-coast-muted">{catalogue.message ?? 'Please try again shortly.'}</p>
          <div className="mt-5 flex flex-wrap gap-3">
            <button className={secondaryButton} onClick={() => { setReload(n => n + 1) }}>
              Try again
            </button>
            {hasAllPermissions(user, ['planner.itineraries.manage']) && (
              <Link className={secondaryButton} to="/planner/saved">View saved trips</Link>
            )}
          </div>
        </div>
      ) : null}

      <dialog
        ref={saveDialog}
        aria-labelledby="save-trip-title"
        className="m-auto w-[calc(100%-2rem)] max-w-lg rounded-3xl border-0 bg-white p-7 text-coast-ink shadow-xl backdrop:bg-coast-ink/40"
      >
        <form onSubmit={save}>
          <h2 id="save-trip-title" className="font-display text-2xl">Make this day yours.</h2>
          <p className="mt-3 text-sm leading-6 text-coast-muted">
            Save a private trip to edit and review later. Draft plans can be updated; confirmed plans are processed and cannot be edited.
          </p>

          <label htmlFor="save-trip-name" className="mt-6 block text-sm font-bold">
            Trip name
            <input
              id="save-trip-name"
              required
              disabled={Boolean(appendTo)}
              maxLength={150}
              className={field}
              value={tripTitle}
              onChange={e => setTripTitle(e.target.value)}
            />
          </label>

          <label htmlFor="save-trip-notes" className="mt-4 block text-sm font-bold">
            Your notes <span className="font-normal text-coast-muted">Optional</span>
            <textarea
              id="save-trip-notes"
              maxLength={500}
              className={`${field} min-h-24 resize-y`}
              value={tripNotes}
              onChange={e => setTripNotes(e.target.value)}
            />
          </label>

          {savedTrips === null ? (
            <button disabled={busy} className={`${secondaryButton} mt-5`} type="button" onClick={() => loadSavedTrips()}>
              Add to a saved trip instead
            </button>
          ) : (
            <div className="mt-5">
              <label className="text-sm font-bold" htmlFor="append-trip">
                Save to
                <select
                  id="append-trip"
                  className={field}
                  value={appendTo}
                  onChange={e => setAppendTo(e.target.value)}
                >
                  <option value="">A new trip</option>
                  {savedTrips.map(trip => (
                    <option key={trip.itineraryId} value={trip.itineraryId}>
                      {trip.title}
                    </option>
                  ))}
                </select>
              </label>
              <div className="mt-3 flex gap-3">
                <button
                  className={secondaryButton}
                  disabled={busy || tripPage === 1}
                  type="button"
                  onClick={() => loadSavedTrips(tripPage - 1)}
                >
                  Previous trips
                </button>
                <button
                  className={secondaryButton}
                  disabled={busy || savedTrips.length < 20}
                  type="button"
                  onClick={() => loadSavedTrips(tripPage + 1)}
                >
                  More trips
                </button>
              </div>
            </div>
          )}

          {saveError && <p role="alert" className="mt-4 text-sm text-amber-900">{saveError}</p>}

          <div className="mt-6 flex flex-wrap gap-3">
            <button disabled={busy || !tripTitle.trim()} className={primaryButton} type="submit">
              {busy ? 'Saving…' : 'Save my trip'}
            </button>
            <button disabled={busy} className={secondaryButton} type="button" onClick={() => { saveDialog.current?.close(); setSelected(null) }}>
              Cancel
            </button>
          </div>
        </form>
      </dialog>
    </>
  )
}

function statusLabel(value: string): string {
  const labels: Record<string, string> = {
    SUITABLE: 'Suitable conditions',
    CAUTION: 'Review caution',
    UNSUITABLE: 'Reschedule this activity',
    UNKNOWN: 'Not verified',
    AVAILABLE: 'Available',
    UNAVAILABLE: 'Unavailable',
    OPEN: 'Open',
    TEMPORARILY_SUSPENDED: 'Temporarily suspended',
    CANCELLED: 'Cancelled',
    NOT_PUBLISHED: 'No longer published',
    COMPLETED: 'Completed',
  }
  return labels[value] ?? 'Not verified'
}

async function listItineraries(page = 1): Promise<Itinerary[]> {
  const response = await fetch(`/api/planner/itineraries?page=${page}&pageSize=20`, {
    credentials: 'include',
    headers: { Accept: 'application/json' },
    signal: AbortSignal.timeout(15000),
  })
  if (!response.ok) {
    const text = await response.text()
    let detail = null
    try {
      const payload = JSON.parse(text)
      if (typeof payload === 'object' && payload !== null && 'detail' in payload && typeof payload.detail === 'string') {
        detail = payload.detail
      }
    } catch {
      // ignore malformed responses
    }
    throw new Error(detail ?? 'We couldn’t load your saved trips just now. Please try again.')
  }
  const payload = (await response.json()) as unknown
  if (!Array.isArray(payload)) {
    throw new Error('We couldn’t read your saved trips just now. Please try again.')
  }
  return payload as Itinerary[]
}

async function getItinerary(id: string): Promise<Itinerary> {
  const response = await fetch(`/api/planner/itineraries/${encodeURIComponent(id)}`, {
    credentials: 'include',
    headers: { Accept: 'application/json' },
    signal: AbortSignal.timeout(15000),
  })
  if (!response.ok) {
    const text = await response.text()
    let detail = null
    try {
      const payload = JSON.parse(text)
      if (typeof payload === 'object' && payload !== null && 'detail' in payload && typeof payload.detail === 'string') {
        detail = payload.detail
      }
    } catch {
      // ignore malformed responses
    }
    throw new Error(detail ?? 'We couldn’t load that trip just now. Please try again.')
  }
  const payload = (await response.json()) as unknown
  if (!payload || typeof payload !== 'object' || !('itineraryId' in payload)) {
    throw new Error('We couldn’t read that trip just now. Please try again.')
  }
  return payload as Itinerary
}
async function updateItinerary(id: string, input: {
  title: string
  description: string | null
  timeZone: string
  concurrencyVersion: number
  startsAt: string
  endsAt: string
  items: ItineraryItemInput[]
  recommendationId?: string
}): Promise<Itinerary> {
  const response = await fetch(`/api/planner/itineraries/${encodeURIComponent(id)}`, {
    method: 'PUT',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify(input),
    signal: AbortSignal.timeout(20000),
  })
  if (!response.ok) {
    const text = await response.text()
    let detail = null
    try {
      const payload = JSON.parse(text)
      if (typeof payload === 'object' && payload !== null && 'detail' in payload && typeof payload.detail === 'string') {
        detail = payload.detail
      }
    } catch {
      // ignore malformed responses
    }
    throw new Error(detail ?? 'We couldn’t save that trip just now. Please try again.')
  }
  const payload = (await response.json()) as unknown
  if (!payload || typeof payload !== 'object' || !('itineraryId' in payload)) {
    throw new Error('We couldn’t read the saved trip just now. Please try again.')
  }
  return payload as Itinerary
}

