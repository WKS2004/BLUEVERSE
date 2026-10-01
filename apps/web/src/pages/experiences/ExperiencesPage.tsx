import { useEffect, useState, useTransition } from 'react'
import { Link } from 'react-router'
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
import {
  addFavourite,
  createDestination,
  createOffering,
  deleteDestination,
  deleteOffering,
  getActivities,
  getDestinations,
  getMapConfig,
  getNearbyExperiences,
  getOfferings,
  getUserFavourites,
  removeFavourite,
  searchMapPlaces,
  updateDestination,
  updateOffering,
} from '../../features/experiences/experienceApi'
import { hasAnyPermission } from '../../features/authorization/permissions'

const FALLBACK_DESTINATIONS = [
  { id: 'dest-mirissa', name: 'Mirissa Coastal Haven', region: 'Southern Province', latitude: 5.9482, longitude: 80.4716 },
  { id: 'dest-nilaveli', name: 'Nilaveli & Pigeon Island', region: 'Eastern Province', latitude: 8.6833, longitude: 81.1833 },
  { id: 'dest-hikkaduwa', name: 'Hikkaduwa Marine Sanctuary', region: 'Southern Province', latitude: 6.1406, longitude: 80.1005 },
  { id: 'dest-kalpitiya', name: 'Kalpitiya Lagoon & Bar Reef', region: 'North Western Province', latitude: 8.2307, longitude: 79.7656 },
  { id: 'dest-arugambay', name: 'Arugam Bay Surf Point', region: 'Eastern Province', latitude: 6.8415, longitude: 81.8354 },
]

