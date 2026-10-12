import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { createAssessment, updateAssessmentDraft, createAlertDraft, updateAlertDraft, getOperationsFormOptions } from '../../features/coastalOperations/operationsApi'
import type { Assessment, CoastalAlert, CoastalTargetType, FormOptions, NamedReference } from '../../features/coastalOperations/operationsApi'

const emptyId = '00000000-0000-0000-0000-000000000000'
const inputClass = 'mt-2 min-h-12 w-full rounded-xl border border-coast-line bg-white px-3 text-sm focus-visible:outline-2 focus-visible:outline-coast-blue'
function offsetLabel(minutes: number | null | undefined) {
  if (typeof minutes !== 'number') return ''
  const value = Math.abs(minutes)
  return ` · UTC${minutes >= 0 ? '+' : '-'}${String(Math.floor(value / 60)).padStart(2, '0')}:${String(value % 60).padStart(2, '0')} now`
}
function localTime(value: string | undefined, zone: string) {
  if (!value) return ''
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(new Date(value))
  const part = (type: string) => parts.find((item) => item.type === type)?.value
  return `${part('year')}-${part('month')}-${part('day')}T${part('hour')}:${part('minute')}`
}
function choices(items: NamedReference[], id: string, fallback: string) {
  return id && !items.some((item) => item.id === id) ? [{ id, title: fallback }, ...items] : items
}

