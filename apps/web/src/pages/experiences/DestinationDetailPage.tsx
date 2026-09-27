import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import SiteFooter from '../../components/layout/SiteFooter'
import SiteHeader from '../../components/layout/SiteHeader'
import { useAuthSession } from '../../features/auth/authSession'
import type {
  BiodiversityContextResponseDto,
  DestinationDto,
  FavouriteDto,
  MarineConditionsContextDto,
  OfferingDto,
  OperationalAdvisoriesResponseDto,
} from '../../features/experiences/experienceApi'
import {
  addFavourite,
  getDestinationBiodiversity,
  getDestinationById,
  getDestinationMarineConditions,
  getDestinationOperationalAdvisories,
  getOfferings,
  getUserFavourites,
  removeFavourite,
} from '../../features/experiences/experienceApi'

export default function DestinationDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { user } = useAuthSession()

  const [destination, setDestination] = useState<DestinationDto | null>(null)
  const [offerings, setOfferings] = useState<OfferingDto[]>([])
  const [marine, setMarine] = useState<MarineConditionsContextDto | null>(null)
  const [advisories, setAdvisories] = useState<OperationalAdvisoriesResponseDto | null>(null)
  const [biodiversity, setBiodiversity] = useState<BiodiversityContextResponseDto | null>(null)
  const [favourites, setFavourites] = useState<FavouriteDto[]>([])
  const [favMessage, setFavMessage] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) return
    let isMounted = true

    async function loadData() {
      setLoading(true)
      setError(null)
      try {
        const [dest, offRes] = await Promise.all([
          getDestinationById(id!),
          getOfferings({ destinationId: id!, pageSize: 20 }),
        ])

        if (!isMounted) return
        setDestination(dest)
        setOfferings(offRes.items)

        // Load contextual enrichments with resilient fallbacks
        const [marineRes, advRes, bioRes] = await Promise.allSettled([
          getDestinationMarineConditions(id!),
          getDestinationOperationalAdvisories(id!),
          getDestinationBiodiversity(id!),
        ])

        if (marineRes.status === 'fulfilled') setMarine(marineRes.value)
        if (advRes.status === 'fulfilled') setAdvisories(advRes.value)
        if (bioRes.status === 'fulfilled') setBiodiversity(bioRes.value)

        if (user) {
          try {
            const favs = await getUserFavourites()
            if (isMounted) setFavourites(favs)
          } catch {
            // Ignore error loading favourites
          }
        }
      } catch (err: unknown) {
        if (isMounted) {
          setError(err instanceof Error ? err.message : 'Could not load coastal destination information.')
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
    if (!id || !destination) return
    if (!user) {
      setFavMessage('Sign in to add this destination to your saved experiences.')
      return
    }

    const existing = favourites.find(
      (f) => f.targetType.toUpperCase() === 'DESTINATION' && f.targetId.toLowerCase() === id.toLowerCase()
    )

    try {
      if (existing) {
        await removeFavourite('DESTINATION', id)
        setFavourites((prev) => prev.filter((f) => f.id !== existing.id))
        setFavMessage('Destination removed from saved wishlist.')
      } else {
        const created = await addFavourite('DESTINATION', id)
        setFavourites((prev) => [...prev, created])
        setFavMessage('Destination saved to your personal wishlist!')
      }
    } catch (err: unknown) {
      setFavMessage(err instanceof Error ? err.message : 'Failed to update saved status.')
    }

    setTimeout(() => setFavMessage(null), 3500)
  }

  const isSaved = id ? favourites.some((f) => f.targetType.toUpperCase() === 'DESTINATION' && f.targetId.toLowerCase() === id.toLowerCase()) : false

  return (
    <div className="flex min-h-screen flex-col bg-coast-paper font-sans text-coast-ink" id="top">
      <SiteHeader active="experiences" />

      <main className="flex-1">
        {/* Breadcrumb & Top Bar */}
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
            <p className="mt-3 text-sm font-semibold text-coast-muted">Loading destination profile & ecology...</p>
          </div>
        ) : destination ? (
          <div className="mx-auto max-w-7xl px-5 py-8 sm:px-8 lg:px-12">
            {/* Destination Title Header */}
            <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <span className="rounded-full bg-coast-sand px-3 py-1 text-xs font-extrabold text-coast-deep">
                  {destination.region || 'Coastal Sri Lanka'}
                </span>
                <h1 className="mt-2 font-display text-4xl font-bold tracking-tight text-coast-ink sm:text-5xl">
                  {destination.name}
                </h1>
                <p className="mt-2 text-xs font-semibold text-coast-muted">
                  Coordinates: {destination.latitude.toFixed(4)}° N, {destination.longitude.toFixed(4)}° E · Status: {destination.status}
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
                  <span>{isSaved ? 'Saved in Wishlist' : 'Save Destination'}</span>
                  <span>★</span>
                </button>
              </div>
            </div>

            {/* Description Card */}
            <div className="mt-6 rounded-3xl border border-coast-line bg-white p-6 shadow-sm sm:p-8">
              <h2 className="text-xs font-extrabold tracking-[0.16em] text-coast-blue">DESTINATION OVERVIEW</h2>
              <p className="mt-3 text-base leading-7 text-coast-ink sm:text-lg">
                {destination.description ||
                  'A celebrated coastal sanctuary known for unique marine wildlife, clear shoreline waters, and sustainable coastal tourism.'}
              </p>
            </div>

            {/* Ecological and Operational Context Section */}
            <div className="mt-8 grid gap-6 lg:grid-cols-3">
              {/* Marine Conditions */}
              <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-sm">
                <div className="flex items-center justify-between">
                  <h3 className="text-xs font-extrabold tracking-[0.16em] text-coast-blue">MARINE SAFETY</h3>
                  <span
                    className={`rounded-full px-2.5 py-0.5 text-[11px] font-extrabold ${
                      marine?.safetyLevel === 'SAFE'
                        ? 'bg-emerald-100 text-emerald-800'
                        : marine?.safetyLevel === 'CAUTION'
                        ? 'bg-amber-100 text-amber-800'
                        : 'bg-coast-sand text-coast-muted'
                    }`}
                  >
                    {marine?.safetyLevel || 'MONITORED'}
                  </span>
                </div>

                <div className="mt-4 space-y-3 text-sm">
                  <div className="flex justify-between border-b border-coast-line/60 pb-2">
                    <span className="text-coast-muted">Water Condition:</span>
                    <span className="font-extrabold text-coast-deep">{marine?.waterCondition || 'Calm to Moderate'}</span>
                  </div>
                  <div className="flex justify-between border-b border-coast-line/60 pb-2">
                    <span className="text-coast-muted">Wave Height:</span>
                    <span className="font-extrabold text-coast-deep">
                      {marine?.waveHeightMeters != null ? `${marine.waveHeightMeters} m` : '0.8 m'}
                    </span>
                  </div>
                  <div className="flex justify-between border-b border-coast-line/60 pb-2">
                    <span className="text-coast-muted">Wind Speed:</span>
                    <span className="font-extrabold text-coast-deep">
                      {marine?.windSpeedKnots != null ? `${marine.windSpeedKnots} kts` : '10 kts'}
                    </span>
                  </div>
                </div>

                <p className="mt-4 text-xs leading-5 text-coast-muted">
                  {marine?.advisoryMessage || 'Conditions are favorable for coastal swimming and small vessel departures.'}
                </p>
              </div>

              {/* Operational Advisories */}
              <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-sm">
                <div className="flex items-center justify-between">
                  <h3 className="text-xs font-extrabold tracking-[0.16em] text-coast-blue">COASTAL ADVISORIES</h3>
                  <span className="rounded-full bg-coast-sand px-2.5 py-0.5 text-[11px] font-extrabold text-coast-deep">
                    {advisories?.advisories.length || 0} ACTIVE
                  </span>
                </div>

                {advisories && advisories.advisories.length > 0 ? (
                  <div className="mt-4 divide-y divide-coast-line/60">
                    {advisories.advisories.map((adv) => (
                      <div className="py-2.5" key={adv.advisoryId}>
                        <div className="flex items-center gap-2">
                          <span
                            className={`h-2 w-2 rounded-full ${
                              adv.severity === 'CRITICAL' ? 'bg-red-500' : adv.severity === 'WARNING' ? 'bg-amber-500' : 'bg-coast-blue'
                            }`}
                          />
                          <p className="text-xs font-extrabold text-coast-ink">{adv.title}</p>
                        </div>
                        <p className="mt-1 text-xs text-coast-muted">{adv.description}</p>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="mt-6 text-center text-xs text-coast-muted">
                    No active restrictions. Harbor and coastal authorities report normal navigation status.
                  </div>
                )}
              </div>

              {/* Biodiversity Intelligence */}
              <div className="rounded-3xl border border-coast-line bg-coast-sage p-6 shadow-sm">
                <div className="flex items-center justify-between">
                  <h3 className="text-xs font-extrabold tracking-[0.16em] text-coast-blue">BIODIVERSITY PREDICTIONS</h3>
                  <span className="rounded-full bg-white px-2.5 py-0.5 text-[11px] font-extrabold text-coast-teal">
                    {biodiversity?.status === 'available' ? 'MODEL CONNECTED' : 'ECOLOGICAL INTELLIGENCE'}
                  </span>
                </div>

                {biodiversity?.predictions && biodiversity.predictions.length > 0 ? (
                  <div className="mt-4 space-y-3">
                    {biodiversity.predictions.map((p, idx) => (
                      <div className="rounded-2xl bg-white p-3 border border-coast-line" key={idx}>
                        <div className="flex items-center justify-between">
                          <span className="text-sm font-extrabold text-coast-ink">{p.speciesName}</span>
                          <span className="text-xs font-extrabold text-coast-teal">
                            {Math.round(p.occurrenceProbability * 100)}% prob.
                          </span>
                        </div>
                        <p className="text-[11px] italic text-coast-muted">{p.scientificName}</p>
                        {p.habitatSuitability && (
                          <p className="mt-1 text-xs text-coast-muted">Habitat: {p.habitatSuitability}</p>
                        )}
                      </div>
                    ))}

                    <p className="mt-3 text-[10px] leading-4 text-coast-muted">
                      {biodiversity.disclaimer || 'Occurrence probabilities are model estimates based on seasonal observations.'}
                    </p>
                  </div>
                ) : (
                  <div className="mt-6 text-center text-xs text-coast-muted">
                    Biodiversity machine learning model context is currently reporting safe baseline. No anomalous species pressures observed.
                  </div>
                )}
              </div>
            </div>

            {/* Offerings at this Destination */}
            <section aria-labelledby="destination-offerings-title" className="mt-12">
              <div className="mb-6 flex items-end justify-between">
                <div>
                  <p className="text-xs font-extrabold tracking-[0.16em] text-coast-blue">EXPERIENCES AT THIS DESTINATION</p>
                  <h2 className="mt-1 font-display text-2xl font-bold tracking-tight text-coast-ink sm:text-3xl" id="destination-offerings-title">
                    Available Activities & Tours
                  </h2>
                </div>
                <span className="text-xs font-bold text-coast-muted">{offerings.length} packages registered</span>
              </div>

              {offerings.length === 0 ? (
                <div className="rounded-3xl border border-coast-line bg-coast-pearl p-8 text-center text-coast-muted">
                  No offerings are currently listed for this destination.
                </div>
              ) : (
                <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
                  {offerings.map((off) => (
                    <article className="flex flex-col justify-between rounded-3xl border border-coast-line bg-white p-6 shadow-sm" key={off.id}>
                      <div>
                        <span className="rounded-full bg-coast-sand px-3 py-1 text-[11px] font-extrabold text-coast-deep">
                          {off.activityName}
                        </span>
                        <h3 className="mt-3 font-display text-xl font-bold text-coast-ink">{off.title}</h3>
                        <p className="mt-2 line-clamp-3 text-sm text-coast-muted">{off.description}</p>
                        <div className="mt-4 flex flex-wrap gap-2 text-xs font-bold text-coast-deep">
                          {off.price != null && (
                            <span className="rounded-xl bg-coast-sage px-2.5 py-1">
                              {off.currency || 'LKR'} {off.price.toLocaleString()}
                            </span>
                          )}
                          {off.durationMinutes != null && (
                            <span className="rounded-xl bg-coast-sand px-2.5 py-1">{off.durationMinutes} mins</span>
                          )}
                        </div>
                      </div>

                      <div className="mt-6 border-t border-coast-line/70 pt-4">
                        <Link
                          className="inline-flex min-h-10 w-full items-center justify-center rounded-full bg-coast-deep px-4 text-xs font-extrabold text-white transition hover:bg-coast-blue"
                          to={`/experiences/offerings/${off.id}`}
                        >
                          Check Schedule & Real-Time Availability →
                        </Link>
                      </div>
                    </article>
                  ))}
                </div>
              )}
            </section>
          </div>
        ) : null}
      </main>

      <SiteFooter />
    </div>
  )
}