export default function ExperiencesPage() {
  const { user } = useAuthSession()
  const [activeTab, setActiveTab] = useState<'catalog' | 'nearby' | 'map'>('catalog')

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

  // Nearby state
  const [nearLocation, setNearLocation] = useState('')
  const [nearLat, setNearLat] = useState('5.9485')
  const [nearLon, setNearLon] = useState('80.4578')
  const [nearRadiusKm, setNearRadiusKm] = useState('50')
  const [showAdvancedCoords, setShowAdvancedCoords] = useState(false)
  const [resolvedLocationName, setResolvedLocationName] = useState<string | null>(null)
  const [nearbyResults, setNearbyResults] = useState<NearbyDestinationDto[]>([])
  const [nearbySearched, setNearbySearched] = useState(false)
  const [nearbyLoading, setNearbyLoading] = useState(false)

  // Activities for offering creation
  const [activities, setActivities] = useState<ActivityDto[]>([])

  // Admin and CRUD states
  const isAdmin =
    hasAnyPermission(user, ['experiences.catalogue.manage', 'auth.role.manage']) ||
    (user?.roles?.includes('Admin') ?? false)

  // Destination modal state
  const [destModalOpen, setDestModalOpen] = useState(false)
  const [destModalMode, setDestModalMode] = useState<'create' | 'edit'>('create')
  const [destEditingId, setDestEditingId] = useState<string | null>(null)
  const [destFormName, setDestFormName] = useState('')
  const [destFormSlug, setDestFormSlug] = useState('')
  const [destFormRegion, setDestFormRegion] = useState('')
  const [destFormDesc, setDestFormDesc] = useState('')
  const [destFormLat, setDestFormLat] = useState('5.9485')
  const [destFormLon, setDestFormLon] = useState('80.4578')
  const [destSubmitting, setDestSubmitting] = useState(false)

  // Offering modal state
  const [offModalOpen, setOffModalOpen] = useState(false)
  const [offModalMode, setOffModalMode] = useState<'create' | 'edit'>('create')
  const [offEditingId, setOffEditingId] = useState<string | null>(null)
  const [offFormDestId, setOffFormDestId] = useState('')
  const [offFormActId, setOffFormActId] = useState('')
  const [offFormTitle, setOffFormTitle] = useState('')
  const [offFormPrice, setOffFormPrice] = useState('5000')
  const [offFormDuration, setOffFormDuration] = useState('120')
  const [offFormCapacity, setOffFormCapacity] = useState('8')
  const [offFormDesc, setOffFormDesc] = useState('')
  const [offSubmitting, setOffSubmitting] = useState(false)

  // Delete confirmation modal state
  const [deleteDialog, setDeleteDialog] = useState<{
    type: 'destination' | 'offering'
    id: string
    title: string
  } | null>(null)
  const [deleteSubmitting, setDeleteSubmitting] = useState(false)

  // Interactive Map state
  const [mapConfig, setMapConfig] = useState<MapConfigDto | null>(null)
  const [placeQuery, setPlaceQuery] = useState('')
  const [placeResults, setPlaceResults] = useState<MapSearchResultItemDto[]>([])
  const [placeSearched, setPlaceSearched] = useState(false)
  const [placeLoading, setPlaceLoading] = useState(false)
  const [selectedSpot, setSelectedSpot] = useState<{
    id?: string
    name: string
    region?: string
    lat: number
    lon: number
    description?: string
    offeringsCount?: number
    isSearchResult?: boolean
  } | null>(null)
  const [activeRegionView, setActiveRegionView] = useState<'ALL' | 'SOUTH' | 'EAST' | 'WEST' | 'NORTH'>('ALL')

  const activeDestinations = destinations.length > 0 ? destinations : FALLBACK_DESTINATIONS

  const osmEmbedUrl = selectedSpot
    ? `https://www.openstreetmap.org/export/embed.html?bbox=${(selectedSpot.lon - 0.12).toFixed(4)}%2C${(selectedSpot.lat - 0.08).toFixed(4)}%2C${(selectedSpot.lon + 0.12).toFixed(4)}%2C${(selectedSpot.lat + 0.08).toFixed(4)}&layer=mapnik&marker=${selectedSpot.lat.toFixed(4)}%2C${selectedSpot.lon.toFixed(4)}`
    : activeRegionView === 'SOUTH'
      ? 'https://www.openstreetmap.org/export/embed.html?bbox=79.9000%2C5.8000%2C81.2000%2C6.4000&layer=mapnik'
      : activeRegionView === 'EAST'
        ? 'https://www.openstreetmap.org/export/embed.html?bbox=81.0000%2C6.6000%2C82.1000%2C8.9000&layer=mapnik'
        : activeRegionView === 'WEST'
          ? 'https://www.openstreetmap.org/export/embed.html?bbox=79.6000%2C6.5000%2C80.3000%2C8.5000&layer=mapnik'
          : activeRegionView === 'NORTH'
            ? 'https://www.openstreetmap.org/export/embed.html?bbox=79.6000%2C8.9000%2C80.9000%2C9.9000&layer=mapnik'
            : 'https://www.openstreetmap.org/export/embed.html?bbox=79.2000%2C5.7000%2C82.2000%2C10.0000&layer=mapnik'

  const osmViewUrl = selectedSpot
    ? `https://www.openstreetmap.org/?mlat=${selectedSpot.lat.toFixed(4)}&mlon=${selectedSpot.lon.toFixed(4)}#map=13/${selectedSpot.lat.toFixed(4)}/${selectedSpot.lon.toFixed(4)}`
    : 'https://www.openstreetmap.org/#map=7/7.8731/80.7718'

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

  // --- CRUD Handlers ---
  function openCreateDestinationModal() {
    setDestModalMode('create')
    setDestEditingId(null)
    setDestFormName('')
    setDestFormSlug('')
    setDestFormRegion('')
    setDestFormDesc('')
    setDestFormLat('5.9485')
    setDestFormLon('80.4578')
    setDestModalOpen(true)
  }

  function openEditDestinationModal(dest: DestinationDto) {
    setDestModalMode('edit')
    setDestEditingId(dest.id)
    setDestFormName(dest.name)
    setDestFormSlug(dest.slug || '')
    setDestFormRegion(dest.region || '')
    setDestFormDesc(dest.description || '')
    setDestFormLat(String(dest.latitude))
    setDestFormLon(String(dest.longitude))
    setDestModalOpen(true)
  }

  async function handleSaveDestination(e: React.FormEvent) {
    e.preventDefault()
    setDestSubmitting(true)
    setError(null)
    try {
      const lat = parseFloat(destFormLat)
      const lon = parseFloat(destFormLon)
      if (isNaN(lat) || isNaN(lon)) {
        throw new Error('Please enter valid latitude and longitude coordinates.')
      }

      if (destModalMode === 'create') {
        const created = await createDestination({
          name: destFormName.trim(),
          slug: destFormSlug.trim() || undefined,
          region: destFormRegion.trim() || undefined,
          description: destFormDesc.trim() || undefined,
          latitude: lat,
          longitude: lon,
        })
        setDestinations((prev) => [created, ...prev])
        setFavMessage(`Destination "${created.name}" created successfully!`)
      } else if (destEditingId) {
        const updated = await updateDestination(destEditingId, {
          name: destFormName.trim(),
          slug: destFormSlug.trim() || undefined,
          region: destFormRegion.trim() || undefined,
          description: destFormDesc.trim() || undefined,
          latitude: lat,
          longitude: lon,
        })
        setDestinations((prev) => prev.map((d) => (d.id === updated.id ? updated : d)))
        setFavMessage(`Destination "${updated.name}" updated successfully!`)
      }
      setDestModalOpen(false)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to save destination.')
    } finally {
      setDestSubmitting(false)
      setTimeout(() => setFavMessage(null), 3500)
    }
  }

  function openCreateOfferingModal() {
    setOffModalMode('create')
    setOffEditingId(null)
    setOffFormDestId(destinations[0]?.id || '')
    setOffFormActId(activities[0]?.id || '')
    setOffFormTitle('')
    setOffFormPrice('5000')
    setOffFormDuration('120')
    setOffFormCapacity('8')
    setOffFormDesc('')
    setOffModalOpen(true)
  }

  function openEditOfferingModal(off: OfferingDto) {
    setOffModalMode('edit')
    setOffEditingId(off.id)
    setOffFormDestId(off.destinationId)
    setOffFormActId(off.activityId)
    setOffFormTitle(off.title)
    setOffFormPrice(off.price != null ? String(off.price) : '')
    setOffFormDuration(off.durationMinutes != null ? String(off.durationMinutes) : '')
    setOffFormCapacity(off.maxCapacity != null ? String(off.maxCapacity) : '')
    setOffFormDesc(off.description || '')
    setOffModalOpen(true)
  }

  async function handleSaveOffering(e: React.FormEvent) {
    e.preventDefault()
    setOffSubmitting(true)
    setError(null)
    try {
      if (offModalMode === 'create') {
        if (!offFormDestId) throw new Error('Please select a destination.')
        if (!offFormActId) throw new Error('Please select an activity.')
        const created = await createOffering({
          destinationId: offFormDestId,
          activityId: offFormActId,
          title: offFormTitle.trim(),
          description: offFormDesc.trim() || undefined,
          price: offFormPrice ? parseFloat(offFormPrice) : 0,
          currency: 'LKR',
          durationMinutes: offFormDuration ? parseInt(offFormDuration, 10) : 60,
          maxCapacity: offFormCapacity ? parseInt(offFormCapacity, 10) : 10,
        })
        setOfferings((prev) => [created, ...prev])
        setFavMessage(`Offering "${created.title}" created successfully!`)
      } else if (offEditingId) {
        const updated = await updateOffering(offEditingId, {
          title: offFormTitle.trim(),
          description: offFormDesc.trim() || undefined,
          price: offFormPrice ? parseFloat(offFormPrice) : undefined,
          currency: 'LKR',
          durationMinutes: offFormDuration ? parseInt(offFormDuration, 10) : undefined,
          maxCapacity: offFormCapacity ? parseInt(offFormCapacity, 10) : undefined,
        })
        setOfferings((prev) => prev.map((o) => (o.id === updated.id ? updated : o)))
        setFavMessage(`Offering "${updated.title}" updated successfully!`)
      }
      setOffModalOpen(false)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to save offering.')
    } finally {
      setOffSubmitting(false)
      setTimeout(() => setFavMessage(null), 3500)
    }
  }

  function confirmDelete(type: 'destination' | 'offering', id: string, title: string) {
    setDeleteDialog({ type, id, title })
  }

  async function handleExecuteDelete() {
    if (!deleteDialog) return
    setDeleteSubmitting(true)
    setError(null)
    try {
      if (deleteDialog.type === 'destination') {
        await deleteDestination(deleteDialog.id)
        setDestinations((prev) => prev.filter((d) => d.id !== deleteDialog.id))
        setOfferings((prev) => prev.filter((o) => o.destinationId !== deleteDialog.id))
        setFavMessage(`Destination "${deleteDialog.title}" deleted.`)
      } else {
        await deleteOffering(deleteDialog.id)
        setOfferings((prev) => prev.filter((o) => o.id !== deleteDialog.id))
        setFavMessage(`Offering "${deleteDialog.title}" deleted.`)
      }
      setDeleteDialog(null)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Deletion failed.')
    } finally {
      setDeleteSubmitting(false)
      setTimeout(() => setFavMessage(null), 3500)
    }
  }

  async function handleNearbySearch(e?: React.FormEvent) {
    if (e) e.preventDefault()
    const radMeters = (parseFloat(nearRadiusKm) || 50) * 1000
    const trimmedLoc = nearLocation.trim()

    setNearbyLoading(true)
    setError(null)
    try {
      let res
      if (trimmedLoc) {
        res = await getNearbyExperiences({ location: trimmedLoc, radiusMeters: radMeters, limit: 15 })
      } else {
        const lat = parseFloat(nearLat)
        const lon = parseFloat(nearLon)
        if (isNaN(lat) || isNaN(lon)) {
          setError('Please enter a location name or valid coordinates.')
          setNearbyLoading(false)
          return
        }
        res = await getNearbyExperiences({ latitude: lat, longitude: lon, radiusMeters: radMeters, limit: 15 })
      }

      setNearbyResults(res.results || [])
      if (res.query?.resolvedLocation) {
        setResolvedLocationName(res.query.resolvedLocation)
      } else if (trimmedLoc) {
        setResolvedLocationName(trimmedLoc)
      } else {
        setResolvedLocationName(null)
      }
      if (res.query?.latitude != null) setNearLat(res.query.latitude.toFixed(4))
      if (res.query?.longitude != null) setNearLon(res.query.longitude.toFixed(4))
      setNearbySearched(true)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Unable to query nearby experiences.')
    } finally {
      setNearbyLoading(false)
    }
  }

  async function handlePlaceSearch(e?: React.FormEvent) {
    if (e) e.preventDefault()
    const trimmed = placeQuery.trim()
    if (!trimmed) return
    if (trimmed.length < 2) {
      setError('Please enter at least 2 characters to search for a coastal place.')
      return
    }

    setPlaceLoading(true)
    setError(null)
    try {
      const [places, config] = await Promise.all([
        searchMapPlaces(trimmed),
        mapConfig ? Promise.resolve(mapConfig) : getMapConfig().catch(() => null),
      ])
      setPlaceResults(places.results)
      if (config) setMapConfig(config)
      setPlaceSearched(true)

      if (places.results.length > 0) {
        const first = places.results[0]
        setSelectedSpot({
          name: first.displayName,
          region: first.region || first.country || 'Sri Lanka',
          lat: first.latitude,
          lon: first.longitude,
          isSearchResult: true,
        })
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Place search failed. Please try another coastal name.')
    } finally {
      setPlaceLoading(false)
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
    setSelectedSpot({
      id: dest.id,
      name: dest.name,
      region: dest.region || 'Coastal Sri Lanka',
      lat: dest.latitude,
      lon: dest.longitude,
      description: dest.description || undefined,
      offeringsCount: (dest as any).offeringsCount ?? offerings.filter((o) => o.destinationId === dest.id).length,
    })
  }

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

        {error && (
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
              onClick={() => setActiveTab('catalog')}
              type="button"
            >
              Destinations & Offerings
            </button>
            <button
              className={`border-b-2 px-5 py-3 text-sm font-extrabold transition-colors ${
                activeTab === 'nearby'
                  ? 'border-coast-deep text-coast-deep'
                  : 'border-transparent text-coast-muted hover:text-coast-deep'
              }`}
              onClick={() => setActiveTab('nearby')}
              type="button"
            >
              Nearby Proximity Search
            </button>
            <button
              className={`border-b-2 px-5 py-3 text-sm font-extrabold transition-colors ${
                activeTab === 'map'
                  ? 'border-coast-deep text-coast-deep'
                  : 'border-transparent text-coast-muted hover:text-coast-deep'
              }`}
              onClick={() => setActiveTab('map')}
              type="button"
            >
              Interactive Coastal Map
            </button>
          </div>
        </section>

        {/* View 1: Catalogue (Destinations & Activities) */}
        {activeTab === 'catalog' && (
          <div className="mx-auto max-w-7xl px-5 py-8 sm:px-8 lg:px-12">
            {/* Filter controls */}
            <form className="mb-8 grid gap-4 rounded-3xl border border-coast-line bg-coast-pearl p-5 shadow-sm sm:grid-cols-4 sm:items-end" onSubmit={handleSearch}>
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
                      <span className="text-xs font-bold text-coast-muted">{destinations.length} destinations found</span>
                      {isAdmin && (
                        <button
                          className="inline-flex min-h-10 items-center gap-1.5 rounded-full bg-coast-deep px-4 text-xs font-extrabold text-white shadow-sm transition hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue"
                          onClick={openCreateDestinationModal}
                          type="button"
                        >
                          <span aria-hidden="true" className="text-sm font-bold">+</span>
                          <span>Add Destination</span>
                        </button>
                      )}
                    </div>
                  </div>

                  {destinations.length === 0 ? (
                    <div className="rounded-3xl border border-coast-line bg-coast-pearl p-8 text-center text-coast-muted">
                      No destinations match the selected search criteria.
                    </div>
                  ) : (
                    <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
                      {destinations.map((dest) => {
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
                                    <>
                                      <button
                                        aria-label={`Edit ${dest.name}`}
                                        className="inline-flex h-8 items-center gap-1 rounded-full border border-coast-line bg-white px-2.5 text-[11px] font-bold text-coast-deep transition hover:border-coast-blue hover:text-coast-blue hover:bg-coast-sand"
                                        onClick={() => openEditDestinationModal(dest)}
                                        title="Edit destination details"
                                        type="button"
                                      >
                                        <span>✎ Edit</span>
                                      </button>
                                      <button
                                        aria-label={`Delete ${dest.name}`}
                                        className="inline-flex h-8 items-center gap-1 rounded-full border border-red-200 bg-white px-2.5 text-[11px] font-bold text-red-600 transition hover:border-red-500 hover:bg-red-50"
                                        onClick={() => confirmDelete('destination', dest.id, dest.name)}
                                        title="Delete destination"
                                        type="button"
                                      >
                                        <span>✕ Delete</span>
                                      </button>
                                    </>
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
                        <button
                          className="inline-flex min-h-10 items-center gap-1.5 rounded-full bg-coast-deep px-4 text-xs font-extrabold text-white shadow-sm transition hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue"
                          onClick={openCreateOfferingModal}
                          type="button"
                        >
                          <span aria-hidden="true" className="text-sm font-bold">+</span>
                          <span>Add Offering</span>
                        </button>
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
                                    <>
                                      <button
                                        aria-label={`Edit ${off.title}`}
                                        className="inline-flex h-8 items-center gap-1 rounded-full border border-coast-line bg-white px-2.5 text-[11px] font-bold text-coast-deep transition hover:border-coast-blue hover:text-coast-blue hover:bg-coast-sand"
                                        onClick={() => openEditOfferingModal(off)}
                                        title="Edit offering details"
                                        type="button"
                                      >
                                        <span>✎ Edit</span>
                                      </button>
                                      <button
                                        aria-label={`Delete ${off.title}`}
                                        className="inline-flex h-8 items-center gap-1 rounded-full border border-red-200 bg-white px-2.5 text-[11px] font-bold text-red-600 transition hover:border-red-500 hover:bg-red-50"
                                        onClick={() => confirmDelete('offering', off.id, off.title)}
                                        title="Delete offering"
                                        type="button"
                                      >
                                        <span>✕ Delete</span>
                                      </button>
                                    </>
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

        {/* View 2: Nearby Proximity Search */}
        {activeTab === 'nearby' && (
          <div className="mx-auto max-w-7xl px-5 py-8 sm:px-8 lg:px-12">
            <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-sm sm:p-8">
              <p className="text-xs font-extrabold tracking-[0.16em] text-coast-blue">LOCATION-AWARE DISCOVERY</p>
              <h2 className="mt-2 font-display text-3xl font-bold tracking-tight text-coast-ink">
                Experiences Near Your Coordinates
              </h2>
              <p className="mt-2 max-w-2xl text-sm leading-6 text-coast-muted">
                Find published coastal destinations within a specified radius using genuine Haversine spatial calculations. Coordinates are used strictly for this request and are not stored.
              </p>

              <div className="mt-5">
                <p className="text-xs font-bold text-coast-muted">Quick coastal destinations:</p>
                <div className="mt-2 flex flex-wrap gap-2">
                  {activeDestinations.slice(0, 6).map((spot) => (
                    <button
                      className="rounded-full border border-coast-line bg-white px-3 py-1.5 text-xs font-bold text-coast-deep transition hover:border-coast-glass hover:bg-coast-sage"
                      key={spot.id || spot.name}
                      onClick={() => {
                        setNearLocation(spot.name)
                        setNearLat(spot.latitude.toFixed(4))
                        setNearLon(spot.longitude.toFixed(4))
                      }}
                      type="button"
                    >
                      {spot.name} ({spot.region})
                    </button>
                  ))}
                </div>
              </div>

              <form className="mt-6 flex flex-col gap-5" onSubmit={handleNearbySearch}>
                {/* Primary User-Friendly Inputs */}
                <div className="grid gap-4 sm:grid-cols-3 sm:items-end">
                  <div className="sm:col-span-2">
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="near-location">
                      COASTAL LOCATION OR PLACE NAME
                    </label>
                    <input
                      className="mt-2 w-full rounded-2xl border border-coast-line bg-white px-4 py-2.5 text-sm text-coast-ink placeholder:text-coast-muted focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                      id="near-location"
                      onChange={(e) => setNearLocation(e.target.value)}
                      placeholder="e.g. Mirissa, Weligama, Galle, Pigeon Island..."
                      type="text"
                      value={nearLocation}
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="near-rad">
                      SEARCH RADIUS (KM)
                    </label>
                    <input
                      className="mt-2 w-full rounded-2xl border border-coast-line bg-white px-4 py-2.5 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                      id="near-rad"
                      max="500"
                      min="1"
                      onChange={(e) => setNearRadiusKm(e.target.value)}
                      type="number"
                      value={nearRadiusKm}
                    />
                  </div>
                </div>

                {/* Collapsible Advanced Coordinate Search */}
                <div className="border-t border-coast-line/70 pt-3">
                  <button
                    type="button"
                    className="inline-flex items-center gap-2 text-xs font-bold text-coast-deep hover:text-coast-blue transition"
                    onClick={() => setShowAdvancedCoords(!showAdvancedCoords)}
                  >
                    <span>{showAdvancedCoords ? '▼ Hide' : '▶ Show'} Advanced Geographic Coordinates</span>
                    <span className="rounded-full bg-coast-sand px-2 py-0.5 text-[10px] text-coast-muted">
                      {nearLat}°N, {nearLon}°E
                    </span>
                  </button>

                  {showAdvancedCoords && (
                    <div className="mt-3 grid gap-4 rounded-2xl border border-coast-line/70 bg-white/60 p-4 sm:grid-cols-2">
                      <div>
                        <label className="block text-xs font-extrabold text-coast-deep" htmlFor="near-lat">
                          LATITUDE (DEGREES)
                        </label>
                        <input
                          className="mt-2 w-full rounded-2xl border border-coast-line bg-white px-4 py-2.5 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                          id="near-lat"
                          onChange={(e) => setNearLat(e.target.value)}
                          placeholder="e.g. 5.9485"
                          step="0.0001"
                          type="number"
                          value={nearLat}
                        />
                      </div>

                      <div>
                        <label className="block text-xs font-extrabold text-coast-deep" htmlFor="near-lon">
                          LONGITUDE (DEGREES)
                        </label>
                        <input
                          className="mt-2 w-full rounded-2xl border border-coast-line bg-white px-4 py-2.5 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                          id="near-lon"
                          onChange={(e) => setNearLon(e.target.value)}
                          placeholder="e.g. 80.4578"
                          step="0.0001"
                          type="number"
                          value={nearLon}
                        />
                      </div>
                    </div>
                  )}
                </div>

                <div>
                  <button
                    className="inline-flex min-h-11 items-center justify-center rounded-full bg-coast-deep px-6 text-sm font-extrabold text-white transition hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue"
                    disabled={nearbyLoading}
                    type="submit"
                  >
                    {nearbyLoading ? 'Finding Coastal Destinations...' : 'Find Coastal Destinations'}
                  </button>
                </div>
              </form>
            </div>

            {/* Results */}
            {nearbySearched && (
              <div className="mt-8">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <h3 className="font-display text-2xl font-bold tracking-tight text-coast-ink">
                      Proximity Results ({nearbyResults.length})
                    </h3>
                    <p className="mt-1 text-xs text-coast-muted">
                      {resolvedLocationName ? (
                        <span>
                          Showing coastal spots within <strong className="text-coast-deep">{nearRadiusKm} km</strong> of{' '}
                          <strong className="text-coast-deep">{resolvedLocationName}</strong> ({nearLat}°N, {nearLon}°E)
                        </span>
                      ) : (
                        <span>
                          Showing coastal spots within <strong className="text-coast-deep">{nearRadiusKm} km</strong> of {nearLat}°N, {nearLon}°E
                        </span>
                      )}
                    </p>
                  </div>
                </div>

                {nearbyResults.length === 0 ? (
                  <p className="mt-4 rounded-3xl border border-coast-line bg-coast-pearl p-6 text-coast-muted">
                    No published destinations found within {nearRadiusKm} km of {resolvedLocationName || `${nearLat}°N, ${nearLon}°E`}.
                  </p>
                ) : (
                  <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                    {nearbyResults.map((item, idx) => {
                      const destId = item.destinationId || (item as any).destination?.id || `nearby-${idx}`
                      const destName = item.name || (item as any).destination?.name || 'Coastal Destination'
                      const destRegion = item.region || (item as any).destination?.region || 'Coastal Sri Lanka'
                      const destDesc = item.description || (item as any).destination?.description || ''

                      return (
                        <article className="rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-sm" key={destId}>
                          <div className="flex items-center justify-between gap-2">
                            <span className="rounded-full bg-coast-sage px-3 py-1 text-xs font-extrabold text-coast-deep">
                              {(item.distanceMeters / 1000).toFixed(1)} km away
                            </span>
                            <span className="text-xs font-bold text-coast-muted">{item.activeOfferingsCount} offerings</span>
                          </div>
                          <h4 className="mt-3 font-display text-xl font-bold text-coast-ink">{destName}</h4>
                          <p className="mt-1 text-xs font-semibold text-coast-muted">{destRegion}</p>
                          {destDesc && <p className="mt-2 line-clamp-2 text-sm text-coast-muted">{destDesc}</p>}
                          <Link
                            className="mt-4 inline-flex min-h-10 w-full items-center justify-center rounded-full bg-coast-deep px-4 text-xs font-extrabold text-white transition hover:bg-coast-blue"
                            to={`/experiences/destinations/${destId}`}
                          >
                            View Details & Ecology →
                          </Link>
                        </article>
                      )
                    })}
                  </div>
                )}
              </div>
            )}
          </div>
        )}

        {/* View 3: Interactive Coastal Map */}
        {activeTab === 'map' && (
          <div className="mx-auto max-w-7xl px-5 py-8 sm:px-8 lg:px-12">
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
                <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-sm">
                  <h3 className="text-sm font-extrabold tracking-wide text-coast-deep">
                    FIND COASTAL SPOTS & BAYS
                  </h3>
                  <p className="mt-1 text-xs text-coast-muted">
                    Search for beaches, coral reefs, bays, and seaside towns.
                  </p>

                  <form className="mt-4 flex gap-2" onSubmit={handlePlaceSearch}>
                    <input
                      className="flex-1 rounded-2xl border border-coast-line bg-white px-4 py-2.5 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                      onChange={(e) => setPlaceQuery(e.target.value)}
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
                                onClick={() => {
                                  setSelectedSpot({
                                    name: p.displayName,
                                    region: p.region || p.country || 'Sri Lanka',
                                    lat: p.latitude,
                                    lon: p.longitude,
                                    isSearchResult: true,
                                  })
                                }}
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
                    <p className="mt-1 text-xs font-semibold text-coast-muted">
                      Coordinates: {selectedSpot.lat.toFixed(4)}°N, {selectedSpot.lon.toFixed(4)}°E
                    </p>

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
                          setNearLat(selectedSpot.lat.toFixed(4))
                          setNearLon(selectedSpot.lon.toFixed(4))
                          setActiveTab('nearby')
                        }}
                      >
                        Find Experiences Nearby
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
              </div>

              {/* Right Column: Real Geographic Map of Sri Lanka */}
              <div className="flex flex-col lg:col-span-7">
                <div className="relative flex flex-col h-[580px] sm:h-[660px] w-full overflow-hidden rounded-3xl border border-coast-line bg-[#e8f3f6] shadow-sm">
                  {/* Map Header / Region Selector Bar */}
                  <div className="flex flex-wrap items-center justify-between gap-2 border-b border-coast-line/70 bg-white/95 px-4 py-2.5 backdrop-blur-sm z-10">
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
                        onClick={() => setSelectedSpot(null)}
                      >
                        Reset to Island View ⟲
                      </button>
                    )}
                  </div>

                  {/* Real Geographic Map of Sri Lanka (OpenStreetMap Cartography) */}
                  <div className="relative flex-1 w-full bg-[#cad2d3]">
                    <iframe
                      title="Interactive Coastal Map of Sri Lanka"
                      src={osmEmbedUrl}
                      className="h-full w-full border-0"
                      loading="lazy"
                    />

                    {/* Surrounding Marine Waters Indicators */}
                    <div className="pointer-events-none absolute bottom-3 left-4 z-10 flex flex-wrap items-center gap-2 rounded-xl bg-white/90 px-3 py-1.5 text-[11px] font-bold text-coast-deep backdrop-blur-sm border border-coast-line/70 shadow-sm">
                      <span className="text-coast-blue">🌊</span>
                      <span>INDIAN OCEAN (South & West)</span>
                      <span className="text-coast-line">•</span>
                      <span>BAY OF BENGAL (East)</span>
                      <span className="text-coast-line">•</span>
                      <span>GULF OF MANNAR (Northwest)</span>
                    </div>

                    {/* External Map Link & Attribution */}
                    <div className="absolute bottom-3 right-4 z-10 flex items-center gap-2">
                      <a
                        href={osmViewUrl}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="rounded-full bg-white/90 px-3 py-1 text-[11px] font-extrabold text-coast-deep backdrop-blur-sm border border-coast-line/70 shadow-sm hover:bg-white transition"
                      >
                        Open on OpenStreetMap ↗
                      </a>
                      <div className="hidden md:block rounded-full bg-white/85 px-3 py-1 text-[10px] font-semibold text-coast-muted backdrop-blur-sm border border-coast-line/60 shadow-sm">
                        {mapConfig?.attribution || 'Map data © OpenStreetMap contributors'}
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}
      </main>

      {/* --- Destination Modal (Create / Edit) --- */}
      {destModalOpen && (
        <div
          aria-labelledby="dest-modal-title"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center bg-coast-deep/60 p-4 backdrop-blur-sm"
          role="dialog"
        >
          <div className="w-full max-w-lg rounded-3xl border border-coast-line bg-white p-6 shadow-2xl">
            <div className="flex items-center justify-between border-b border-coast-line pb-4">
              <h3 className="font-display text-xl font-bold text-coast-ink" id="dest-modal-title">
                {destModalMode === 'create' ? 'Add New Coastal Destination' : 'Edit Coastal Destination'}
              </h3>
              <button
                aria-label="Close modal"
                className="rounded-full p-1 text-coast-muted hover:bg-coast-sand hover:text-coast-ink"
                onClick={() => setDestModalOpen(false)}
                type="button"
              >
                ✕
              </button>
            </div>

            <form className="mt-4 space-y-4" onSubmit={handleSaveDestination}>
              <div>
                <label className="block text-xs font-extrabold text-coast-deep" htmlFor="modal-dest-name">
                  DESTINATION NAME *
                </label>
                <input
                  className="mt-1 w-full rounded-2xl border border-coast-line bg-coast-paper px-4 py-2 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                  id="modal-dest-name"
                  onChange={(e) => setDestFormName(e.target.value)}
                  placeholder="e.g. Mirissa Coastal Haven"
                  required
                  type="text"
                  value={destFormName}
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-extrabold text-coast-deep" htmlFor="modal-dest-slug">
                    URL SLUG
                  </label>
                  <input
                    className="mt-1 w-full rounded-2xl border border-coast-line bg-coast-paper px-4 py-2 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                    id="modal-dest-slug"
                    onChange={(e) => setDestFormSlug(e.target.value)}
                    placeholder="e.g. mirissa-haven"
                    type="text"
                    value={destFormSlug}
                  />
                </div>
                <div>
                  <label className="block text-xs font-extrabold text-coast-deep" htmlFor="modal-dest-region">
                    COASTAL REGION
                  </label>
                  <input
                    className="mt-1 w-full rounded-2xl border border-coast-line bg-coast-paper px-4 py-2 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                    id="modal-dest-region"
                    onChange={(e) => setDestFormRegion(e.target.value)}
                    placeholder="e.g. Southern Province"
                    type="text"
                    value={destFormRegion}
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-extrabold text-coast-deep" htmlFor="modal-dest-lat">
                    LATITUDE *
                  </label>
                  <input
                    className="mt-1 w-full rounded-2xl border border-coast-line bg-coast-paper px-4 py-2 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                    id="modal-dest-lat"
                    onChange={(e) => setDestFormLat(e.target.value)}
                    placeholder="5.9485"
                    required
                    step="any"
                    type="number"
                    value={destFormLat}
                  />
                </div>
                <div>
                  <label className="block text-xs font-extrabold text-coast-deep" htmlFor="modal-dest-lon">
                    LONGITUDE *
                  </label>
                  <input
                    className="mt-1 w-full rounded-2xl border border-coast-line bg-coast-paper px-4 py-2 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                    id="modal-dest-lon"
                    onChange={(e) => setDestFormLon(e.target.value)}
                    placeholder="80.4578"
                    required
                    step="any"
                    type="number"
                    value={destFormLon}
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-extrabold text-coast-deep" htmlFor="modal-dest-desc">
                  DESCRIPTION & ECOLOGY
                </label>
                <textarea
                  className="mt-1 w-full rounded-2xl border border-coast-line bg-coast-paper px-4 py-2 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                  id="modal-dest-desc"
                  onChange={(e) => setDestFormDesc(e.target.value)}
                  placeholder="Describe marine habitat, coastal features, and biodiversity context..."
                  rows={3}
                  value={destFormDesc}
                />
              </div>

              <div className="mt-6 flex justify-end gap-3 pt-3 border-t border-coast-line">
                <button
                  className="rounded-full border border-coast-line px-5 py-2 text-xs font-bold text-coast-muted transition hover:bg-coast-sand"
                  onClick={() => setDestModalOpen(false)}
                  type="button"
                >
                  Cancel
                </button>
                <button
                  className="rounded-full bg-coast-deep px-5 py-2 text-xs font-extrabold text-white transition hover:bg-coast-blue disabled:opacity-50"
                  disabled={destSubmitting}
                  type="submit"
                >
                  {destSubmitting ? 'Saving...' : destModalMode === 'create' ? 'Create Destination' : 'Update Destination'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* --- Offering Modal (Create / Edit) --- */}
      {offModalOpen && (
        <div
          aria-labelledby="off-modal-title"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center bg-coast-deep/60 p-4 backdrop-blur-sm"
          role="dialog"
        >
          <div className="w-full max-w-lg rounded-3xl border border-coast-line bg-white p-6 shadow-2xl">
            <div className="flex items-center justify-between border-b border-coast-line pb-4">
              <h3 className="font-display text-xl font-bold text-coast-ink" id="off-modal-title">
                {offModalMode === 'create' ? 'Add New Experience Offering' : 'Edit Experience Offering'}
              </h3>
              <button
                aria-label="Close modal"
                className="rounded-full p-1 text-coast-muted hover:bg-coast-sand hover:text-coast-ink"
                onClick={() => setOffModalOpen(false)}
                type="button"
              >
                ✕
              </button>
            </div>

            <form className="mt-4 space-y-4" onSubmit={handleSaveOffering}>
              {offModalMode === 'create' && (
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="modal-off-dest">
                      DESTINATION *
                    </label>
                    <select
                      className="mt-1 w-full rounded-2xl border border-coast-line bg-coast-paper px-4 py-2 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                      id="modal-off-dest"
                      onChange={(e) => setOffFormDestId(e.target.value)}
                      required
                      value={offFormDestId}
                    >
                      <option value="">Select Destination</option>
                      {destinations.map((d) => (
                        <option key={d.id} value={d.id}>
                          {d.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="modal-off-act">
                      ACTIVITY TYPE *
                    </label>
                    <select
                      className="mt-1 w-full rounded-2xl border border-coast-line bg-coast-paper px-4 py-2 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                      id="modal-off-act"
                      onChange={(e) => setOffFormActId(e.target.value)}
                      required
                      value={offFormActId}
                    >
                      <option value="">Select Activity</option>
                      {activities.map((a) => (
                        <option key={a.id} value={a.id}>
                          {a.name} ({a.code})
                        </option>
                      ))}
                    </select>
                  </div>
                </div>
              )}

              <div>
                <label className="block text-xs font-extrabold text-coast-deep" htmlFor="modal-off-title">
                  OFFERING TITLE *
                </label>
                <input
                  className="mt-1 w-full rounded-2xl border border-coast-line bg-coast-paper px-4 py-2 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                  id="modal-off-title"
                  onChange={(e) => setOffFormTitle(e.target.value)}
                  placeholder="e.g. Guided Blue Whale Safari"
                  required
                  type="text"
                  value={offFormTitle}
                />
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="block text-xs font-extrabold text-coast-deep" htmlFor="modal-off-price">
                    PRICE (LKR)
                  </label>
                  <input
                    className="mt-1 w-full rounded-2xl border border-coast-line bg-coast-paper px-4 py-2 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                    id="modal-off-price"
                    onChange={(e) => setOffFormPrice(e.target.value)}
                    placeholder="5000"
                    type="number"
                    value={offFormPrice}
                  />
                </div>
                <div>
                  <label className="block text-xs font-extrabold text-coast-deep" htmlFor="modal-off-duration">
                    DURATION (MINS)
                  </label>
                  <input
                    className="mt-1 w-full rounded-2xl border border-coast-line bg-coast-paper px-4 py-2 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                    id="modal-off-duration"
                    onChange={(e) => setOffFormDuration(e.target.value)}
                    placeholder="120"
                    type="number"
                    value={offFormDuration}
                  />
                </div>
                <div>
                  <label className="block text-xs font-extrabold text-coast-deep" htmlFor="modal-off-cap">
                    MAX GUESTS
                  </label>
                  <input
                    className="mt-1 w-full rounded-2xl border border-coast-line bg-coast-paper px-4 py-2 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                    id="modal-off-cap"
                    onChange={(e) => setOffFormCapacity(e.target.value)}
                    placeholder="8"
                    type="number"
                    value={offFormCapacity}
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-extrabold text-coast-deep" htmlFor="modal-off-desc">
                  DESCRIPTION & INCLUSIONS
                </label>
                <textarea
                  className="mt-1 w-full rounded-2xl border border-coast-line bg-coast-paper px-4 py-2 text-sm text-coast-ink focus:border-coast-blue focus:outline-none focus:ring-2 focus:ring-coast-glass"
                  id="modal-off-desc"
                  onChange={(e) => setOffFormDesc(e.target.value)}
                  placeholder="Detail what is included, safety gear, certified guides, eco-rules..."
                  rows={3}
                  value={offFormDesc}
                />
              </div>

              <div className="mt-6 flex justify-end gap-3 pt-3 border-t border-coast-line">
                <button
                  className="rounded-full border border-coast-line px-5 py-2 text-xs font-bold text-coast-muted transition hover:bg-coast-sand"
                  onClick={() => setOffModalOpen(false)}
                  type="button"
                >
                  Cancel
                </button>
                <button
                  className="rounded-full bg-coast-deep px-5 py-2 text-xs font-extrabold text-white transition hover:bg-coast-blue disabled:opacity-50"
                  disabled={offSubmitting}
                  type="submit"
                >
                  {offSubmitting ? 'Saving...' : offModalMode === 'create' ? 'Create Offering' : 'Update Offering'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* --- Delete Confirmation Dialog --- */}
      {deleteDialog && (
        <div
          aria-labelledby="delete-dialog-title"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center bg-coast-deep/60 p-4 backdrop-blur-sm"
          role="dialog"
        >
          <div className="w-full max-w-md rounded-3xl border border-red-200 bg-white p-6 shadow-2xl">
            <h3 className="font-display text-xl font-bold text-red-900" id="delete-dialog-title">
              Confirm Permanent Deletion
            </h3>
            <p className="mt-3 text-sm text-coast-muted leading-relaxed">
              Are you sure you want to permanently delete the {deleteDialog.type}{' '}
              <strong className="text-coast-ink font-semibold">"{deleteDialog.title}"</strong>?
              {deleteDialog.type === 'destination' && (
                <span className="block mt-1 text-xs text-red-600 font-medium">
                  Note: Any offerings linked to this destination will also be removed.
                </span>
              )}
            </p>

            <div className="mt-6 flex justify-end gap-3 pt-3 border-t border-coast-line">
              <button
                className="rounded-full border border-coast-line px-5 py-2 text-xs font-bold text-coast-muted transition hover:bg-coast-sand"
                onClick={() => setDeleteDialog(null)}
                type="button"
              >
                Cancel
              </button>
              <button
                className="rounded-full bg-red-600 px-5 py-2 text-xs font-extrabold text-white transition hover:bg-red-700 disabled:opacity-50"
                disabled={deleteSubmitting}
                onClick={handleExecuteDelete}
                type="button"
              >
                {deleteSubmitting ? 'Deleting...' : 'Yes, Delete Permanently'}
              </button>
            </div>
          </div>
        </div>
      )}

      <SiteFooter />
    </div>
  )
}
