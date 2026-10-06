import { lazy, Suspense, useEffect, useMemo, useRef, useState, useTransition } from 'react'
import { Link, useSearchParams } from 'react-router'
import SiteFooter from '../../components/layout/SiteFooter'
import SiteHeader from '../../components/layout/SiteHeader'
import { useAuthSession } from '../../features/auth/authSession'
import type {
  ActivityDto,
  DestinationDto,
  FavouriteDto,
  MapConfigDto,
  MapSearchResultItemDto,
  NearbyDestinationDto,
  OfferingDto,
} from '../../features/experiences/experienceApi'
import type { ExperienceMapFocus, ExperienceMapView } from '../../features/experiences/ExperienceCoastalMap'
import {
  addFavourite,
  getActivities,
  getDestinations,
  getMapConfig,
  getNearbyExperiences,
  getOfferings,
  getUserFavourites,
  removeFavourite,
  searchMapPlaces,
} from '../../features/experiences/experienceApi'
import { hasAnyPermission } from '../../features/authorization/permissions'

const ExperienceCoastalMap = lazy(() => import('../../features/experiences/ExperienceCoastalMap'))
type ExperienceTab = 'catalog' | 'map'

const FALLBACK_DESTINATIONS = [
  { id: 'dest-mirissa', name: 'Mirissa Coastal Haven', region: 'Southern Province', latitude: 5.9482, longitude: 80.4716 },
  { id: 'dest-nilaveli', name: 'Nilaveli & Pigeon Island', region: 'Eastern Province', latitude: 8.6833, longitude: 81.1833 },
  { id: 'dest-hikkaduwa', name: 'Hikkaduwa Marine Sanctuary', region: 'Southern Province', latitude: 6.1406, longitude: 80.1005 },
  { id: 'dest-kalpitiya', name: 'Kalpitiya Lagoon & Bar Reef', region: 'North Western Province', latitude: 8.2307, longitude: 79.7656 },
  { id: 'dest-arugambay', name: 'Arugam Bay Surf Point', region: 'Eastern Province', latitude: 6.8415, longitude: 81.8354 },
]

const COASTAL_MAP_VIEWS: Record<'ALL' | 'SOUTH' | 'EAST' | 'WEST' | 'NORTH', ExperienceMapView> = {
  ALL: { latitude: 7.8731, longitude: 80.7718, zoom: 6.5 },
  SOUTH: { latitude: 5.98, longitude: 80.62, zoom: 8.5 },
  EAST: { latitude: 7.35, longitude: 81.55, zoom: 8 },
  WEST: { latitude: 7.15, longitude: 79.92, zoom: 8 },
  NORTH: { latitude: 9.05, longitude: 80.05, zoom: 8 },
}

function isValidMapCoordinate(latitude: number, longitude: number) {
  return Number.isFinite(latitude)
    && Number.isFinite(longitude)
    && latitude >= -85.0511
    && latitude <= 85.0511
    && longitude >= -180
    && longitude <= 180
    && !(latitude === 0 && longitude === 0)
}

