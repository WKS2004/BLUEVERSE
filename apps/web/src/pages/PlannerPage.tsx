import { useState } from 'react'
import { Link } from 'react-router'
import SiteFooter from '../components/layout/SiteFooter'
import SiteHeader from '../components/layout/SiteHeader'
import AccountAreaNavigation from '../components/account/AccountAreaNavigation'
import { useAuthSession } from '../features/auth/authSession'
import {
  createRecommendations,
  getBiodiversityPredictions,
  getWorkflow,
  PlannerApiError,
  type BiodiversityPrediction,
  type RecommendationCandidate,
  type RecommendationResult,
  type WorkflowStatus,
} from '../features/planner/plannerApi'

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

const EXPERIENCE_LEVELS = ['BEGINNER', 'INTERMEDIATE', 'ADVANCED'] as const

type FormState = {
  targetDestinationId: string
  startsAt: string
  endsAt: string
  durationHours: string
  preferredActivityIds: string
  experienceLevel: string
  includeBiodiversityContext: boolean
}

const initialForm: FormState = {
  targetDestinationId: '',
  startsAt: '',
  endsAt: '',
  durationHours: '4',
  preferredActivityIds: '',
  experienceLevel: 'INTERMEDIATE',
  includeBiodiversityContext: true,
}

function formatTimestamp(value: string): string {
  return new Date(value).toISOString()
}

function formatDateTime(value: string): string {
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString()
}

function parseActivityIds(raw: string): string[] {
  return raw
    .split(/[\s,]+/)
    .map((item) => item.trim())
    .filter((item) => item.length > 0)
}

function validate(form: FormState): string | null {
  if (!GUID_PATTERN.test(form.targetDestinationId.trim())) {
    return 'Enter the target destination as a valid GUID (for example 3fa85f64-5717-4562-b3fc-2c963f66afa9).'
  }
  if (!form.startsAt || !form.endsAt) {
    return 'Choose both a start and an end date for the trip.'
  }
  const starts = new Date(form.startsAt)
  const ends = new Date(form.endsAt)
  if (Number.isNaN(starts.getTime()) || Number.isNaN(ends.getTime())) {
    return 'The selected dates could not be read.'
  }
  if (ends.getTime() <= starts.getTime()) {
    return 'The end date must be after the start date.'
  }
  const duration = Number(form.durationHours)
  if (!Number.isFinite(duration) || duration <= 0) {
    return 'Planned hours must be a positive number.'
  }
  if (duration > (ends.getTime() - starts.getTime()) / 3_600_000) {
    return 'Planned hours must fit within the selected date range.'
  }
  const now = Date.now()
  if (starts.getTime() <= now || ends.getTime() > now + 30 * 24 * 3_600_000) {
    return 'Planning dates must be in the future and within the next 30 days.'
  }
  const activityIds = parseActivityIds(form.preferredActivityIds)
  if (activityIds.some((id) => !GUID_PATTERN.test(id))) {
    return 'Preferred activity IDs must be valid GUIDs, separated by commas.'
  }
  return null
}

