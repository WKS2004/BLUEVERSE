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
  isActive: boolean
  version: number
  createdAt: string
  updatedAt: string
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

export class MarineApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'MarineApiError'
    this.status = status
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
      throw new MarineApiError(response.status, detail)
    }
    if (payload === undefined) throw new MarineApiError(response.status, 'The server returned a response that could not be read.')
    return payload as T
  })
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

export const getMarineHistory = (filters: { latitude?: number; longitude?: number; from?: string; to?: string } = {}) =>
  request<MarineConditionSnapshot[]>(() => {
    const query = new URLSearchParams()
    if (filters.latitude !== undefined) query.set('latitude', String(filters.latitude))
    if (filters.longitude !== undefined) query.set('longitude', String(filters.longitude))
    if (filters.from) query.set('from', filters.from)
    if (filters.to) query.set('to', filters.to)
    return fetch(`/api/marine/history?${query.toString()}`, requestOptions())
  })

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

export const getMarineSafetyProfiles = () =>
  request<MarineSafetyProfile[]>(() => fetch('/api/marine/safety-profiles', requestOptions()))

export const createMarineSafetyProfile = (input: {
  activityId: string
  maxWindSpeed: number
  maxWaveHeight: number
  maxSwellHeight: number
  cautionWindSpeed?: number
  cautionWaveHeight?: number
  cautionSwellHeight?: number
}) =>
  request<MarineSafetyProfile>(() => fetch('/api/marine/safety-profiles', requestOptions('POST', input)))

export const updateMarineSafetyProfile = (id: string, input: {
  maxWindSpeed: number
  maxWaveHeight: number
  maxSwellHeight: number
  cautionWindSpeed?: number
  cautionWaveHeight?: number
  cautionSwellHeight?: number
  isActive: boolean
}) =>
  request<MarineSafetyProfile>(() => fetch(`/api/marine/safety-profiles/${encodeURIComponent(id)}`, requestOptions('PUT', input)))

export const deactivateMarineSafetyProfile = (id: string) =>
  request<void>(() => fetch(`/api/marine/safety-profiles/${encodeURIComponent(id)}`, requestOptions('DELETE')))
