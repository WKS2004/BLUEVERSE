import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import PlannerLayout, { field, primaryButton, secondaryButton } from '../features/planner/PlannerLayout'
import {
  confirmItinerary,
  deleteItinerary,
  getEvaluationHistory,
  getItinerary,
  reviewItinerary,
  updateItinerary,
  type Evaluation,
  type Itinerary,
  type ItineraryItem,
} from '../features/planner/plannerApi'
import { dateTime, plannerError, statusLabel, utcTime, wallTime } from '../features/planner/plannerPresentation'
import { hasAllPermissions } from '../features/authorization/permissions'
import { useAuthSession } from '../features/auth/authSession'

export default function ItineraryPage() {
  const { itineraryId } = useParams()
  return (
    <PlannerLayout permission="planner.itineraries.manage">
      <Trip key={itineraryId} />
    </PlannerLayout>
  )
}

function Trip() {
  const { itineraryId = '' } = useParams()
  const navigate = useNavigate()
  const { user } = useAuthSession()
  const [trip, setTrip] = useState<Itinerary | null>(null)
  const [draft, setDraft] = useState<Itinerary | null>(null)
  const [history, setHistory] = useState<Evaluation[]>([])
  const [historyError, setHistoryError] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [retry, setRetry] = useState(0)
  const [removed, setRemoved] = useState<{ item: ItineraryItem; index: number } | null>(null)
  const confirmDelete = useRef<HTMLDialogElement>(null)

  const zone = trip?.timeZone ?? 'Asia/Colombo'
  const editing = Boolean(draft)

  useEffect(() => {
    if (!editing) return
    const protectDraft = (event: BeforeUnloadEvent) => {
      event.preventDefault()
      event.returnValue = ''
    }
    window.addEventListener('beforeunload', protectDraft)
    return () => window.removeEventListener('beforeunload', protectDraft)
  }, [editing])

  useEffect(() => {
    let current = true
    getItinerary(itineraryId).then(value => {
      if (current) {
        setTrip(value)
        setDraft(null)
        setError(null)
      }
    }).catch(cause => {
      if (current) { setError(plannerError(cause)) }
    }).finally(() => {
      if (current) { setLoading(false) }
    })

    getEvaluationHistory(itineraryId).then(value => {
      if (current) {
        setHistory(value)
        setHistoryError(null)
      }
    }).catch(cause => {
      if (current) { setHistoryError(plannerError(cause)) }
    })

    return () => { current = false }
  }, [itineraryId, retry])

  async function save(event: React.FormEvent) {
    event.preventDefault()
    if (!draft || busy) return
    setBusy(true)
    setError(null)
    try {
      const saved = await updateItinerary(itineraryId, {
        title: draft.title,
        description: draft.description,
        startsAt: draft.startsAt,
        endsAt: draft.endsAt,
        timeZone: draft.timeZone,
        concurrencyVersion: draft.concurrencyVersion,
        items: draft.items.map((item, i) => ({ ...item, orderIndex: i })),
      })
      setTrip(saved)
      setDraft(null)
      setRemoved(null)
      setNotice('Your trip has been saved. Review conditions again if you changed the times.')
    } catch (cause) {
      setError(plannerError(cause))
    } finally {
      setBusy(false)
    }
  }

  async function review() {
    if (busy || draft) return
    setBusy(true)
    setError(null)
    try {
      const value = await reviewItinerary(itineraryId)
      setHistory(h => [value, ...h].slice(0, 20))
      setHistoryError(null)
      setTrip(await getItinerary(itineraryId))
      setNotice('Conditions reviewed. Your chosen experiences and times have been kept.')
    } catch (cause) {
      setError(plannerError(cause))
    } finally {
      setBusy(false)
    }
  }

  async function removeTrip() {
    if (busy) return
    setBusy(true)
    try {
      await deleteItinerary(itineraryId)
      confirmDelete.current?.close()
      navigate('/planner/saved')
    } catch (cause) {
      setError(plannerError(cause))
      confirmDelete.current?.close()
    } finally {
      setBusy(false)
    }
  }

  function move(index: number, direction: number) {
    if (!draft) return
    const items = [...draft.items]
    const [item] = items.splice(index, 1)
    items.splice(index + direction, 0, item)
    setDraft({ ...draft, items })
  }

  function changeTime(index: number, property: 'scheduledStart' | 'scheduledEnd', value: string) {
    if (!draft) return
    try {
      setDraft({
        ...draft,
        items: draft.items.map((item, i) => {
          return i === index
            ? { ...item, [property]: utcTime(value, zone) }
            : item
        }),
      })
      setError(null)
    } catch {
      setError('Choose a complete, unambiguous local date and time for this experience.')
    }
  }

  const current = draft ?? trip

  return (
    <>
      <Link
        className="mb-7 inline-flex min-h-11 items-center gap-2 text-sm font-bold text-coast-deep focus-visible:outline-2"
        to="/planner/saved"
      >
        <span aria-hidden="true">←</span> All saved trips
      </Link>

      {error && (
        <div
          role="alert"
          className="mb-6 rounded-2xl border border-amber-200 bg-amber-50 p-5 text-sm text-amber-950"
        >
          {error}
          <button
            disabled={busy}
            className={`${secondaryButton} ml-3 mt-2`}
            onClick={() => {
              setLoading(true)
              setRetry(n => n + 1)
            }}
          >
            Reload trip
          </button>
        </div>
      )}

      {notice && (
        <p role="status" className="mb-6 rounded-2xl bg-coast-sage p-5 text-sm">
          {notice}
        </p>
      )}

      {loading ? (
        <p aria-live="polite" className="py-16 text-coast-muted">Opening your coastal day…</p>
      ) : !current ? null : (
        <>
          <div className="mb-9 flex flex-wrap items-end justify-between gap-6">
            <div>
              <p className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">Your day, at your pace</p>
              <h1 className="mt-3 max-w-3xl font-display text-4xl tracking-tight sm:text-5xl">
                {trip?.title}
              </h1>
              <p className="mt-4 leading-7 text-coast-muted">
                {dateTime(current.startsAt, zone)} – {dateTime(current.endsAt, zone)} · {current.items.length} experiences
              </p>
              <p className="mt-2 text-xs text-coast-muted">
                Times shown in {zone}. Saving a trip doesn’t make a booking.
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              {trip?.status === 'Draft' && (
                <button
                  className={secondaryButton}
                  disabled={busy || Boolean(draft)}
                  onClick={() => {
                    setDraft(structuredClone(trip))
                    setNotice(null)
                  }}
                >
                  Edit trip
                </button>
              )}
              {trip?.status === 'Confirmed' && (
                <button
                  className={primaryButton}
                  disabled={busy || Boolean(draft) || !trip?.items.length}
                  onClick={review}
                >
                  {busy ? 'Working on your trip…' : 'Review current conditions'}
                </button>
              )}
            </div>
          </div>

          <div className="grid items-start gap-8 lg:grid-cols-[minmax(0,1fr)_320px]">
            <div>
              {draft && (
                <form className="mb-7 rounded-3xl border border-coast-line bg-white p-6 sm:p-8" onSubmit={save}>
                  <h2 className="font-display text-2xl">Shape your day</h2>

                  <label className="mt-5 block text-sm font-bold" htmlFor="trip-name">
                    Trip name
                    <input
                      className={field}
                      id="trip-name"
                      required
                      maxLength={150}
                      value={draft.title}
                      onChange={e => setDraft({ ...draft, title: e.target.value })}
                    />
                  </label>

                  <label className="mt-5 block text-sm font-bold" htmlFor="trip-notes">
                    Your notes
                    <span className="font-normal text-coast-muted">Optional</span>
                    <textarea
                      className={`${field} min-h-28 resize-y`}
                      id="trip-notes"
                      maxLength={500}
                      value={draft.description ?? ''}
                      onChange={e => setDraft({ ...draft, description: e.target.value })}
                    />
                  </label>

                  <div className="mt-5 grid gap-4 sm:grid-cols-2">
                    <label className="text-sm font-bold">
                      Trip starts
                      <input
                        className={field}
                        type="datetime-local"
                        value={wallTime(draft.startsAt, zone)}
                        onChange={e => {
                          try {
                            setDraft({ ...draft, startsAt: utcTime(e.target.value, zone) })
                          } catch {
                            setError('Choose a valid trip start.')
                          }
                        }}
                      />
                    </label>
                    <label className="text-sm font-bold">
                      Trip ends
                      <input
                        className={field}
                        type="datetime-local"
                        value={wallTime(draft.endsAt, zone)}
                        onChange={e => {
                          try {
                            setDraft({ ...draft, endsAt: utcTime(e.target.value, zone) })
                          } catch {
                            setError('Choose a valid trip end.')
                          }
                        }}
                      />
                    </label>
                  </div>

                  <p className="mt-4 text-sm leading-6 text-coast-muted">
                    Adjust stops below. Times must fit your trip and cannot overlap.
                    Changing a time requires a new condition review.
                  </p>

                  <div className="mt-6 flex flex-wrap gap-3">
                    <button
                      className={primaryButton}
                      disabled={busy || !draft.title.trim()}
                      type="submit"
                    >
                      Save changes
                    </button>
                    <button
                      className={secondaryButton}
                      disabled={busy}
                      type="button"
                      onClick={() => {
                        setDraft(null)
                        setRemoved(null)
                        setError(null)
                      }}
                    >
                      Cancel changes
                    </button>
                  </div>
                </form>
              )}

              {removed && draft && (
                <div
                  role="status"
                  className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-coast-sage p-4 text-sm"
                >
                  <p>{removed.item.title} removed from this draft.</p>
                  <button
                    className={secondaryButton}
                    onClick={() => {
                      const items = [...draft.items]
                      items.splice(removed.index, 0, removed.item)
                      setDraft({ ...draft, items })
                      setRemoved(null)
                    }}
                  >
                    Undo removal
                  </button>
                </div>
              )}

              <ol className="space-y-5" aria-label="Planned experiences">
                {current.items.map((item, index) => (
                  <li key={item.itemId} className="rounded-3xl border border-coast-line bg-white p-6 sm:p-8">
                    <div className="flex gap-4">
                      <span
                        aria-hidden="true"
                        className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-coast-sage font-bold text-coast-deep"
                      >
                        {index + 1}
                      </span>
                      <div className="min-w-0 flex-1">
                        <h2 className="font-display text-xl">{item.title}</h2>
                        <p className="mt-2 text-sm text-coast-muted">
                          {dateTime(item.scheduledStart, item.timeZone ?? zone)} –
                          {dateTime(item.scheduledEnd, item.timeZone ?? zone)}
                        </p>
                      </div>
                    </div>

                    {draft ? (
                      <div className="mt-5">
                        <div className="grid gap-4 sm:grid-cols-2">
                          <label className="text-xs font-bold">
                            {item.title} starts
                            <input
                              className={field}
                              type="datetime-local"
                              value={wallTime(item.scheduledStart, zone)}
                              onChange={e => changeTime(index, 'scheduledStart', e.target.value)}
                            />
                          </label>
                          <label className="text-xs font-bold">
                            {item.title} ends
                            <input
                              className={field}
                              type="datetime-local"
                              value={wallTime(item.scheduledEnd, zone)}
                              onChange={e => changeTime(index, 'scheduledEnd', e.target.value)}
                            />
                          </label>
                        </div>

                        <div className="mt-4 flex flex-wrap gap-2">
                          <button
                            className={secondaryButton}
                            disabled={busy || index === 0}
                            aria-label={`Move ${item.title} earlier in the list`}
                            onClick={() => move(index, -1)}
                          >
                            Move up
                          </button>
                          <button
                            className={secondaryButton}
                            disabled={busy || index === draft.items.length - 1}
                            aria-label={`Move ${item.title} later in the list`}
                            onClick={() => move(index, 1)}
                          >
                            Move down
                          </button>
                          <button
                            className={secondaryButton}
                            disabled={busy}
                            onClick={() => {
                              setRemoved({ item, index })
                              setDraft({
                                ...draft,
                                items: draft.items.filter(i => i.itemId !== item.itemId),
                              })
                            }}
                          >
                            Remove {item.title}
                          </button>
                        </div>
                      </div>
                    ) : (
                      <>
                        <div className="mt-5 flex flex-wrap gap-2 text-xs font-semibold">
                          {[item.lastAvailabilityStatus, item.lastSuitabilityStatus, item.lastOperationalStatus].map(
                            (status, i) => (
                              <span key={i} className="rounded-lg bg-coast-paper px-3 py-2">
                                {statusLabel(status)}
                              </span>
                            )
                          )}
                        </div>
                        {item.advisoryNote && (
                          <p className="mt-4 rounded-xl bg-amber-50 p-4 text-sm leading-6 text-amber-950">
                            {item.advisoryNote}
                          </p>
                        )}
                      </>
                    )}
                  </li>
                ))}
              </ol>

              {!current.items.length && (
                <section className="rounded-3xl border border-dashed border-coast-line p-9 text-center">
                  <h2 className="font-display text-2xl">A little room in your day.</h2>
                  <p className="mt-3 text-coast-muted">Find another experience and add it to this trip.</p>
                </section>
              )}

              {!draft && hasAllPermissions(user, ['planner.recommendations.create']) && (
                <Link className={`${secondaryButton} mt-6`} to="/planner">
                  Find another experience
                </Link>
              )}
            </div>

            <aside className="rounded-3xl bg-coast-sage p-6">
              <p className="text-xs font-bold uppercase tracking-[0.18em] text-coast-teal">Before you go</p>
              <h2 className="mt-3 font-display text-2xl">Keep the coast in mind.</h2>
              <p className="mt-4 text-sm leading-7 text-coast-muted">
                Conditions and availability can change. Review this trip near departure and follow local guidance.
                Missing information means a check is needed, not that an activity is safe.
              </p>

              {trip?.description && !draft && (
                <div className="mt-6 border-t border-coast-line pt-5">
                  <h3 className="text-sm font-bold">Your notes</h3>
                  <p className="mt-3 whitespace-pre-wrap text-sm leading-7 text-coast-muted">
                    {trip.description}
                  </p>
                </div>
              )}

              {trip?.status === 'Draft' && (
                <>
                  <button
                    disabled={busy || Boolean(draft)}
                    className="mt-8 min-h-11 rounded-full px-2 text-sm font-semibold text-coast-muted underline underline-offset-4 focus-visible:outline-2"
                    onClick={() => confirmDelete.current?.showModal()}
                  >
                    Delete this trip
                  </button>
                  <button
                    disabled={busy || Boolean(draft)}
                    className="mt-8 min-h-11 rounded-full px-2 text-sm font-semibold text-coast-muted underline underline-offset-4 focus-visible:outline-2"
                    onClick={async () => {
                      if (busy) return
                      setBusy(true)
                      try {
                        const updated = await confirmItinerary(itineraryId)
                        setTrip(updated)
                        setDraft(null)
                        setNotice('Your trip has been confirmed. It can no longer be edited from here.')
                      } catch (cause) {
                        setError(plannerError(cause))
                      } finally {
                        setBusy(false)
                      }
                    }}
                  >
                    Confirm this trip
                  </button>
                </>
              )}

            </aside>
          </div>

          <section className="mt-12 border-t border-coast-line pt-9">
            <h2 className="font-display text-2xl">Condition review history</h2>
            <p className="mt-3 text-sm text-coast-muted">
              Each review keeps a snapshot. Your selected experiences and times stay yours to change.
            </p>
            {historyError && (
              <p role="alert" className="mt-4 text-sm text-amber-900">
                Review history could not be loaded. {historyError}
              </p>
            )}
            {history.length === 0 ? (
              <p className="mt-6 text-sm text-coast-muted">
                No condition reviews yet. Review your trip before you go.
              </p>
            ) : (
              <div className="mt-6 space-y-4">
                {history.map(evaluation => (
                  <details
                    key={evaluation.evaluationId}
                    className="rounded-2xl border border-coast-line bg-white p-5"
                    open={history[0] === evaluation}
                  >
                    <summary className="cursor-pointer font-semibold focus-visible:outline-2">
                      {dateTime(evaluation.evaluatedAt)} ·
                      {evaluation.requiresReview ? 'Your attention is needed' : 'No cautions found'} ·
                      {evaluation.hasChanges ? 'Evidence changed' : 'No new changes'}
                    </summary>
                    <p className="mt-4 text-sm leading-6 text-coast-muted">
                      {evaluation.summary.replace(/[0-9a-f]{8}-[0-9a-f-]{27,}/gi, 'this experience')}
                    </p>
                    <ul className="mt-5 space-y-4">
                      {evaluation.items.map(item => (
                        <li className="border-t border-coast-line pt-4 text-sm" key={item.itemId}>
                          <h3 className="font-bold">
                            {trip?.items.find(stop => stop.itemId === item.itemId)?.title ??
                              'Previously planned experience'}
                          </h3>
                          <p className="mt-2 text-coast-muted">
                            {statusLabel(item.previousSuitability)} → {statusLabel(item.currentSuitability)} ·
                            {statusLabel(item.currentAvailability)} ·
                            {statusLabel(item.currentOperationalStatus)}
                          </p>
                          <p className="mt-2">
                            {item.suggestedAction === 'CANCEL_OR_RESCHEDULE'
                              ? 'Choose a different time or remove this experience.'
                              : item.suggestedAction === 'REVIEW_CONDITIONS'
                                ? 'Review local guidance before proceeding.'
                                : 'Keep this experience and check again near departure.'}
                          </p>
                          {item.marineConditionTime && (
                            <p className="mt-2 text-xs text-coast-muted">
                              Marine evidence for {dateTime(item.marineConditionTime)}
                            </p>
                          )}
                        </li>
                      ))}
                    </ul>
                  </details>
                ))}
              </div>
            )}
          </section>
        </>
      )}

      <dialog
        ref={confirmDelete}
        aria-labelledby="delete-title"
        className="m-auto w-[calc(100%-2rem)] max-w-md rounded-3xl bg-white p-7 text-coast-ink backdrop:bg-coast-ink/40"
      >
        <h2 id="delete-title" className="font-display text-2xl">Delete this saved trip?</h2>
        <p className="mt-3 text-sm leading-7 text-coast-muted">
          “{trip?.title}” and its condition review history will be removed. This cannot be undone.
        </p>
        <div className="mt-6 flex flex-wrap gap-3">
          <button className={primaryButton} disabled={busy} onClick={removeTrip}>
            Delete trip
          </button>
          <button
            className={secondaryButton}
            disabled={busy}
            onClick={() => confirmDelete.current?.close()}
          >
            Keep my trip
          </button>
        </div>
      </dialog>
    </>
  )
}
