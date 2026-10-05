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
}

export type RecommendationResult = {
  recommendationId: string
  workflowId: string
  status: string
  generatedAt: string
  candidates: RecommendationCandidate[]
  excludedCandidatesCount: number
  uncertaintyNotes: string[]
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
  destinationId: string
  activityId: string | null
  status: string
  predictedSpecies: {
    speciesId: string
    scientificName: string
    commonName: string
    habitatSuitability: number
    confidenceLevel: string
  }[]
  modelMetadata: { modelVersion: string | null; inferenceTimestamp: string | null } | null
  limitations: string
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

function request<T>(send: () => Promise<Response>): Promise<T> {
  return withLoadingScreen(async () => parseResponse<T>(await send()), COASTAL_LOADING_CONTEXT)
}

export type RecommendationPreferences = {
  targetDestinationId: string
  startsAt: string
  endsAt: string
  durationHours: number
  preferredActivityIds: string[]
  experienceLevel: string | null
  includeBiodiversityContext: boolean
}

export function createRecommendations(preferences: RecommendationPreferences): Promise<RecommendationResult> {
  return request<RecommendationResult>(() => fetch('/api/planner/recommendations', {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({
      targetDestinationId: preferences.targetDestinationId,
      startsAt: preferences.startsAt,
      endsAt: preferences.endsAt,
      durationHours: preferences.durationHours,
      preferredActivityIds: preferences.preferredActivityIds,
      experienceLevel: preferences.experienceLevel,
      includeBiodiversityContext: preferences.includeBiodiversityContext,
    }),
  }))
}

export function getRecommendation(recommendationId: string): Promise<RecommendationResult> {
  return request<RecommendationResult>(() => fetch(`/api/planner/recommendations/${encodeURIComponent(recommendationId)}`, {
    credentials: 'include',
    headers: { Accept: 'application/json' },
  }))
}

export function getWorkflow(workflowId: string): Promise<WorkflowStatus> {
  return request<WorkflowStatus>(() => fetch(`/api/planner/workflows/${encodeURIComponent(workflowId)}`, {
    credentials: 'include',
    headers: { Accept: 'application/json' },
  }))
}

export function getBiodiversityPredictions(destinationId: string, activityId: string | null): Promise<BiodiversityPrediction> {
  const query = activityId ? `?destinationId=${encodeURIComponent(destinationId)}&activityId=${encodeURIComponent(activityId)}` : `?destinationId=${encodeURIComponent(destinationId)}`
  return request<BiodiversityPrediction>(() => fetch('/api/planner/biodiversity/predictions' + query, {
    credentials: 'include',
    headers: { Accept: 'application/json' },
  }))
}
