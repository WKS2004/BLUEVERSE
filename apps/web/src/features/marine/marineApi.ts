import type { MarineActivityReference } from "./marineActivities"
import { withLoadingScreen } from '../loading/backendLoading.ts'

export type MarineSafetyProfile = {
  id: string
  activityId: string
  activityName: string
  maxWindSpeed: number
  maxWaveHeight: number
  maxSwellHeight: number
  cautionWindSpeed: number | null
  cautionWaveHeight: number | null
  cautionSwellHeight: number | null
  windCriteriaSource: string | null
  windCriteriaRationale: string | null
  waveCriteriaSource: string | null
  waveCriteriaRationale: string | null
  swellCriteriaSource: string | null
  swellCriteriaRationale: string | null
  isActive: boolean
  version: number
  createdAt: string
  updatedAt: string
  createdByUserId: string | null
  reviewedByUserId: string | null
  reviewedAt: string | null
  effectiveFrom: string | null
  effectiveTo: string | null
}

export type MarineConditionSnapshot = {
  id: string
  latitude: number
  longitude: number
  forecastTime: string
  retrievedAt: string
  windSpeed: number | null
  waveHeight: number | null
  swellHeight: number | null
  rain: number | null
  weatherCode: number | null
  source: string
  freshnessStatus: 'FRESH' | 'STALE' | 'UNAVAILABLE' | (string & {})
  missingFields: string[]
}

export type MarineSuitabilityResult = {
  status: 'SUITABLE' | 'CAUTION' | 'UNSUITABLE' | 'UNKNOWN' | (string & {})
  activityId: string
  activityName: string
  location: { latitude: number; longitude: number }
  requestedTime: string
  forecastTime: string
  evaluatedAt: string
  conditions: {
    windSpeed: number | null
    waveHeight: number | null
    swellHeight: number | null
    rain: number | null
    weatherCode: number | null
  } | null
  source: string | null
  retrievedAt: string | null
  freshness: string | null
  missingFields: string[]
  violations: string[]
  cautionFactors: string[]
  assessmentId: string
  snapshotId: string
}

export type MarineAssessmentHistory = {
  id: string
  activityId: string
  activityName: string | null
  conditionSnapshotId: string
  safetyProfileId: string
  profileVersion: number
  latitude: number
  longitude: number
  requestedTime: string
  forecastTime: string | null
  evaluatedAt: string
  result: string
  violations: string[]
  cautionFactors: string[]
  missingFields: string[]
  conditionMissingFields: string[]
  conditions: NonNullable<MarineSuitabilityResult['conditions']>
  conditionRetrievedAt: string | null
  criteria: { factor: string; maximum: number | null; caution: number | null; source: string | null; rationale: string | null }[]
  evidenceCompleteness: 'COMPLETE' | 'LEGACY_INCOMPLETE' | (string & {})
  source: string
  freshnessStatus: string
  createdAt: string
}

export type MarineDependencyAvailability = {
  availability: 'UNAVAILABLE'
  dependency: string
  code: string
  reason: string
  missingFields: string[]
  retryable: boolean
}

function dependencyAvailability(payload: unknown): MarineDependencyAvailability | undefined {
  if (typeof payload !== 'object' || payload === null) return undefined
  const row = payload as Record<string, unknown>
  if (row.availability !== 'UNAVAILABLE' || typeof row.dependency !== 'string' ||
      typeof row.code !== 'string' || typeof row.reason !== 'string' ||
      typeof row.retryable !== 'boolean' || !Array.isArray(row.missingFields) ||
      !row.missingFields.every(field => typeof field === 'string')) return undefined
  return { availability: 'UNAVAILABLE', dependency: row.dependency, code: row.code,
    reason: row.reason, missingFields: row.missingFields as string[], retryable: row.retryable }
}

export class MarineApiError extends Error {
  readonly status: number
  readonly dependencyAvailability?: MarineDependencyAvailability

