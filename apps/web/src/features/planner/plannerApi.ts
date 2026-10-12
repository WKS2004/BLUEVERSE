import { COASTAL_LOADING_CONTEXT, withLoadingScreen } from '../loading/backendLoading.ts'

export type SuitabilitySummary = {
  status: string
  marineConditionTime: string | null
  safetyProfileId: string | null
}

export type BiodiversityContext = {
  speciesName: string
  probability: number
  uncertainty: string
  predictionTimestamp: string
}

export type RecommendationCandidate = {
  destinationId: string
  activityId: string
  offeringId: string | null
  title: string
  scheduledStart: string
  scheduledEnd: string
  availabilityStatus: string
  suitability: SuitabilitySummary
  operationalStatus: string
  biodiversityContext: BiodiversityContext | null
  fitScore: number
  reasons: string[]
  timeZone?: string
}

export type RecommendationResult = {
  recommendationId: string
  workflowId: string
  status: string
  generatedAt: string
  candidates: RecommendationCandidate[]
  excludedCandidatesCount: number
  uncertaintyNotes: string[]
  outcome?: 'MATCHES_FOUND' | 'NO_MATCHES' | 'DEPENDENCIES_UNAVAILABLE'
}

export type WorkflowStatus = {
  workflowId: string
  workflowType: string
  status: string
  initiatorUserId: string | null
  objective: string | null
  createdAt: string
  completedAt: string | null
  resultSummary: string | null
  failureReason: string | null
}

export type BiodiversityPrediction = {
  contractVersion: 1
  destinationId: string
  activityId: string | null
  status: 'available' | 'not_requested' | 'unavailable' | 'invalid' | 'stale'
  message: string
  uncertaintyText?: string | null
  provenance?: { provider: string; modelVersion: string | null; inferenceTimestamp: string | null } | null
  predictedSpecies: {
    speciesId: string
    scientificName: string
    commonName: string
    habitatSuitability: number
    confidenceLevel: string
    uncertaintyText?: string | null
  }[]
  modelMetadata: { modelVersion: string | null; inferenceTimestamp: string | null } | null
  limitations: string
}

export type RecommendationHighlight = {
  id: string
  title: string
  startsAt: string
  endsAt: string
  timeZone: string
  destinationId: string
  destinationName: string
  activityId: string
  activityName: string
  offeringId: string | null
  fitScore: number | null
  suitabilityStatus: string
  availabilityStatus: string
  operationalStatus: string
  recommendationId: string | null
  uncertaintyNotes: string[]
  generatedAt: string
  outcome: string
  itemCount: number
  evidenceCheckedAt?: string | null
}

export class PlannerApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'PlannerApiError'
    this.status = status
  }
}

async function parseResponse<T>(response: Response): Promise<T> {
  const responseText = await response.text()
  let payload: unknown
  if (responseText.trim()) {
    try {
      payload = JSON.parse(responseText) as unknown
    } catch {
      if (response.ok) {
        throw new PlannerApiError(response.status, 'The server returned a response that could not be read.')
      }
    }
  }

  if (!response.ok) {
    const detail =
      typeof payload === 'object' && payload !== null && !Array.isArray(payload) && 'detail' in payload && typeof payload.detail === 'string'
        ? payload.detail
        : null
    throw new PlannerApiError(response.status, detail ?? 'We couldn’t complete that just now. Please try again.')
  }

  if (payload === undefined) {
    throw new PlannerApiError(response.status, 'The server returned an empty response.')
  }
  return payload as T
}

type ReadLoading = 'screen' | 'inline'

function request<T>(send: () => Promise<Response>, loading: ReadLoading = 'screen'): Promise<T> {
  const read = async () => parseResponse<T>(await send())
  return loading === 'inline' ? read() : withLoadingScreen(read, COASTAL_LOADING_CONTEXT)
}

export type DestinationOption = {
  destinationId: string
  name: string
  region: string
  timeZone: string
  activities: { activityId: string; name: string }[]
}

export type PlannerCatalogue = {
  status: 'AVAILABLE' | 'UNAVAILABLE'
  destinations: DestinationOption[]
  message: string | null
}

export type ItineraryItemInput = {
  itemId?: string
  destinationId: string
  activityId: string
  offeringId: string | null
  title: string
  orderIndex: number
  scheduledStart: string
  scheduledEnd: string
  timeZone?: string
}

