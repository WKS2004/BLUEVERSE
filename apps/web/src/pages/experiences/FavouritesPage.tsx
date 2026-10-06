import { useEffect, useState } from 'react'
import { Link, useLocation } from 'react-router'
import SiteFooter from '../../components/layout/SiteFooter'
import SiteHeader from '../../components/layout/SiteHeader'
import { authEntryHref } from '../../features/auth/authNavigation'
import { useAuthSession } from '../../features/auth/authSession'
import type { FavouriteDto } from '../../features/experiences/experienceApi'
import {
  getUserFavourites,
  removeFavourite,
} from '../../features/experiences/experienceApi'

export default function FavouritesPage() {
  const { user, status } = useAuthSession()
  const location = useLocation()

  const [favourites, setFavourites] = useState<FavouriteDto[]>([])
  const [filterType, setFilterType] = useState<string>('ALL')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [actionMessage, setActionMessage] = useState<string | null>(null)

  useEffect(() => {
    if (status === 'checking') return
    if (!user) {
      setLoading(false)
      return
    }

    let isMounted = true
    async function loadFavs() {
      setLoading(true)
      setError(null)
      try {
        const data = await getUserFavourites()
        if (isMounted) setFavourites(data)
      } catch (err: unknown) {
        if (isMounted) {
          setError(err instanceof Error ? err.message : 'Unable to load saved coastal wishlist.')
        }
      } finally {
        if (isMounted) setLoading(false)
      }
    }

    loadFavs()
    return () => {
      isMounted = false
    }
  }, [user, status])

  async function handleRemove(targetType: string, targetId: string) {
    try {
      await removeFavourite(targetType, targetId)
      setFavourites((prev) =>
        prev.filter(
          (f) => !(f.targetType.toUpperCase() === targetType.toUpperCase() && f.targetId.toLowerCase() === targetId.toLowerCase())
        )
      )
      setActionMessage('Item removed from your saved wishlist.')
    } catch (err: unknown) {
      setActionMessage(err instanceof Error ? err.message : 'Could not remove item from wishlist.')
    }
    setTimeout(() => setActionMessage(null), 3000)
  }

  const filtered = favourites.filter((f) => {
    if (filterType === 'ALL') return true
    return f.targetType.toUpperCase() === filterType
  })

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

        <section aria-labelledby="wishlist-title" className="mx-auto max-w-7xl px-5 py-8 sm:px-8 lg:px-12">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <p className="text-xs font-extrabold tracking-[0.16em] text-coast-blue">PERSONAL WISHLIST</p>
              <h1 className="mt-2 font-display text-4xl font-bold tracking-tight text-coast-ink sm:text-5xl" id="wishlist-title">
                Saved Coastal Experiences
              </h1>
              <p className="mt-2 text-sm leading-6 text-coast-muted">
                Your personal bookmarked coastal destinations, marine wildlife tours, and scheduled activities.
              </p>
            </div>

            {user && (
              <div className="flex items-center gap-2">
                <span className="text-xs font-bold text-coast-muted">Filter:</span>
                <select
                  className="rounded-2xl border border-coast-line bg-white px-3 py-1.5 text-xs font-bold text-coast-deep focus:border-coast-blue focus:outline-none"
                  onChange={(e) => setFilterType(e.target.value)}
                  value={filterType}
                >
                  <option value="ALL">All Items ({favourites.length})</option>
                  <option value="DESTINATION">Destinations</option>
                  <option value="OFFERING">Offerings</option>
                  <option value="ACTIVITY">Activities</option>
                </select>
              </div>
            )}
          </div>

          {actionMessage && (
            <div aria-live="polite" className="mt-4 rounded-2xl border border-coast-teal/30 bg-coast-sage p-4 text-xs font-semibold text-coast-deep">
              {actionMessage}
            </div>
          )}

          {error && (
            <div role="alert" className="mt-4 rounded-2xl border border-red-200 bg-red-50 p-4 text-xs font-semibold text-red-900">
              {error}
            </div>
          )}

          {/* Signed-out state */}
          {!user && status !== 'checking' && (
            <div className="mt-8 rounded-3xl border border-coast-line bg-coast-pearl p-8 text-center sm:p-12">
              <span className="font-display text-5xl text-coast-deep">★</span>
              <h2 className="mt-4 font-display text-2xl font-bold text-coast-ink">
                Sign in to manage your saved wishlist
              </h2>
              <p className="mt-2 text-sm text-coast-muted">
                Saving destinations and offerings requires an active BLUEVERSE account so your wishlist is preserved securely.
              </p>
              <div className="mt-6 flex justify-center gap-3">
                <Link
                  className="inline-flex min-h-11 items-center justify-center rounded-full bg-coast-deep px-6 text-sm font-extrabold text-white transition hover:bg-coast-blue"
                  to={authEntryHref('/signin', location)}
                >
                  Sign in
                </Link>
                <Link
                  className="inline-flex min-h-11 items-center justify-center rounded-full border border-coast-line bg-white px-6 text-sm font-bold text-coast-deep transition hover:bg-coast-sand"
                  to={authEntryHref('/signup', location)}
                >
                  Create account
                </Link>
              </div>
            </div>
          )}

          {/* Signed-in Content */}
          {user && (
            <>
              {loading ? (
                <div className="py-20 text-center">
                  <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-coast-deep border-r-transparent" />
                  <p className="mt-3 text-xs font-semibold text-coast-muted">Loading your wishlist items...</p>
                </div>
              ) : filtered.length === 0 ? (
                <div className="mt-8 rounded-3xl border border-coast-line bg-coast-pearl p-10 text-center">
                  <p className="text-sm font-semibold text-coast-muted">
                    No items in your saved wishlist matching this filter.
                  </p>
                  <Link
                    className="mt-4 inline-flex min-h-10 items-center justify-center rounded-full bg-coast-deep px-5 text-xs font-extrabold text-white transition hover:bg-coast-blue"
                    to="/experiences"
                  >
                    Browse Coastal Experiences Directory
                  </Link>
                </div>
              ) : (
                <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                  {filtered.map((item) => (
                    <article
                      className="flex flex-col justify-between rounded-3xl border border-coast-line bg-white p-6 shadow-sm transition hover:border-coast-glass"
                      key={item.id}
                    >
                      <div>
                        <div className="flex items-center justify-between">
                          <span className="rounded-full bg-coast-sand px-3 py-1 text-[11px] font-extrabold text-coast-deep">
                            {item.targetType}
                          </span>
                          <span className="text-[11px] font-bold text-coast-muted">
                            Saved {new Date(item.createdAt).toLocaleDateString()}
                          </span>
                        </div>

                        <h3 className="mt-3 font-display text-xl font-bold text-coast-ink">
                          {item.targetTitle || 'Coastal Experience Item'}
                        </h3>
                        {item.targetStatus && (
                          <p className="mt-1 text-xs text-coast-muted">Status: {item.targetStatus}</p>
                        )}
                      </div>

                      <div className="mt-6 flex items-center justify-between gap-2 border-t border-coast-line/70 pt-4">
                        {item.targetType.toUpperCase() === 'DESTINATION' ? (
                          <Link
                            className="inline-flex min-h-9 items-center rounded-full bg-coast-sand px-3 text-xs font-bold text-coast-deep hover:bg-coast-glass"
                            to={`/experiences/destinations/${item.targetId}`}
                          >
                            View Destination →
                          </Link>
                        ) : item.targetType.toUpperCase() === 'OFFERING' ? (
                          <Link
                            className="inline-flex min-h-9 items-center rounded-full bg-coast-sand px-3 text-xs font-bold text-coast-deep hover:bg-coast-glass"
                            to={`/experiences/offerings/${item.targetId}`}
                          >
                            View Offering →
                          </Link>
                        ) : (
                          <Link
                            className="inline-flex min-h-9 items-center rounded-full bg-coast-sand px-3 text-xs font-bold text-coast-deep hover:bg-coast-glass"
                            to="/experiences"
                          >
                            Explore Experiences →
                          </Link>
                        )}

                        <button
                          className="text-xs font-bold text-red-700 transition hover:text-red-900"
                          onClick={() => handleRemove(item.targetType, item.targetId)}
                          type="button"
                        >
                          Remove
                        </button>
                      </div>
                    </article>
                  ))}
                </div>
              )}
            </>
          )}
        </section>
      </main>

      <SiteFooter />
    </div>
  )
}
