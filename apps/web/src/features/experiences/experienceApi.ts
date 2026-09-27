import { withLoadingScreen } from '../loading/backendLoading'

export type DestinationDto = {
  id: string
  name: string
  slug: string
  description?: string | null
  region?: string | null
  latitude: number
  longitude: number
  status: string
  createdAt: string
  updatedAt: string
}

export type CreateDestinationRequest = {
  name: string
  slug?: string
  description?: string
  region?: string
  latitude: number
  longitude: number
}

export type UpdateDestinationRequest = {
  name: string
  slug?: string
  description?: string
  region?: string
  latitude: number
  longitude: number
}

export type ActivityDto = {
  id: string
  code: string
  name: string
  description?: string | null
  category?: string | null
  status: string
  createdAt: string
  updatedAt: string
}

export type CreateActivityRequest = {
  code: string
  name: string
  description?: string
  category?: string
}

export type UpdateActivityRequest = {
  name: string
  description?: string
  category?: string
}

export type OfferingDto = {
  id: string
  destinationId: string
  destinationName: string
  activityId: string
  activityName: string
  activityCode: string
  title: string
  description?: string | null
  price?: number | null
  currency?: string | null
  durationMinutes?: number | null
  maxCapacity?: number | null
  status: string
  createdAt: string
  updatedAt: string
}

export type CreateOfferingRequest = {
  destinationId: string
  activityId: string
  title: string
  description?: string
  price?: number
  currency?: string
  durationMinutes?: number
  maxCapacity?: number
}

export type UpdateOfferingRequest = {
  title: string
  description?: string
  price?: number
  currency?: string
  durationMinutes?: number
  maxCapacity?: number
}