  constructor(status: number, message: string, dependencyAvailability?: MarineDependencyAvailability) {
    super(message)
    this.name = 'MarineApiError'
    this.status = status
    this.dependencyAvailability = dependencyAvailability
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

async function request<T>(send: () => Promise<Response>, showLoadingScreen = true): Promise<T> {
  const operation = async () => {
    const response = await send()

    if (response.status === 204) return undefined as T

    const content = await response.text()
    let payload: unknown
    if (content.trim()) {
      try { payload = JSON.parse(content) as unknown } catch { payload = undefined }
    }
    if (!response.ok) {
      const detail = typeof payload === 'object' && payload !== null && 'detail' in payload && typeof payload.detail === 'string'
        ? payload.detail
        : response.status === 403
          ? 'Your current roles do not allow this action.'
          : response.status === 503
            ? 'Marine conditions cannot be retrieved right now. The provider may be briefly unavailable; please try again shortly.'
            : 'We couldn’t complete that marine request. Please try again.'
      throw new MarineApiError(response.status, detail, dependencyAvailability(payload))
    }
    if (payload === undefined) throw new MarineApiError(response.status, 'The server returned a response that could not be read.')
    return payload as T
  }
  return showLoadingScreen ? withLoadingScreen(operation) : operation()
}

export const getMarineConditions = (latitude: number, longitude: number, dateTime?: string) =>
  request<MarineConditionSnapshot>(() => {
    const query = new URLSearchParams({
      latitude: String(latitude),
      longitude: String(longitude),
      ...(dateTime ? { time: dateTime } : {}),
    })
    return fetch(`/api/marine/current?${query.toString()}`, requestOptions())
  })

export const getMarineHistory = (filters: { latitude?: number; longitude?: number; from?: string; to?: string } = {}, showLoadingScreen = true) =>
  request<MarineConditionSnapshot[]>(() => {
    const query = new URLSearchParams()
    if (filters.latitude !== undefined) query.set('latitude', String(filters.latitude))
    if (filters.longitude !== undefined) query.set('longitude', String(filters.longitude))
    if (filters.from) query.set('from', filters.from)
    if (filters.to) query.set('to', filters.to)
    return fetch(`/api/marine/history?${query.toString()}`, requestOptions())
  }, showLoadingScreen)

export const getMarineAssessments = (filters: { activityId?: string; result?: string; from?: string; to?: string } = {}, showLoadingScreen = true) =>
  request<MarineAssessmentHistory[]>(() => {
    const query = new URLSearchParams()
    if (filters.activityId) query.set('activityId', filters.activityId)
    if (filters.result) query.set('result', filters.result)
    if (filters.from) query.set('from', filters.from)
    if (filters.to) query.set('to', filters.to)
    return fetch(`/api/marine/assessments?${query.toString()}`, requestOptions())
  }, showLoadingScreen)

export const evaluateMarineSuitability = (input: {
  activityId: string
  latitude: number
  longitude: number
  dateTime?: string
}) =>
  request<MarineSuitabilityResult>(() => fetch('/api/marine/evaluate', requestOptions('POST', {
    activityId: input.activityId,
    latitude: input.latitude,
    longitude: input.longitude,
    ...(input.dateTime ? { dateTime: input.dateTime } : {}),
  })))

export const getMarineSafetyProfiles = (showLoadingScreen = true) =>
  request<MarineSafetyProfile[]>(() => fetch('/api/marine/safety-profiles', requestOptions()), showLoadingScreen)

export const createMarineSafetyProfile = (input: {
  activityId: string
  maxWindSpeed: number
  maxWaveHeight: number
  maxSwellHeight: number
  windCriteriaSource: string
  windCriteriaRationale: string
  waveCriteriaSource: string
  waveCriteriaRationale: string
  swellCriteriaSource: string
  swellCriteriaRationale: string
  cautionWindSpeed?: number
  cautionWaveHeight?: number
  cautionSwellHeight?: number
}) =>
  request<MarineSafetyProfile>(() => fetch('/api/marine/safety-profiles', requestOptions('POST', input)))

export const updateMarineSafetyProfile = (id: string, input: {
  maxWindSpeed: number
  maxWaveHeight: number
  maxSwellHeight: number
  windCriteriaSource: string
  windCriteriaRationale: string
  waveCriteriaSource: string
  waveCriteriaRationale: string
  swellCriteriaSource: string
  swellCriteriaRationale: string
  cautionWindSpeed?: number
  cautionWaveHeight?: number
  cautionSwellHeight?: number
}) =>
  request<MarineSafetyProfile>(() => fetch(`/api/marine/safety-profiles/${encodeURIComponent(id)}`, requestOptions('PUT', input)))

export const reviewMarineSafetyProfile = (id: string) =>
  request<MarineSafetyProfile>(() => fetch(`/api/marine/safety-profiles/${encodeURIComponent(id)}/review`, requestOptions('POST')))

export const deactivateMarineSafetyProfile = (id: string) =>
  request<void>(() => fetch(`/api/marine/safety-profiles/${encodeURIComponent(id)}`, requestOptions('DELETE')))

export async function getMarineActivities(showLoadingScreen = true): Promise<MarineActivityReference[]> {
  const rows = await request<unknown>(() => fetch('/api/marine/activities', requestOptions()), showLoadingScreen)
  if (!Array.isArray(rows) || rows.some(row => !row || typeof row.id !== 'string' ||
      !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(row.id) ||
      row.id === '00000000-0000-0000-0000-000000000000' || typeof row.name !== 'string' || !row.name.trim() ||
      typeof row.activityType !== 'string' || row.isActive !== true) ||
      new Set(rows.map(row => row.id)).size !== rows.length) {
    throw new MarineApiError(502, 'The activity list could not be read. Please retry.')
  }
  return rows as MarineActivityReference[]
}