function CandidateCard({ candidate }: { candidate: RecommendationCandidate }) {
  const [prediction, setPrediction] = useState<BiodiversityPrediction | null>(null)
  const [predictionError, setPredictionError] = useState<string | null>(null)
  const [loadingPrediction, setLoadingPrediction] = useState(false)

  async function inspectBiodiversity() {
    setLoadingPrediction(true)
    setPredictionError(null)
    setPrediction(null)
    try {
      setPrediction(await getBiodiversityPredictions(candidate.destinationId, candidate.activityId))
    } catch (error) {
      setPredictionError(error instanceof Error ? error.message : 'We couldn’t load the biodiversity context.')
    } finally {
      setLoadingPrediction(false)
    }
  }

  return (
    <article className="rounded-3xl border border-coast-line bg-white p-6 shadow-sm sm:p-7">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 className="break-words font-display text-xl tracking-[-0.02em] [overflow-wrap:anywhere]">{candidate.title}</h3>
          <p className="mt-1 text-sm text-coast-muted">
            {formatDateTime(candidate.scheduledStart)} – {formatDateTime(candidate.scheduledEnd)}
          </p>
        </div>
        <span className="inline-flex items-center rounded-full bg-coast-sand px-3 py-1.5 text-xs font-bold text-coast-deep">
          Fit {Math.round(candidate.fitScore * 100)}%
        </span>
      </div>
      <dl className="mt-4 grid gap-2 text-sm sm:grid-cols-2">
        <div className="rounded-2xl bg-coast-pearl px-4 py-3">
          <dt className="text-[11px] font-extrabold tracking-[0.15em] text-coast-blue">MARINE SUITABILITY</dt>
          <dd className="mt-1 font-bold text-coast-deep">{candidate.suitability.status}</dd>
        </div>
        <div className="rounded-2xl bg-coast-pearl px-4 py-3">
          <dt className="text-[11px] font-extrabold tracking-[0.15em] text-coast-blue">OPERATIONS</dt>
          <dd className="mt-1 font-bold text-coast-deep">{candidate.operationalStatus}</dd>
        </div>
        <div className="rounded-2xl bg-coast-pearl px-4 py-3">
          <dt className="text-[11px] font-extrabold tracking-[0.15em] text-coast-blue">AVAILABILITY</dt>
          <dd className="mt-1 font-bold text-coast-deep">{candidate.availabilityStatus}</dd>
        </div>
        <div className="rounded-2xl bg-coast-pearl px-4 py-3">
          <dt className="text-[11px] font-extrabold tracking-[0.15em] text-coast-blue">BIODIVERSITY</dt>
          <dd className="mt-1 font-bold text-coast-deep">
            {candidate.biodiversityContext ? `${candidate.biodiversityContext.speciesName} (${candidate.biodiversityContext.uncertainty})` : 'Not requested'}
          </dd>
        </div>
      </dl>
      {candidate.reasons.length > 0 && (
        <ul className="mt-4 list-inside list-disc space-y-1 text-sm text-coast-muted">
          {candidate.reasons.map((reason) => <li key={reason}>{reason}</li>)}
        </ul>
      )}
      {candidate.activityId && (
        <button
          type="button"
          onClick={inspectBiodiversity}
          disabled={loadingPrediction}
          className="mt-5 inline-flex min-h-10 items-center rounded-full border border-coast-line px-4 text-sm font-bold text-coast-deep transition duration-200 hover:bg-coast-sage disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue"
        >
          {loadingPrediction ? 'Inspecting…' : 'Inspect biodiversity context'}
        </button>
      )}
      {predictionError && <p className="mt-3 text-sm font-semibold text-red-700">{predictionError}</p>}
      {prediction && (
        <section aria-label="Biodiversity prediction" className="mt-4 rounded-2xl border border-coast-line bg-coast-pearl p-5">
          <p className="text-[11px] font-extrabold tracking-[0.15em] text-coast-blue">PREDICTED SPECIES · {prediction.status}</p>
          {prediction.predictedSpecies.length === 0 ? (
            <p className="mt-2 text-sm text-coast-muted">No species predictions were returned for this candidate.</p>
          ) : (
            <ul className="mt-3 space-y-2">
              {prediction.predictedSpecies.map((species) => (
                <li key={species.speciesId} className="text-sm">
                  <span className="font-bold text-coast-deep">{species.commonName || species.scientificName}</span>
                  <span className="text-coast-muted"> — {species.scientificName}, habitat suitability {Math.round(species.habitatSuitability * 100)}%, {species.confidenceLevel} confidence</span>
                </li>
              ))}
            </ul>
          )}
          {prediction.modelMetadata?.modelVersion && (
            <p className="mt-3 text-xs text-coast-muted">Model {prediction.modelMetadata.modelVersion}</p>
          )}
          <p className="mt-2 text-xs leading-5 text-coast-muted">{prediction.limitations}</p>
        </section>
      )}
    </article>
  )
}