function DraftForm({ assessment, alert, kind, onCancel, onSaved }: { assessment?: Assessment | null; alert?: CoastalAlert | null; kind: 'assessment' | 'alert'; onCancel: () => void; onSaved: (result: Assessment | CoastalAlert) => void }) {
  const existing = assessment ?? alert
  const initialZone = existing?.timeZoneId ?? 'Etc/UTC'
  const [title, setTitle] = useState(assessment?.title || assessment?.objective.slice(0, 160) || alert?.title || '')
  const [content, setContent] = useState(assessment?.objective ?? alert?.description ?? '')
  const [targetType, setTargetType] = useState<CoastalTargetType>(existing?.targetType ?? 'DESTINATION')
  const [targetId, setTargetId] = useState(existing?.targetId === emptyId ? '' : existing?.targetId ?? '')
  const [planId, setPlanId] = useState(assessment?.sourceWorkflowId ?? '')
  const [assessmentId, setAssessmentId] = useState(alert?.assessmentId ?? '')
  const [zoneId, setZoneId] = useState(initialZone)
  const [start, setStart] = useState(() => assessment?.periodStartsLocal ?? alert?.validFromLocal ?? localTime(assessment?.periodStartsAt ?? alert?.validFrom, initialZone))
  const [end, setEnd] = useState(() => assessment?.periodEndsLocal ?? alert?.validUntilLocal ?? localTime(assessment?.periodEndsAt ?? alert?.validUntil, initialZone))
  const [severity, setSeverity] = useState(alert?.severity ?? 'MODERATE')
  const [visibility, setVisibility] = useState(alert?.visibility ?? 'OPERATIONS')
  const [options, setOptions] = useState<FormOptions | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let active = true
    void getOperationsFormOptions({ quiet: true }).then((data) => { if (active) { setOptions(data); setError(null) } }).catch(() => { if (active) setError('The selection lists could not be loaded. Retry before saving.') })
    return () => { active = false }
  }, [retry])
  async function save(event: FormEvent) {
    event.preventDefault()
    if (!options || !options.timeZones.some((zone) => zone.id === zoneId && zone.rulesAvailable)) return
    setSaving(true); setError(null)
    try {
      if (kind === 'assessment') {
        const body = { title: title.trim(), objective: content.trim(), targetType, targetId: targetId || undefined, sourceWorkflowId: planId || undefined, timeZoneId: zoneId, periodStartsAt: start, periodEndsAt: end }
        onSaved(assessment ? await updateAssessmentDraft(assessment.assessmentId, { ...body, expectedVersion: assessment.version }) : await createAssessment(body))
      } else {
        const body = { title: title.trim(), description: content.trim(), targetType, targetId: targetId || undefined, assessmentId: assessmentId || undefined, timeZoneId: zoneId, severity, visibility, validFrom: start, validUntil: end }
        onSaved(alert ? await updateAlertDraft(alert.alertId, { ...body, expectedVersion: alert.version }) : await createAlertDraft(body))
      }
    } catch (failure) { setError(failure instanceof Error ? failure.message : 'The draft could not be saved. Retry.') }
    finally { setSaving(false) }
  }
  const targets = choices((options?.targets.items ?? []).filter((item) => item.targetType === targetType), targetId, `Previously linked ${targetType.toLowerCase()}`)
  const plans = choices(options?.plans.items ?? [], planId, 'Previously linked coastal plan')
  return <form aria-label={kind === 'assessment' ? 'Assessment draft' : 'Advisory draft'} className="mt-6 rounded-3xl border border-coast-line bg-white p-5 sm:p-8" onSubmit={(event) => void save(event)}>
    <h2 className="font-display text-2xl">{existing ? 'Update your draft' : kind === 'assessment' ? 'Start an operations assessment' : 'Prepare a coastal advisory'}</h2>
    <p className="mt-2 text-sm text-coast-muted">Give your draft a clear title. Link a coastal record when its catalogue is available, before publishing.</p>
    {!options && !error && <p aria-live="polite" className="mt-4 text-sm">Loading selection lists…</p>}
    {error && <div className="mt-4 rounded-xl bg-red-50 p-4" role="alert">{error} {!options && <button className="ml-2 underline" onClick={() => setRetry((value) => value + 1)} type="button">Retry selection lists</button>}</div>}
    <div className="mt-5 grid gap-5 sm:grid-cols-2">
      <label className="text-sm font-bold sm:col-span-2">{kind === 'assessment' ? 'Assessment title' : 'Advisory title'}<input className={inputClass} maxLength={160} onChange={(event) => setTitle(event.target.value)} required value={title} /></label>
      <label className="text-sm font-bold">Coastal record type<select className={inputClass} disabled={!!alert?.assessmentId} onChange={(event) => { setTargetType(event.target.value as CoastalTargetType); setTargetId('') }} value={targetType}>{['DESTINATION', 'ACTIVITY', 'OFFERING', 'SESSION'].map((value) => <option key={value}>{value}</option>)}</select></label>
      <label className="text-sm font-bold">Coastal record<select className={inputClass} disabled={!options || !!assessmentId} onChange={(event) => setTargetId(event.target.value)} value={targetId}><option value="">Link later</option>{targets.map((item) => <option key={item.id} value={item.id}>{item.title}</option>)}</select></label>
      {options?.targets.status !== 'AVAILABLE' && <p className="text-sm leading-6 text-coast-muted sm:col-span-2">The coastal catalogue is not connected yet. You can save this draft and link its record before publication.</p>}
      {kind === 'assessment' ? <label className="text-sm font-bold sm:col-span-2">Related coastal plan<select className={inputClass} disabled={!options} onChange={(event) => setPlanId(event.target.value)} value={planId}><option value="">No related plan</option>{plans.map((item) => <option key={item.id} value={item.id}>{item.title}</option>)}</select>{options?.plans.status !== 'AVAILABLE' && <span className="mt-2 block text-xs font-normal text-coast-muted">Coastal plans will appear when the planner is connected.</span>}</label>
        : <><label className="text-sm font-bold sm:col-span-2">Related assessment<select className={inputClass} disabled={!!alert || !options} onChange={(event) => { setAssessmentId(event.target.value); const selected = options?.assessments.find((item) => item.id === event.target.value); if (selected?.targetId && selected.targetType) { setTargetId(selected.targetId); setTargetType(selected.targetType) } }} value={assessmentId}><option value="">No related assessment</option>{choices(options?.assessments ?? [], assessmentId, 'Previously linked assessment').map((item) => <option key={item.id} value={item.id}>{item.title}</option>)}</select></label><label className="text-sm font-bold">Severity<select className={inputClass} onChange={(event) => setSeverity(event.target.value)} value={severity}>{['LOW', 'MODERATE', 'HIGH', 'CRITICAL'].map((value) => <option key={value}>{value}</option>)}</select></label><label className="text-sm font-bold">Audience<select className={inputClass} onChange={(event) => setVisibility(event.target.value)} value={visibility}><option value="OPERATIONS">Operations team</option><option value="PUBLIC">Coastal visitors</option></select></label></>}
      <label className="text-sm font-bold sm:col-span-2">Time zone<select className={inputClass} disabled={!options} onChange={(event) => setZoneId(event.target.value)} required value={zoneId}>{options?.timeZones.map((zone) => <option disabled={!zone.rulesAvailable} key={zone.id} value={zone.id}>{zone.country} — {zone.location}{offsetLabel(zone.currentOffsetMinutes)}</option>)}</select><span className="mt-2 block text-xs font-normal text-coast-muted">Applies to both dates. Daylight-saving changes are resolved automatically.</span></label>
      <label className="text-sm font-bold">{kind === 'assessment' ? 'Starts at' : 'Visible from'}<input className={inputClass} onChange={(event) => setStart(event.target.value)} required type="datetime-local" value={start} /></label>
      <label className="text-sm font-bold">{kind === 'assessment' ? 'Ends at' : 'Valid until'}<input className={inputClass} onChange={(event) => setEnd(event.target.value)} required type="datetime-local" value={end} /></label>
      <label className="text-sm font-bold sm:col-span-2">{kind === 'assessment' ? 'What should be reviewed?' : 'Advisory message'}<textarea className={`${inputClass} min-h-28 py-3`} maxLength={kind === 'assessment' ? 2000 : 4000} onChange={(event) => setContent(event.target.value)} required value={content} /></label>
    </div>
    <div className="mt-6 flex flex-wrap gap-3"><button className="min-h-11 rounded-full bg-coast-deep px-5 font-bold text-white disabled:opacity-50" disabled={saving || !options} type="submit">{saving ? 'Saving…' : existing ? 'Update draft' : 'Save draft'}</button><button className="min-h-11 rounded-full border border-red-200 px-5 font-bold text-red-800 hover:bg-red-50" disabled={saving} onClick={onCancel} type="button">Cancel</button></div>
  </form>
}
export function AssessmentDraftForm({ existing, onCancel, onSaved }: { existing: Assessment | null; onCancel: () => void; onSaved: (result: Assessment) => void }) {
  return <DraftForm assessment={existing} kind="assessment" onCancel={onCancel} onSaved={(result) => onSaved(result as Assessment)} />
}
export function AlertDraftForm({ existing, onCancel, onSaved }: { existing: CoastalAlert | null; onCancel: () => void; onSaved: (result: CoastalAlert) => void }) {
  return <DraftForm alert={existing} kind="alert" onCancel={onCancel} onSaved={(result) => onSaved(result as CoastalAlert)} />
}