export type ScheduleDto = {
  id: string
  offeringId: string
  startsAt: string
  endsAt: string
  timeZoneId: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export type CreateScheduleRequest = {
  startsAt: string
  endsAt: string
  timeZoneId?: string
  isActive?: boolean
}

export type UpdateScheduleRequest = {
  startsAt: string
  endsAt: string
  timeZoneId?: string
  isActive?: boolean
}

export type PublicationEvaluationResponse = {
  targetId: string
  targetType: string
  currentStatus: string
  requestedStatus: string
  canTransition: boolean
  reasons: string[]
}

export type AvailabilityEvaluationRequest = {
  offeringId: string
  startsAt: string
  endsAt: string
}

export type OfferingSummaryDto = {
  offeringId: string
  offeringTitle: string
  destinationId: string
  destinationName: string
  destinationStatus: string
  activityId: string
  activityName: string
  activityStatus: string
  offeringStatus: string
}

export type OperationalRestrictionContextDto = {
  hasRestriction: boolean
  restrictionType?: string | null
  severity?: string | null
  reason?: string | null
  effectiveUntil?: string | null
  sourceStatus: string
  responded: boolean
  targetEndpoint?: string | null
  attemptsCount: number
  latencyMs: number
  remoteStatus?: string | null
  message?: string | null
}

export type AvailabilityEvaluationResponse = {
  offeringId: string
  startsAt: string
  endsAt: string
  status: 'AVAILABLE' | 'UNAVAILABLE' | 'UNKNOWN' | string
  reasonCodes: string[]
  offering: OfferingSummaryDto
  operationalRestriction?: OperationalRestrictionContextDto | null
  evaluatedAt: string
}

export type FocalSpeciesPredictionDto = {
  speciesName: string
  scientificName: string
  conservationStatus: string
  occurrenceProbability: number
  habitatSuitability?: string | null
  primaryThreats?: string | null
}

export type BiodiversityContextResponseDto = {
  destinationId: string
  destinationName: string
  latitude: number
  longitude: number
  status: 'available' | 'unavailable' | 'not_connected' | string
  predictions: FocalSpeciesPredictionDto[]
  modelVersion?: string | null
  modelSource?: string | null
  uncertaintyNotes?: string | null
  evaluatedAt?: string | null
  disclaimer: string
  responded: boolean
  targetEndpoint?: string | null
  attemptsCount: number
  latencyMs: number
  remoteStatus?: string | null
  message?: string | null
}

export type MarineConditionsContextDto = {
  destinationId: string
  destinationName: string
  latitude: number
  longitude: number
  responded: boolean
  targetEndpoint?: string | null
  attemptsCount: number
  latencyMs: number
  remoteStatus: string
  safetyLevel: string
  waterCondition: string
  waveHeightMeters?: number | null
  windSpeedKnots?: number | null
  tideStatus?: string | null
  advisoryMessage?: string | null
  fallbackUsed: boolean
  evaluatedAt?: string | null
  disclaimer: string
}

export type ActiveAdvisoryItemDto = {
  advisoryId: string
  title: string
  severity: string
  description: string
  issuedAt: string
  expiresAt?: string | null
}

export type OperationalAdvisoriesResponseDto = {
  destinationId: string
  destinationName: string
  responded: boolean
  targetEndpoint?: string | null
  attemptsCount: number
  latencyMs: number
  remoteStatus: string
  advisories: ActiveAdvisoryItemDto[]
  fallbackUsed: boolean
  message: string
}

export type FavouriteDto = {
  id: string
  userId: string
  targetType: string
  targetId: string
  targetTitle?: string | null
  targetStatus?: string | null
  createdAt: string
}

export type MapConfigDto = {
  provider: string
  tileServiceType: string
  vectorTileUrl: string
  availableStyles: Record<string, string>
  defaultStyle: string
  attribution: string
  documentationUrl: string
}

export type MapSearchResultItemDto = {
  displayName: string
  latitude: number
  longitude: number
  type?: string | null
  category?: string | null
  region?: string | null
  country?: string | null
}

export type MapSearchResponseDto = {
  query: string
  results: MapSearchResultItemDto[]
  source: string
  fallback: boolean
  retrievedAt: string
}

export type NearbyDestinationDto = {
  destinationId: string
  name: string
  slug: string
  description?: string | null
  region?: string | null
  latitude: number
  longitude: number
  distanceMeters: number
  activeOfferingsCount: number
}

export type NearbyResponse = {
  query: {
    latitude: number
    longitude: number
    radiusMeters: number
    limit: number
  }
  count: number
  results: NearbyDestinationDto[]
}

export type MicroserviceDependencyReportDto = {
  serviceName: string
  endpoint: string
  responded: boolean
  latencyMs: number
  statusCode?: number | null
  status: string
  message?: string | null
  checkedAt: string
}

export type DependenciesStatusResponseDto = {
  serviceName: string
  evaluatedAt: string
  dependencies: MicroserviceDependencyReportDto[]
  allHealthy: boolean
}

export type AgentContextResponseDto = {
  agentName: string
  status: string
  detail: string
  plannedTools: string[]
  checkedAt: string
}

export type PagedResult<T> = {
  total: number
  page: number
  pageSize: number
  items: T[]
}

export class ExperienceApiError extends Error {
  readonly status: number
  readonly errorData?: unknown

  constructor(status: number, message: string, errorData?: unknown) {
    super(message)
    this.name = 'ExperienceApiError'
    this.status = status
    this.errorData = errorData
  }
}

function requestOptions(method = 'GET', body?: unknown): RequestInit {
  return {
    method,
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  }
}

async function request<T>(send: () => Promise<Response>): Promise<T> {
  return withLoadingScreen(async () => {
    let response: Response
    try {
      response = await send()
    } catch (networkError) {
      throw new ExperienceApiError(0, 'Unable to connect to the BLUEVERSE coastal service. Please check your connection and try again.', networkError)
    }

    if (response.status === 204) return undefined as T

    const content = await response.text()
    let payload: unknown
    if (content.trim()) {
      try {
        payload = JSON.parse(content) as unknown
      } catch {
        payload = undefined
      }
    }

    if (!response.ok) {
      let detail = 'We could not complete your coastal experience request. Please try again.'
      if (typeof payload === 'object' && payload !== null && 'detail' in payload && typeof payload.detail === 'string') {
        detail = payload.detail
      } else if (response.status === 401) {
        detail = 'Authentication is required for this action. Please sign in.'
      } else if (response.status === 403) {
        detail = 'Your account does not have permission for this catalogue action.'
      } else if (response.status === 404) {
        detail = 'The requested coastal destination or experience was not found.'
      }
      throw new ExperienceApiError(response.status, detail, payload)
    }

    if (payload === undefined) {
      throw new ExperienceApiError(response.status, 'The server returned an empty or unreadable response.')
    }

    return payload as T
  })
}

// ----------------- Destinations -----------------
export const getDestinations = (params?: { query?: string; region?: string; status?: string; page?: number; pageSize?: number }) => {
  const query = new URLSearchParams()
  if (params?.query) query.set('query', params.query)
  if (params?.region) query.set('region', params.region)
  if (params?.status) query.set('status', params.status)
  if (params?.page) query.set('page', String(params.page))
  if (params?.pageSize) query.set('pageSize', String(params.pageSize))

  const qs = query.toString()
  if (qs) {
    return request<PagedResult<DestinationDto>>(() => fetch(`/api/experiences/destinations?${qs}`, requestOptions()))
  }
  return request<PagedResult<DestinationDto>>(() => fetch('/api/experiences/destinations', requestOptions()))
}

export const getDestinationById = (id: string) =>
  request<DestinationDto>(() => fetch(`/api/experiences/destinations/${encodeURIComponent(id)}`, requestOptions()))

export const createDestination = (input: CreateDestinationRequest) =>
  request<DestinationDto>(() => fetch('/api/experiences/destinations', requestOptions('POST', input)))

export const updateDestination = (id: string, input: UpdateDestinationRequest) =>
  request<DestinationDto>(() => fetch(`/api/experiences/destinations/${encodeURIComponent(id)}`, requestOptions('PUT', input)))

export const evaluateDestinationPublication = (id: string, status: string) =>
  request<PublicationEvaluationResponse>(() =>
    fetch(`/api/experiences/destinations/${encodeURIComponent(id)}/publication-evaluations`, requestOptions('POST', { status }))
  )

export const updateDestinationPublication = (id: string, status: string) =>
  request<DestinationDto>(() =>
    fetch(`/api/experiences/destinations/${encodeURIComponent(id)}/publication`, requestOptions('PATCH', { status }))
  )

export const getDestinationMarineConditions = (id: string) =>
  request<MarineConditionsContextDto>(() =>
    fetch(`/api/experiences/destinations/${encodeURIComponent(id)}/marine-conditions`, requestOptions())
  )

export const getDestinationOperationalAdvisories = (id: string) =>
  request<OperationalAdvisoriesResponseDto>(() =>
    fetch(`/api/experiences/destinations/${encodeURIComponent(id)}/operational-advisories`, requestOptions())
  )

export const getDestinationBiodiversity = (id: string) =>
  request<BiodiversityContextResponseDto>(() =>
    fetch(`/api/experiences/destinations/${encodeURIComponent(id)}/biodiversity`, requestOptions())
  )

// ----------------- Activities -----------------
export const getActivities = (params?: { destinationId?: string; category?: string; query?: string; status?: string; page?: number; pageSize?: number }) => {
  const query = new URLSearchParams()
  if (params?.destinationId) query.set('destinationId', params.destinationId)
  if (params?.category) query.set('category', params.category)
  if (params?.query) query.set('query', params.query)
  if (params?.status) query.set('status', params.status)
  if (params?.page) query.set('page', String(params.page))
  if (params?.pageSize) query.set('pageSize', String(params.pageSize))

  const qs = query.toString()
  if (qs) {
    return request<PagedResult<ActivityDto>>(() => fetch(`/api/experiences/activities?${qs}`, requestOptions()))
  }
  return request<PagedResult<ActivityDto>>(() => fetch('/api/experiences/activities', requestOptions()))
}

export const getActivityById = (id: string) =>
  request<ActivityDto>(() => fetch(`/api/experiences/activities/${encodeURIComponent(id)}`, requestOptions()))

export const createActivity = (input: CreateActivityRequest) =>
  request<ActivityDto>(() => fetch('/api/experiences/activities', requestOptions('POST', input)))

export const updateActivity = (id: string, input: UpdateActivityRequest) =>
  request<ActivityDto>(() => fetch(`/api/experiences/activities/${encodeURIComponent(id)}`, requestOptions('PUT', input)))

export const evaluateActivityPublication = (id: string, status: string) =>
  request<PublicationEvaluationResponse>(() =>
    fetch(`/api/experiences/activities/${encodeURIComponent(id)}/publication-evaluations`, requestOptions('POST', { status }))
  )

export const updateActivityPublication = (id: string, status: string) =>
  request<ActivityDto>(() =>
    fetch(`/api/experiences/activities/${encodeURIComponent(id)}/publication`, requestOptions('PATCH', { status }))
  )

// ----------------- Offerings -----------------
export const getOfferings = (params?: { activityId?: string; destinationId?: string; status?: string; page?: number; pageSize?: number }) => {
  const query = new URLSearchParams()
  if (params?.activityId) query.set('activityId', params.activityId)
  if (params?.destinationId) query.set('destinationId', params.destinationId)
  if (params?.status) query.set('status', params.status)
  if (params?.page) query.set('page', String(params.page))
  if (params?.pageSize) query.set('pageSize', String(params.pageSize))

  const qs = query.toString()
  if (qs) {
    return request<PagedResult<OfferingDto>>(() => fetch(`/api/experiences/offerings?${qs}`, requestOptions()))
  }
  return request<PagedResult<OfferingDto>>(() => fetch('/api/experiences/offerings', requestOptions()))
}

export const getOfferingById = (id: string) =>
  request<OfferingDto>(() => fetch(`/api/experiences/offerings/${encodeURIComponent(id)}`, requestOptions()))

export const createOffering = (input: CreateOfferingRequest) =>
  request<OfferingDto>(() => fetch('/api/experiences/offerings', requestOptions('POST', input)))

export const updateOffering = (id: string, input: UpdateOfferingRequest) =>
  request<OfferingDto>(() => fetch(`/api/experiences/offerings/${encodeURIComponent(id)}`, requestOptions('PUT', input)))

export const evaluateOfferingPublication = (id: string, status: string) =>
  request<PublicationEvaluationResponse>(() =>
    fetch(`/api/experiences/offerings/${encodeURIComponent(id)}/publication-evaluations`, requestOptions('POST', { status }))
  )

export const updateOfferingPublication = (id: string, status: string) =>
  request<OfferingDto>(() =>
    fetch(`/api/experiences/offerings/${encodeURIComponent(id)}/publication`, requestOptions('PATCH', { status }))
  )

export const getOfferingSchedules = (id: string, params?: { from?: string; to?: string }) => {
  const query = new URLSearchParams()
  if (params?.from) query.set('from', params.from)
  if (params?.to) query.set('to', params.to)
  const qs = query.toString()
  if (qs) {
    return request<ScheduleDto[]>(() =>
      fetch(`/api/experiences/offerings/${encodeURIComponent(id)}/schedules?${qs}`, requestOptions())
    )
  }
  return request<ScheduleDto[]>(() =>
    fetch(`/api/experiences/offerings/${encodeURIComponent(id)}/schedules`, requestOptions())
  )
}

export const addOfferingSchedule = (id: string, input: CreateScheduleRequest) =>
  request<ScheduleDto>(() => fetch(`/api/experiences/offerings/${encodeURIComponent(id)}/schedules`, requestOptions('POST', input)))

export const updateOfferingSchedule = (id: string, scheduleId: string, input: UpdateScheduleRequest) =>
  request<ScheduleDto>(() =>
    fetch(`/api/experiences/offerings/${encodeURIComponent(id)}/schedules/${encodeURIComponent(scheduleId)}`, requestOptions('PUT', input))
  )

export const deleteOfferingSchedule = (id: string, scheduleId: string) =>
  request<void>(() =>
    fetch(`/api/experiences/offerings/${encodeURIComponent(id)}/schedules/${encodeURIComponent(scheduleId)}`, requestOptions('DELETE'))
  )

// ----------------- Core Non-CRUD Availability Evaluation -----------------
export const evaluateAvailability = (input: AvailabilityEvaluationRequest) =>
  request<AvailabilityEvaluationResponse>(() =>
    fetch('/api/experiences/availability/evaluations', requestOptions('POST', input))
  )

// ----------------- Favourites -----------------
export const getUserFavourites = () =>
  request<FavouriteDto[]>(() => fetch('/api/experiences/favourites', requestOptions()))

export const addFavourite = (targetType: string, targetId: string) =>
  request<FavouriteDto>(() =>
    fetch(`/api/experiences/favourites/${encodeURIComponent(targetType)}/${encodeURIComponent(targetId)}`, requestOptions('PUT'))
  )

export const removeFavourite = (targetType: string, targetId: string) =>
  request<void>(() =>
    fetch(`/api/experiences/favourites/${encodeURIComponent(targetType)}/${encodeURIComponent(targetId)}`, requestOptions('DELETE'))
  )

// ----------------- Map & Nearby Proximity -----------------
export const getMapConfig = () =>
  request<MapConfigDto>(() => fetch('/api/experiences/map/config', requestOptions()))

export const searchMapPlaces = (query: string) =>
  request<MapSearchResponseDto>(() => fetch(`/api/experiences/map/search?q=${encodeURIComponent(query)}`, requestOptions()))

export const getNearbyExperiences = (latitude: number, longitude: number, radiusMeters = 50000, limit = 10) => {
  const query = new URLSearchParams({
    latitude: String(latitude),
    longitude: String(longitude),
    radiusMeters: String(radiusMeters),
    limit: String(limit),
  })
  return request<NearbyResponse>(() => fetch(`/api/experiences/nearby?${query.toString()}`, requestOptions()))
}

// ----------------- Diagnostics & Agent Seam -----------------
export const getDependenciesStatus = () =>
  request<DependenciesStatusResponseDto>(() => fetch('/api/experiences/dependencies/status', requestOptions()))

export const getAgentContext = () =>
  request<AgentContextResponseDto>(() => fetch('/api/experiences/agent/context', requestOptions()))
