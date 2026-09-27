import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import SiteFooter from '../../components/layout/SiteFooter'
import SiteHeader from '../../components/layout/SiteHeader'
import { useAuthSession } from '../../features/auth/authSession'
import type {
  AvailabilityEvaluationResponse,
  FavouriteDto,
  OfferingDto,
  ScheduleDto,
} from '../../features/experiences/experienceApi'
import {
  addFavourite,
  evaluateAvailability,
  getOfferingById,
  getOfferingSchedules,
  getUserFavourites,
  removeFavourite,
} from '../../features/experiences/experienceApi'

export default function OfferingDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { user } = useAuthSession()

  const [offering, setOffering] = useState<OfferingDto | null>(null)
  const [schedules, setSchedules] = useState<ScheduleDto[]>([])
  const [favourites, setFavourites] = useState<FavouriteDto[]>([])
  const [favMessage, setFavMessage] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  // Interactive Availability Evaluation State (Non-CRUD operation)
  const [evalStart, setEvalStart] = useState(() => {
    const d = new Date()
    d.setDate(d.getDate() + 1)
    d.setHours(9, 0, 0, 0)
    return d.toISOString().slice(0, 16)
  })
  const [evalEnd, setEvalEnd] = useState(() => {
    const d = new Date()
    d.setDate(d.getDate() + 1)
    d.setHours(12, 0, 0, 0)
    return d.toISOString().slice(0, 16)
  })
  const [evaluating, setEvaluating] = useState(false)
  const [evalResult, setEvalResult] = useState<AvailabilityEvaluationResponse | null>(null)
  const [evalError, setEvalError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) return
    let isMounted = true

    async function loadData() {
      setLoading(true)
      setError(null)
      try {
        const [off, scheds] = await Promise.all([
          getOfferingById(id!),
          getOfferingSchedules(id!),
        ])

        if (!isMounted) return
        setOffering(off)
        setSchedules(scheds)

        if (user) {
          try {
            const favs = await getUserFavourites()
            if (isMounted) setFavourites(favs)
          } catch {
            // Ignore wishlist load error
          }
        }
      } catch (err: unknown) {
        if (isMounted) {
          setError(err instanceof Error ? err.message : 'Unable to load offering specifications.')
        }
      } finally {
        if (isMounted) setLoading(false)
      }
    }

    loadData()
    return () => {
      isMounted = false
    }
  }, [id, user])

  async function handleToggleFavourite() {
    if (!id || !offering) return
    if (!user) {
      setFavMessage('Sign in to add this offering to your saved experiences.')
      return
    }

    const existing = favourites.find(
      (f) => f.targetType.toUpperCase() === 'OFFERING' && f.targetId.toLowerCase() === id.toLowerCase()
    )

    try {
      if (existing) {
        await removeFavourite('OFFERING', id)
        setFavourites((prev) => prev.filter((f) => f.id !== existing.id))
        setFavMessage('Offering removed from your wishlist.')
      } else {
        const created = await addFavourite('OFFERING', id)
        setFavourites((prev) => [...prev, created])
        setFavMessage('Offering saved to your wishlist!')
      }
    } catch (err: unknown) {
      setFavMessage(err instanceof Error ? err.message : 'Failed to update saved status.')
    }

    setTimeout(() => setFavMessage(null), 3500)
  }

  async function handleEvaluateAvailability(e: React.FormEvent) {
    e.preventDefault()
    if (!id) return

    setEvaluating(true)
    setEvalError(null)
    setEvalResult(null)

    try {
      const startsAt = new Date(evalStart).toISOString()
      const endsAt = new Date(evalEnd).toISOString()

      const res = await evaluateAvailability({
        offeringId: id,
        startsAt,
        endsAt,
      })

      setEvalResult(res)
    } catch (err: unknown) {
      setEvalError(err instanceof Error ? err.message : 'Availability evaluation encountered an issue.')
    } finally {
      setEvaluating(false)
    }
  }

  const isSaved = id
    ? favourites.some((f) => f.targetType.toUpperCase() === 'OFFERING' && f.targetId.toLowerCase() === id.toLowerCase())
    : false

  return (
    <div className="flex min-h-screen flex-col bg-coast-paper font-sans text-coast-ink" id="top">
      <SiteHeader active="experiences" />

      <main className="flex-1">
        <div className="mx-auto max-w-7xl px-5 pt-8 sm:px-8 lg:px-12">
          <Link
            className="inline-flex items-center gap-2 text-xs font-extrabold tracking-wide text-coast-muted hover:text-coast-deep"
            to="/experiences"
          >
            ← Back to Experiences Directory
          </Link>
        </div>

        {favMessage && (
          <div className="mx-auto mt-4 max-w-7xl px-5 sm:px-8 lg:px-12">
            <div aria-live="polite" className="rounded-2xl border border-coast-teal/30 bg-coast-sage p-4 text-sm font-semibold text-coast-deep">
              {favMessage}
            </div>
          </div>
        )}

        {error && (
          <div className="mx-auto mt-4 max-w-7xl px-5 sm:px-8 lg:px-12">
            <div role="alert" className="rounded-2xl border border-red-200 bg-red-50 p-6 text-sm font-semibold text-red-900">
              {error}
            </div>
          </div>
        )}

        {loading ? (
          <div className="py-24 text-center">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-coast-deep border-r-transparent" />
            <p className="mt-3 text-sm font-semibold text-coast-muted">Loading offering details & schedule data...</p>
          </div>
        ) : offering ? (
          <div className="mx-auto max-w-7xl px-5 py-8 sm:px-8 lg:px-12">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <span className="rounded-full bg-coast-sand px-3 py-1 text-xs font-extrabold text-coast-deep">
                  {offering.activityName} ({offering.activityCode})
                </span>
                <h1 className="mt-2 font-display text-4xl font-bold tracking-tight text-coast-ink sm:text-5xl">
                  {offering.title}
                </h1>
                <p className="mt-2 text-sm text-coast-muted">
                  Destination:{' '}
                  <Link
                    className="font-bold text-coast-deep underline hover:text-coast-blue"
                    to={`/experiences/destinations/${offering.destinationId}`}
                  >
                    {offering.destinationName}
                  </Link>
                </p>
              </div>

              <div className="flex items-center gap-3">
                <button
                  className={`inline-flex min-h-11 items-center gap-2 rounded-full border px-5 text-sm font-extrabold transition ${
                    isSaved
                      ? 'border-coast-teal bg-coast-teal text-white'
                      : 'border-coast-line bg-white text-coast-deep hover:bg-coast-sand'
                  }`}
                  onClick={handleToggleFavourite}
                  type="button"
                >
                  <span>{isSaved ? 'Saved in Wishlist' : 'Save Offering'}</span>
                  <span>★</span>
                </button>
              </div>
            </div>

            <div className="mt-8 grid gap-8 lg:grid-cols-3">
              {/* Left Column: Details & Schedules */}
              <div className="space-y-6 lg:col-span-2">
                <div className="rounded-3xl border border-coast-line bg-white p-6 shadow-sm sm:p-8">
                  <h2 className="text-xs font-extrabold tracking-[0.16em] text-coast-blue">OFFERING SPECIFICATIONS</h2>
                  <p className="mt-4 text-base leading-7 text-coast-ink">
                    {offering.description || 'Guided coastal ocean experience operated by certified instructors.'}
                  </p>

                  <div className="mt-6 grid grid-cols-2 gap-4 sm:grid-cols-4">
                    <div className="rounded-2xl bg-coast-sand p-3">
                      <span className="block text-[11px] font-bold text-coast-muted">Pricing</span>
                      <span className="text-sm font-extrabold text-coast-deep">
                        {offering.price != null ? `${offering.currency || 'LKR'} ${offering.price.toLocaleString()}` : 'Inquire'}
                      </span>
                    </div>

                    <div className="rounded-2xl bg-coast-sand p-3">
                      <span className="block text-[11px] font-bold text-coast-muted">Duration</span>
                      <span className="text-sm font-extrabold text-coast-deep">
                        {offering.durationMinutes ? `${offering.durationMinutes} mins` : 'Flexible'}
                      </span>
                    </div>

                    <div className="rounded-2xl bg-coast-sand p-3">
                      <span className="block text-[11px] font-bold text-coast-muted">Capacity</span>
                      <span className="text-sm font-extrabold text-coast-deep">
                        {offering.maxCapacity ? `${offering.maxCapacity} guests` : 'Group'}
                      </span>
                    </div>

                    <div className="rounded-2xl bg-coast-sand p-3">
                      <span className="block text-[11px] font-bold text-coast-muted">Catalogue Status</span>
                      <span className="text-sm font-extrabold text-coast-deep">{offering.status}</span>
                    </div>
                  </div>
                </div>

                {/* Schedules list */}
                <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-sm sm:p-8">
                  <div className="flex items-center justify-between">
                    <div>
                      <p className="text-xs font-extrabold tracking-[0.16em] text-coast-blue">TIMETABLES</p>
                      <h3 className="mt-1 font-display text-2xl font-bold tracking-tight text-coast-ink">
                        Scheduled Departure Slots
                      </h3>
                    </div>
                    <span className="text-xs font-bold text-coast-muted">{schedules.length} slots found</span>
                  </div>

                  {schedules.length === 0 ? (
                    <p className="mt-4 text-xs text-coast-muted">
                      No operational schedule slots currently registered for this offering. Check availability below for on-demand inquiries.
                    </p>
                  ) : (
                    <div className="mt-6 grid gap-3 sm:grid-cols-2">
                      {schedules.map((s) => {
                        const start = new Date(s.startsAt)
                        const end = new Date(s.endsAt)
                        return (
                          <div className="rounded-2xl border border-coast-line bg-white p-4 shadow-sm" key={s.id}>
                            <div className="flex items-center justify-between">
                              <span className="text-xs font-extrabold text-coast-deep">
                                {start.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })}
                              </span>
                              <span
                                className={`rounded-full px-2 py-0.5 text-[10px] font-extrabold ${
                                  s.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-100 text-gray-600'
                                }`}
                              >
                                {s.isActive ? 'Active' : 'Inactive'}
                              </span>
                            </div>
                            <p className="mt-2 text-sm font-bold text-coast-ink">
                              {start.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })} –{' '}
                              {end.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                            </p>
                            <p className="mt-1 text-[11px] text-coast-muted">Time zone: {s.timeZoneId || 'Asia/Colombo'}</p>
                          </div>
                        )
                      })}
                    </div>
                  )}
                </div>
              </div>

              {/* Right Column: Availability Evaluator (Non-CRUD Core Operation) */}
              <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-sm sm:p-8">
                <p className="text-xs font-extrabold tracking-[0.16em] text-coast-blue">AUTHORITATIVE EVALUATION</p>
                <h3 className="mt-2 font-display text-2xl font-bold tracking-tight text-coast-ink">
                  Check Real-Time Availability
                </h3>
                <p className="mt-2 text-xs leading-5 text-coast-muted">
                  Evaluates offering publication, schedule coverage, and live operational restrictions from Member 4 Coastal Operations.
                </p>

                <form className="mt-6 space-y-4" onSubmit={handleEvaluateAvailability}>
                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="eval-start">
                      REQUESTED DEPARTURE START
                    </label>
                    <input
                      className="mt-2 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2.5 text-xs text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                      id="eval-start"
                      onChange={(e) => setEvalStart(e.target.value)}
                      required
                      type="datetime-local"
                      value={evalStart}
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="eval-end">
                      REQUESTED DEPARTURE END
                    </label>
                    <input
                      className="mt-2 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2.5 text-xs text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                      id="eval-end"
                      onChange={(e) => setEvalEnd(e.target.value)}
                      required
                      type="datetime-local"
                      value={evalEnd}
                    />
                  </div>

                  <button
                    className="mt-2 inline-flex min-h-11 w-full items-center justify-center rounded-full bg-coast-deep px-5 text-xs font-extrabold text-white transition hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue disabled:opacity-50"
                    disabled={evaluating}
                    type="submit"
                  >
                    {evaluating ? 'Evaluating Business Constraints...' : 'Evaluate Availability'}
                  </button>
                </form>

                {evalError && (
                  <div className="mt-4 rounded-2xl bg-red-50 p-3 text-xs text-red-900" role="alert">
                    {evalError}
                  </div>
                )}

                {/* Evaluation Result Display */}
                {evalResult && (
                  <div
                    className={`mt-6 rounded-2xl border p-5 ${
                      evalResult.status === 'AVAILABLE'
                        ? 'border-emerald-200 bg-emerald-50 text-emerald-950'
                        : 'border-amber-200 bg-amber-50 text-amber-950'
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-extrabold uppercase tracking-wider">Evaluation Result</span>
                      <span
                        className={`rounded-full px-2.5 py-0.5 text-xs font-extrabold ${
                          evalResult.status === 'AVAILABLE'
                            ? 'bg-emerald-200 text-emerald-900'
                            : 'bg-amber-200 text-amber-900'
                        }`}
                      >
                        {evalResult.status}
                      </span>
                    </div>

                    <div className="mt-4 space-y-2 text-xs">
                      <p>
                        <span className="font-bold">Offering:</span> {evalResult.offering.offeringTitle}
                      </p>
                      <p>
                        <span className="font-bold">Destination:</span> {evalResult.offering.destinationName}
                      </p>

                      {evalResult.reasonCodes && evalResult.reasonCodes.length > 0 && (
                        <div className="mt-3">
                          <p className="font-bold text-[11px] tracking-wide">EVALUATION REASONS:</p>
                          <ul className="mt-1 list-disc pl-4 space-y-1">
                            {evalResult.reasonCodes.map((code, idx) => (
                              <li key={idx}>{code}</li>
                            ))}
                          </ul>
                        </div>
                      )}

                      {evalResult.operationalRestriction && (
                        <div className="mt-3 border-t border-black/10 pt-2 text-[11px]">
                          <span className="font-bold">Operational Restriction Source:</span>{' '}
                          {evalResult.operationalRestriction.hasRestriction ? (
                            <span className="text-red-700 font-bold">
                              Restricted ({evalResult.operationalRestriction.reason})
                            </span>
                          ) : (
                            <span className="text-emerald-700 font-bold">Clear (No restrictions)</span>
                          )}
                        </div>
                      )}

                      <p className="mt-3 text-[10px] text-gray-500">
                        Evaluated at: {new Date(evalResult.evaluatedAt).toLocaleString()}
                      </p>
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>
        ) : null}
      </main>

      <SiteFooter />
    </div>
  )
}