export default function PlannerPage() {
  const { user, status } = useAuthSession()
  const [form, setForm] = useState<FormState>(initialForm)
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<RecommendationResult | null>(null)
  const [workflow, setWorkflow] = useState<WorkflowStatus | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [loadingWorkflow, setLoadingWorkflow] = useState(false)

  function update(patch: Partial<FormState>) {
    setForm((current) => ({ ...current, ...patch }))
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    setError(null)
    const problem = validate(form)
    if (problem) {
      setError(problem)
      return
    }
    setSubmitting(true)
    try {
      const created = await createRecommendations({
        targetDestinationId: form.targetDestinationId.trim(),
        startsAt: formatTimestamp(form.startsAt),
        endsAt: formatTimestamp(form.endsAt),
        durationHours: Number(form.durationHours),
        preferredActivityIds: parseActivityIds(form.preferredActivityIds),
        experienceLevel: form.experienceLevel,
        includeBiodiversityContext: form.includeBiodiversityContext,
      })
      setResult(created)
      setWorkflow(null)
    } catch (apiError) {
      setResult(null)
      setWorkflow(null)
      setError(apiError instanceof Error ? apiError.message : 'We couldn’t generate recommendations just now. Please try again.')
    } finally {
      setSubmitting(false)
    }
  }

  async function showWorkflow() {
    if (!result) return
    setLoadingWorkflow(true)
    try {
      setWorkflow(await getWorkflow(result.workflowId))
    } catch (apiError) {
      setWorkflow(null)
      setError(apiError instanceof PlannerApiError ? apiError.message : 'We couldn’t load the planning workflow status.')
    } finally {
      setLoadingWorkflow(false)
    }
  }

  return (
    <div className="flex min-h-screen flex-col bg-coast-paper font-sans text-coast-ink" id="top">
      <SiteHeader active="planner" />
      <main className="mx-auto grid w-full max-w-[90rem] flex-1 grid-cols-1 content-start gap-6 px-4 py-6 sm:px-8 sm:py-10 lg:grid-cols-[15rem_minmax(0,1fr)] lg:items-start lg:gap-8 lg:px-6 lg:py-0">
        <AccountAreaNavigation active="planner" />
        <div className="min-w-0 lg:py-12">
          <section aria-labelledby="planner-title" className="relative isolate overflow-hidden rounded-[2rem] bg-coast-deep text-white shadow-sm">
            <div aria-hidden="true" className="absolute inset-0 -z-10 bg-[radial-gradient(ellipse_at_85%_10%,rgba(201,224,230,0.32),transparent_42%)]" />
            <div className="px-5 py-8 sm:px-8 sm:py-10 lg:px-10 lg:py-12">
              <p className="text-[11px] font-extrabold tracking-[0.18em] text-coast-glass">SMART COASTAL PLANNER</p>
              <h1 className="mt-3 max-w-3xl font-display text-4xl leading-tight tracking-[-0.05em] sm:text-5xl" id="planner-title">Plan a day the coast will remember.</h1>
              <p className="mt-4 max-w-2xl text-sm leading-6 text-white/80 sm:text-base sm:leading-7">Share your constraints and the planner weighs marine suitability, availability and operational advisories to assemble candidate days — with honest uncertainty notes when peer services are quiet.</p>
            </div>
          </section>

          {status === 'checking' ? null : !user ? (
            <section className="mt-5 rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-sm sm:p-9">
              <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">SIGNED-IN PLANNING</p>
              <h2 className="mt-3 font-display text-3xl tracking-[-0.04em]">Sign in to plan with the coast.</h2>
              <p className="mt-3 max-w-xl text-sm leading-6 text-coast-muted">Recommendations are generated for your account so saved plans stay yours.</p>
              <Link className="mt-6 inline-flex min-h-11 items-center justify-center rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white transition hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue" to="/signin">Sign in</Link>
            </section>
          ) : (
            <>
              <section aria-labelledby="planner-constraints-title" className="mt-5 rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-sm sm:p-7">
                <h2 className="font-display text-2xl tracking-[-0.035em]" id="planner-constraints-title">Trip constraints</h2>
                <form className="mt-5 grid gap-4" onSubmit={submit}>
                  <div>
                    <label className="text-xs font-extrabold tracking-[0.15em] text-coast-blue" htmlFor="planner-destination">TARGET DESTINATION ID</label>
                    <input
                      className="mt-2 w-full rounded-2xl border border-coast-line bg-white px-4 py-3 text-sm text-coast-ink focus-visible:outline-2 focus-visible:outline-coast-blue"
                      id="planner-destination"
                      placeholder="3fa85f64-5717-4562-b3fc-2c963f66afa9"
                      value={form.targetDestinationId}
                      onChange={(event) => update({ targetDestinationId: event.target.value })}
                    />
                  </div>
                  <div className="grid gap-4 sm:grid-cols-2">
                    <div>
                      <label className="text-xs font-extrabold tracking-[0.15em] text-coast-blue" htmlFor="planner-starts">STARTS</label>
                      <input
                        className="mt-2 w-full rounded-2xl border border-coast-line bg-white px-4 py-3 text-sm text-coast-ink focus-visible:outline-2 focus-visible:outline-coast-blue"
                        id="planner-starts"
                        type="datetime-local"
                        value={form.startsAt}
                        onChange={(event) => update({ startsAt: event.target.value })}
                      />
                    </div>
                    <div>
                      <label className="text-xs font-extrabold tracking-[0.15em] text-coast-blue" htmlFor="planner-ends">ENDS</label>
                      <input
                        className="mt-2 w-full rounded-2xl border border-coast-line bg-white px-4 py-3 text-sm text-coast-ink focus-visible:outline-2 focus-visible:outline-coast-blue"
                        id="planner-ends"
                        type="datetime-local"
                        value={form.endsAt}
                        onChange={(event) => update({ endsAt: event.target.value })}
                      />
                    </div>
                  </div>
                  <div className="grid gap-4 sm:grid-cols-2">
                    <div>
                      <label className="text-xs font-extrabold tracking-[0.15em] text-coast-blue" htmlFor="planner-duration">PLANNED HOURS</label>
                      <input
                        className="mt-2 w-full rounded-2xl border border-coast-line bg-white px-4 py-3 text-sm text-coast-ink focus-visible:outline-2 focus-visible:outline-coast-blue"
                        id="planner-duration"
                        type="number"
                        min={1}
                        value={form.durationHours}
                        onChange={(event) => update({ durationHours: event.target.value })}
                      />
                    </div>
                    <div>
                      <label className="text-xs font-extrabold tracking-[0.15em] text-coast-blue" htmlFor="planner-experience">EXPERIENCE LEVEL</label>
                      <select
                        className="mt-2 w-full rounded-2xl border border-coast-line bg-white px-4 py-3 text-sm text-coast-ink focus-visible:outline-2 focus-visible:outline-coast-blue"
                        id="planner-experience"
                        value={form.experienceLevel}
                        onChange={(event) => update({ experienceLevel: event.target.value })}
                      >
                        {EXPERIENCE_LEVELS.map((level) => <option key={level} value={level}>{level.charAt(0)}{level.slice(1).toLowerCase()}</option>)}
                      </select>
                    </div>
                  </div>
                  <div>
                    <label className="text-xs font-extrabold tracking-[0.15em] text-coast-blue" htmlFor="planner-activities">PREFERRED ACTIVITY IDS (OPTIONAL)</label>
                    <input
                      className="mt-2 w-full rounded-2xl border border-coast-line bg-white px-4 py-3 text-sm text-coast-ink focus-visible:outline-2 focus-visible:outline-coast-blue"
                      id="planner-activities"
                      placeholder="Separate multiple GUIDs with commas"
                      value={form.preferredActivityIds}
                      onChange={(event) => update({ preferredActivityIds: event.target.value })}
                    />
                  </div>
                  <label className="flex items-center gap-3 text-sm font-semibold text-coast-deep">
                    <input
                      className="h-4 w-4 accent-coast-blue"
                      type="checkbox"
                      checked={form.includeBiodiversityContext}
                      onChange={(event) => update({ includeBiodiversityContext: event.target.checked })}
                    />
                    Include biodiversity context for each candidate
                  </label>
                  {error && (
                    <p aria-live="polite" className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm font-semibold text-red-700">{error}</p>
                  )}
                  <button
                    className="inline-flex min-h-12 w-fit items-center justify-center rounded-full bg-coast-deep px-6 text-sm font-extrabold text-white transition duration-200 hover:-translate-y-0.5 hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:opacity-60"
                    type="submit"
                    disabled={submitting}
                  >
                    {submitting ? 'Planning…' : 'Generate recommendations'}
                  </button>
                </form>
              </section>

              {result && (
                <section aria-labelledby="planner-results-title" className="mt-8">
                  <div className="mb-4 flex flex-wrap items-end justify-between gap-4">
                    <div>
                      <p className="text-[11px] font-extrabold tracking-[0.18em] text-coast-blue">CANDIDATES · {result.status}</p>
                      <h2 className="mt-2 font-display text-3xl tracking-[-0.045em]" id="planner-results-title">
                        {result.candidates.length === 0 ? 'No candidates this time.' : `${result.candidates.length} candidate${result.candidates.length === 1 ? '' : 's'} for your coast.`}
                      </h2>
                    </div>
                    <button
                      className="inline-flex min-h-10 items-center rounded-full border border-coast-line px-4 text-sm font-bold text-coast-deep transition hover:bg-coast-sage disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue"
                      type="button"
                      onClick={showWorkflow}
                      disabled={loadingWorkflow}
                    >
                      {loadingWorkflow ? 'Loading…' : 'Planning workflow status'}
                    </button>
                  </div>
                  {result.excludedCandidatesCount > 0 && (
                    <p className="mb-4 text-sm text-coast-muted">{result.excludedCandidatesCount} candidate{result.excludedCandidatesCount === 1 ? '' : 's'} were excluded by safety, availability or operational rules.</p>
                  )}
                  {result.uncertaintyNotes.length > 0 && (
                    <ul className="mb-4 space-y-2">
                      {result.uncertaintyNotes.map((note) => (
                        <li className="rounded-2xl border border-coast-line bg-coast-sand/60 px-4 py-3 text-sm text-coast-deep" key={note}>{note}</li>
                      ))}
                    </ul>
                  )}
                  {workflow && (
                    <div className="mb-4 rounded-2xl border border-coast-line bg-white p-5 text-sm">
                      <p className="text-[11px] font-extrabold tracking-[0.15em] text-coast-blue">WORKFLOW {workflow.workflowType}</p>
                      <p className="mt-1 font-bold text-coast-deep">Status: {workflow.status}</p>
                      {workflow.resultSummary && <p className="mt-1 text-coast-muted">{workflow.resultSummary}</p>}
                      {workflow.failureReason && <p className="mt-1 font-semibold text-red-700">{workflow.failureReason}</p>}
                      <p className="mt-1 text-xs text-coast-muted">Created {formatDateTime(workflow.createdAt)}{workflow.completedAt ? `, completed ${formatDateTime(workflow.completedAt)}` : ''}</p>
                    </div>
                  )}
                  <div className="grid gap-4">
                    {result.candidates.map((candidate) => <CandidateCard key={`${candidate.destinationId}-${candidate.activityId}-${candidate.scheduledStart}`} candidate={candidate} />)}
                  </div>
                </section>
              )}
            </>
          )}
        </div>
      </main>
      <SiteFooter />
    </div>
  )
}