export type ItineraryItem = ItineraryItemInput & {
  itemId: string
  itineraryId: string
  lastSuitabilityStatus: string
  lastAvailabilityStatus: string
  lastOperationalStatus: string
  advisoryNote: string | null
  fitScore: number
}

export type ItineraryInput = {
  title: string
  description: string | null
  startsAt: string
  endsAt: string
  items: ItineraryItemInput[]
  recommendationId?: string
  timeZone?: string
}

export type Itinerary = Omit<ItineraryInput, 'items'> & {
  itineraryId: string
  ownerUserId: string
  concurrencyVersion: number
  createdAt: string
  updatedAt: string
  status: string
  items: ItineraryItem[]
  timeZone: string
}

export type Evaluation = {
  evaluationId: string
  itineraryId: string
  evaluatedAt: string
  hasChanges: boolean
  requiresReview: boolean
  concurrencyVersion: number
  summary: string
  items: {
    itemId: string
    currentAvailability: string
    currentSuitability: string
    currentOperationalStatus: string
    previousAvailability: string
    previousSuitability: string
    previousOperationalStatus: string
    advisoryMessage: string | null
    suggestedAction: string
    marineConditionTime: string | null
  }[]
}

export type TripListItem = Pick<Itinerary, 'itineraryId' | 'title' | 'startsAt' | 'endsAt' | 'status' | 'timeZone' | 'concurrencyVersion'> & {
  itemCount: number
}

export type CreateRecommendationWorkflowOptions = {
  targetDestinationId: string
  startsAt: string
  endsAt: string
  durationHours: number
  preferredActivityIds: string[]
  experienceLevel: string | null
  includeBiodiversityContext: boolean
  workflowType?: string
  workflowOwnerUserId?: string
}

const headers = { Accept: 'application/json', 'Content-Type': 'application/json' }