export default function ExperiencesPage() {
  const { user } = useAuthSession()
  const [searchParams, setSearchParams] = useSearchParams()
  const activeTab: ExperienceTab = searchParams.get('tab') === 'map' ? 'map' : 'catalog'
  const [hasSwitchedExperienceTab, setHasSwitchedExperienceTab] = useState(false)
  const changeExperienceTab = (tab: ExperienceTab) => {
    if (tab !== activeTab) setHasSwitchedExperienceTab(true)

    const nextSearchParams = new URLSearchParams(searchParams)
    if (tab === 'map') nextSearchParams.set('tab', 'map')
    else nextSearchParams.delete('tab')
    setSearchParams(nextSearchParams, { replace: true })
  }

  // Catalog state
  const [destinations, setDestinations] = useState<DestinationDto[]>([])
  const [offerings, setOfferings] = useState<OfferingDto[]>([])
  const [searchQuery, setSearchQuery] = useState('')
  const [selectedRegion, setSelectedRegion] = useState('')
  const [selectedCategory, setSelectedCategory] = useState('')
  const [favourites, setFavourites] = useState<FavouriteDto[]>([])
  const [favMessage, setFavMessage] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  // Activities
  const [activities, setActivities] = useState<ActivityDto[]>([])

  // Admin and CRUD states
  const isAdmin =
    hasAnyPermission(user, ['experiences.catalogue.manage', 'auth.role.system.manage'])

  // Interactive Map state
  const [mapConfig, setMapConfig] = useState<MapConfigDto | null>(null)
  const [placeQuery, setPlaceQuery] = useState('')
  const [placeResults, setPlaceResults] = useState<MapSearchResultItemDto[]>([])
  const [placeSearched, setPlaceSearched] = useState(false)
  const [placeLoading, setPlaceLoading] = useState(false)
  const [placeSearchError, setPlaceSearchError] = useState<string | null>(null)
  const [selectedSpot, setSelectedSpot] = useState<{
    id?: string
    name: string
    region?: string
    lat: number
    lon: number
    description?: string
    offeringsCount?: number
    isSearchResult?: boolean
    isCurrentLocation?: boolean
  } | null>(null)
  const [isLocating, setIsLocating] = useState(false)
  const [locationError, setLocationError] = useState<string | null>(null)
  const [nearbyResults, setNearbyResults] = useState<NearbyDestinationDto[] | null>(null)
  const [nearbyError, setNearbyError] = useState<string | null>(null)
  const [isLoadingNearby, setIsLoadingNearby] = useState(false)
  const locationRequestId = useRef(0)
  const nearbyRequestId = useRef(0)
  const placeSearchRequestId = useRef(0)
  const [activeRegionView, setActiveRegionView] = useState<'ALL' | 'SOUTH' | 'EAST' | 'WEST' | 'NORTH'>('ALL')

  const activeDestinations = destinations.length > 0 ? destinations : FALLBACK_DESTINATIONS

  const mapView: ExperienceMapView = selectedSpot
    ? {
        latitude: selectedSpot.lat,
        longitude: selectedSpot.lon,
        zoom: selectedSpot.isCurrentLocation ? 12 : 12.5,
      }
    : COASTAL_MAP_VIEWS[activeRegionView]

  const mapFocus = useMemo<ExperienceMapFocus | null>(() => selectedSpot ? {
    name: selectedSpot.name,
    latitude: selectedSpot.lat,
    longitude: selectedSpot.lon,
    isCurrentLocation: selectedSpot.isCurrentLocation,
  } : null, [selectedSpot])

  const [, startTransition] = useTransition()

  useEffect(() => {
    let isMounted = true
    async function loadInitial() {
      setLoading(true)
      setError(null)
      try {
        const [destsRes, offRes] = await Promise.all([
          getDestinations({ pageSize: 30 }),
          getOfferings({ pageSize: 30 }),
        ])
        if (isMounted) {
          setDestinations(destsRes.items)
          setOfferings(offRes.items)
        }

        getActivities()
          .then((acts) => {
            if (isMounted) setActivities(Array.isArray(acts) ? acts : (acts?.items ?? []))
          })
          .catch(() => {})

        getMapConfig()
          .then((cfg) => {
            if (isMounted) setMapConfig(cfg)
          })
          .catch(() => {})

        if (user) {
          try {
            const favs = await getUserFavourites()
            if (isMounted) setFavourites(favs)
          } catch {
            // Favourites can fail silently for guest/loading state
          }
        }
      } catch (err: unknown) {
        if (isMounted) {
          setError(err instanceof Error ? err.message : 'Unable to load coastal experiences catalogue.')
        }
      } finally {
        if (isMounted) setLoading(false)
      }
    }

    loadInitial()
    return () => {
      isMounted = false
    }
  }, [user])

  useEffect(() => () => {
    locationRequestId.current += 1
    nearbyRequestId.current += 1
    placeSearchRequestId.current += 1
  }, [])

  async function handleSearch(e?: React.FormEvent) {
    if (e) e.preventDefault()
    setLoading(true)
    setError(null)
    try {
      const [destsRes, offRes] = await Promise.all([
        getDestinations({
          query: searchQuery.trim() || undefined,
          region: selectedRegion || undefined,
          pageSize: 30,
        }),
        getOfferings({
          pageSize: 30,
        }),
      ])
      setDestinations(destsRes.items)
      setOfferings(offRes.items)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Search failed. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  async function handleToggleFavourite(targetType: 'DESTINATION' | 'OFFERING', targetId: string) {
    if (!user) {
      setFavMessage('Please sign in to save experiences to your personal wishlist.')
      return
    }

    const existing = favourites.find(
      (f) => f.targetType.toUpperCase() === targetType && f.targetId.toLowerCase() === targetId.toLowerCase()
    )

    try {
      if (existing) {
        await removeFavourite(targetType, targetId)
        setFavourites((prev) => prev.filter((f) => f.id !== existing.id))
        setFavMessage('Item removed from your saved wishlist.')
      } else {
        const created = await addFavourite(targetType, targetId)
        setFavourites((prev) => [...prev, created])
        setFavMessage('Item saved to your wishlist!')
      }
    } catch (err: unknown) {
      setFavMessage(err instanceof Error ? err.message : 'Could not update your wishlist.')
    }

    setTimeout(() => setFavMessage(null), 3500)
  }

  function isFav(targetType: string, targetId: string): boolean {
    return favourites.some(
      (f) => f.targetType.toUpperCase() === targetType.toUpperCase() && f.targetId.toLowerCase() === targetId.toLowerCase()
    )
  }

  async function loadNearbyAt(latitude: number, longitude: number, label: string) {
    const requestId = ++nearbyRequestId.current
    if (!isValidMapCoordinate(latitude, longitude)) {
      setNearbyResults(null)
      setIsLoadingNearby(false)
      setNearbyError('This location could not be used. Search for another coastal place.')
      return
    }
    setNearbyResults(null)
    setNearbyError(null)
    setIsLoadingNearby(true)
    try {
      const response = await getNearbyExperiences({ latitude, longitude, radiusMeters: 50000, limit: 10 })
      if (requestId !== nearbyRequestId.current) return
      setNearbyResults(response.results)
      if (response.results.length === 0) {
        setNearbyError(`No published destinations were found within 50 km of ${label}.`)
      }
    } catch (err: unknown) {
      if (requestId !== nearbyRequestId.current) return
      setNearbyError(err instanceof Error ? err.message : 'Nearby destinations could not be loaded. Try again.')
    } finally {
      if (requestId === nearbyRequestId.current) setIsLoadingNearby(false)
    }
  }

  function clearNearbyResults() {
    nearbyRequestId.current += 1
    setIsLoadingNearby(false)
    setNearbyResults(null)
    setNearbyError(null)
  }

  function cancelPlaceSearch() {
    placeSearchRequestId.current += 1
    setPlaceLoading(false)
  }

  function handleMapSearchResult(place: MapSearchResultItemDto, fromSearchResponse = false) {
    if (!fromSearchResponse) cancelPlaceSearch()
    if (!isValidMapCoordinate(place.latitude, place.longitude)) {
      setLocationError('That place returned invalid coordinates. Search for another coastal place.')
      return
    }
    locationRequestId.current += 1
    setIsLocating(false)
    setLocationError(null)
    setSelectedSpot({
      name: place.displayName,
      region: place.region || place.country || 'Sri Lanka',
      lat: place.latitude,
      lon: place.longitude,
      isSearchResult: true,
    })
    void loadNearbyAt(place.latitude, place.longitude, place.displayName)
  }

  function handleUseCurrentLocation() {
    cancelPlaceSearch()
    const requestId = ++locationRequestId.current
    nearbyRequestId.current += 1
    setIsLocating(true)
    setLocationError(null)
    setNearbyResults(null)
    setNearbyError(null)
    setIsLoadingNearby(false)

    if (typeof window === 'undefined' || window.isSecureContext === false) {
      setIsLocating(false)
      setLocationError('Current location is available only on a secure connection. Search for a coastal place instead.')
      return
    }
    if (!navigator.geolocation) {
      setIsLocating(false)
      setLocationError('Location is not available in this browser. Search for a coastal place instead.')
      return
    }

    navigator.geolocation.getCurrentPosition(
      (position) => {
        if (requestId !== locationRequestId.current) return
        const { latitude, longitude } = position.coords
        if (!isValidMapCoordinate(latitude, longitude)) {
          setIsLocating(false)
          setLocationError('Your approximate location could not be read. Search for a coastal place instead.')
          return
        }

        setSelectedSpot({
          name: 'Your approximate location',
          region: 'Current location',
          lat: latitude,
          lon: longitude,
          isCurrentLocation: true,
        })
        setIsLocating(false)
        void loadNearbyAt(latitude, longitude, 'your approximate location')
      },
      (error) => {
        if (requestId !== locationRequestId.current) return
        setIsLocating(false)
        setLocationError(error.code === error.PERMISSION_DENIED
          ? 'Location permission was not granted. Search for a coastal place instead.'
          : error.code === error.TIMEOUT
            ? 'Getting your approximate location took too long. Try again or search for a place.'
            : 'Your approximate location is unavailable. Search for a coastal place instead.')
      },
      { enableHighAccuracy: false, maximumAge: 60000, timeout: 10000 },
    )
  }


  async function handlePlaceSearch(e?: React.FormEvent) {
    if (e) e.preventDefault()
    cancelPlaceSearch()
    const trimmed = placeQuery.trim()
    if (!trimmed) {
      setPlaceSearched(false)
      setPlaceResults([])
      setPlaceSearchError(null)
      return
    }
    if (trimmed.length < 2) {
      setPlaceSearched(false)
      setPlaceResults([])
      setPlaceSearchError('Enter at least 2 characters to search for a coastal place.')
      return
    }
    const requestId = ++placeSearchRequestId.current

    locationRequestId.current += 1
    nearbyRequestId.current += 1
    setIsLocating(false)
    setIsLoadingNearby(false)
    setNearbyResults(null)
    setNearbyError(null)
    setLocationError(null)
    setPlaceResults([])
    setPlaceSearched(false)
    setPlaceLoading(true)
    setPlaceSearchError(null)
    try {
      const [places, config] = await Promise.all([
        searchMapPlaces(trimmed),
        mapConfig ? Promise.resolve(mapConfig) : getMapConfig().catch(() => null),
      ])
      if (requestId !== placeSearchRequestId.current) return
      const validPlaces = places.results.filter((place) =>
        isValidMapCoordinate(place.latitude, place.longitude),
      )
      setPlaceResults(validPlaces)
      if (config) setMapConfig(config)
      setPlaceSearched(true)

      if (validPlaces.length > 0) {
        handleMapSearchResult(validPlaces[0], true)
      } else if (places.results.length > 0) {
        setLocationError('The place search returned invalid coordinates. Search for another coastal place.')
      }
    } catch (err: unknown) {
      if (requestId === placeSearchRequestId.current) {
        setPlaceSearchError(err instanceof Error ? err.message : 'Place search failed. Please try another coastal name.')
      }
    } finally {
      if (requestId === placeSearchRequestId.current) setPlaceLoading(false)
    }
  }

  function handleSelectDestination(dest: {
    id?: string
    name: string
    region?: string | null
    latitude: number
    longitude: number
    description?: string | null
    offeringsCount?: number
  }) {
    cancelPlaceSearch()
    if (!isValidMapCoordinate(dest.latitude, dest.longitude)) {
      setLocationError('This destination has invalid coordinates, so it cannot be shown on the map.')
      return
    }
    locationRequestId.current += 1
    setIsLocating(false)
    setLocationError(null)
    setSelectedSpot({
      id: dest.id,
      name: dest.name,
      region: dest.region || 'Coastal Sri Lanka',
      lat: dest.latitude,
      lon: dest.longitude,
      description: dest.description || undefined,
      offeringsCount: dest.offeringsCount ?? offerings.filter((o) => o.destinationId === dest.id).length,
    })
    void loadNearbyAt(dest.latitude, dest.longitude, dest.name)
  }

  // Filtered destinations based on search query and region
  const filteredDestinations = destinations.filter((dest) => {
    if (selectedRegion && dest.region !== selectedRegion) {
      return false
    }
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase()
      const matchName = dest.name.toLowerCase().includes(q)
      const matchRegion = dest.region?.toLowerCase().includes(q)
      const matchDesc = dest.description?.toLowerCase().includes(q)
      if (!matchName && !matchRegion && !matchDesc) return false
    }
    return true
  })

  // Filtered offerings based on search
  const filteredOfferings = offerings.filter((off) => {
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase()
      const matchTitle = off.title.toLowerCase().includes(q)
      const matchDesc = off.description?.toLowerCase().includes(q)
      const matchDest = off.destinationName.toLowerCase().includes(q)
      const matchAct = off.activityName.toLowerCase().includes(q)
      if (!matchTitle && !matchDesc && !matchDest && !matchAct) return false
    }
    if (selectedCategory && off.activityCode !== selectedCategory) {
      return false
    }
    return true
  })

  return (
    <div className="flex min-h-screen flex-col bg-coast-paper font-sans text-coast-ink" id="top">
      <SiteHeader active="experiences" />

      <main className="flex-1">
        {/* Hero Section */}
        <section aria-labelledby="experiences-hero-title" className="relative isolate overflow-hidden bg-coast-deep py-12 text-white sm:py-16">
          <div aria-hidden="true" className="absolute inset-0 -z-10 bg-[radial-gradient(ellipse_at_85%_10%,rgba(201,224,230,0.3),transparent_46%)]" />
          <div className="mx-auto max-w-7xl px-5 sm:px-8 lg:px-12">
            <div className="max-w-3xl">
              <p className="text-[11px] font-extrabold tracking-[0.2em] text-coast-glass sm:text-xs">
                COASTAL EXPERIENCES & BIODIVERSITY DISCOVERY
              </p>
              <h1 className="mt-3 font-display text-4xl leading-tight tracking-[-0.045em] sm:text-5xl lg:text-6xl" id="experiences-hero-title">
                Explore our coast with <span className="text-coast-glass">living context.</span>
              </h1>
              <p className="mt-4 text-sm leading-6 text-white/85 sm:text-base sm:leading-7">
                Discover authentic coastal destinations, marine wildlife sanctuaries, and scheduled sea activities across Sri Lanka, backed by environmental telemetry and biodiversity intelligence.
              </p>

              <div className="mt-6 flex flex-wrap items-center gap-3">
                <Link
                  className="inline-flex min-h-11 items-center gap-2 rounded-full bg-coast-paper px-5 text-sm font-extrabold text-coast-deep transition duration-200 hover:-translate-y-0.5 hover:bg-white hover:shadow-md focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
                  to="/experiences/favourites"
                >
                  <span>My Saved Wishlist</span>
                  <span aria-hidden="true">★</span>
                </Link>
                {isAdmin && (
                  <Link
                    className="inline-flex min-h-11 items-center gap-2 rounded-full border border-white/30 px-5 text-sm font-bold text-white transition duration-200 hover:bg-white/10 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
                    to="/experiences/manage"
                  >
                    <span>Catalogue Management</span>
                    <span aria-hidden="true">↗</span>
                  </Link>
                )}
              </div>
            </div>
          </div>
        </section>

        {/* Feedback Alert */}
        {favMessage && (
          <div className="mx-auto mt-4 max-w-7xl px-5 sm:px-8 lg:px-12">
            <div aria-live="polite" className="rounded-2xl border border-coast-teal/30 bg-coast-sage p-4 text-sm font-semibold text-coast-deep">
              {favMessage}
            </div>
          </div>
        )}

        {error && activeTab === 'catalog' && (
          <div className="mx-auto mt-4 max-w-7xl px-5 sm:px-8 lg:px-12">
            <div role="alert" className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm font-semibold text-red-900">
              {error}
            </div>
          </div>
        )}

        {/* Navigation Tabs */}
        <section aria-label="Experience discovery views" className="mx-auto mt-8 max-w-7xl px-5 sm:px-8 lg:px-12">
          <div className="flex border-b border-coast-line">
            <button
              className={`border-b-2 px-5 py-3 text-sm font-extrabold transition-colors ${
                activeTab === 'catalog'
                  ? 'border-coast-deep text-coast-deep'
                  : 'border-transparent text-coast-muted hover:text-coast-deep'
              }`}
              onClick={() => changeExperienceTab('catalog')}
              type="button"
            >
              Destinations & Offerings
            </button>
            <button
              className={`border-b-2 px-5 py-3 text-sm font-extrabold transition-colors ${
                activeTab === 'map'
                  ? 'border-coast-deep text-coast-deep'
                  : 'border-transparent text-coast-muted hover:text-coast-deep'
              }`}
              onClick={() => changeExperienceTab('map')}
              type="button"
            >
              Interactive Coastal Map
            </button>
          </div>
        </section>

        {/* View 1: Catalogue (Destinations & Activities) */}
        {activeTab === 'catalog' && (
          <div className={`mx-auto max-w-7xl px-5 py-8 sm:px-8 lg:px-12 ${hasSwitchedExperienceTab ? 'motion-safe:animate-coast-tab' : ''}`}>
            {/* Filter controls */}
            <form className="mb-8 grid gap-4 rounded-3xl border border-coast-line bg-coast-pearl p-5 shadow-sm sm:grid-cols-5 sm:items-end" onSubmit={handleSearch}>
              <div className="sm:col-span-2">
                <label className="block text-xs font-extrabold tracking-wide text-coast-deep" htmlFor="exp-search">
                  SEARCH COASTAL EXPERIENCES
                </label>
                <input
                  className="mt-2 w-full rounded-2xl border border-coast-line bg-white px-4 py-2.5 text-sm text-coast-ink transition focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                  id="exp-search"
                  onChange={(e) => setSearchQuery(e.target.value)}
                  placeholder="e.g. Coral reef, Whales, Mirissa, Snorkeling..."
                  type="search"
                  value={searchQuery}
                />
              </div>

              <div>
                <label className="block text-xs font-extrabold tracking-wide text-coast-deep" htmlFor="exp-region">
                  COASTAL REGION
                </label>
                <select
                  className="mt-2 w-full rounded-2xl border border-coast-line bg-white px-4 py-2.5 text-sm text-coast-ink transition focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                  id="exp-region"
                  onChange={(e) => setSelectedRegion(e.target.value)}
                  value={selectedRegion}
                >
                  <option value="">All Coastal Regions</option>
                  <option value="Southern Province">Southern Province</option>
                  <option value="Eastern Province">Eastern Province</option>
                  <option value="Western Province">Western Province</option>
                  <option value="North Western">North Western</option>
                  <option value="Northern Province">Northern Province</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-extrabold tracking-wide text-coast-deep" htmlFor="exp-category">
                  ACTIVITY TYPE
                </label>
                <select
                  className="mt-2 w-full rounded-2xl border border-coast-line bg-white px-4 py-2.5 text-sm text-coast-ink transition focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                  id="exp-category"
                  onChange={(e) => setSelectedCategory(e.target.value)}
                  value={selectedCategory}
                >
                  <option value="">All Activities</option>
                  {activities.map((act) => (
                    <option key={act.id} value={act.code}>
                      {act.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex gap-2">
                <button
                  className="inline-flex min-h-11 flex-1 items-center justify-center rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white transition hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue"
                  type="submit"
                >
                  Filter
                </button>
                <button
                  className="inline-flex min-h-11 items-center justify-center rounded-full border border-coast-line px-4 text-xs font-bold text-coast-muted transition hover:bg-coast-sand"
                  onClick={() => {
                    setSearchQuery('')
                    setSelectedRegion('')
                    setSelectedCategory('')
                    startTransition(() => {
                      getDestinations({ pageSize: 30 }).then((res) => setDestinations(res.items))
                    })
                  }}
                  type="button"
                >
                  Reset
                </button>
              </div>
            </form>

            {loading ? (
              <div className="py-16 text-center">
                <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-coast-deep border-r-transparent" />
                <p className="mt-3 text-sm font-semibold text-coast-muted">Loading coastal catalogue...</p>
              </div>
            ) : (
              <>
                {/* Destinations Section */}
                <section aria-labelledby="section-destinations-title" className="mb-14">
                  <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
                    <div>
                      <p className="text-xs font-extrabold tracking-[0.16em] text-coast-blue">AUTHORITATIVE PLACES</p>
                      <h2 className="mt-1 font-display text-2xl font-bold tracking-tight text-coast-ink sm:text-3xl" id="section-destinations-title">
                        Coastal Destinations
                      </h2>
                    </div>
                    <div className="flex flex-wrap items-center gap-3">
                      <span className="text-xs font-bold text-coast-muted">{filteredDestinations.length} destinations found</span>
                      {isAdmin && (
                        <Link
                          className="inline-flex min-h-10 items-center gap-1.5 rounded-full bg-coast-deep px-4 text-xs font-extrabold text-white shadow-sm transition hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue"
                          to="/experiences/manage#destinations"
                        >
                          <span aria-hidden="true" className="text-sm font-bold">+</span>
                          <span>Add Destination</span>
                        </Link>
                      )}
                    </div>
                  </div>

                  {filteredDestinations.length === 0 ? (
                    <div className="rounded-3xl border border-coast-line bg-coast-pearl p-8 text-center text-coast-muted">
                      No destinations match the selected search criteria.
                    </div>
                  ) : (
                    <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
                      {filteredDestinations.map((dest) => {
                        const saved = isFav('DESTINATION', dest.id)
                        return (
                          <article
                            className="group flex flex-col justify-between rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-sm transition duration-200 hover:-translate-y-1 hover:border-coast-glass hover:shadow-md"
                            key={dest.id}
                          >
                            <div>
                              <div className="flex items-start justify-between gap-3">
                                <span className="rounded-full bg-coast-sage px-3 py-1 text-[11px] font-extrabold text-coast-deep">
                                  {dest.region || 'Coastal Sri Lanka'}
                                </span>
                                <div className="flex items-center gap-1.5">
                                  {isAdmin && (
                                    <Link
                                      aria-label={`Edit ${dest.name}`}
                                      className="inline-flex h-8 items-center gap-1 rounded-full border border-coast-line bg-white px-2.5 text-[11px] font-bold text-coast-deep transition hover:border-coast-blue hover:text-coast-blue hover:bg-coast-sand"
                                      title="Edit destination details"
                                      to={`/experiences/manage?editDestination=${dest.id}#destinations`}
                                    >
                                      <span>✎ Edit</span>
                                    </Link>
                                  )}
                                  <button
                                    aria-label={saved ? `Remove ${dest.name} from saved` : `Save ${dest.name}`}
                                    className={`inline-flex h-8 w-8 items-center justify-center rounded-full border transition ${
                                      saved
                                        ? 'border-coast-teal bg-coast-teal text-white'
                                        : 'border-coast-line bg-white text-coast-muted hover:border-coast-blue hover:text-coast-blue'
                                    }`}
                                    onClick={() => handleToggleFavourite('DESTINATION', dest.id)}
                                    type="button"
                                  >
                                    ★
                                  </button>
                                </div>
                              </div>

                              <h3 className="mt-4 font-display text-xl font-bold tracking-tight text-coast-ink group-hover:text-coast-deep">
                                {dest.name}
                              </h3>
                              <p className="mt-2 line-clamp-3 text-sm leading-6 text-coast-muted">
                                {dest.description || 'Pristine coastal destination with rich marine biodiversity and ocean activities.'}
                              </p>

                              <div className="mt-4 flex items-center gap-3 text-xs font-semibold text-coast-muted">
                                <span className="inline-flex items-center gap-1">
                                  <svg className="h-4 w-4 text-coast-blue" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                                    <circle cx="12" cy="12" r="10" />
                                    <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
                                  </svg>
                                  {dest.latitude.toFixed(4)}°N, {dest.longitude.toFixed(4)}°E
                                </span>
                              </div>
                            </div>

                            <div className="mt-6 border-t border-coast-line/70 pt-4">
                              <Link
                                className="inline-flex min-h-10 w-full items-center justify-center gap-2 rounded-full bg-coast-sand px-4 text-xs font-extrabold text-coast-deep transition hover:bg-coast-glass"
                                to={`/experiences/destinations/${dest.id}`}
                              >
                                <span>Inspect Destination & Ecology</span>
                                <span aria-hidden="true">→</span>
                              </Link>
                            </div>
                          </article>
                        )
                      })}
                    </div>
                  )}
                </section>

                {/* Offerings Section */}
                <section aria-labelledby="section-offerings-title">
                  <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
                    <div>
                      <p className="text-xs font-extrabold tracking-[0.16em] text-coast-blue">PARTICIPATORY PACKAGES</p>
                      <h2 className="mt-1 font-display text-2xl font-bold tracking-tight text-coast-ink sm:text-3xl" id="section-offerings-title">
                        Experience Offerings & Tours
                      </h2>
                    </div>
                    <div className="flex flex-wrap items-center gap-3">
                      <span className="text-xs font-bold text-coast-muted">{filteredOfferings.length} offerings available</span>
                      {isAdmin && (
                        <Link
                          className="inline-flex min-h-10 items-center gap-1.5 rounded-full bg-coast-deep px-4 text-xs font-extrabold text-white shadow-sm transition hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue"
                          to="/experiences/manage#offerings"
                        >
                          <span aria-hidden="true" className="text-sm font-bold">+</span>
                          <span>Add Offering</span>
                        </Link>
                      )}
                    </div>
                  </div>

                  {filteredOfferings.length === 0 ? (
                    <div className="rounded-3xl border border-coast-line bg-coast-pearl p-8 text-center text-coast-muted">
                      No offerings currently registered for this query.
                    </div>
                  ) : (
                    <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
                      {filteredOfferings.map((off) => {
                        const saved = isFav('OFFERING', off.id)
                        return (
                          <article
                            className="group flex flex-col justify-between rounded-3xl border border-coast-line bg-white p-6 shadow-sm transition duration-200 hover:-translate-y-1 hover:border-coast-glass hover:shadow-md"
                            key={off.id}
                          >
                            <div>
                              <div className="flex items-start justify-between gap-3">
                                <span className="rounded-full bg-coast-sand px-3 py-1 text-[11px] font-extrabold text-coast-deep">
                                  {off.activityName}
                                </span>
                                <div className="flex items-center gap-1.5">
                                  {isAdmin && (
                                    <Link
                                      aria-label={`Edit ${off.title}`}
                                      className="inline-flex h-8 items-center gap-1 rounded-full border border-coast-line bg-white px-2.5 text-[11px] font-bold text-coast-deep transition hover:border-coast-blue hover:text-coast-blue hover:bg-coast-sand"
                                      title="Edit offering details"
                                      to={`/experiences/manage?editOffering=${off.id}#offerings`}
                                    >
                                      <span>✎ Edit</span>
                                    </Link>
                                  )}
                                  <button
                                    aria-label={saved ? `Remove ${off.title} from saved` : `Save ${off.title}`}
                                    className={`inline-flex h-8 w-8 items-center justify-center rounded-full border transition ${
                                      saved
                                        ? 'border-coast-teal bg-coast-teal text-white'
                                        : 'border-coast-line bg-white text-coast-muted hover:border-coast-blue hover:text-coast-blue'
                                    }`}
                                    onClick={() => handleToggleFavourite('OFFERING', off.id)}
                                    type="button"
                                  >
                                    ★
                                  </button>
                                </div>
                              </div>

                              <h3 className="mt-4 font-display text-xl font-bold tracking-tight text-coast-ink group-hover:text-coast-deep">
                                {off.title}
                              </h3>
                              <p className="mt-1 text-xs font-semibold text-coast-muted">
                                Destination: <span className="text-coast-deep font-bold">{off.destinationName}</span>
                              </p>
                              <p className="mt-2 line-clamp-3 text-sm leading-6 text-coast-muted">
                                {off.description || 'Guided coastal experience with experienced local instructors and safety equipment.'}
                              </p>

                              <div className="mt-4 flex flex-wrap gap-2 text-xs font-bold text-coast-deep">
                                {off.price != null && (
                                  <span className="rounded-xl bg-coast-sage px-2.5 py-1">
                                    {off.currency || 'LKR'} {off.price.toLocaleString()}
                                  </span>
                                )}
                                {off.durationMinutes != null && (
                                  <span className="rounded-xl bg-coast-sand px-2.5 py-1">
                                    {off.durationMinutes} mins
                                  </span>
                                )}
                                {off.maxCapacity != null && (
                                  <span className="rounded-xl bg-coast-sand px-2.5 py-1">
                                    Max {off.maxCapacity} guests
                                  </span>
                                )}
                              </div>
                            </div>

                            <div className="mt-6 border-t border-coast-line/70 pt-4">
                              <Link
                                className="inline-flex min-h-10 w-full items-center justify-center gap-2 rounded-full bg-coast-deep px-4 text-xs font-extrabold text-white transition hover:bg-coast-blue"
                                to={`/experiences/offerings/${off.id}`}
                              >
                                <span>Check Real-Time Availability</span>
                                <span aria-hidden="true">→</span>
                              </Link>
                            </div>
                          </article>
                        )
                      })}
                    </div>
                  )}
                </section>
              </>
            )}
          </div>
        )}


        {/* View 3: Interactive Coastal Map */}
        {activeTab === 'map' && (
          <div className={`mx-auto max-w-7xl px-5 py-8 sm:px-8 lg:px-12 ${hasSwitchedExperienceTab ? 'motion-safe:animate-coast-tab' : ''}`}>
            <div className="mb-8">
              <p className="text-xs font-extrabold tracking-[0.16em] text-coast-blue">COASTAL EXPLORER</p>
              <h2 className="mt-2 font-display text-3xl font-bold tracking-tight text-coast-ink sm:text-4xl">
                Interactive Coastal Map of Sri Lanka
              </h2>
              <p className="mt-2 max-w-3xl text-sm leading-6 text-coast-muted">
                Explore marine sanctuaries, coastal bays, surfing beaches, and harbor towns across Sri Lanka.
                Click any marker on the map to inspect details or search for specific spots.
              </p>
            </div>

            <div className="grid gap-8 lg:grid-cols-12">
              {/* Left Column: Search & Place Details */}
              <div className="flex flex-col gap-6 lg:col-span-5">
                {/* Search Card */}
                <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-sm" data-testid="map-place-search-card">
                  <h3 className="text-sm font-extrabold tracking-wide text-coast-deep">
                    FIND COASTAL SPOTS & BAYS
                  </h3>
                  <p className="mt-1 text-xs text-coast-muted">
                    Search for beaches, coral reefs, bays, and seaside towns.
                  </p>

                  <form className="mt-4 flex gap-2" onSubmit={handlePlaceSearch}>
                    <input
                      className="flex-1 rounded-2xl border border-coast-line bg-white px-4 py-2.5 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                          onChange={(e) => {
                            cancelPlaceSearch()
                            setPlaceQuery(e.target.value)
                        setPlaceSearchError(null)
                      }}
                      placeholder="e.g. Mirissa, Trincomalee, Bentota..."
                      type="search"
                      value={placeQuery}
                    />
                    <button
                      className="inline-flex min-h-11 items-center justify-center rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white transition hover:bg-coast-blue"
                      disabled={placeLoading}
                      type="submit"
                    >
                      {placeLoading ? 'Searching...' : 'Search'}
                    </button>
                  </form>
                  {placeSearchError && (
                    <p className="mt-3 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs leading-5 text-amber-900" role="alert">
                      {placeSearchError}
                    </p>
                  )}

                  {/* Coastal Highlights from Database */}
                  <div className="mt-4 border-t border-coast-line/60 pt-4">
                    <p className="text-[11px] font-bold text-coast-muted">Coastal destinations from catalogue:</p>
                    <div className="mt-2 flex flex-wrap gap-1.5">
                      {activeDestinations.map((dest) => (
                        <button
                          key={dest.id || dest.name}
                          type="button"
                          className={`rounded-full border px-2.5 py-1 text-xs font-semibold transition ${
                            selectedSpot?.id === dest.id || selectedSpot?.name === dest.name
                              ? 'border-coast-teal bg-coast-teal/15 font-bold text-coast-deep'
                              : 'border-coast-line bg-white text-coast-deep hover:border-coast-glass hover:bg-coast-sage'
                          }`}
                          onClick={() => handleSelectDestination(dest as DestinationDto)}
                        >
                          {dest.name}
                        </button>
                      ))}
                    </div>
                  </div>

                  {/* Search Results List */}
                  {placeSearched && (
                    <div className="mt-5 border-t border-coast-line/60 pt-4">
                      <div className="flex items-center justify-between">
                        <h4 className="text-xs font-bold text-coast-deep">Places Found ({placeResults.length})</h4>
                        {placeResults.length > 0 && (
                          <button
                            type="button"
                            className="text-[11px] text-coast-muted hover:text-coast-deep underline"
                            onClick={() => {
                              setPlaceSearched(false)
                              setPlaceResults([])
                            }}
                          >
                            Clear
                          </button>
                        )}
                      </div>

                      {placeResults.length === 0 ? (
                        <p className="mt-2 text-xs text-coast-muted leading-relaxed">
                          No places found matching &ldquo;{placeQuery}&rdquo;. Try another coastal town, bay, or island.
                        </p>
                      ) : (
                        <ul className="mt-2.5 divide-y divide-coast-line max-h-48 overflow-y-auto pr-1">
                          {placeResults.map((p, idx) => (
                            <li key={idx} className="py-2 flex items-center justify-between gap-2">
                              <div className="min-w-0">
                                <p className="text-xs font-bold text-coast-ink truncate">{p.displayName}</p>
                                <p className="text-[11px] text-coast-muted">
                                  {p.latitude.toFixed(4)}°N, {p.longitude.toFixed(4)}°E {p.region ? `· ${p.region}` : ''}
                                </p>
                              </div>
                              <button
                                type="button"
                                className="shrink-0 rounded-full bg-coast-sand px-3 py-1 text-[11px] font-extrabold text-coast-deep hover:bg-coast-glass transition"
                                onClick={() => handleMapSearchResult(p)}
                              >
                                View
                              </button>
                            </li>
                          ))}
                        </ul>
                      )}
                    </div>
                  )}
                </div>

                {/* Selected Location Card */}
                {selectedSpot ? (
                  <div className="rounded-3xl border border-coast-teal/30 bg-coast-sand/40 p-6 shadow-sm">
                    <span className="rounded-full bg-coast-sage px-3 py-1 text-[10px] font-extrabold uppercase tracking-wider text-coast-deep">
                      {selectedSpot.region || 'Coastal Destination'}
                    </span>
                    <h3 className="mt-3 font-display text-2xl font-bold text-coast-ink">
                      {selectedSpot.name}
                    </h3>
                    {selectedSpot.isCurrentLocation ? (
                      <p className="mt-1 text-xs text-coast-muted">
                        Your approximate position is held temporarily to find nearby destinations.
                      </p>
                    ) : (
                      <p className="mt-1 text-xs font-semibold text-coast-muted">
                        Coordinates: {selectedSpot.lat.toFixed(4)}°N, {selectedSpot.lon.toFixed(4)}°E
                      </p>
                    )}

                    {selectedSpot.description && (
                      <p className="mt-3 text-xs leading-5 text-coast-muted">
                        {selectedSpot.description}
                      </p>
                    )}

                    {selectedSpot.offeringsCount != null && selectedSpot.offeringsCount > 0 && (
                      <div className="mt-3 inline-flex items-center gap-1.5 rounded-full bg-white px-3 py-1 text-xs font-bold text-coast-deep border border-coast-line">
                        <span>🌊</span>
                        <span>{selectedSpot.offeringsCount} active coastal tours available</span>
                      </div>
                    )}

                    <div className="mt-5 flex flex-col gap-2 sm:flex-row">
                      {selectedSpot.id && (
                        <Link
                          className="inline-flex min-h-10 flex-1 items-center justify-center rounded-full bg-coast-deep px-4 text-xs font-extrabold text-white transition hover:bg-coast-blue"
                          to={`/experiences/destinations/${selectedSpot.id}`}
                        >
                          Inspect Destination & Ecology →
                        </Link>
                      )}
                      <button
                        type="button"
                        className="inline-flex min-h-10 flex-1 items-center justify-center rounded-full border border-coast-line bg-white px-4 text-xs font-extrabold text-coast-deep transition hover:bg-coast-sand"
                        onClick={() => {
                          setSearchQuery(selectedSpot.name)
                          changeExperienceTab('catalog')
                        }}
                      >
                        Explore Experiences Here
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="rounded-3xl border border-dashed border-coast-line bg-coast-pearl/60 p-6 text-center text-coast-muted">
                    <p className="text-xs font-semibold">
                      Click any coastal marker on the map to explore that destination, or use the search bar above.
                    </p>
                  </div>
                )}

                {locationError && (
                  <p className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900" role="alert">
                    {locationError}
                  </p>
                )}

                {(isLoadingNearby || nearbyError || nearbyResults !== null) && (
                  <section aria-live="polite" className="rounded-3xl border border-coast-line bg-white p-5 shadow-sm">
                    <div className="flex items-center justify-between gap-3">
                      <h3 className="text-sm font-extrabold text-coast-deep">Nearby coastal destinations</h3>
                      {isLoadingNearby && <span className="text-xs text-coast-muted">Finding places…</span>}
                    </div>
                    {nearbyError && (
                      <p className="mt-3 rounded-xl bg-coast-sage px-3 py-2 text-xs leading-5 text-coast-muted" role="status">
                        {nearbyError}
                      </p>
                    )}
                    {nearbyResults && nearbyResults.length > 0 && (
                      <ul className="mt-3 divide-y divide-coast-line">
                        {nearbyResults.map((destination) => (
                          <li key={destination.destinationId} className="flex items-center justify-between gap-3 py-3">
                            <div className="min-w-0">
                              <p className="truncate text-sm font-bold text-coast-ink">{destination.name}</p>
                              <p className="text-xs text-coast-muted">
                                {destination.region || 'Coastal destination'} · {(destination.distanceMeters / 1000).toFixed(1)} km
                              </p>
                            </div>
                            <Link
                              className="shrink-0 rounded-full bg-coast-sand px-3 py-1.5 text-xs font-extrabold text-coast-deep hover:bg-coast-glass"
                              to={`/experiences/destinations/${destination.destinationId}`}
                            >
                              View
                            </Link>
                          </li>
                        ))}
                      </ul>
                    )}
                  </section>
                )}
              </div>

              {/* Right Column: Interactive coastal map */}
              <div className="flex flex-col lg:col-span-7">
                <div className="relative flex h-[65vh] min-h-[520px] max-h-[720px] w-full flex-col overflow-hidden rounded-3xl border border-coast-line bg-[#e8f3f6] shadow-sm">
                  {/* Map Header / Region Selector Bar */}
                  <div className="z-10 flex flex-wrap items-center justify-between gap-2 border-b border-coast-line/70 bg-white/95 px-4 py-2.5 backdrop-blur-sm">
                    <div className="flex flex-wrap gap-1.5">
                      {(
                        [
                          ['ALL', 'All Coastlines'],
                          ['SOUTH', 'South Coast'],
                          ['EAST', 'East Coast'],
                          ['WEST', 'West Coast'],
                          ['NORTH', 'North Coast'],
                        ] as const
                      ).map(([key, label]) => (
                        <button
                          key={key}
                          type="button"
                          className={`rounded-full px-3 py-1 text-xs font-extrabold transition shadow-sm ${
                            activeRegionView === key && !selectedSpot
                              ? 'bg-coast-deep text-white'
                              : 'bg-coast-pearl text-coast-deep border border-coast-line hover:bg-white'
                          }`}
                          onClick={() => {
                            cancelPlaceSearch()
                            locationRequestId.current += 1
                            setIsLocating(false)
                            clearNearbyResults()
                            setLocationError(null)
                            setSelectedSpot(null)
                            setActiveRegionView(key)
                          }}
                        >
                          {label}
                        </button>
                      ))}
                    </div>

                    {selectedSpot && (
                      <button
                        type="button"
                        className="rounded-full border border-coast-line bg-white px-2.5 py-1 text-xs font-bold text-coast-muted hover:text-coast-deep transition"
                        onClick={() => {
                          cancelPlaceSearch()
                          locationRequestId.current += 1
                          setIsLocating(false)
                          clearNearbyResults()
                          setLocationError(null)
                          setSelectedSpot(null)
                        }}
                      >
                        Reset to Island View ⟲
                      </button>
                    )}

                  </div>

                  <div className="relative min-h-0 flex-1 w-full bg-[#cad2d3]">
                    <Suspense fallback={(
                      <div className="grid h-full min-h-72 place-items-center p-6 text-center text-sm text-coast-muted" role="status">
                        Preparing the interactive coastal map…
                      </div>
                    )}>
                      <ExperienceCoastalMap
                        destinations={destinations}
                        focus={mapFocus}
                        onDestinationSelected={handleSelectDestination}
                        styleUrl={mapConfig ? mapConfig.availableStyles[mapConfig.defaultStyle] ?? null : null}
                        view={mapView}
                      />
                    </Suspense>
                    <button
                      aria-label="Use current location"
                      className="absolute bottom-12 right-4 z-30 grid size-12 place-items-center rounded-full border border-coast-line bg-white text-coast-deep shadow-lg transition hover:bg-coast-pearl focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue disabled:cursor-wait disabled:opacity-70"
                      disabled={isLocating}
                      onClick={handleUseCurrentLocation}
                      title="Use current location"
                      type="button"
                    >
                      {isLocating ? (
                        <span className="size-5 animate-spin rounded-full border-2 border-coast-line border-t-coast-deep" aria-hidden="true" />
                      ) : (
                        <svg aria-hidden="true" className="size-5" fill="none" viewBox="0 0 24 24">
                          <circle cx="12" cy="12" r="7" stroke="currentColor" strokeWidth="1.8" />
                          <circle cx="12" cy="12" r="2" fill="currentColor" />
                          <path d="M12 2v3M12 19v3M2 12h3m14 0h3" stroke="currentColor" strokeLinecap="round" strokeWidth="1.8" />
                        </svg>
                      )}
                    </button>
                  </div>
                </div>
                <p className="mt-2 text-center text-xs text-coast-muted">
                  Pan and zoom to explore. Select a marker to see its destination. Map tiles: {mapConfig?.attribution || 'OpenFreeMap and OpenStreetMap contributors'}.
                </p>
              </div>
            </div>
          </div>
        )}
      </main>

      <SiteFooter />
    </div>
  )
}
