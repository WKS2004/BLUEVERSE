import { PlannerApiError, type RecommendationHighlight } from './plannerApi.ts'

export function plannerError(error: unknown): string {
  if (error instanceof PlannerApiError) {
    if (error.status === 401) return 'Your session has ended. Sign in again to continue.'
    if (error.status === 403) return 'Your account doesn’t have access to this action. Ask an administrator for coastal planning access.'
    if (error.status === 404) return 'This trip or search is no longer available.'
    if (error.status === 409) return 'This trip changed in another window. Reload it before saving your changes.'
    if (error.status < 500) return error.message
  }
  return 'We couldn’t connect just now. Your changes are still here. Please try again.'
}
export function dateTime(value: string, zone = Intl.DateTimeFormat().resolvedOptions().timeZone): string {
  return new Intl.DateTimeFormat('en-GB', { timeZone: zone, day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}
export function wallTime(value: string, zone: string): string {
  const parts = new Intl.DateTimeFormat('en-GB', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(new Date(value))
  const part = (key: string) => parts.find(p => p.type === key)?.value
  return `${part('year')}-${part('month')}-${part('day')}T${part('hour')}:${part('minute')}`
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
  const labels: Record<string, string> = { SUITABLE: 'Suitable conditions', CAUTION: 'Review caution', UNSUITABLE: 'Reschedule this activity', UNKNOWN: 'Not verified', AVAILABLE: 'Available', UNAVAILABLE: 'Unavailable', OPEN: 'Open', TEMPORARILY_SUSPENDED: 'Temporarily suspended', CANCELLED: 'Cancelled', NOT_PUBLISHED: 'No longer published', COMPLETED: 'Completed' }
  return labels[value] ?? 'Not verified'
}

export function validatePlanningWindow(startsAt: string, endsAt: string, hours: number): string | null {
  if (Date.parse(endsAt) <= Date.parse(startsAt)) return 'The end of your trip must be after the start.'
  const now = Date.now()
  if (Date.parse(startsAt) < now || Date.parse(endsAt) > now + 30 * 86400000) return 'Choose future dates within the next 30 days.'
  if (!Number.isInteger(hours) || hours < 1 || hours > (Date.parse(endsAt) - Date.parse(startsAt)) / 3600000) return 'Choose a whole number of hours that fits within your trip.'
  return null
}

export type { RecommendationHighlight }
