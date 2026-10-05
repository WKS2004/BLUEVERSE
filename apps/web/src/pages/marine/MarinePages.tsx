import { useEffect, useMemo, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import AccountAreaNavigation from '../../components/account/AccountAreaNavigation'
import SiteFooter from '../../components/layout/SiteFooter'
import SiteHeader from '../../components/layout/SiteHeader'
import {
  MarineApiError,
  deactivateMarineSafetyProfile,
  createMarineSafetyProfile,
  evaluateMarineSuitability,
  getMarineAssessments,
  getMarineHistory,
  getMarineSafetyProfiles,
  reviewMarineSafetyProfile,
  updateMarineSafetyProfile,
} from '../../features/marine/marineApi'
import type { MarineAssessmentHistory, MarineConditionSnapshot, MarineSafetyProfile, MarineSuitabilityResult } from '../../features/marine/marineApi'
import { MARINE_ACTIVITY_REFERENCES } from '../../features/marine/marineActivities'
import { hasAllPermissions } from '../../features/authorization/permissions'
import { useAuthSession } from '../../features/auth/authSession'

const cardClass = 'min-w-0 rounded-3xl border border-coast-line bg-white p-5 shadow-[0_12px_35px_rgba(24,57,76,0.06)] sm:p-7'
const inputClass = 'mt-2 min-h-11 w-full min-w-0 rounded-2xl border border-coast-line bg-white px-4 py-2.5 text-sm text-coast-ink outline-none transition focus:border-coast-blue focus:ring-2 focus:ring-coast-glass'
const primaryButton = 'inline-flex min-h-10 items-center justify-center rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white transition hover:-translate-y-0.5 hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:cursor-not-allowed disabled:opacity-50'
const secondaryButton = 'inline-flex min-h-10 items-center justify-center rounded-full border border-coast-line bg-white px-4 text-sm font-bold text-coast-deep transition hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:cursor-not-allowed disabled:opacity-50'
const dangerButton = 'inline-flex min-h-10 items-center justify-center rounded-full border border-red-200 bg-red-50 px-4 text-sm font-bold text-red-800 transition hover:bg-red-100 focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-red-700 disabled:cursor-not-allowed disabled:opacity-50'

function MarinePageFrame({ title, intro, children }: { title: string; intro: string; children: ReactNode }) {
  return <div className="flex min-h-screen flex-col bg-coast-paper font-sans text-coast-ink">
    <SiteHeader />
    <main className="mx-auto grid w-full max-w-[90rem] flex-1 grid-cols-1 content-start gap-6 px-4 py-6 sm:px-8 sm:py-10 lg:grid-cols-[15rem_minmax(0,1fr)] lg:items-start lg:gap-8 lg:px-6 lg:py-0">
      <AccountAreaNavigation active="marine" />
      <div className="min-w-0 lg:py-12">
        <header className="mb-7 sm:mb-9">
          <p className="text-[11px] font-extrabold tracking-[0.18em] text-coast-blue">MARINE CONDITIONS &amp; SAFETY</p>
          <h1 className="mt-2 break-words font-display text-4xl leading-tight tracking-[-0.05em] sm:text-5xl">{title}</h1>
          <p className="mt-3 max-w-3xl text-sm leading-6 text-coast-muted sm:text-base">{intro}</p>
        </header>
        {children}
      </div>
    </main>
    <SiteFooter />
  </div>
}

function MarineErrorMessage({ children }: { children: string }) {
  return <p className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm leading-6 text-red-900" role="alert">{children}</p>
}

function MarineNotice({ children }: { children: string }) {
  return <p className="rounded-2xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm leading-6 text-emerald-900" role="status">{children}</p>
}

function getMarineError(error: unknown) {
  return error instanceof MarineApiError ? error.message : 'We couldn’t reach the marine service just now. Please try again.'
}

const unitByFactor: Record<string, string> = {
  windSpeed: 'km/h',
  waveHeight: 'm',
  swellHeight: 'm',
  rain: 'mm',
  weatherCode: '',
}

const factorLabels: Record<string, string> = {
  windSpeed: 'Wind speed',
  waveHeight: 'Wave height',
  swellHeight: 'Swell height',
  rain: 'Rainfall',
  weatherCode: 'Weather code',
  freshConditions: 'Fresh condition evidence',
}

function formatFactor(field: string) {
  return factorLabels[field] ?? field
}

function factorValue(snapshot: MarineConditionSnapshot, field: string): string {
  const value = field === 'windSpeed' ? snapshot.windSpeed
    : field === 'waveHeight' ? snapshot.waveHeight
    : field === 'swellHeight' ? snapshot.swellHeight
    : field === 'rain' ? snapshot.rain
    : field === 'weatherCode' ? snapshot.weatherCode
    : null
  if (value === null || value === undefined) return 'Not reported'
  const unit = unitByFactor[field]
  return `${value}${unit ? ` ${unit}` : ''}`
}

function formatTimestamp(value: string | null) {
  if (!value) return '—'
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return value
  return parsed.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' }) + ' UTC'
}

function FreshnessChip({ freshness }: { freshness: string | null | undefined }) {
  const status = freshness ?? 'UNAVAILABLE'
  const tone = status === 'FRESH'
    ? 'bg-emerald-50 text-emerald-800'
    : status === 'STALE'
      ? 'bg-coast-sand text-coast-deep'
      : 'bg-red-50 text-red-900'
  return <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-[10px] font-extrabold tracking-wide ${tone}`}>{status}</span>
}

function SnapshotEvidencePanel({ snapshot }: { snapshot: MarineConditionSnapshot }) {
  const factors = ['windSpeed', 'waveHeight', 'swellHeight', 'rain', 'weatherCode']
  return <div className="mt-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
    {factors.map((factor) => <div className="rounded-2xl bg-coast-paper p-4" key={factor}>
      <p className="text-xs font-bold text-coast-muted">{formatFactor(factor)}</p>
      <p className="mt-1 font-display text-xl text-coast-ink">{factorValue(snapshot, factor)}</p>
    </div>)}
    <dl className="rounded-2xl bg-coast-paper p-4 text-xs leading-5 text-coast-muted sm:col-span-2 lg:col-span-3">
      <div className="flex flex-wrap gap-x-8 gap-y-1">
        <div><dt className="font-bold text-coast-deep">Source</dt><dd>{snapshot.source}</dd></div>
        <div><dt className="font-bold text-coast-deep">Forecast / observation time</dt><dd>{formatTimestamp(snapshot.forecastTime)}</dd></div>
        <div><dt className="font-bold text-coast-deep">Retrieved</dt><dd>{formatTimestamp(snapshot.retrievedAt)}</dd></div>
        <div><dt className="font-bold text-coast-deep">Freshness</dt><dd>{snapshot.freshnessStatus}</dd></div>
      </div>
      <p className="mt-3">
        Forecast time and retrieval time are different facts; this evidence is decision support, not a substitute for professional maritime navigation information.
      </p>
    </dl>
  </div>
}

function StatusResultCard({ result }: { result: MarineSuitabilityResult }) {
  const tone = result.status === 'SUITABLE'
    ? 'border-emerald-200 bg-emerald-50'
    : result.status === 'CAUTION'
      ? 'border-coast-glass bg-coast-sand'
      : result.status === 'UNSUITABLE'
        ? 'border-red-200 bg-red-50'
        : 'border-coast-line bg-coast-paper'
  return <section aria-live="polite" className={`rounded-3xl border p-6 ${tone}`}>
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div>
        <p className="text-xs font-extrabold tracking-[0.15em] text-coast-deep">SERVER ASSESSMENT</p>
        <h3 className="mt-1 font-display text-2xl tracking-[-0.03em]">{result.activityName}</h3>
      </div>
      <span className="rounded-full bg-white/80 px-4 py-2 font-display text-lg font-bold text-coast-ink">{result.status}</span>
    </div>
    <p className="mt-3 text-sm leading-6 text-coast-ink">
      Evaluated for {result.location.latitude}, {result.location.longitude} at {formatTimestamp(result.requestedTime)}.
      The classification is calculated on the server from configured safety rules; this page never reclassifies it.
    </p>
    {result.violations.length > 0 && <div className="mt-4">
      <p className="text-xs font-extrabold tracking-[0.12em] text-red-900">LIMITS EXCEEDED</p>
      <ul className="mt-2 grid gap-1 text-sm text-red-900">{result.violations.map((violation) => <li key={violation}>{violation}</li>)}</ul>
    </div>}
    {result.cautionFactors.length > 0 && <div className="mt-4">
      <p className="text-xs font-extrabold tracking-[0.12em] text-coast-deep">CAUTION FACTORS</p>
      <ul className="mt-2 grid gap-1 text-sm text-coast-ink">{result.cautionFactors.map((factor) => <li key={factor}>{formatFactor(factor)} is within the configured caution band.</li>)}</ul>
    </div>}
    {result.missingFields.length > 0 && <div className="mt-4">
      <p className="text-xs font-extrabold tracking-[0.12em] text-coast-muted">MISSING OR STALE EVIDENCE</p>
      <ul className="mt-2 grid gap-1 text-sm text-coast-muted">{result.missingFields.map((field) => <li key={field}>{formatFactor(field)} was unavailable, so it could not support a positive classification.</li>)}</ul>
    </div>}
    <p className="mt-4 text-xs leading-5 text-coast-muted">
      Source: {result.source ?? 'not available'} · Retrieved: {formatTimestamp(result.retrievedAt)} · Freshness: {result.freshness ?? 'unknown'}
      {result.freshness !== 'FRESH' ? ' · Stale or incomplete evidence yields UNKNOWN, never a fabricated result.' : ''}
    </p>
  </section>
}

const COORDINATE_PATTERN = /^-?\d{1,3}(?:\.\d{1,5})?$/

function parseCoordinate(raw: string, min: number, max: number): number | null {
  if (!COORDINATE_PATTERN.test(raw.trim())) return null
  const value = Number(raw)
  if (!Number.isFinite(value) || value < min || value > max) return null
  return value
}

function toUtcTimestamp(raw: string): string | null {
  if (!raw) return ''
  const parsed = new Date(`${raw}:00Z`)
  return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString()
}

export function MarineConditionsPage() {
  const [latitude, setLatitude] = useState('6.025')
  const [longitude, setLongitude] = useState('80.216')
  const [dateTime, setDateTime] = useState('')
  const [activityId, setActivityId] = useState(MARINE_ACTIVITY_REFERENCES[0].id)
  const [snapshot, setSnapshot] = useState<MarineConditionSnapshot | null>(null)
  const [result, setResult] = useState<MarineSuitabilityResult | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const parsedLatitude = parseCoordinate(latitude, -90, 90)
    const parsedLongitude = parseCoordinate(longitude, -180, 180)
    if (parsedLatitude === null || parsedLongitude === null) {
      setFormError('Enter a valid latitude (−90 to 90) and longitude (−180 to 180) in decimal degrees.')
      return
    }
    if (dateTime) {
      const timestamp = toUtcTimestamp(dateTime)
      if (timestamp === null) {
        setFormError('Enter the requested date and time as a valid UTC moment.')
        return
      }
    }
    setFormError(null)
    setError(null)
    setLoading(true)
    try {
      const requestedTime = dateTime ? toUtcTimestamp(dateTime) : undefined
      const suitability = await evaluateMarineSuitability({
        activityId,
        latitude: parsedLatitude,
        longitude: parsedLongitude,
        dateTime: requestedTime || undefined,
      })
      if (!suitability.conditions || !suitability.source || !suitability.freshness || !suitability.retrievedAt || !suitability.forecastTime) {
        throw new MarineApiError(502, 'The assessment did not include its matching condition evidence. Please retry.')
      }
      const conditions: MarineConditionSnapshot = {
        id: suitability.snapshotId,
        latitude: suitability.location.latitude,
        longitude: suitability.location.longitude,
        forecastTime: suitability.forecastTime,
        retrievedAt: suitability.retrievedAt,
        ...suitability.conditions,
        source: suitability.source,
        freshnessStatus: suitability.freshness,
        missingFields: suitability.missingFields,
      }
      setSnapshot(conditions)
      setResult(suitability)
    } catch (reason) {
      setError(getMarineError(reason))
    } finally {
      setLoading(false)
    }
  }

  return <MarinePageFrame
    intro="Look up coastal weather and marine evidence for a place and time, then see the server’s deterministic suitability result for a coastal activity. Conditions come from the backend Open-Meteo integration; classifications are never recalculated here."
    title="Marine conditions"
  >
    <div className="grid gap-5">
      <section className={cardClass}>
        <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">CONDITION QUERY</p>
        <form className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-4" onSubmit={submit}>
          <label className="text-sm font-bold" htmlFor="marine-latitude">Latitude
            <input className={inputClass} id="marine-latitude" inputMode="decimal" onChange={(event) => setLatitude(event.target.value)} required value={latitude} />
          </label>
          <label className="text-sm font-bold" htmlFor="marine-longitude">Longitude
            <input className={inputClass} id="marine-longitude" inputMode="decimal" onChange={(event) => setLongitude(event.target.value)} required value={longitude} />
          </label>
          <label className="text-sm font-bold" htmlFor="marine-datetime">Requested time <span className="font-normal text-coast-muted">(UTC, optional)</span>
            <input className={inputClass} id="marine-datetime" onChange={(event) => setDateTime(event.target.value)} type="datetime-local" value={dateTime} />
          </label>
          <label className="text-sm font-bold" htmlFor="marine-activity">Activity
            <select className={inputClass} id="marine-activity" onChange={(event) => setActivityId(event.target.value)} value={activityId}>
              {MARINE_ACTIVITY_REFERENCES.map((activity) => <option key={activity.id} value={activity.id}>{activity.name}</option>)}
            </select>
          </label>
          <div className="flex items-end md:col-span-2 xl:col-span-4">
            <button className={primaryButton} disabled={loading} type="submit">{loading ? 'Checking the coast…' : 'Check conditions & suitability'}</button>
          </div>
        </form>
        {formError && <div className="mt-4"><MarineErrorMessage>{formError}</MarineErrorMessage></div>}
        {error && <div className="mt-4"><MarineErrorMessage>{error}</MarineErrorMessage></div>}
      </section>

      {result && <StatusResultCard result={result} />}
      {snapshot && <section className={cardClass} aria-label="Condition evidence">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-coast-line pb-4">
          <div>
            <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">CONDITION EVIDENCE</p>
            <h2 className="mt-1 font-display text-2xl tracking-[-0.035em]">{snapshot.latitude}, {snapshot.longitude}</h2>
          </div>
          <FreshnessChip freshness={snapshot.freshnessStatus} />
        </div>
        <SnapshotEvidencePanel snapshot={snapshot} />
      </section>}
      {!result && !snapshot && !loading && !error && !formError && <section className={cardClass}>
        <p className="text-sm leading-6 text-coast-muted">
          Enter a coastal location — for example 6.025, 80.216 near Weligama — and check the sea before you plan. Missing or stale evidence is always labelled rather than silently treated as safe.
        </p>
      </section>}
      <section className="rounded-3xl bg-coast-sage/70 p-5 text-sm leading-6 text-coast-deep">
        Suitability results are decision support from configured safety profiles. They are not a marine navigation service, an official closure authority or an autonomous safety decision.
      </section>
    </div>
  </MarinePageFrame>
}

export function MarineHistoryPage() {
  const [latitude, setLatitude] = useState('')
  const [longitude, setLongitude] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [activityId, setActivityId] = useState('')
  const [resultFilter, setResultFilter] = useState('')
  const [snapshots, setSnapshots] = useState<MarineConditionSnapshot[]>([])
  const [assessments, setAssessments] = useState<MarineAssessmentHistory[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    void Promise.allSettled([getMarineHistory(), getMarineAssessments()]).then(([conditionRows, assessmentRows]) => {
      if (!active) return
      if (conditionRows.status === 'fulfilled') setSnapshots(conditionRows.value)
      if (assessmentRows.status === 'fulfilled') setAssessments(assessmentRows.value)
      const failed = [conditionRows, assessmentRows].find((row) => row.status === 'rejected')
      if (failed?.status === 'rejected') setError(getMarineError(failed.reason))
    }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])

  async function search(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const parsedLatitude = latitude.trim() ? parseCoordinate(latitude, -90, 90) : ('' as const)
    const parsedLongitude = longitude.trim() ? parseCoordinate(longitude, -180, 180) : ('' as const)
    if (parsedLatitude === null || parsedLongitude === null) {
      setFormError('Enter a valid latitude (−90 to 90) and longitude (−180 to 180) in decimal degrees, or leave both blank.')
      return
    }
    const fromTimestamp = from ? toUtcTimestamp(from) : ''
    const toTimestamp = to ? toUtcTimestamp(to) : ''
    if (fromTimestamp === null || toTimestamp === null) {
      setFormError('Enter the history window as valid UTC dates and times.')
      return
    }
    if (fromTimestamp && toTimestamp && new Date(fromTimestamp) > new Date(toTimestamp)) {
      setFormError('The From time must be earlier than or equal to the To time.')
      return
    }
    setFormError(null)
    setError(null)
    setLoading(true)
    try {
      const [items, assessmentItems] = await Promise.all([
        getMarineHistory({
        ...(parsedLatitude === '' ? {} : { latitude: parsedLatitude }),
        ...(parsedLongitude === '' ? {} : { longitude: parsedLongitude }),
        ...(from ? { from: fromTimestamp } : {}),
        ...(to ? { to: toTimestamp } : {}),
        }),
        getMarineAssessments({
          ...(activityId ? { activityId } : {}),
          ...(resultFilter ? { result: resultFilter } : {}),
          ...(from ? { from: fromTimestamp } : {}),
          ...(to ? { to: toTimestamp } : {}),
        }),
      ])
      setSnapshots(items)
      setAssessments(assessmentItems)
    } catch (reason) {
      setError(getMarineError(reason))
    } finally {
      setLoading(false)
    }
  }

  return <MarinePageFrame
    intro="Review stored condition readings and past suitability assessments with the source evidence and exact reviewed limits that produced each result."
    title="Marine history"
  >
    <div className="grid gap-5">
      <section className={cardClass}>
        <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">FILTER HISTORY</p>
        <form className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-4" onSubmit={search}>
          <label className="text-sm font-bold" htmlFor="history-latitude">Latitude
            <input className={inputClass} id="history-latitude" inputMode="decimal" onChange={(event) => setLatitude(event.target.value)} value={latitude} />
          </label>
          <label className="text-sm font-bold" htmlFor="history-longitude">Longitude
            <input className={inputClass} id="history-longitude" inputMode="decimal" onChange={(event) => setLongitude(event.target.value)} value={longitude} />
          </label>
          <label className="text-sm font-bold" htmlFor="history-from">From <span className="font-normal text-coast-muted">(UTC)</span>
            <input className={inputClass} id="history-from" onChange={(event) => setFrom(event.target.value)} type="datetime-local" value={from} />
          </label>
          <label className="text-sm font-bold" htmlFor="history-to">To <span className="font-normal text-coast-muted">(UTC)</span>
            <input className={inputClass} id="history-to" onChange={(event) => setTo(event.target.value)} type="datetime-local" value={to} />
          </label>
          <label className="text-sm font-bold" htmlFor="history-activity">Assessment activity
            <select className={inputClass} id="history-activity" onChange={(event) => setActivityId(event.target.value)} value={activityId}>
              <option value="">All activities</option>
              {MARINE_ACTIVITY_REFERENCES.map((activity) => <option key={activity.id} value={activity.id}>{activity.name}</option>)}
            </select>
          </label>
          <label className="text-sm font-bold" htmlFor="history-result">Assessment result
            <select className={inputClass} id="history-result" onChange={(event) => setResultFilter(event.target.value)} value={resultFilter}>
              <option value="">All results</option>
              {['SUITABLE', 'CAUTION', 'UNSUITABLE', 'UNKNOWN'].map((result) => <option key={result} value={result}>{result}</option>)}
            </select>
          </label>
          <div className="flex items-end">
            <button className={secondaryButton} disabled={loading} type="submit">Apply filters</button>
          </div>
        </form>
        {formError && <div className="mt-4"><MarineErrorMessage>{formError}</MarineErrorMessage></div>}
        {error && <div className="mt-4"><MarineErrorMessage>{error}</MarineErrorMessage></div>}
      </section>

      <section className={cardClass}>
        <div className="flex flex-wrap items-end justify-between gap-4 border-b border-coast-line pb-5">
          <div><p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">STORED SNAPSHOTS</p><h2 className="mt-2 font-display text-2xl tracking-[-0.035em]">Past condition evidence</h2></div>
          <span className="rounded-full bg-coast-sage px-3 py-1.5 text-xs font-extrabold text-coast-deep">{snapshots.length} snapshots</span>
        </div>
        {loading ? null : snapshots.length === 0
          ? <p className="py-5 text-sm text-coast-muted">No condition snapshots match these filters yet. Run a condition query to store the first one.</p>
          : <ul className="divide-y divide-coast-line">
            {snapshots.map((snapshot) => <li className="grid gap-2 py-4" key={snapshot.id}>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="font-bold text-coast-deep">{snapshot.latitude}, {snapshot.longitude}</p>
                <FreshnessChip freshness={snapshot.freshnessStatus} />
              </div>
              <p className="text-sm text-coast-muted">
                {formatTimestamp(snapshot.forecastTime)} · retrieved {formatTimestamp(snapshot.retrievedAt)} · {snapshot.source}
              </p>
              <p className="text-sm text-coast-ink">
                {['windSpeed', 'waveHeight', 'swellHeight', 'rain'].map((factor) => `${formatFactor(factor)}: ${factorValue(snapshot, factor)}`).join(' · ')}
              </p>
              {snapshot.missingFields.length > 0 && <p className="text-xs text-coast-muted">Missing: {snapshot.missingFields.map(formatFactor).join(', ')}</p>}
            </li>)}
          </ul>}
      </section>

      <section className={cardClass}>
        <div className="flex flex-wrap items-end justify-between gap-4 border-b border-coast-line pb-5">
          <div><p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">SUITABILITY ASSESSMENTS</p><h2 className="mt-2 font-display text-2xl tracking-[-0.035em]">Past activity decisions</h2></div>
          <span className="rounded-full bg-coast-sage px-3 py-1.5 text-xs font-extrabold text-coast-deep">{assessments.length} assessments</span>
        </div>
        {loading ? null : assessments.length === 0
          ? <p className="py-5 text-sm text-coast-muted">No suitability assessments match these filters yet.</p>
          : <ul className="divide-y divide-coast-line">
            {assessments.map((assessment) => <li className="grid gap-3 py-5" key={assessment.id}>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div><h3 className="font-bold text-coast-deep">{assessment.activityName ?? 'Activity name not recorded'} · {assessment.result}</h3><p className="mt-1 text-xs text-coast-muted">Assessment {assessment.id} · profile v{assessment.profileVersion} · snapshot {assessment.conditionSnapshotId}</p></div>
                <FreshnessChip freshness={assessment.freshnessStatus} />
              </div>
              <p className="text-sm text-coast-muted">{assessment.latitude}, {assessment.longitude} · requested {formatTimestamp(assessment.requestedTime)} · forecast {formatTimestamp(assessment.forecastTime)} · evaluated {formatTimestamp(assessment.evaluatedAt)}</p>
              <p className="text-sm text-coast-ink">Wind: {assessment.conditions?.windSpeed ?? 'Not recorded'} km/h · Wave: {assessment.conditions?.waveHeight ?? 'Not recorded'} m · Swell: {assessment.conditions?.swellHeight ?? 'Not recorded'} m · Rain: {assessment.conditions?.rain ?? 'Not recorded'} mm · {assessment.source || 'source not recorded'} · retrieved {formatTimestamp(assessment.conditionRetrievedAt)}</p>
              {assessment.evidenceCompleteness !== 'COMPLETE' && <p className="rounded-xl bg-coast-sand px-3 py-2 text-xs leading-5 text-coast-deep">Some evidence predates evidence snapshots and cannot be reconstructed reliably.</p>}
              {(assessment.violations ?? []).length > 0 && <p className="text-sm text-red-800">Exceeded limits: {assessment.violations.join('; ')}</p>}
              {(assessment.cautionFactors ?? []).length > 0 && <p className="text-sm text-coast-deep">Caution factors: {assessment.cautionFactors.map(formatFactor).join(', ')}</p>}
              {(assessment.missingFields ?? []).length > 0 && <p className="text-xs text-coast-muted">Assessment missing: {assessment.missingFields.map(formatFactor).join(', ')}</p>}
              {(assessment.criteria ?? []).length === 0
                ? <p className="text-xs leading-5 text-coast-muted">Safety limits and their sources and rationales were not recorded for this assessment.</p>
                : (assessment.criteria ?? []).map((criterion) => <p className="text-xs leading-5 text-coast-muted" key={criterion.factor}>
                  {formatFactor(criterion.factor)} limit {criterion.maximum === null ? 'not recorded' : `${criterion.maximum}${unitByFactor[criterion.factor] ? ` ${unitByFactor[criterion.factor]}` : ''}`} · {criterion.source ?? 'source not recorded'} · {criterion.rationale ?? 'rationale not recorded'}
                </p>)}
            </li>)}
          </ul>}
      </section>
    </div>
  </MarinePageFrame>
}

type ProfileDraft = {
  maxWindSpeed: string
  maxWaveHeight: string
  maxSwellHeight: string
  cautionWindSpeed: string
  cautionWaveHeight: string
  cautionSwellHeight: string
  windCriteriaSource: string
  windCriteriaRationale: string
  waveCriteriaSource: string
  waveCriteriaRationale: string
  swellCriteriaSource: string
  swellCriteriaRationale: string
}

const emptyDraft: ProfileDraft = {
  maxWindSpeed: '',
  maxWaveHeight: '',
  maxSwellHeight: '',
  cautionWindSpeed: '',
  cautionWaveHeight: '',
  cautionSwellHeight: '',
  windCriteriaSource: '',
  windCriteriaRationale: '',
  waveCriteriaSource: '',
  waveCriteriaRationale: '',
  swellCriteriaSource: '',
  swellCriteriaRationale: '',
}

function profileToDraft(profile: MarineSafetyProfile): ProfileDraft {
  return {
    maxWindSpeed: String(profile.maxWindSpeed),
    maxWaveHeight: String(profile.maxWaveHeight),
    maxSwellHeight: String(profile.maxSwellHeight),
    cautionWindSpeed: profile.cautionWindSpeed === null ? '' : String(profile.cautionWindSpeed),
    cautionWaveHeight: profile.cautionWaveHeight === null ? '' : String(profile.cautionWaveHeight),
    cautionSwellHeight: profile.cautionSwellHeight === null ? '' : String(profile.cautionSwellHeight),
    windCriteriaSource: profile.windCriteriaSource ?? '',
    windCriteriaRationale: profile.windCriteriaRationale ?? '',
    waveCriteriaSource: profile.waveCriteriaSource ?? '',
    waveCriteriaRationale: profile.waveCriteriaRationale ?? '',
    swellCriteriaSource: profile.swellCriteriaSource ?? '',
    swellCriteriaRationale: profile.swellCriteriaRationale ?? '',
  }
}

const profileCriteria = [
  { factor: 'Wind', source: 'windCriteriaSource', rationale: 'windCriteriaRationale' },
  { factor: 'Wave', source: 'waveCriteriaSource', rationale: 'waveCriteriaRationale' },
  { factor: 'Swell', source: 'swellCriteriaSource', rationale: 'swellCriteriaRationale' },
] as const

function parsePositiveLimit(raw: string, max: number): number | null {
  if (!COORDINATE_PATTERN.test(raw.trim())) return null
  const value = Number(raw)
  if (!Number.isFinite(value) || value <= 0 || value > max) return null
  return value
}

function SafetyProfileForm({ idPrefix, profile, draft, onDraftChange, onSubmit, busy, submitLabel, leading }: {
  idPrefix: string
  profile?: MarineSafetyProfile | null
  draft: ProfileDraft
  onDraftChange: (draft: ProfileDraft) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  busy: boolean
  submitLabel: string
  leading?: ReactNode
}) {
  const field = (key: keyof ProfileDraft) => (event: { target: { value: string } }) =>
    onDraftChange({ ...draft, [key]: event.target.value })

  return <form className="mt-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-3" onSubmit={onSubmit}>
    {leading && <div className="sm:col-span-2 xl:col-span-3">{leading}</div>}
    <label className="text-sm font-bold" htmlFor={`${idPrefix}-max-wind`}>Maximum wind speed <span className="font-normal text-coast-muted">(km/h)</span>
      <input className={inputClass} disabled={busy} id={`${idPrefix}-max-wind`} inputMode="decimal" max={1000} min={0.01} onChange={field('maxWindSpeed')} required step="any" type="number" value={draft.maxWindSpeed} />
    </label>
    <label className="text-sm font-bold" htmlFor={`${idPrefix}-max-wave`}>Maximum wave height <span className="font-normal text-coast-muted">(m)</span>
      <input className={inputClass} disabled={busy} id={`${idPrefix}-max-wave`} inputMode="decimal" max={50} min={0.01} onChange={field('maxWaveHeight')} required step="any" type="number" value={draft.maxWaveHeight} />
    </label>
    <label className="text-sm font-bold" htmlFor={`${idPrefix}-max-swell`}>Maximum swell height <span className="font-normal text-coast-muted">(m)</span>
      <input className={inputClass} disabled={busy} id={`${idPrefix}-max-swell`} inputMode="decimal" max={50} min={0.01} onChange={field('maxSwellHeight')} required step="any" type="number" value={draft.maxSwellHeight} />
    </label>
    <label className="text-sm font-bold" htmlFor={`${idPrefix}-caution-wind`}>Caution wind speed <span className="font-normal text-coast-muted">(optional)</span>
      <input className={inputClass} disabled={busy} id={`${idPrefix}-caution-wind`} inputMode="decimal" max={1000} min={0.01} onChange={field('cautionWindSpeed')} step="any" type="number" value={draft.cautionWindSpeed} />
    </label>
    <label className="text-sm font-bold" htmlFor={`${idPrefix}-caution-wave`}>Caution wave height <span className="font-normal text-coast-muted">(optional)</span>
      <input className={inputClass} disabled={busy} id={`${idPrefix}-caution-wave`} inputMode="decimal" max={50} min={0.01} onChange={field('cautionWaveHeight')} step="any" type="number" value={draft.cautionWaveHeight} />
    </label>
    <label className="text-sm font-bold" htmlFor={`${idPrefix}-caution-swell`}>Caution swell height <span className="font-normal text-coast-muted">(optional)</span>
      <input className={inputClass} disabled={busy} id={`${idPrefix}-caution-swell`} inputMode="decimal" max={50} min={0.01} onChange={field('cautionSwellHeight')} step="any" type="number" value={draft.cautionSwellHeight} />
    </label>
    <div className="grid gap-3 sm:col-span-2 xl:col-span-3">
      <h3 className="font-display text-lg">Evidence for each limit</h3>
      <p className="text-sm leading-6 text-coast-muted">Cite the source that supports each wind, wave and swell limit, then explain why it applies to this activity. A second authorized manager must review the draft before it can be used.</p>
      {profileCriteria.map(({ factor, source, rationale }) => <fieldset className="grid gap-3 rounded-2xl border border-coast-line p-4 md:grid-cols-2" key={factor}>
        <legend className="px-2 text-sm font-extrabold text-coast-deep">{factor} criteria</legend>
        <label className="text-sm font-bold" htmlFor={`${idPrefix}-${source}`}>Source reference
          <input className={inputClass} disabled={busy} id={`${idPrefix}-${source}`} maxLength={512} minLength={3} onChange={field(source)} placeholder="Publication, authority or URL" required value={draft[source]} />
        </label>
        <label className="text-sm font-bold" htmlFor={`${idPrefix}-${rationale}`}>Rationale
          <textarea className={`${inputClass} min-h-24`} disabled={busy} id={`${idPrefix}-${rationale}`} maxLength={2000} minLength={10} onChange={field(rationale)} placeholder={`Why this ${factor.toLowerCase()} limit is appropriate`} required value={draft[rationale]} />
        </label>
      </fieldset>)}
    </div>
    <div className="flex flex-wrap items-center gap-3 sm:col-span-2 xl:col-span-3">
      <button className={primaryButton} disabled={busy} type="submit">{submitLabel}</button>
      {profile && <p className="text-xs text-coast-muted">Saving creates a new immutable profile version and leaves the current approved version in place until review.</p>}
    </div>
  </form>
}

export function MarineSafetyProfilesPage() {
  const { user } = useAuthSession()
  const canManage = hasAllPermissions(user, ['marine.profile.read', 'marine.profile.manage'])
  const [profiles, setProfiles] = useState<MarineSafetyProfile[]>([])
  const [selectedId, setSelectedId] = useState('')
  const [createActivityId, setCreateActivityId] = useState(MARINE_ACTIVITY_REFERENCES[0].id)
  const [createDraft, setCreateDraft] = useState<ProfileDraft>(emptyDraft)
  const [editDraft, setEditDraft] = useState<ProfileDraft>(emptyDraft)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const selected = profiles.find((profile) => profile.id === selectedId) ?? null
  const selectedHasProvenance = Boolean(
    selected?.windCriteriaSource?.trim() && selected.windCriteriaRationale?.trim() &&
    selected.waveCriteriaSource?.trim() && selected.waveCriteriaRationale?.trim() &&
    selected.swellCriteriaSource?.trim() && selected.swellCriteriaRationale?.trim(),
  )
  const profiledActivityIds = useMemo(
    () => new Set(profiles.filter((profile) => profile.isActive).map((profile) => profile.activityId)),
    [profiles],
  )

  useEffect(() => {
    let active = true
    void getMarineSafetyProfiles().then((items) => {
      if (!active) return
      setProfiles(items)
      const initialProfile = items[0] ?? null
      setSelectedId(initialProfile?.id ?? '')
      setEditDraft(initialProfile ? profileToDraft(initialProfile) : emptyDraft)
    }).catch((reason: unknown) => {
      if (active) setError(getMarineError(reason))
    }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])

  function parseDraft(draft: ProfileDraft): {
    limits: { maxWindSpeed: number; maxWaveHeight: number; maxSwellHeight: number }
    caution: { cautionWindSpeed?: number; cautionWaveHeight?: number; cautionSwellHeight?: number }
    criteria: Pick<ProfileDraft, 'windCriteriaSource' | 'windCriteriaRationale' | 'waveCriteriaSource' | 'waveCriteriaRationale' | 'swellCriteriaSource' | 'swellCriteriaRationale'>
  } | { error: string } {
    const maxWind = parsePositiveLimit(draft.maxWindSpeed, 1000)
    const maxWave = parsePositiveLimit(draft.maxWaveHeight, 50)
    const maxSwell = parsePositiveLimit(draft.maxSwellHeight, 50)
    if (maxWind === null || maxWave === null || maxSwell === null) {
      return { error: 'Enter positive maximum limits: wind up to 1000 km/h, wave and swell up to 50 m.' }
    }
    const cautionWind = draft.cautionWindSpeed.trim() ? parsePositiveLimit(draft.cautionWindSpeed, 1000) : ('' as const)
    const cautionWave = draft.cautionWaveHeight.trim() ? parsePositiveLimit(draft.cautionWaveHeight, 50) : ('' as const)
    const cautionSwell = draft.cautionSwellHeight.trim() ? parsePositiveLimit(draft.cautionSwellHeight, 50) : ('' as const)
    if (cautionWind === null || cautionWave === null || cautionSwell === null) {
      return { error: 'Optional caution limits must be positive numbers below their maximum.' }
    }
    for (const criterion of profileCriteria) {
      const source = draft[criterion.source].trim()
      const rationale = draft[criterion.rationale].trim()
      if (source.length < 3 || source.length > 512) return { error: `Add a source reference of 3 to 512 characters for the ${criterion.factor.toLowerCase()} limit.` }
      if (rationale.length < 10 || rationale.length > 2000) return { error: `Add a rationale of 10 to 2000 characters for the ${criterion.factor.toLowerCase()} limit.` }
    }
    return {
      limits: { maxWindSpeed: maxWind, maxWaveHeight: maxWave, maxSwellHeight: maxSwell },
      criteria: {
        windCriteriaSource: draft.windCriteriaSource.trim(),
        windCriteriaRationale: draft.windCriteriaRationale.trim(),
        waveCriteriaSource: draft.waveCriteriaSource.trim(),
        waveCriteriaRationale: draft.waveCriteriaRationale.trim(),
        swellCriteriaSource: draft.swellCriteriaSource.trim(),
        swellCriteriaRationale: draft.swellCriteriaRationale.trim(),
      },
      caution: {
        ...(cautionWind === '' ? {} : { cautionWindSpeed: cautionWind }),
        ...(cautionWave === '' ? {} : { cautionWaveHeight: cautionWave }),
        ...(cautionSwell === '' ? {} : { cautionSwellHeight: cautionSwell }),
      },
    }
  }

  async function createProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const parsed = parseDraft(createDraft)
    if ('error' in parsed) { setError(parsed.error); return }
    setBusy(true); setError(null); setNotice(null)
    try {
      const created = await createMarineSafetyProfile({ activityId: createActivityId, ...parsed.limits, ...parsed.criteria, ...parsed.caution })
      const items = await getMarineSafetyProfiles()
      setProfiles(items)
      setSelectedId(created.id)
      setEditDraft(profileToDraft(items.find((profile) => profile.id === created.id) ?? created))
      setCreateDraft(emptyDraft)
      setNotice(`Version ${created.version} for ${created.activityName} is saved and awaiting review. Current approved limits remain in effect until it is approved.`)
    } catch (reason) { setError(getMarineError(reason)) } finally { setBusy(false) }
  }

  async function saveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selected || !canManage) return
    const parsed = parseDraft(editDraft)
    if ('error' in parsed) { setError(parsed.error); return }
    setBusy(true); setError(null); setNotice(null)
    try {
      const updated = await updateMarineSafetyProfile(selected.id, { ...parsed.limits, ...parsed.criteria, ...parsed.caution })
      const items = await getMarineSafetyProfiles()
      setProfiles(items)
      setSelectedId(updated.id)
      setEditDraft(profileToDraft(items.find((profile) => profile.id === updated.id) ?? updated))
      setNotice(`Version ${updated.version} for ${updated.activityName} is saved and awaiting review. The earlier version remains unchanged.`)
    } catch (reason) { setError(getMarineError(reason)) } finally { setBusy(false) }
  }

  async function reviewProfile() {
    if (!selected || !canManage || selected.reviewedAt || busy) return
    setBusy(true); setError(null); setNotice(null)
    try {
      const approved = await reviewMarineSafetyProfile(selected.id)
      const items = await getMarineSafetyProfiles()
      setProfiles(items)
      setSelectedId(approved.id)
      setEditDraft(profileToDraft(items.find((profile) => profile.id === approved.id) ?? approved))
      setNotice(`Version ${approved.version} for ${approved.activityName} is approved and effective from ${formatTimestamp(approved.effectiveFrom)}.`)
    } catch (reason) { setError(getMarineError(reason)) } finally { setBusy(false) }
  }

  async function deactivateProfile() {
    if (!selected || !canManage || !window.confirm(`Deactivate the ${selected.activityName} safety profile? Assessments keep their history; the activity cannot be evaluated until a new profile is active.`)) return
    setBusy(true); setError(null); setNotice(null)
    try {
      await deactivateMarineSafetyProfile(selected.id)
      const items = await getMarineSafetyProfiles()
      setProfiles(items)
      const nextProfile = items.find((profile) => profile.id === selected.id) ?? items[0] ?? null
      setSelectedId(nextProfile?.id ?? '')
      setEditDraft(nextProfile ? profileToDraft(nextProfile) : emptyDraft)
      setNotice(`The ${selected.activityName} safety profile was deactivated. Assessments keep their history.`)
    } catch (reason) { setError(getMarineError(reason)) } finally { setBusy(false) }
  }

  return <MarinePageFrame
    intro="Review activity limits with their cited basis. Each change creates an immutable draft; a different authorized manager must approve the cited wind, wave and swell criteria before a version can be used."
    title="Safety profiles"
  >
    <div className="grid gap-5">
      {error && <MarineErrorMessage>{error}</MarineErrorMessage>}
      {notice && <MarineNotice>{notice}</MarineNotice>}

      {canManage && <section className={cardClass}>
        <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">NEW PROFILE VERSION</p>
        <p className="mt-2 text-sm text-coast-muted">Creating a draft keeps the current approved limits in effect. A different authorized manager must review the new version before it can be used.</p>
        <SafetyProfileForm
          busy={busy}
          draft={createDraft}
          idPrefix="create-profile"
          leading={<label className="block text-sm font-bold" htmlFor="create-activity">Activity
            <select className={inputClass} disabled={busy} id="create-activity" onChange={(event) => setCreateActivityId(event.target.value)} value={createActivityId}>
              {MARINE_ACTIVITY_REFERENCES.map((activity) => <option key={activity.id} value={activity.id}>{activity.name}{profiledActivityIds.has(activity.id) ? ' · has an active profile' : ''}</option>)}
            </select>
          </label>}
          onDraftChange={setCreateDraft}
          onSubmit={createProfile}
          profile={null}
          submitLabel="Create profile"
        />
      </section>}

      <div className="grid min-w-0 gap-5 xl:grid-cols-[minmax(15rem,0.8fr)_minmax(0,1.4fr)]">
        <section aria-label="Safety profile list" className={`${cardClass} self-start`}>
          <div className="flex items-center justify-between gap-3 border-b border-coast-line pb-4"><h2 className="font-display text-xl">Profiles</h2><span className="rounded-full bg-coast-sage px-3 py-1 text-xs font-bold text-coast-deep">{profiles.length}</span></div>
          {loading ? null : profiles.length === 0
            ? <p className="py-5 text-sm text-coast-muted">No safety profiles are configured yet{canManage ? '. Create the first one above.' : '.'}</p>
            : <ul className="mt-3 grid gap-2">
              {profiles.map((profile) => <li key={profile.id}>
                <button aria-current={profile.id === selectedId ? 'true' : undefined} className={`w-full rounded-2xl border px-4 py-3 text-left transition ${profile.id === selectedId ? 'border-coast-blue bg-coast-sage/70' : 'border-transparent hover:border-coast-line hover:bg-coast-paper'}`} onClick={() => { setSelectedId(profile.id); setEditDraft(profileToDraft(profile)) }} type="button">
                  <span className="flex flex-wrap items-center justify-between gap-2">
                    <span className="break-words font-bold text-coast-deep">{profile.activityName}</span>
                    <span className={`shrink-0 rounded-full px-2 py-1 text-[10px] font-extrabold ${profile.isActive && profile.reviewedAt ? 'bg-emerald-50 text-emerald-800' : 'bg-coast-paper text-coast-muted'}`}>{profile.isActive && profile.reviewedAt ? 'ACTIVE' : profile.reviewedAt ? 'SUPERSEDED' : 'REVIEW REQUIRED'}</span>
                  </span>
                  <span className="mt-1 block text-xs text-coast-muted">Version {profile.version} · v{profile.version} limits · updated {formatTimestamp(profile.updatedAt)}</span>
                </button>
              </li>)}
            </ul>}
        </section>

        {selected ? <section className={cardClass}>
          <div className="flex flex-wrap items-start justify-between gap-3 border-b border-coast-line pb-4">
            <div><p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">PROFILE DETAILS</p><h2 className="mt-1 break-words font-display text-2xl">{selected.activityName}</h2></div>
            <span className="rounded-full bg-coast-sand px-3 py-1.5 text-xs font-extrabold text-coast-deep">Version {selected.version}</span>
          </div>
          <div className="mt-4 grid gap-2 rounded-2xl bg-coast-paper p-4 text-sm text-coast-muted">
            <p><span className="font-bold text-coast-deep">Approval:</span> {selected.reviewedAt ? `reviewed ${formatTimestamp(selected.reviewedAt)}${selected.isActive ? ' · effective' : ''}` : 'not reviewed; this version cannot support assessments'}</p>
            {selected.effectiveFrom && <p><span className="font-bold text-coast-deep">Effective from:</span> {formatTimestamp(selected.effectiveFrom)}</p>}
            {selected.effectiveTo && <p><span className="font-bold text-coast-deep">Effective until:</span> {formatTimestamp(selected.effectiveTo)}</p>}
            <p><span className="font-bold text-coast-deep">Wind source:</span> {selected.windCriteriaSource ?? 'Not recorded'}. {selected.windCriteriaRationale ?? ''}</p>
            <p><span className="font-bold text-coast-deep">Wave source:</span> {selected.waveCriteriaSource ?? 'Not recorded'}. {selected.waveCriteriaRationale ?? ''}</p>
            <p><span className="font-bold text-coast-deep">Swell source:</span> {selected.swellCriteriaSource ?? 'Not recorded'}. {selected.swellCriteriaRationale ?? ''}</p>
          </div>
          <SafetyProfileForm busy={busy || !canManage} draft={editDraft} idPrefix="edit-profile" onDraftChange={setEditDraft} onSubmit={saveProfile} profile={selected} submitLabel="Save limits" />
          {canManage
            ? <div className="mt-6 border-t border-coast-line pt-5">
              {!selected.reviewedAt && <div className="mb-4 grid gap-2">
                {selectedHasProvenance
                  ? <>
                    <button className={primaryButton} disabled={busy || selected.createdByUserId === user?.id} onClick={() => void reviewProfile()} type="button">Review and activate this version</button>
                    {selected.createdByUserId === user?.id
                      ? <p className="text-xs text-coast-muted">A different authorized manager must review this version. Sign in with another manager account to approve it.</p>
                      : <p className="text-xs text-coast-muted">Approval makes this immutable version effective and records the review time.</p>}
                  </>
                  : <p className="text-xs text-coast-muted">This older version has no complete source record. Add source and rationale for every factor, save a new version, then have another manager review it.</p>}
              </div>}
              <button className={dangerButton} disabled={busy} onClick={() => void deactivateProfile()} type="button">Deactivate profile</button>
              <p className="mt-2 text-xs text-coast-muted">Profiles are deactivated rather than deleted so assessment history keeps its reference.</p>
            </div>
            : <p className="mt-6 rounded-2xl bg-coast-paper p-4 text-sm leading-6 text-coast-muted">Changing safety limits requires the marine profile manage permission in addition to read access.</p>}
        </section> : loading ? null : <section className={cardClass}><p className="text-sm text-coast-muted">Choose a profile to review its configured limits.</p></section>}
      </div>
    </div>
  </MarinePageFrame>
}
