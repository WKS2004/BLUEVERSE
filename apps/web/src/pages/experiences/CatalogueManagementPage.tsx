import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import SiteFooter from '../../components/layout/SiteFooter'
import SiteHeader from '../../components/layout/SiteHeader'
import type {
  ActivityDto,
  AgentContextResponseDto,
  DependenciesStatusResponseDto,
  DestinationDto,
  OfferingDto,
  PublicationEvaluationResponse,
  ScheduleDto,
} from '../../features/experiences/experienceApi'
import {
  addOfferingSchedule,
  createActivity,
  createDestination,
  createOffering,
  deleteOfferingSchedule,
  evaluateActivityPublication,
  evaluateDestinationPublication,
  evaluateOfferingPublication,
  getActivities,
  getAgentContext,
  getDependenciesStatus,
  getDestinations,
  getOfferings,
  getOfferingSchedules,
  updateActivityPublication,
  updateDestinationPublication,
  updateOfferingPublication,
} from '../../features/experiences/experienceApi'

export default function CatalogueManagementPage() {
  const [activeTab, setActiveTab] = useState<'destinations' | 'activities' | 'offerings' | 'diagnostics'>('destinations')

  // Destinations state
  const [destinations, setDestinations] = useState<DestinationDto[]>([])
  const [newDestName, setNewDestName] = useState('')
  const [newDestSlug, setNewDestSlug] = useState('')
  const [newDestRegion, setNewDestRegion] = useState('')
  const [newDestDesc, setNewDestDesc] = useState('')
  const [newDestLat, setNewDestLat] = useState('5.9485')
  const [newDestLon, setNewDestLon] = useState('80.4578')

  // Activities state
  const [activities, setActivities] = useState<ActivityDto[]>([])
  const [newActCode, setNewActCode] = useState('')
  const [newActName, setNewActName] = useState('')
  const [newActCategory, setNewActCategory] = useState('')
  const [newActDesc, setNewActDesc] = useState('')

  // Offerings state
  const [offerings, setOfferings] = useState<OfferingDto[]>([])
  const [newOffDestId, setNewOffDestId] = useState('')
  const [newOffActId, setNewOffActId] = useState('')
  const [newOffTitle, setNewOffTitle] = useState('')
  const [newOffPrice, setNewOffPrice] = useState('5000')
  const [newOffDuration, setNewOffDuration] = useState('120')
  const [newOffCapacity, setNewOffCapacity] = useState('8')
  const [newOffDesc, setNewOffDesc] = useState('')

  // Schedules state
  const [selectedOfferingForSched, setSelectedOfferingForSched] = useState<string>('')
  const [schedules, setSchedules] = useState<ScheduleDto[]>([])
  const [newSchedStart, setNewSchedStart] = useState('')
  const [newSchedEnd, setNewSchedEnd] = useState('')

  // Diagnostics & Seam
  const [diagStatus, setDiagStatus] = useState<DependenciesStatusResponseDto | null>(null)
  const [agentSeam, setAgentSeam] = useState<AgentContextResponseDto | null>(null)

  // Evaluation & Action Feedback
  const [evalResult, setEvalResult] = useState<PublicationEvaluationResponse | null>(null)
  const [statusMessage, setStatusMessage] = useState<string | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    loadAll()
  }, [])

  async function loadAll() {
    setLoading(true)
    try {
      const [dRes, aRes, oRes] = await Promise.all([
        getDestinations({ pageSize: 50 }),
        getActivities({ pageSize: 50 }),
        getOfferings({ pageSize: 50 }),
      ])
      setDestinations(dRes.items)
      setActivities(aRes.items)
      setOfferings(oRes.items)
      if (dRes.items.length > 0 && !newOffDestId) setNewOffDestId(dRes.items[0].id)
      if (aRes.items.length > 0 && !newOffActId) setNewOffActId(aRes.items[0].id)
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Error loading catalogue data.')
    } finally {
      setLoading(false)
    }
  }

  // Load schedules when an offering is selected
  useEffect(() => {
    if (!selectedOfferingForSched) return
    getOfferingSchedules(selectedOfferingForSched)
      .then((s) => setSchedules(s))
      .catch(() => setSchedules([]))
  }, [selectedOfferingForSched])

  // Destination Creation
  async function handleCreateDestination(e: React.FormEvent) {
    e.preventDefault()
    setErrorMessage(null)
    setStatusMessage(null)
    try {
      const created = await createDestination({
        name: newDestName.trim(),
        slug: newDestSlug.trim() || undefined,
        description: newDestDesc.trim() || undefined,
        region: newDestRegion.trim() || undefined,
        latitude: parseFloat(newDestLat),
        longitude: parseFloat(newDestLon),
      })
      setDestinations((prev) => [created, ...prev])
      setStatusMessage(`Destination "${created.name}" created in DRAFT status!`)
      setNewDestName('')
      setNewDestSlug('')
      setNewDestDesc('')
      setNewDestRegion('')
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Destination creation failed.')
    }
  }

  // Activity Creation
  async function handleCreateActivity(e: React.FormEvent) {
    e.preventDefault()
    setErrorMessage(null)
    setStatusMessage(null)
    try {
      const created = await createActivity({
        code: newActCode.trim().toUpperCase(),
        name: newActName.trim(),
        description: newActDesc.trim() || undefined,
        category: newActCategory.trim() || undefined,
      })
      setActivities((prev) => [created, ...prev])
      setStatusMessage(`Activity "${created.name}" created in DRAFT status!`)
      setNewActCode('')
      setNewActName('')
      setNewActDesc('')
      setNewActCategory('')
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Activity creation failed.')
    }
  }

  // Offering Creation
  async function handleCreateOffering(e: React.FormEvent) {
    e.preventDefault()
    setErrorMessage(null)
    setStatusMessage(null)
    try {
      const created = await createOffering({
        destinationId: newOffDestId,
        activityId: newOffActId,
        title: newOffTitle.trim(),
        description: newOffDesc.trim() || undefined,
        price: parseFloat(newOffPrice) || 0,
        currency: 'LKR',
        durationMinutes: parseInt(newOffDuration, 10) || 60,
        maxCapacity: parseInt(newOffCapacity, 10) || 10,
      })
      setOfferings((prev) => [created, ...prev])
      setStatusMessage(`Offering "${created.title}" created in DRAFT status!`)
      setNewOffTitle('')
      setNewOffDesc('')
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Offering creation failed.')
    }
  }

  // Pre-flight publication evaluations
  async function handleEvaluateDestination(id: string, targetStatus: string) {
    setEvalResult(null)
    try {
      const res = await evaluateDestinationPublication(id, targetStatus)
      setEvalResult(res)
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Evaluation check failed.')
    }
  }

  async function handleTransitionDestination(id: string, targetStatus: string) {
    setErrorMessage(null)
    setStatusMessage(null)
    try {
      const updated = await updateDestinationPublication(id, targetStatus)
      setDestinations((prev) => prev.map((d) => (d.id === updated.id ? updated : d)))
      setStatusMessage(`Destination publication updated to ${updated.status}`)
      setEvalResult(null)
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Transition blocked by business rules.')
    }
  }

  async function handleEvaluateActivity(id: string, targetStatus: string) {
    setEvalResult(null)
    try {
      const res = await evaluateActivityPublication(id, targetStatus)
      setEvalResult(res)
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Evaluation check failed.')
    }
  }

  async function handleTransitionActivity(id: string, targetStatus: string) {
    setErrorMessage(null)
    setStatusMessage(null)
    try {
      const updated = await updateActivityPublication(id, targetStatus)
      setActivities((prev) => prev.map((a) => (a.id === updated.id ? updated : a)))
      setStatusMessage(`Activity publication updated to ${updated.status}`)
      setEvalResult(null)
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Transition blocked.')
    }
  }

  async function handleEvaluateOffering(id: string, targetStatus: string) {
    setEvalResult(null)
    try {
      const res = await evaluateOfferingPublication(id, targetStatus)
      setEvalResult(res)
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Evaluation check failed.')
    }
  }

  async function handleTransitionOffering(id: string, targetStatus: string) {
    setErrorMessage(null)
    setStatusMessage(null)
    try {
      const updated = await updateOfferingPublication(id, targetStatus)
      setOfferings((prev) => prev.map((o) => (o.id === updated.id ? updated : o)))
      setStatusMessage(`Offering publication updated to ${updated.status}`)
      setEvalResult(null)
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Transition blocked.')
    }
  }

  // Schedules management
  async function handleAddSchedule(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedOfferingForSched || !newSchedStart || !newSchedEnd) return
    try {
      const added = await addOfferingSchedule(selectedOfferingForSched, {
        startsAt: new Date(newSchedStart).toISOString(),
        endsAt: new Date(newSchedEnd).toISOString(),
        timeZoneId: 'Asia/Colombo',
        isActive: true,
      })
      setSchedules((prev) => [...prev, added])
      setStatusMessage('Schedule slot added successfully.')
      setNewSchedStart('')
      setNewSchedEnd('')
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Failed to add schedule.')
    }
  }

  async function handleDeleteSchedule(scheduleId: string) {
    if (!selectedOfferingForSched) return
    try {
      await deleteOfferingSchedule(selectedOfferingForSched, scheduleId)
      setSchedules((prev) => prev.filter((s) => s.id !== scheduleId))
      setStatusMessage('Schedule slot removed.')
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Failed to remove schedule slot.')
    }
  }

  // Load diagnostics
  async function handleLoadDiagnostics() {
    setLoading(true)
    try {
      const [diag, seam] = await Promise.all([getDependenciesStatus(), getAgentContext()])
      setDiagStatus(diag)
      setAgentSeam(seam)
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Failed to load diagnostics.')
    } finally {
      setLoading(false)
    }
  }

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

        <section aria-labelledby="mgmt-title" className="mx-auto max-w-7xl px-5 py-8 sm:px-8 lg:px-12">
          <div>
            <p className="text-xs font-extrabold tracking-[0.16em] text-coast-blue">AUTHORITATIVE CATALOGUE</p>
            <h1 className="mt-2 font-display text-4xl font-bold tracking-tight text-coast-ink sm:text-5xl" id="mgmt-title">
              Catalogue Management Workspace
            </h1>
            <p className="mt-2 text-sm leading-6 text-coast-muted">
              Maintain coastal destinations, activities, participatory offerings, schedules, and publication lifecycle with pre-flight evaluation.
            </p>
          </div>

          {/* Feedback Banners */}
          {statusMessage && (
            <div aria-live="polite" className="mt-4 rounded-2xl border border-coast-teal/30 bg-coast-sage p-4 text-xs font-semibold text-coast-deep">
              {statusMessage}
            </div>
          )}

          {errorMessage && (
            <div role="alert" className="mt-4 rounded-2xl border border-red-200 bg-red-50 p-4 text-xs font-semibold text-red-900">
              {errorMessage}
            </div>
          )}

          {loading && (
            <p className="mt-4 text-xs font-semibold text-coast-muted">Refreshing coastal management catalog...</p>
          )}

          {/* Pre-flight Evaluation Result Modal / Card */}
          {evalResult && (
            <div className="mt-6 rounded-3xl border border-coast-line bg-white p-6 shadow-md">
              <div className="flex items-center justify-between">
                <span className="text-xs font-extrabold tracking-wide text-coast-blue">PRE-FLIGHT PUBLICATION AUDIT</span>
                <span
                  className={`rounded-full px-3 py-1 text-xs font-extrabold ${
                    evalResult.canTransition ? 'bg-emerald-100 text-emerald-800' : 'bg-red-100 text-red-800'
                  }`}
                >
                  {evalResult.canTransition ? 'TRANSITION ELIGIBLE' : 'TRANSITION BLOCKED'}
                </span>
              </div>
              <p className="mt-2 text-xs text-coast-muted">
                Target: {evalResult.targetType} ({evalResult.targetId}) · Requested Status: {evalResult.requestedStatus}
              </p>
              {evalResult.reasons && evalResult.reasons.length > 0 && (
                <ul className="mt-3 list-disc pl-5 text-xs text-coast-ink space-y-1">
                  {evalResult.reasons.map((r, i) => (
                    <li key={i}>{r}</li>
                  ))}
                </ul>
              )}
            </div>
          )}

          {/* Management Subtabs */}
          <div className="mt-8 flex border-b border-coast-line">
            <button
              className={`border-b-2 px-5 py-3 text-sm font-extrabold transition ${
                activeTab === 'destinations' ? 'border-coast-deep text-coast-deep' : 'border-transparent text-coast-muted hover:text-coast-deep'
              }`}
              onClick={() => setActiveTab('destinations')}
              type="button"
            >
              Destinations ({destinations.length})
            </button>
            <button
              className={`border-b-2 px-5 py-3 text-sm font-extrabold transition ${
                activeTab === 'activities' ? 'border-coast-deep text-coast-deep' : 'border-transparent text-coast-muted hover:text-coast-deep'
              }`}
              onClick={() => setActiveTab('activities')}
              type="button"
            >
              Activities ({activities.length})
            </button>
            <button
              className={`border-b-2 px-5 py-3 text-sm font-extrabold transition ${
                activeTab === 'offerings' ? 'border-coast-deep text-coast-deep' : 'border-transparent text-coast-muted hover:text-coast-deep'
              }`}
              onClick={() => setActiveTab('offerings')}
              type="button"
            >
              Offerings & Schedules ({offerings.length})
            </button>
            <button
              className={`border-b-2 px-5 py-3 text-sm font-extrabold transition ${
                activeTab === 'diagnostics' ? 'border-coast-deep text-coast-deep' : 'border-transparent text-coast-muted hover:text-coast-deep'
              }`}
              onClick={() => {
                setActiveTab('diagnostics')
                handleLoadDiagnostics()
              }}
              type="button"
            >
              Dependencies & Agent Seam
            </button>
          </div>

          {/* 1. Destinations Panel */}
          {activeTab === 'destinations' && (
            <div className="mt-8 space-y-8">
              {/* Destination Form */}
              <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-sm sm:p-8">
                <h3 className="font-display text-xl font-bold text-coast-ink">Register New Coastal Destination</h3>
                <form className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3" onSubmit={handleCreateDestination}>
                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="d-name">
                      DESTINATION NAME
                    </label>
                    <input
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="d-name"
                      onChange={(e) => setNewDestName(e.target.value)}
                      placeholder="e.g. Mirissa Bay"
                      required
                      value={newDestName}
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="d-slug">
                      SLUG (OPTIONAL)
                    </label>
                    <input
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="d-slug"
                      onChange={(e) => setNewDestSlug(e.target.value)}
                      placeholder="e.g. mirissa-bay"
                      value={newDestSlug}
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="d-region">
                      REGION
                    </label>
                    <input
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="d-region"
                      onChange={(e) => setNewDestRegion(e.target.value)}
                      placeholder="e.g. Southern Province"
                      value={newDestRegion}
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="d-lat">
                      LATITUDE
                    </label>
                    <input
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="d-lat"
                      onChange={(e) => setNewDestLat(e.target.value)}
                      required
                      step="0.0001"
                      type="number"
                      value={newDestLat}
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="d-lon">
                      LONGITUDE
                    </label>
                    <input
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="d-lon"
                      onChange={(e) => setNewDestLon(e.target.value)}
                      required
                      step="0.0001"
                      type="number"
                      value={newDestLon}
                    />
                  </div>

                  <div className="sm:col-span-2 lg:col-span-3">
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="d-desc">
                      DESCRIPTION
                    </label>
                    <textarea
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="d-desc"
                      onChange={(e) => setNewDestDesc(e.target.value)}
                      placeholder="Describe coastal sanctuary, geographic features, access points..."
                      rows={2}
                      value={newDestDesc}
                    />
                  </div>

                  <div className="sm:col-span-2 lg:col-span-3">
                    <button
                      className="inline-flex min-h-11 items-center justify-center rounded-full bg-coast-deep px-6 text-xs font-extrabold text-white transition hover:bg-coast-blue"
                      type="submit"
                    >
                      Save Destination (DRAFT)
                    </button>
                  </div>
                </form>
              </div>

              {/* Destinations List */}
              <div className="rounded-3xl border border-coast-line bg-white p-6 shadow-sm">
                <h3 className="font-display text-xl font-bold text-coast-ink">Managed Destinations</h3>
                <div className="mt-4 divide-y divide-coast-line">
                  {destinations.map((d) => (
                    <div className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center sm:justify-between" key={d.id}>
                      <div>
                        <div className="flex items-center gap-2">
                          <h4 className="text-sm font-extrabold text-coast-ink">{d.name}</h4>
                          <span
                            className={`rounded-full px-2 py-0.5 text-[10px] font-extrabold ${
                              d.status === 'PUBLISHED'
                                ? 'bg-emerald-100 text-emerald-800'
                                : d.status === 'DRAFT'
                                ? 'bg-amber-100 text-amber-800'
                                : 'bg-gray-100 text-gray-700'
                            }`}
                          >
                            {d.status}
                          </span>
                        </div>
                        <p className="mt-1 text-xs text-coast-muted">
                          {d.region || 'Coastal'} · {d.latitude.toFixed(4)}°N, {d.longitude.toFixed(4)}°E
                        </p>
                      </div>

                      <div className="flex flex-wrap items-center gap-2">
                        <button
                          className="rounded-full border border-coast-line px-3 py-1.5 text-[11px] font-bold text-coast-deep hover:bg-coast-sand"
                          onClick={() => handleEvaluateDestination(d.id, 'PUBLISHED')}
                          type="button"
                        >
                          Audit Publish
                        </button>
                        {d.status !== 'PUBLISHED' && (
                          <button
                            className="rounded-full bg-coast-deep px-3 py-1.5 text-[11px] font-bold text-white hover:bg-coast-blue"
                            onClick={() => handleTransitionDestination(d.id, 'PUBLISHED')}
                            type="button"
                          >
                            Publish
                          </button>
                        )}
                        {d.status === 'PUBLISHED' && (
                          <button
                            className="rounded-full border border-coast-line px-3 py-1.5 text-[11px] font-bold text-coast-muted hover:bg-coast-sand"
                            onClick={() => handleTransitionDestination(d.id, 'DRAFT')}
                            type="button"
                          >
                            Unpublish
                          </button>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          )}

          {/* 2. Activities Panel */}
          {activeTab === 'activities' && (
            <div className="mt-8 space-y-8">
              {/* Activity Form */}
              <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-sm sm:p-8">
                <h3 className="font-display text-xl font-bold text-coast-ink">Register New Coastal Activity Category</h3>
                <form className="mt-6 grid gap-4 sm:grid-cols-3" onSubmit={handleCreateActivity}>
                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="a-code">
                      ACTIVITY CODE (UNIQUE)
                    </label>
                    <input
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="a-code"
                      onChange={(e) => setNewActCode(e.target.value)}
                      placeholder="e.g. SNORKELING"
                      required
                      value={newActCode}
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="a-name">
                      ACTIVITY NAME
                    </label>
                    <input
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="a-name"
                      onChange={(e) => setNewActName(e.target.value)}
                      placeholder="e.g. Snorkeling Tour"
                      required
                      value={newActName}
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="a-cat">
                      CATEGORY
                    </label>
                    <input
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="a-cat"
                      onChange={(e) => setNewActCategory(e.target.value)}
                      placeholder="e.g. Marine Wildlife"
                      value={newActCategory}
                    />
                  </div>

                  <div className="sm:col-span-3">
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="a-desc">
                      DESCRIPTION
                    </label>
                    <textarea
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="a-desc"
                      onChange={(e) => setNewActDesc(e.target.value)}
                      placeholder="Requirements, safety expectations, duration..."
                      rows={2}
                      value={newActDesc}
                    />
                  </div>

                  <div className="sm:col-span-3">
                    <button
                      className="inline-flex min-h-11 items-center justify-center rounded-full bg-coast-deep px-6 text-xs font-extrabold text-white transition hover:bg-coast-blue"
                      type="submit"
                    >
                      Save Activity (DRAFT)
                    </button>
                  </div>
                </form>
              </div>

              {/* Activities List */}
              <div className="rounded-3xl border border-coast-line bg-white p-6 shadow-sm">
                <h3 className="font-display text-xl font-bold text-coast-ink">Managed Activity Taxonomy</h3>
                <div className="mt-4 divide-y divide-coast-line">
                  {activities.map((a) => (
                    <div className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center sm:justify-between" key={a.id}>
                      <div>
                        <div className="flex items-center gap-2">
                          <h4 className="text-sm font-extrabold text-coast-ink">{a.name}</h4>
                          <span className="rounded-full bg-coast-sand px-2 py-0.5 text-[10px] font-extrabold text-coast-deep">
                            {a.code}
                          </span>
                          <span className="text-[10px] font-bold text-coast-muted">[{a.status}]</span>
                        </div>
                        <p className="mt-1 text-xs text-coast-muted">{a.description || a.category}</p>
                      </div>

                      <div className="flex flex-wrap items-center gap-2">
                        <button
                          className="rounded-full border border-coast-line px-3 py-1.5 text-[11px] font-bold text-coast-deep hover:bg-coast-sand"
                          onClick={() => handleEvaluateActivity(a.id, 'PUBLISHED')}
                          type="button"
                        >
                          Audit Publish
                        </button>
                        {a.status !== 'PUBLISHED' && (
                          <button
                            className="rounded-full bg-coast-deep px-3 py-1.5 text-[11px] font-bold text-white hover:bg-coast-blue"
                            onClick={() => handleTransitionActivity(a.id, 'PUBLISHED')}
                            type="button"
                          >
                            Publish
                          </button>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          )}

          {/* 3. Offerings & Schedules Panel */}
          {activeTab === 'offerings' && (
            <div className="mt-8 space-y-8">
              {/* Offering Form */}
              <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-sm sm:p-8">
                <h3 className="font-display text-xl font-bold text-coast-ink">Create New Experience Offering</h3>
                <form className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3" onSubmit={handleCreateOffering}>
                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="o-dest">
                      PARENT DESTINATION
                    </label>
                    <select
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="o-dest"
                      onChange={(e) => setNewOffDestId(e.target.value)}
                      value={newOffDestId}
                    >
                      {destinations.map((d) => (
                        <option key={d.id} value={d.id}>
                          {d.name} ({d.region})
                        </option>
                      ))}
                    </select>
                  </div>

                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="o-act">
                      ACTIVITY CATEGORY
                    </label>
                    <select
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="o-act"
                      onChange={(e) => setNewOffActId(e.target.value)}
                      value={newOffActId}
                    >
                      {activities.map((a) => (
                        <option key={a.id} value={a.id}>
                          {a.name} ({a.code})
                        </option>
                      ))}
                    </select>
                  </div>

                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="o-title">
                      PACKAGE TITLE
                    </label>
                    <input
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="o-title"
                      onChange={(e) => setNewOffTitle(e.target.value)}
                      placeholder="e.g. Morning Dolphin Cruise"
                      required
                      value={newOffTitle}
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="o-price">
                      PRICE (LKR)
                    </label>
                    <input
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="o-price"
                      onChange={(e) => setNewOffPrice(e.target.value)}
                      required
                      type="number"
                      value={newOffPrice}
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="o-dur">
                      DURATION (MINUTES)
                    </label>
                    <input
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="o-dur"
                      onChange={(e) => setNewOffDuration(e.target.value)}
                      type="number"
                      value={newOffDuration}
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="o-cap">
                      MAX CAPACITY (GUESTS)
                    </label>
                    <input
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="o-cap"
                      onChange={(e) => setNewOffCapacity(e.target.value)}
                      type="number"
                      value={newOffCapacity}
                    />
                  </div>

                  <div className="sm:col-span-2 lg:col-span-3">
                    <label className="block text-xs font-extrabold text-coast-deep" htmlFor="o-desc">
                      DESCRIPTION & INCLUSIONS
                    </label>
                    <textarea
                      className="mt-1.5 w-full rounded-2xl border border-coast-line bg-white px-3.5 py-2 text-xs text-coast-ink focus:border-coast-blue focus:outline-none"
                      id="o-desc"
                      onChange={(e) => setNewOffDesc(e.target.value)}
                      rows={2}
                      value={newOffDesc}
                    />
                  </div>

                  <div className="sm:col-span-2 lg:col-span-3">
                    <button
                      className="inline-flex min-h-11 items-center justify-center rounded-full bg-coast-deep px-6 text-xs font-extrabold text-white transition hover:bg-coast-blue"
                      type="submit"
                    >
                      Save Offering (DRAFT)
                    </button>
                  </div>
                </form>
              </div>

              {/* Offerings and Schedules Grid */}
              <div className="grid gap-8 lg:grid-cols-2">
                {/* Offerings list */}
                <div className="rounded-3xl border border-coast-line bg-white p-6 shadow-sm">
                  <h3 className="font-display text-xl font-bold text-coast-ink">Offerings Catalogue</h3>
                  <div className="mt-4 divide-y divide-coast-line max-h-96 overflow-y-auto">
                    {offerings.map((o) => (
                      <div className="py-3" key={o.id}>
                        <div className="flex items-center justify-between">
                          <h4 className="text-xs font-extrabold text-coast-ink">{o.title}</h4>
                          <span className="text-[10px] font-bold text-coast-deep">[{o.status}]</span>
                        </div>
                        <p className="mt-1 text-[11px] text-coast-muted">
                          {o.destinationName} · {o.price != null ? `LKR ${o.price.toLocaleString()}` : ''}
                        </p>

                        <div className="mt-2 flex flex-wrap gap-2">
                          <button
                            className="rounded-full bg-coast-sand px-2.5 py-1 text-[10px] font-bold text-coast-deep hover:bg-coast-glass"
                            onClick={() => setSelectedOfferingForSched(o.id)}
                            type="button"
                          >
                            Manage Timetable ({o.id === selectedOfferingForSched ? 'Active' : 'Select'})
                          </button>
                          <button
                            className="rounded-full border border-coast-line px-2.5 py-1 text-[10px] font-bold text-coast-deep hover:bg-coast-sand"
                            onClick={() => handleEvaluateOffering(o.id, 'PUBLISHED')}
                            type="button"
                          >
                            Audit
                          </button>
                          {o.status !== 'PUBLISHED' && (
                            <button
                              className="rounded-full bg-coast-deep px-2.5 py-1 text-[10px] font-bold text-white hover:bg-coast-blue"
                              onClick={() => handleTransitionOffering(o.id, 'PUBLISHED')}
                              type="button"
                            >
                              Publish
                            </button>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>

                {/* Schedules Manager for Selected Offering */}
                <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-sm">
                  <h3 className="font-display text-xl font-bold text-coast-ink">Timetable Slot Maintenance</h3>
                  {selectedOfferingForSched ? (
                    <div>
                      <p className="mt-1 text-xs text-coast-muted">
                        Maintaining departure schedules for offering ID: <span className="font-mono">{selectedOfferingForSched}</span>
                      </p>

                      <form className="mt-4 space-y-3 rounded-2xl bg-white p-4 border border-coast-line" onSubmit={handleAddSchedule}>
                        <div>
                          <label className="block text-[11px] font-bold text-coast-deep" htmlFor="s-start">
                            SLOT START TIME
                          </label>
                          <input
                            className="mt-1 w-full rounded-xl border border-coast-line bg-white px-3 py-1.5 text-xs focus:outline-none"
                            id="s-start"
                            onChange={(e) => setNewSchedStart(e.target.value)}
                            required
                            type="datetime-local"
                            value={newSchedStart}
                          />
                        </div>
                        <div>
                          <label className="block text-[11px] font-bold text-coast-deep" htmlFor="s-end">
                            SLOT END TIME
                          </label>
                          <input
                            className="mt-1 w-full rounded-xl border border-coast-line bg-white px-3 py-1.5 text-xs focus:outline-none"
                            id="s-end"
                            onChange={(e) => setNewSchedEnd(e.target.value)}
                            required
                            type="datetime-local"
                            value={newSchedEnd}
                          />
                        </div>
                        <button
                          className="inline-flex min-h-9 items-center justify-center rounded-full bg-coast-deep px-4 text-xs font-bold text-white hover:bg-coast-blue"
                          type="submit"
                        >
                          Add Slot to Schedule
                        </button>
                      </form>

                      <div className="mt-4 divide-y divide-coast-line max-h-60 overflow-y-auto">
                        {schedules.map((s) => (
                          <div className="flex items-center justify-between py-2 text-xs" key={s.id}>
                            <div>
                              <p className="font-bold text-coast-ink">
                                {new Date(s.startsAt).toLocaleString()} – {new Date(s.endsAt).toLocaleTimeString()}
                              </p>
                              <span className="text-[10px] text-coast-muted">{s.timeZoneId}</span>
                            </div>
                            <button
                              className="text-xs font-bold text-red-600 hover:text-red-800"
                              onClick={() => handleDeleteSchedule(s.id)}
                              type="button"
                            >
                              Delete
                            </button>
                          </div>
                        ))}
                      </div>
                    </div>
                  ) : (
                    <p className="mt-4 text-xs text-coast-muted">
                      Select an offering from the list to add or manage scheduled departure windows.
                    </p>
                  )}
                </div>
              </div>
            </div>
          )}

          {/* 4. Diagnostics & Seam Panel */}
          {activeTab === 'diagnostics' && (
            <div className="mt-8 grid gap-8 lg:grid-cols-2">
              {/* Dependencies Diagnostics */}
              <div className="rounded-3xl border border-coast-line bg-white p-6 shadow-sm sm:p-8">
                <div className="flex items-center justify-between">
                  <h3 className="font-display text-2xl font-bold text-coast-ink">Peer Microservices Health</h3>
                  <button
                    className="rounded-full bg-coast-sand px-3 py-1 text-xs font-bold text-coast-deep hover:bg-coast-glass"
                    onClick={handleLoadDiagnostics}
                    type="button"
                  >
                    Refresh
                  </button>
                </div>

                <p className="mt-2 text-xs leading-5 text-coast-muted">
                  Fault-tolerant connectivity status of external member services (Marine Safety, Coastal Operations, Biodiversity Inference).
                </p>

                {diagStatus ? (
                  <div className="mt-6 space-y-3">
                    {diagStatus.dependencies.map((dep, idx) => (
                      <div className="rounded-2xl border border-coast-line bg-coast-pearl p-4 text-xs" key={idx}>
                        <div className="flex items-center justify-between">
                          <span className="font-extrabold text-coast-ink">{dep.serviceName}</span>
                          <span
                            className={`rounded-full px-2 py-0.5 text-[10px] font-extrabold ${
                              dep.responded ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'
                            }`}
                          >
                            {dep.status}
                          </span>
                        </div>
                        <p className="mt-1 font-mono text-[11px] text-coast-muted">{dep.endpoint}</p>
                        <p className="mt-1 text-[11px] text-coast-muted">
                          Latency: {dep.latencyMs}ms · Checked: {new Date(dep.checkedAt).toLocaleTimeString()}
                        </p>
                        {dep.message && <p className="mt-1 text-xs text-coast-deep">{dep.message}</p>}
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="py-12 text-center text-xs text-coast-muted">Loading microservice health status...</div>
                )}
              </div>

              {/* Agent Seam Status */}
              <div className="rounded-3xl border border-coast-line bg-coast-sage p-6 shadow-sm sm:p-8">
                <h3 className="font-display text-2xl font-bold text-coast-ink">Agentic AI Pre-G07 Seam</h3>
                <p className="mt-2 text-xs leading-5 text-coast-muted">
                  Typed context seam providing component catalog telemetry for future Agentic AI orchestration under ADR-0007 and ADR-0020.
                </p>

                {agentSeam ? (
                  <div className="mt-6 space-y-4 text-xs">
                    <div className="rounded-2xl bg-white p-4 border border-coast-line">
                      <span className="font-bold text-coast-deep">Agent Role:</span> {agentSeam.agentName}
                    </div>
                    <div className="rounded-2xl bg-white p-4 border border-coast-line">
                      <span className="font-bold text-coast-deep">Integration Status:</span>{' '}
                      <span className="rounded-full bg-amber-100 px-2 py-0.5 font-bold text-amber-800">
                        {agentSeam.status} (Expected Pre-G07)
                      </span>
                    </div>
                    <div className="rounded-2xl bg-white p-4 border border-coast-line">
                      <span className="font-bold text-coast-deep">Seam Detail:</span>
                      <p className="mt-1 text-coast-ink leading-5">{agentSeam.detail}</p>
                    </div>

                    <div className="rounded-2xl bg-white p-4 border border-coast-line">
                      <span className="font-bold text-coast-deep">Planned Registered Tools:</span>
                      <ul className="mt-2 list-disc pl-5 space-y-1 text-coast-muted font-mono">
                        {agentSeam.plannedTools.map((t, idx) => (
                          <li key={idx}>{t}</li>
                        ))}
                      </ul>
                    </div>
                  </div>
                ) : (
                  <div className="py-12 text-center text-xs text-coast-muted">Loading Agent Seam contract...</div>
                )}
              </div>
            </div>
          )}
        </section>
      </main>

      <SiteFooter />
    </div>
  )
}