function object(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function instant(value: unknown): boolean {
  return typeof value === 'string' && Number.isFinite(Date.parse(value))
}

function optionalText(value: unknown): boolean {
  return value == null || typeof value === 'string'
}

function optionalInstant(value: unknown): boolean {
  return value == null || instant(value)
}

function textList(value: unknown): boolean {
  return Array.isArray(value) && value.every(v => typeof v === 'string')
}

function zone(value: unknown): boolean {
  if (typeof value !== 'string') return false
  try {
    new Intl.DateTimeFormat('en', { timeZone: value })
    return true
  } catch {
    return false
  }
}

function isRecommendations(value: unknown): boolean {
  return (
    object(value) &&
    typeof value.recommendationId === 'string' &&
    typeof value.workflowId === 'string' &&
    typeof value.status === 'string' &&
    (value.outcome === undefined || ['MATCHES_FOUND', 'NO_MATCHES', 'DEPENDENCIES_UNAVAILABLE'].includes(String(value.outcome))) &&
    instant(value.generatedAt) &&
    typeof value.excludedCandidatesCount === 'number' &&
    textList(value.uncertaintyNotes) &&
    Array.isArray(value.candidates) &&
    value.candidates.every(
      c =>
        object(c) &&
        ['destinationId', 'activityId', 'title', 'operationalStatus', 'availabilityStatus'].every(k => typeof c[k] === 'string') &&
        instant(c.scheduledStart) &&
        instant(c.scheduledEnd) &&
        textList(c.reasons) &&
        object(c.suitability) &&
        typeof c.suitability.status === 'string' &&
        optionalInstant(c.suitability.marineConditionTime) &&
        typeof c.fitScore === 'number' &&
        Number.isFinite(c.fitScore) &&
        c.fitScore >= 0 &&
        c.fitScore <= 1 &&
        (c.timeZone === undefined || zone(c.timeZone)) &&
        (c.biodiversityContext === null ||
          (object(c.biodiversityContext) &&
            typeof c.biodiversityContext.speciesName === 'string' &&
            typeof c.biodiversityContext.probability === 'number' &&
            c.biodiversityContext.probability >= 0 &&
            c.biodiversityContext.probability <= 1 &&
            instant(c.biodiversityContext.predictionTimestamp)))
    )
  )
}

const biodiversityMessages = {
  available: 'Wildlife context is available. A prediction does not guarantee a sighting.',
  not_requested: 'Wildlife context was not requested.',
  unavailable: 'Wildlife predictions are temporarily unavailable. Please try again later.',
  invalid: 'Wildlife predictions could not be verified for this location.',
  stale: 'The wildlife prediction is out of date. Request a fresh result.',
} as const

function normalizePrediction(value: unknown): unknown {
  if (!object(value)) return value
  const status = typeof value.status === 'string' ? value.status.toLowerCase() : ''
  const known = Object.hasOwn(biodiversityMessages, status)
  let normalizedStatus = known ? status as keyof typeof biodiversityMessages : 'unavailable'
  const metadata = object(value.modelMetadata) ? value.modelMetadata : null
  const provenance = object(value.provenance) ? value.provenance : null
  if (normalizedStatus === 'available' && (
    !metadata || typeof metadata.modelVersion !== 'string' || !metadata.modelVersion || !instant(metadata.inferenceTimestamp) ||
    !provenance || typeof provenance.provider !== 'string' || !provenance.provider ||
    provenance.modelVersion !== metadata.modelVersion || !instant(provenance.inferenceTimestamp)
  )) normalizedStatus = 'unavailable'
  const available = normalizedStatus === 'available'
  const contractVersion = value.contractVersion
  if (contractVersion !== 1) return {
    ...value,
    contractVersion: 1,
    status: 'unavailable',
    message: biodiversityMessages.unavailable,
    limitations: biodiversityMessages.unavailable,
    uncertaintyText: null,
    provenance: null,
    predictedSpecies: [],
    modelMetadata: null,
  }
  if (!known) return {
    ...value,
    contractVersion: 1,
    status: 'unavailable',
    message: biodiversityMessages.unavailable,
    limitations: biodiversityMessages.unavailable,
    uncertaintyText: null,
    provenance: null,
    predictedSpecies: [],
    modelMetadata: null,
  }
  return {
    ...value,
    contractVersion: 1,
    status: normalizedStatus,
    message: available && typeof value.message === 'string' ? value.message : biodiversityMessages[normalizedStatus],
    limitations: available && typeof value.limitations === 'string' ? value.limitations : biodiversityMessages[normalizedStatus],
    uncertaintyText: available && typeof value.uncertaintyText === 'string' ? value.uncertaintyText : null,
    predictedSpecies: available && Array.isArray(value.predictedSpecies) ? value.predictedSpecies : [],
    modelMetadata: available ? value.modelMetadata : null,
    provenance: available ? value.provenance : null,
  }
}

function isPrediction(value: unknown): value is BiodiversityPrediction {
  return (
    object(value) &&
    value.contractVersion === 1 &&
    typeof value.destinationId === 'string' &&
    (value.activityId === null || typeof value.activityId === 'string') &&
    ['available', 'not_requested', 'unavailable', 'invalid', 'stale'].includes(String(value.status)) &&
    typeof value.message === 'string' &&
    typeof value.limitations === 'string' &&
    Array.isArray(value.predictedSpecies) &&
    value.predictedSpecies.every(
      s =>
        object(s) &&
        ['speciesId', 'commonName', 'scientificName', 'confidenceLevel'].every(k => typeof s[k] === 'string') &&
        typeof s.habitatSuitability === 'number' &&
        Number.isFinite(s.habitatSuitability) &&
        s.habitatSuitability >= 0 &&
        s.habitatSuitability <= 1
    ) &&
    (value.modelMetadata === null ||
      (object(value.modelMetadata) &&
        (typeof value.modelMetadata.modelVersion === 'string' || value.modelMetadata.modelVersion === null) &&
        instant(value.modelMetadata.inferenceTimestamp)))
  )
}

export function isHighlight(value: unknown): value is RecommendationHighlight {
  return (
    object(value) &&
    typeof value.id === 'string' &&
    typeof value.title === 'string' &&
    instant(value.startsAt) &&
    instant(value.endsAt) &&
    zone(value.timeZone) &&
    typeof value.destinationId === 'string' &&
    typeof value.destinationName === 'string' &&
    typeof value.activityId === 'string' &&
    typeof value.activityName === 'string' &&
    (value.offeringId === null || typeof value.offeringId === 'string') &&
    (value.fitScore === null || (typeof value.fitScore === 'number' && Number.isFinite(value.fitScore) && value.fitScore >= 0 && value.fitScore <= 1)) &&
    typeof value.suitabilityStatus === 'string' &&
    typeof value.availabilityStatus === 'string' &&
    typeof value.operationalStatus === 'string' &&
    (value.recommendationId === null || typeof value.recommendationId === 'string') &&
    textList(value.uncertaintyNotes) &&
    instant(value.generatedAt) &&
    optionalInstant(value.evidenceCheckedAt) &&
    typeof value.outcome === 'string' &&
    typeof value.itemCount === 'number' &&
    Number.isInteger(value.itemCount)
  )
}

function isStop(value: unknown): boolean {
  return (
    object(value) &&
    ['itemId', 'title', 'destinationId', 'activityId', 'scheduledStart', 'scheduledEnd', 'lastSuitabilityStatus', 'lastAvailabilityStatus', 'lastOperationalStatus'].every(k => typeof value[k] === 'string') &&
    typeof value.orderIndex === 'number' &&
    Number.isInteger(value.orderIndex) &&
    instant(value.scheduledStart) &&
    instant(value.scheduledEnd) &&
    optionalText(value.advisoryNote) &&
    (value.timeZone === undefined || zone(value.timeZone))
  )
}

function isTrip(value: unknown): boolean {
  return (
    object(value) &&
    ['itineraryId', 'title', 'startsAt', 'endsAt', 'status'].every(k => typeof value[k] === 'string') &&
    typeof value.concurrencyVersion === 'number' &&
    Number.isInteger(value.concurrencyVersion) &&
    optionalText(value.description) &&
    Array.isArray(value.items) &&
    value.items.every(isStop) &&
    instant(value.startsAt) &&
    instant(value.endsAt) &&
    zone(value.timeZone)
  )
}

function isEvaluation(value: unknown): boolean {
  return (
    object(value) &&
    typeof value.evaluationId === 'string' &&
    instant(value.evaluatedAt) &&
    typeof value.summary === 'string' &&
    typeof value.hasChanges === 'boolean' &&
    typeof value.requiresReview === 'boolean' &&
    typeof value.concurrencyVersion === 'number' &&
    Array.isArray(value.items) &&
    value.items.every(
      i =>
        object(i) &&
        ['itemId', 'currentAvailability', 'currentSuitability', 'currentOperationalStatus', 'previousAvailability', 'previousSuitability', 'previousOperationalStatus', 'suggestedAction'].every(k => typeof i[k] === 'string') &&
        optionalText(i.advisoryMessage) &&
        optionalInstant(i.marineConditionTime)
    )
  )
}

function checked<T>(promise: Promise<unknown>, validator: (value: unknown) => value is T): Promise<T>
function checked<T>(promise: Promise<T>, validator: (value: unknown) => boolean): Promise<T>
async function checked<T>(promise: Promise<T>, validator: (value: unknown) => boolean): Promise<T> {
  const value = await promise
  if (!validator(value)) throw new PlannerApiError(502, 'We couldn’t read the trip information. Please try again.')
  return value
}

export function getPlannerCatalogue(loading: ReadLoading = 'screen'): Promise<PlannerCatalogue> {
  return checked(
    request<PlannerCatalogue>(() =>
      fetch('/api/planner/catalogue', {
        credentials: 'include',
        headers: { Accept: 'application/json' },
        signal: AbortSignal.timeout(12000),
      }), loading
    ),
    v =>
      object(v) &&
      ['AVAILABLE', 'UNAVAILABLE'].includes(String(v.status)) &&
      optionalText(v.message) &&
      Array.isArray(v.destinations) &&
      v.destinations.every(
        d =>
          object(d) &&
          ['destinationId', 'name', 'region'].every(k => typeof d[k] === 'string') &&
          zone(d.timeZone) &&
          Array.isArray(d.activities) &&
          d.activities.every(a => object(a) && typeof a.activityId === 'string' && typeof a.name === 'string')
      )
  )
}

export function listItineraries(page = 1, pageSize = 20, loading: ReadLoading = 'screen'): Promise<Itinerary[]> {
  return checked(
    request<Itinerary[]>(() =>
      fetch(`/api/planner/itineraries?page=${page}&pageSize=${pageSize}`, {
        credentials: 'include',
        headers: { Accept: 'application/json' },
        signal: AbortSignal.timeout(15000),
      }), loading
    ),
    v => Array.isArray(v) && v.every(isTrip)
  )
}

export function getItinerary(id: string): Promise<Itinerary> {
  return checked(
    request<Itinerary>(() =>
      fetch(`/api/planner/itineraries/${encodeURIComponent(id)}`, {
        credentials: 'include',
        headers: { Accept: 'application/json' },
        signal: AbortSignal.timeout(15000),
      })
    ),
    isTrip
  )
}

export function createItinerary(input: ItineraryInput): Promise<Itinerary> {
  return checked(
    request<Itinerary>(() =>
      fetch('/api/planner/itineraries', {
        method: 'POST',
        credentials: 'include',
        headers,
        body: JSON.stringify(input),
        signal: AbortSignal.timeout(20000),
      })
    ),
    isTrip
  )
}

export function updateItinerary(id: string, input: ItineraryInput & { concurrencyVersion: number }): Promise<Itinerary> {
  return checked(
    request<Itinerary>(() =>
      fetch(`/api/planner/itineraries/${encodeURIComponent(id)}`, {
        method: 'PUT',
        credentials: 'include',
        headers,
        body: JSON.stringify(input),
        signal: AbortSignal.timeout(20000),
      })
    ),
    isTrip
  )
}

export function deleteItinerary(id: string): Promise<void> {
  return withLoadingScreen(async () => {
    const response = await fetch(`/api/planner/itineraries/${encodeURIComponent(id)}`, {
      method: 'DELETE',
      credentials: 'include',
      headers: { Accept: 'application/json' },
      signal: AbortSignal.timeout(15000),
    })
    if (response.status !== 204) {
      await parseResponse(response)
      throw new PlannerApiError(502, 'We couldn’t confirm that the trip was deleted. Reload your saved trips.')
    }
  }, COASTAL_LOADING_CONTEXT)
}

export function reviewItinerary(id: string): Promise<Evaluation> {
  return checked(
    request<Evaluation>(() =>
      fetch(`/api/planner/itineraries/${encodeURIComponent(id)}/re-evaluations`, {
        method: 'POST',
        credentials: 'include',
        headers,
        body: JSON.stringify({ reEvaluationMode: 'FULL_ASSESSMENT' }),
        signal: AbortSignal.timeout(90000),
      })
    ),
    isEvaluation
  )
}

export function getEvaluationHistory(id: string): Promise<Evaluation[]> {
  return checked(
    request<Evaluation[]>(() =>
      fetch(`/api/planner/itineraries/${encodeURIComponent(id)}/re-evaluations`, {
        credentials: 'include',
        headers: { Accept: 'application/json' },
        signal: AbortSignal.timeout(15000),
      })
    ),
    v => Array.isArray(v) && v.every(isEvaluation)
  )
}

export function confirmItinerary(id: string): Promise<Itinerary> {
  return checked(
    request<Itinerary>(() =>
      fetch(`/api/planner/itineraries/${encodeURIComponent(id)}/confirm`, {
        method: 'POST',
        credentials: 'include',
        headers: { Accept: 'application/json' },
        signal: AbortSignal.timeout(20000),
      })
    ),
    isTrip
  )
}

export function getRecommendationHighlights(): Promise<RecommendationHighlight[]> {
  const pending = plannerHighlightsCache.get('highlights')
  if (pending) return pending
  const requestPromise = request<RecommendationHighlight[]>(() =>
    fetch('/api/planner/recommendations/highlights', {
      credentials: 'include',
      headers: { Accept: 'application/json' },
      signal: AbortSignal.timeout(15000),
    })
  )
  const checkedPromise: Promise<RecommendationHighlight[]> = requestPromise.then(value => {
    if (!Array.isArray(value) || !value.every(isHighlight)) {
      throw new PlannerApiError(502, 'We couldn’t read the coastal highlights. Please try again.')
    }
    return value
  })
  const inFlight = checkedPromise.then(value => {
    if (plannerHighlightsCache.get('highlights') === inFlight) plannerHighlightsCache.delete('highlights')
    return value
  }, cause => {
    if (plannerHighlightsCache.get('highlights') === inFlight) plannerHighlightsCache.delete('highlights')
    throw cause
  })
  plannerHighlightsCache.set('highlights', inFlight)
  return inFlight
}

export type RecommendationPreferences = {
  targetDestinationId: string
  startsAt: string
  endsAt: string
  durationHours: number
  preferredActivityIds: string[]
  experienceLevel: string | null
  includeBiodiversityContext: boolean
  timeZone?: string
}

export function createRecommendations(preferences: RecommendationPreferences): Promise<RecommendationResult> {
  return checked(
    request<RecommendationResult>(() =>
      fetch('/api/planner/recommendations', {
        method: 'POST',
        credentials: 'include',
        signal: AbortSignal.timeout(50000),
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({
          targetDestinationId: preferences.targetDestinationId,
          startsAt: preferences.startsAt,
          endsAt: preferences.endsAt,
          durationHours: preferences.durationHours,
          preferredActivityIds: preferences.preferredActivityIds,
          experienceLevel: preferences.experienceLevel,
          includeBiodiversityContext: preferences.includeBiodiversityContext,
          ...(preferences.timeZone ? { timeZone: preferences.timeZone } : {}),
        }),
      })
    ),
    isRecommendations
  )
}

export function getRecommendation(recommendationId: string): Promise<RecommendationResult> {
  return checked(
    request<RecommendationResult>(() =>
      fetch(`/api/planner/recommendations/${encodeURIComponent(recommendationId)}`, {
        credentials: 'include',
        headers: { Accept: 'application/json' },
        signal: AbortSignal.timeout(15000),
      })
    ),
    isRecommendations
  )
}

export function getWorkflow(workflowId: string): Promise<WorkflowStatus> {
  return checked(
    request<WorkflowStatus>(() =>
      fetch(`/api/planner/workflows/${encodeURIComponent(workflowId)}`, {
        credentials: 'include',
        headers: { Accept: 'application/json' },
        signal: AbortSignal.timeout(15000),
      })
    ),
    v =>
      object(v) &&
      typeof v.workflowId === 'string' &&
      typeof v.status === 'string' &&
      instant(v.createdAt) &&
      optionalText(v.resultSummary) &&
      optionalText(v.failureReason)
  )
}

export function getBiodiversityPredictions(destinationId: string, activityId: string | null): Promise<BiodiversityPrediction> {
  const query =
    activityId
      ? `?destinationId=${encodeURIComponent(destinationId)}&activityId=${encodeURIComponent(activityId)}`
      : `?destinationId=${encodeURIComponent(destinationId)}`
  return checked(
    request<unknown>(() =>
      fetch('/api/planner/biodiversity/predictions' + query, {
        credentials: 'include',
        headers: { Accept: 'application/json' },
        signal: AbortSignal.timeout(15000),
      })
    ).then(normalizePrediction),
    isPrediction
  )
}

// Interpret a user's wall clock in the destination's IANA time zone, independently
// of the browser location. Reject nonexistent or ambiguous clocks around DST.
export function utcTime(value: string, zone: string): string {
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value)) throw new Error('Choose a valid date and time.')
  const reference = Date.parse(value + ':00Z')
  if (!Number.isFinite(reference)) throw new Error('Choose a valid date and time.')
  const candidates = new Set<number>()
  for (const shift of [-86400000, 0, 86400000]) {
    const sample = reference + shift
    const displayed = Date.parse(wallTime(new Date(sample).toISOString(), zone) + ':00Z')
    const candidate = reference - (displayed - sample)
    if (wallTime(new Date(candidate).toISOString(), zone) === value) candidates.add(candidate)
  }
  if (candidates.size !== 1) throw new Error('That local time is skipped or occurs twice. Choose another time.')
  return new Date([...candidates][0]).toISOString()
}

export function statusLabel(value: string): string {
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

export function validatePlanningWindow(startsAt: string, endsAt: string, hours: number): string | null {
  if (Date.parse(endsAt) <= Date.parse(startsAt)) return 'The end of your trip must be after the start.'
  const now = Date.now()
  if (Date.parse(startsAt) < now || Date.parse(endsAt) > now + 30 * 86400000) return 'Choose future dates within the next 30 days.'
  if (!Number.isInteger(hours) || hours < 1 || hours > (Date.parse(endsAt) - Date.parse(startsAt)) / 3600000)
    return 'Choose a whole number of hours that fits within your trip.'
  return null
}

export function wallTime(value: string, zone: string): string {
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone: zone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(new Date(value))
  const part = (key: string) => parts.find(p => p.type === key)?.value
  return `${part('year')}-${part('month')}-${part('day')}T${part('hour')}:${part('minute')}`
}


export function dateTime(value: string, zone = Intl.DateTimeFormat().resolvedOptions().timeZone): string {
  return new Intl.DateTimeFormat('en-GB', { timeZone: zone, day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }).format(
    new Date(value),
  )
}

export const plannerHighlightsCache = new Map<string, Promise<RecommendationHighlight[]>>()

