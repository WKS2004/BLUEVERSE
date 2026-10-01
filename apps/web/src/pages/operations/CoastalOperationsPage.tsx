import { useEffect, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import coastalWalk from '../../assets/coastal/coastal-walk.jpg'
import { hasAllPermissions } from '../../features/authorization/permissions'
import type { AuthUser } from '../../features/auth/auth'
import { useAuthSession } from '../../features/auth/authSession'
import {
  CoastalOperationsApiError,
  cancelAssessmentDraft,
  createAlertDraft,
  createAssessment,
  decideAlert,
  getAssessmentDetail,
  getEvidenceImage,
  getTargetHistory,
  getTargetStatus,
  listAlerts,
  listAssessments,
  submitAssessmentDraft,
  updateAssessmentDraft,
  updateAlertDraft,
  uploadAssessmentEvidence,
  withdrawAlertDraft,
} from '../../features/coastalOperations/operationsApi'
import type {
  Assessment,
  AssessmentDetail,
  CoastalAlert,
  CoastalTargetType,
  Evidence,
  OperationalHistoryItem,
  OperationalStatus,
} from '../../features/coastalOperations/operationsApi'
import { coastalOperationsPermissions, hasCoastalOperationsAccess } from '../../features/coastalOperations/permissions'
import AccountAreaNavigation from '../../components/account/AccountAreaNavigation'
import SiteFooter from '../../components/layout/SiteFooter'
import SiteHeader from '../../components/layout/SiteHeader'

const targetTypes: CoastalTargetType[] = ['DESTINATION', 'ACTIVITY', 'OFFERING', 'SESSION']
const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i
const offsetPattern = /^(?:Z|[+-](?:(?:0\d|1[0-3]):[0-5]\d|14:00))$/
const localDateTimePattern = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/
const maxEvidenceBytes = 5 * 1024 * 1024

type AssessmentForm = {
  targetType: CoastalTargetType
  targetId: string
  sourceWorkflowId: string
  periodStartsAt: string
  startOffset: string
  periodEndsAt: string
  endOffset: string
  objective: string
}

type AlertForm = {
  targetType: CoastalTargetType
  targetId: string
  assessmentId: string
  title: string
  description: string
  severity: string
  visibility: string
  validFrom: string
  fromOffset: string
  validUntil: string
  untilOffset: string
}

const emptyAssessmentForm: AssessmentForm = {
  targetType: 'DESTINATION', targetId: '', sourceWorkflowId: '', periodStartsAt: '', startOffset: '',
  periodEndsAt: '', endOffset: '', objective: '',
}

const emptyAlertForm: AlertForm = {
  targetType: 'DESTINATION', targetId: '', assessmentId: '', title: '', description: '',
  severity: 'MODERATE', visibility: 'OPERATIONS', validFrom: '', fromOffset: '', validUntil: '', untilOffset: '',
}

function label(value: string | null | undefined) {
  if (!value) return 'Not recorded'
  return value.toLowerCase().split('_').map((part) => part.charAt(0).toUpperCase() + part.slice(1)).join(' ')
}

function formatDate(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.valueOf()) ? 'Time not available' : new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

function explicitInstant(local: string, offset: string) {
  if (!localDateTimePattern.test(local) || !offsetPattern.test(offset)) return null
  const instant = `${local}:00${offset}`
  return Number.isNaN(Date.parse(instant)) ? null : instant
}

function instantFields(value: string) {
  const zone = value.endsWith('Z') ? 'Z' : value.slice(-6)
  return { local: value.slice(0, 16), offset: offsetPattern.test(zone) ? zone : 'Z' }
}

function fieldClass(error = false) {
  return `mt-1 min-h-12 w-full rounded-2xl border bg-white px-4 py-3 text-sm text-coast-ink outline-none transition focus:border-coast-blue focus:ring-4 focus:ring-coast-glass/70 ${error ? 'border-red-600' : 'border-coast-line'}`
}

function StatusPill({ value }: { value: string }) {
  const normalized = value.toUpperCase()
  const positive = ['AVAILABLE', 'OPEN', 'ACTIVE', 'PUBLISHED', 'SUCCEEDED', 'APPROVED'].includes(normalized)
  const quiet = ['NOT_CONNECTED', 'SUBMITTED', 'PROPOSED', 'UNKNOWN', 'PENDING_APPROVAL'].includes(normalized)
  const styles = positive ? 'bg-coast-sage text-coast-deep' : quiet ? 'bg-coast-sand text-coast-muted' : 'bg-red-50 text-red-900'
  return <span className={`inline-flex min-h-7 items-center rounded-full px-3 text-xs font-bold ${styles}`}>{label(value)}</span>
}

function Notice({ tone = 'info', children }: { tone?: 'info' | 'success' | 'error'; children: ReactNode }) {
  const styles = tone === 'error' ? 'border-red-200 bg-red-50 text-red-900' : tone === 'success' ? 'border-coast-glass bg-coast-sage text-coast-deep' : 'border-coast-line bg-coast-sand text-coast-ink'
  return <p className={`rounded-2xl border px-4 py-3 text-sm leading-6 ${styles}`} role={tone === 'error' ? 'alert' : 'status'}>{children}</p>
}

function InstantFields({
  title,
  startLabel,
  endLabel,
  start,
  startOffset,
  end,
  endOffset,
  onChange,
}: {
  title: string
  startLabel: string
  endLabel: string
  start: string
  startOffset: string
  end: string
  endOffset: string
  onChange: (field: 'start' | 'startOffset' | 'end' | 'endOffset', value: string) => void
}) {
  return <fieldset className="grid gap-4 rounded-2xl border border-coast-line bg-white/70 p-4 sm:grid-cols-2 sm:p-5">
    <legend className="px-2 text-sm font-extrabold text-coast-ink">{title}</legend>
    <label className="text-sm font-bold text-coast-ink">{startLabel}
      <input className={fieldClass()} onChange={(event) => onChange('start', event.target.value)} required type="datetime-local" value={start} />
    </label>
    <label className="text-sm font-bold text-coast-ink">Time-zone offset at start
      <input autoCapitalize="characters" className={fieldClass()} maxLength={6} onChange={(event) => onChange('startOffset', event.target.value.toUpperCase())} placeholder="Z or +05:30" required value={startOffset} />
    </label>
    <label className="text-sm font-bold text-coast-ink">{endLabel}
      <input className={fieldClass()} onChange={(event) => onChange('end', event.target.value)} required type="datetime-local" value={end} />
    </label>
    <label className="text-sm font-bold text-coast-ink">Time-zone offset at end
      <input autoCapitalize="characters" className={fieldClass()} maxLength={6} onChange={(event) => onChange('endOffset', event.target.value.toUpperCase())} placeholder="Z or +05:30" required value={endOffset} />
    </label>
    <p className="text-xs leading-5 text-coast-muted sm:col-span-2">Enter the offset that applies on each date. BLUEVERSE stores these as exact instants; it does not guess your time zone.</p>
  </fieldset>
}

function AssessmentDraftForm({ existing, onCancel, onSaved }: { existing: Assessment | null; onCancel: () => void; onSaved: (assessment: Assessment) => void }) {
  const [form, setForm] = useState<AssessmentForm>(() => {
    if (!existing) return emptyAssessmentForm
    const start = instantFields(existing.periodStartsAt)
    const end = instantFields(existing.periodEndsAt)
    return {
      targetType: existing.targetType,
      targetId: existing.targetId,
      sourceWorkflowId: existing.sourceWorkflowId ?? '',
      periodStartsAt: start.local,
      startOffset: start.offset,
      periodEndsAt: end.local,
      endOffset: end.offset,
      objective: existing.objective,
    }
  })
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  function update<K extends keyof AssessmentForm>(key: K, value: AssessmentForm[K]) {
    setForm((current) => ({ ...current, [key]: value }))
  }

  function updatePeriod(field: 'start' | 'startOffset' | 'end' | 'endOffset', value: string) {
    const keys = { start: 'periodStartsAt', startOffset: 'startOffset', end: 'periodEndsAt', endOffset: 'endOffset' } as const
    update(keys[field], value as never)
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const start = explicitInstant(form.periodStartsAt, form.startOffset)
    const end = explicitInstant(form.periodEndsAt, form.endOffset)
    if (!uuidPattern.test(form.targetId.trim())) return setError('Check the coastal record ID and try again.')
    if (form.sourceWorkflowId.trim() && !uuidPattern.test(form.sourceWorkflowId.trim())) return setError('Check the related coastal plan ID and try again.')
    if (!start || !end || Date.parse(end) <= Date.parse(start)) return setError('Add valid RFC 3339 times with explicit offsets, and make the end later than the start.')

    setError(null)
    setSaving(true)
    try {
      const input = {
        targetType: form.targetType,
        targetId: form.targetId.trim(),
        sourceWorkflowId: form.sourceWorkflowId.trim() || undefined,
        periodStartsAt: start,
        periodEndsAt: end,
        objective: form.objective.trim(),
      }
      const assessment = existing
        ? await updateAssessmentDraft(existing.assessmentId, { ...input, expectedVersion: existing.version })
        : await createAssessment(input)
      onSaved(assessment)
    } catch (requestError) {
      setError(requestError instanceof CoastalOperationsApiError ? requestError.message : 'We could not save this assessment draft. Please try again.')
    } finally {
      setSaving(false)
    }
  }

  return <form className="grid gap-6 rounded-3xl border border-coast-line bg-coast-pearl p-5 shadow-sm sm:p-7 lg:p-8" onSubmit={(event) => void submit(event)}>
    <div>
      <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">{existing ? 'EDIT ASSESSMENT' : 'NEW ASSESSMENT'}</p>
      <h3 className="mt-2 font-display text-2xl tracking-[-0.035em]">{existing ? 'Update your assessment draft' : 'Start a coastal operations assessment'}</h3>
      <p className="mt-2 max-w-2xl text-sm leading-6 text-coast-muted">{existing ? 'Update the details while this assessment is still a draft.' : 'Save a draft now. When it is ready, submit it to check the latest coastal context.'}</p>
    </div>
    {error && <Notice tone="error">{error}</Notice>}
    <div className="grid gap-4 sm:grid-cols-2">
      <label className="text-sm font-bold">Coastal record type
        <select className={fieldClass()} onChange={(event) => update('targetType', event.target.value as CoastalTargetType)} value={form.targetType}>{targetTypes.map((type) => <option key={type} value={type}>{label(type)}</option>)}</select>
      </label>
      <label className="text-sm font-bold">Coastal record ID
        <input autoComplete="off" className={fieldClass()} maxLength={36} onChange={(event) => update('targetId', event.target.value)} placeholder="ID from the destination, activity, offering, or session details" required value={form.targetId} />
      </label>
      <label className="text-sm font-bold sm:col-span-2">Related coastal plan ID <span className="font-normal text-coast-muted">(optional)</span>
        <input autoComplete="off" className={fieldClass()} maxLength={36} onChange={(event) => update('sourceWorkflowId', event.target.value)} placeholder="ID from the related itinerary or plan" value={form.sourceWorkflowId} />
      </label>
    </div>
    <InstantFields title="Assessment period" startLabel="Starts at" endLabel="Ends at" start={form.periodStartsAt} startOffset={form.startOffset} end={form.periodEndsAt} endOffset={form.endOffset} onChange={updatePeriod} />
    <label className="text-sm font-bold">What should the team assess?
      <textarea className={`${fieldClass()} min-h-28 resize-y`} maxLength={2000} onChange={(event) => update('objective', event.target.value)} placeholder="Describe the operational concern or review objective." required value={form.objective} />
      <span className="mt-1 block text-xs font-normal text-coast-muted">Keep the objective focused on a BLUEVERSE-managed activity or coastal experience.</span>
    </label>
    <div className="flex flex-wrap gap-3">
      <button className="inline-flex min-h-11 items-center justify-center rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white transition hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:cursor-wait disabled:opacity-60" disabled={saving} type="submit">{saving ? 'Saving…' : existing ? 'Update draft' : 'Save draft'}</button>
      <button className="inline-flex min-h-11 items-center justify-center rounded-full border border-coast-line px-5 text-sm font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue" onClick={onCancel} type="button">Cancel</button>
    </div>
  </form>
}

function AlertDraftForm({
  existing,
  onCancel,
  onSaved,
}: {
  existing: CoastalAlert | null
  onCancel: () => void
  onSaved: (alert: CoastalAlert) => void
}) {
  const [form, setForm] = useState<AlertForm>(() => existing ? {
    targetType: existing.targetType,
    targetId: existing.targetId,
    assessmentId: existing.assessmentId ?? '',
    title: existing.title,
    description: existing.description,
    severity: existing.severity,
    visibility: existing.visibility,
    validFrom: instantFields(existing.validFrom).local,
    fromOffset: instantFields(existing.validFrom).offset,
    validUntil: instantFields(existing.validUntil).local,
    untilOffset: instantFields(existing.validUntil).offset,
  } : emptyAlertForm)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  function update<K extends keyof AlertForm>(key: K, value: AlertForm[K]) {
    setForm((current) => ({ ...current, [key]: value }))
  }

  function updatePeriod(field: 'start' | 'startOffset' | 'end' | 'endOffset', value: string) {
    const keys = { start: 'validFrom', startOffset: 'fromOffset', end: 'validUntil', endOffset: 'untilOffset' } as const
    update(keys[field], value as never)
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const from = explicitInstant(form.validFrom, form.fromOffset)
    const until = explicitInstant(form.validUntil, form.untilOffset)
    if (!uuidPattern.test(form.targetId.trim())) return setError('Check the coastal record ID and try again.')
    if (form.assessmentId.trim() && !uuidPattern.test(form.assessmentId.trim())) return setError('Check the related assessment ID and try again.')
    if (!from || !until || Date.parse(until) <= Date.parse(from)) return setError('Add valid RFC 3339 times with explicit offsets, and make the end later than the start.')

    setError(null)
    setSaving(true)
    try {
      const payload = {
        targetType: form.targetType,
        targetId: form.targetId.trim(),
        title: form.title.trim(),
        description: form.description.trim(),
        severity: form.severity,
        visibility: form.visibility,
        validFrom: from,
        validUntil: until,
      }
      const alert = existing
        ? await updateAlertDraft(existing.alertId, { ...payload, expectedVersion: existing.version })
        : await createAlertDraft({ ...payload, assessmentId: form.assessmentId.trim() || undefined })
      onSaved(alert)
    } catch (requestError) {
      setError(requestError instanceof CoastalOperationsApiError ? requestError.message : 'We could not save this alert draft. Please try again.')
    } finally {
      setSaving(false)
    }
  }

  return <form className="grid gap-5 rounded-3xl border border-coast-line bg-coast-pearl p-5 shadow-sm sm:p-7" onSubmit={(event) => void submit(event)}>
    <div>
      <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">{existing ? 'EDIT PROPOSED ALERT' : 'NEW ALERT DRAFT'}</p>
      <h3 className="mt-2 font-display text-2xl tracking-[-0.035em]">{existing ? 'Update the draft details' : 'Share a coastal advisory'}</h3>
      <p className="mt-2 text-sm leading-6 text-coast-muted">This creates an unpublished draft. Publishing is a separate permission-checked action.</p>
    </div>
    {error && <Notice tone="error">{error}</Notice>}
    <div className="grid gap-4 sm:grid-cols-2">
      <label className="text-sm font-bold">Coastal record type
        <select className={fieldClass()} onChange={(event) => update('targetType', event.target.value as CoastalTargetType)} value={form.targetType}>{targetTypes.map((type) => <option key={type} value={type}>{label(type)}</option>)}</select>
      </label>
      <label className="text-sm font-bold">Coastal record ID
        <input autoComplete="off" className={fieldClass()} maxLength={36} onChange={(event) => update('targetId', event.target.value)} placeholder="ID from the destination, activity, offering, or session details" required value={form.targetId} />
      </label>
      <label className="text-sm font-bold">Severity
        <select className={fieldClass()} onChange={(event) => update('severity', event.target.value)} value={form.severity}>{['LOW', 'MODERATE', 'HIGH', 'CRITICAL'].map((severity) => <option key={severity} value={severity}>{label(severity)}</option>)}</select>
      </label>
      <label className="text-sm font-bold">Who can see this?
        <select className={fieldClass()} onChange={(event) => update('visibility', event.target.value)} value={form.visibility}><option value="OPERATIONS">Operations team</option><option value="PUBLIC">Public coastal visitors</option></select>
      </label>
      {!existing && <label className="text-sm font-bold sm:col-span-2">Related assessment ID <span className="font-normal text-coast-muted">(optional)</span>
        <input autoComplete="off" className={fieldClass()} maxLength={36} onChange={(event) => update('assessmentId', event.target.value)} value={form.assessmentId} />
      </label>}
      <label className="text-sm font-bold sm:col-span-2">Title
        <input className={fieldClass()} maxLength={160} onChange={(event) => update('title', event.target.value)} required value={form.title} />
      </label>
      <label className="text-sm font-bold sm:col-span-2">What should people know?
        <textarea className={`${fieldClass()} min-h-28 resize-y`} maxLength={4000} onChange={(event) => update('description', event.target.value)} required value={form.description} />
      </label>
    </div>
    <InstantFields title="Advisory period" startLabel="Visible from" endLabel="Valid until" start={form.validFrom} startOffset={form.fromOffset} end={form.validUntil} endOffset={form.untilOffset} onChange={updatePeriod} />
    {form.severity === 'HIGH' || form.severity === 'CRITICAL' ? <Notice>Publishing a {label(form.severity).toLowerCase()} advisory requires a different authorized reviewer from its drafter and linked assessment initiator.</Notice> : null}
    <div className="flex flex-wrap gap-3">
      <button className="inline-flex min-h-11 items-center justify-center rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:cursor-wait disabled:opacity-60" disabled={saving} type="submit">{saving ? 'Saving…' : existing ? 'Save draft' : 'Create draft'}</button>
      <button className="inline-flex min-h-11 items-center justify-center rounded-full border border-coast-line px-5 text-sm font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue" onClick={onCancel} type="button">Cancel</button>
    </div>
  </form>
}

function AssessmentCard({ assessment, selected, onSelect }: { assessment: Assessment; selected: boolean; onSelect: () => void }) {
  return <button aria-expanded={selected} className={`w-full rounded-3xl border p-5 text-left transition hover:border-coast-glass hover:shadow-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue ${selected ? 'border-coast-blue bg-coast-sage/60' : 'border-coast-line bg-coast-pearl'}`} onClick={onSelect} type="button">
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p className="text-xs font-extrabold tracking-[0.12em] text-coast-muted">{label(assessment.targetType)}</p>
        <h3 className="mt-2 max-w-4xl font-display text-xl tracking-[-0.03em] sm:text-2xl">{assessment.objective}</h3>
      </div>
      <StatusPill value={assessment.workflowStatus} />
    </div>
    <div className="mt-5 flex flex-wrap gap-x-6 gap-y-2 text-sm text-coast-muted">
      <span>Period: {formatDate(assessment.periodStartsAt)} – {formatDate(assessment.periodEndsAt)}</span>
      <span>Updated {formatDate(assessment.updatedAt)}</span>
    </div>
    <p className="mt-3 text-xs font-semibold text-coast-deep">{selected ? 'Hide review details' : 'Open review details'} <span aria-hidden="true">→</span></p>
  </button>
}

function CoastalOperationsWorkspace({ user }: { user: AuthUser | null }) {
  const [assessmentItems, setAssessmentItems] = useState<Assessment[]>([])
  const [assessmentError, setAssessmentError] = useState<string | null>(null)
  const [alertItems, setAlertItems] = useState<CoastalAlert[]>([])
  const [alertError, setAlertError] = useState<string | null>(null)
  const [selectedAssessmentId, setSelectedAssessmentId] = useState<string | null>(null)
  const [detail, setDetail] = useState<AssessmentDetail | null>(null)
  const [detailError, setDetailError] = useState<{ assessmentId: string; message: string } | null>(null)
  const [targetStatus, setTargetStatus] = useState<OperationalStatus | null>(null)
  const [targetHistory, setTargetHistory] = useState<OperationalHistoryItem[]>([])
  const [targetError, setTargetError] = useState<string | null>(null)
  const [lookupTargetType, setLookupTargetType] = useState<CoastalTargetType>('DESTINATION')
  const [lookupTargetId, setLookupTargetId] = useState('')
  const [lookupStatus, setLookupStatus] = useState<OperationalStatus | null>(null)
  const [lookupHistory, setLookupHistory] = useState<OperationalHistoryItem[]>([])
  const [lookupError, setLookupError] = useState<string | null>(null)
  const [notice, setNotice] = useState<{ text: string; tone: 'success' | 'error' } | null>(null)
  const [busy, setBusy] = useState(false)
  const [refreshKey, setRefreshKey] = useState(0)
  const [showAssessmentForm, setShowAssessmentForm] = useState(false)
  const [editingAssessment, setEditingAssessment] = useState<Assessment | null>(null)
  const [showAlertForm, setShowAlertForm] = useState(false)
  const [editingAlert, setEditingAlert] = useState<CoastalAlert | null>(null)
  const [assessmentConfirmation, setAssessmentConfirmation] = useState<{ assessment: Assessment; action: 'submit' | 'cancel' } | null>(null)
  const [decisionConfirmation, setDecisionConfirmation] = useState<{ alert: CoastalAlert; decision: 'PUBLISH' | 'RESOLVE' } | null>(null)
  const [withdrawalConfirmation, setWithdrawalConfirmation] = useState<CoastalAlert | null>(null)
  const [evidencePreview, setEvidencePreview] = useState<{ evidenceId: string; url: string } | null>(null)

  const canReadAssessments = hasAllPermissions(user, [coastalOperationsPermissions.assessmentRead]) || hasAllPermissions(user, [coastalOperationsPermissions.assessmentQueueRead])
  const canReadQueue = hasAllPermissions(user, [coastalOperationsPermissions.assessmentQueueRead])
  const canCreateAssessment = hasAllPermissions(user, [coastalOperationsPermissions.assessmentCreate])
  const canUpdateAssessment = hasAllPermissions(user, [coastalOperationsPermissions.assessmentUpdate])
  const canDeleteAssessment = hasAllPermissions(user, [coastalOperationsPermissions.assessmentDelete])
  const canSubmitAssessment = hasAllPermissions(user, [coastalOperationsPermissions.assessmentSubmit])
  const canDecideAssessment = hasAllPermissions(user, [coastalOperationsPermissions.assessmentDecide])
  const canUploadEvidence = hasAllPermissions(user, [coastalOperationsPermissions.evidenceUpload])
  const canReadEvidence = hasAllPermissions(user, [coastalOperationsPermissions.evidenceRead])
  const canReadStatus = hasAllPermissions(user, [coastalOperationsPermissions.targetStatusRead])
  const canReadHistory = hasAllPermissions(user, [coastalOperationsPermissions.targetHistoryRead])
  const canReadAlerts = hasAllPermissions(user, [coastalOperationsPermissions.alertRead]) || hasAllPermissions(user, [coastalOperationsPermissions.alertManage])
  const canManageAlerts = hasAllPermissions(user, [coastalOperationsPermissions.alertManage])
  const canDecideAlerts = hasAllPermissions(user, [coastalOperationsPermissions.alertDecide])
  const hasAccess = hasCoastalOperationsAccess(user)

  useEffect(() => () => {
    if (evidencePreview) URL.revokeObjectURL(evidencePreview.url)
  }, [evidencePreview])

  useEffect(() => {
    let current = true
    if (canReadAssessments) {
      void listAssessments().then((page) => {
        if (!current) return
        setAssessmentError(null)
        setAssessmentItems(page.items)
      }).catch((error: unknown) => {
        if (current) setAssessmentError(error instanceof CoastalOperationsApiError ? error.message : 'We could not load assessments. Retry when the connection is available.')
      })
    }
    if (canReadAlerts) {
      void listAlerts().then((page) => {
        if (!current) return
        setAlertError(null)
        setAlertItems(page.items)
      }).catch((error: unknown) => {
        if (current) setAlertError(error instanceof CoastalOperationsApiError ? error.message : 'We could not load advisories. Retry when the connection is available.')
      })
    }
    return () => { current = false }
  }, [canReadAssessments, canReadAlerts, refreshKey])

  useEffect(() => {
    if (!selectedAssessmentId || !canReadAssessments) return
    let current = true
    void getAssessmentDetail(selectedAssessmentId).then(async (result) => {
      if (!current) return
      setDetail(result)
      setDetailError(null)
      setTargetStatus(null)
      setTargetHistory([])
      setTargetError(null)
      const { targetType, targetId } = result.assessment
      const lookups = await Promise.allSettled([
        canReadStatus ? getTargetStatus(targetType, targetId) : Promise.resolve(null),
        canReadHistory ? getTargetHistory(targetType, targetId) : Promise.resolve(null),
      ])
      if (!current) return
      if (lookups[0].status === 'fulfilled') setTargetStatus(lookups[0].value)
      if (lookups[1].status === 'fulfilled' && lookups[1].value) setTargetHistory(lookups[1].value.items)
      if (lookups.some((lookup) => lookup.status === 'rejected')) setTargetError('Some operational history is not available for this record yet.')
    }).catch((error: unknown) => {
      if (current) setDetailError({
        assessmentId: selectedAssessmentId,
        message: error instanceof CoastalOperationsApiError ? error.message : 'We could not open this assessment. Try again.',
      })
    })
    return () => { current = false }
  }, [selectedAssessmentId, canReadAssessments, canReadStatus, canReadHistory, refreshKey])

  async function refreshDetail() {
    if (!selectedAssessmentId) return
    try { setDetail(await getAssessmentDetail(selectedAssessmentId)); setDetailError(null) }
    catch (error) { setDetailError({ assessmentId: selectedAssessmentId, message: error instanceof CoastalOperationsApiError ? error.message : 'We could not refresh this assessment.' }) }
  }

  async function handleEvidence(file: File, assessment: Assessment) {
    if (file.type !== 'image/png' || !file.name.toLowerCase().endsWith('.png')) {
      setNotice({ text: 'Evidence must be a PNG image.', tone: 'error' })
      return
    }
    if (file.size > maxEvidenceBytes) {
      setNotice({ text: 'That image is larger than the 5 MiB limit.', tone: 'error' })
      return
    }
    if ((detail?.evidence.length ?? 0) >= 5) {
      setNotice({ text: 'This assessment already has five evidence images.', tone: 'error' })
      return
    }
    setBusy(true)
    try {
      const evidence = await uploadAssessmentEvidence(assessment.assessmentId, file)
      setAssessmentItems((items) => items.map((item) => item.assessmentId === assessment.assessmentId ? { ...item, version: evidence.assessmentVersion } : item))
      setNotice({ text: 'Evidence was added to the assessment.', tone: 'success' })
      await refreshDetail()
    } catch (error) {
      setNotice({ text: error instanceof CoastalOperationsApiError ? error.message : 'We could not upload this image. Please retry.', tone: 'error' })
    } finally { setBusy(false) }
  }

  async function viewEvidence(assessmentId: string, evidence: Evidence) {
    setBusy(true)
    try {
      const blob = await getEvidenceImage(assessmentId, evidence.evidenceId)
      if (evidencePreview) URL.revokeObjectURL(evidencePreview.url)
      setEvidencePreview({ evidenceId: evidence.evidenceId, url: URL.createObjectURL(blob) })
    } catch (error) {
      setNotice({ text: error instanceof CoastalOperationsApiError ? error.message : 'We could not display this image. Please retry.', tone: 'error' })
    } finally { setBusy(false) }
  }

  async function handleAssessmentSaved(assessment: Assessment) {
    setShowAssessmentForm(false)
    setEditingAssessment(null)
    setSelectedAssessmentId(assessment.assessmentId)
    setNotice({ text: 'Assessment draft saved. Submit it when you are ready to check coastal context.', tone: 'success' })
    setRefreshKey((key) => key + 1)
  }

  async function confirmAssessmentAction() {
    if (!assessmentConfirmation) return
    const { assessment, action } = assessmentConfirmation
    setBusy(true)
    try {
      if (action === 'submit') {
        await submitAssessmentDraft(assessment.assessmentId, assessment.version)
        setNotice({ text: 'The assessment was submitted. No automated proposal was created.', tone: 'success' })
      } else {
        await cancelAssessmentDraft(assessment.assessmentId, assessment.version)
        setNotice({ text: 'The assessment draft was cancelled and retained in the audit history for authorized reviewers.', tone: 'success' })
        setSelectedAssessmentId(null)
        setDetail(null)
      }
      setAssessmentConfirmation(null)
      setEditingAssessment(null)
      setShowAssessmentForm(false)
      setRefreshKey((key) => key + 1)
    } catch (error) {
      setNotice({ text: error instanceof CoastalOperationsApiError ? error.message : 'We could not update this assessment. Refresh and retry.', tone: 'error' })
      setAssessmentConfirmation(null)
    } finally { setBusy(false) }
  }

  async function handleAlertSaved(alert: CoastalAlert) {
    setEditingAlert(null)
    setShowAlertForm(false)
    setNotice({ text: `“${alert.title}” is saved as a proposed draft. It has not been published.`, tone: 'success' })
    setRefreshKey((key) => key + 1)
  }

  async function confirmAlertDecision() {
    if (!decisionConfirmation) return
    setBusy(true)
    try {
      await decideAlert(decisionConfirmation.alert.alertId, decisionConfirmation.decision, decisionConfirmation.alert.version)
      setNotice({ text: decisionConfirmation.decision === 'PUBLISH' ? 'The advisory is now active.' : 'The advisory has been resolved.', tone: 'success' })
      setDecisionConfirmation(null)
      setRefreshKey((key) => key + 1)
    } catch (error) {
      setNotice({ text: error instanceof CoastalOperationsApiError ? error.message : 'We could not update this advisory. Refresh and retry.', tone: 'error' })
    } finally { setBusy(false) }
  }

  async function confirmAlertWithdrawal() {
    if (!withdrawalConfirmation) return
    setBusy(true)
    try {
      await withdrawAlertDraft(withdrawalConfirmation.alertId, withdrawalConfirmation.version)
      setNotice({ text: '“' + withdrawalConfirmation.title + '” was withdrawn. Its history remains available to advisory managers.', tone: 'success' })
      setWithdrawalConfirmation(null)
      setRefreshKey((key) => key + 1)
    } catch (error) {
      setNotice({ text: error instanceof CoastalOperationsApiError ? error.message : 'We could not withdraw this draft. Refresh and retry.', tone: 'error' })
      setWithdrawalConfirmation(null)
    } finally { setBusy(false) }
  }

  async function lookupTarget(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!uuidPattern.test(lookupTargetId.trim())) {
      setLookupError('Check the coastal record ID and try again.')
      return
    }
    setLookupError(null)
    setLookupStatus(null)
    setLookupHistory([])
    const requests = await Promise.allSettled([
      canReadStatus ? getTargetStatus(lookupTargetType, lookupTargetId.trim()) : Promise.resolve(null),
      canReadHistory ? getTargetHistory(lookupTargetType, lookupTargetId.trim()) : Promise.resolve(null),
    ])
    if (requests[0].status === 'fulfilled') setLookupStatus(requests[0].value)
    if (requests[1].status === 'fulfilled' && requests[1].value) setLookupHistory(requests[1].value.items)
    if (requests.every((request) => request.status === 'rejected')) setLookupError('That coastal record is not available to your account. Check its ID and permissions, then retry.')
  }

  const page = <div className="flex min-h-screen flex-col bg-coast-paper font-sans text-coast-ink" id="top">
    <SiteHeader active="operations" />
    <main className="mx-auto grid w-full max-w-[90rem] flex-1 grid-cols-1 content-start gap-6 px-4 py-6 sm:px-8 sm:py-10 lg:grid-cols-[15rem_minmax(0,1fr)] lg:items-start lg:gap-8 lg:px-6 lg:py-0">
      <AccountAreaNavigation active="operations" />
      <div className="min-w-0 lg:py-10">
        <section aria-labelledby="operations-title" className="relative isolate overflow-hidden rounded-[2rem] bg-coast-deep text-white shadow-sm">
          <img alt="A quiet coastal cove in daylight" className="absolute inset-0 -z-20 h-full w-full object-cover opacity-35" src={coastalWalk} />
          <div aria-hidden="true" className="absolute inset-0 -z-10 bg-gradient-to-r from-coast-deep via-coast-deep/90 to-coast-deep/35" />
          <div className="max-w-3xl px-5 py-8 sm:px-8 sm:py-10 lg:px-10 lg:py-12">
            <p className="text-[11px] font-extrabold tracking-[0.18em] text-coast-glass">COASTAL CARE</p>
            <h1 className="mt-3 font-display text-4xl leading-tight tracking-[-0.05em] sm:text-5xl" id="operations-title">Look after the places we share.</h1>
            <p className="mt-4 max-w-2xl text-sm leading-6 text-white/85 sm:text-base sm:leading-7">Save an assessment draft, follow its coastal context, and prepare clear advisories for the right people.</p>
          </div>
        </section>

        {!hasAccess ? <section className="mt-6 rounded-3xl border border-coast-line bg-coast-pearl p-6 sm:p-9">
          <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">ACCOUNT ACCESS</p>
          <h2 className="mt-3 font-display text-3xl tracking-[-0.04em]">This workspace is not available to your account.</h2>
          <p className="mt-3 max-w-xl text-sm leading-6 text-coast-muted">Coastal Operations access is granted through your account permissions. If you need access, contact your BLUEVERSE administrator.</p>
        </section> : <>
          {notice && <div className="mt-5"><Notice tone={notice.tone}>{notice.text}</Notice></div>}
          <section className="mt-10 grid gap-5 xl:grid-cols-[minmax(0,1fr)_auto] xl:items-center">
            <div>
              <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">OPERATIONS WORKSPACE</p>
              <h2 className="mt-2 font-display text-3xl tracking-[-0.045em] sm:text-4xl">Assessments and advisories</h2>
              <p className="mt-2 max-w-2xl text-sm leading-6 text-coast-muted">Review coastal assessments, follow important context, and share updates with the right people.</p>
            </div>
            <div className="flex flex-wrap gap-3">
              {canCreateAssessment && <button className="inline-flex min-h-11 items-center justify-center rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white shadow-sm transition hover:-translate-y-0.5 hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue" onClick={() => { setShowAssessmentForm((shown) => !shown); setEditingAssessment(null); setShowAlertForm(false); setEditingAlert(null) }} type="button">{showAssessmentForm && !editingAssessment ? 'Close assessment form' : 'New assessment'}</button>}
              {canManageAlerts && <button className="inline-flex min-h-11 items-center justify-center rounded-full border border-coast-line bg-coast-pearl px-5 text-sm font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue" onClick={() => { setEditingAlert(null); setShowAlertForm((shown) => !shown); setShowAssessmentForm(false); setEditingAssessment(null) }} type="button">{showAlertForm ? 'Close alert form' : 'Prepare an advisory'}</button>}
              <button aria-label="Refresh coastal operations" className="inline-flex min-h-11 items-center justify-center rounded-full border border-coast-line bg-white px-4 text-sm font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:cursor-wait" disabled={busy} onClick={() => setRefreshKey((key) => key + 1)} type="button">Refresh</button>
            </div>
          </section>

          {(showAssessmentForm || editingAssessment) && (editingAssessment ? canUpdateAssessment : canCreateAssessment) && <section aria-label={editingAssessment ? 'Edit assessment draft' : 'Create assessment'} className="mt-8"><AssessmentDraftForm key={editingAssessment?.assessmentId ?? 'new'} existing={editingAssessment} onCancel={() => { setShowAssessmentForm(false); setEditingAssessment(null) }} onSaved={(assessment) => void handleAssessmentSaved(assessment)} /></section>}
          {showAlertForm && canManageAlerts && <section aria-label="Create advisory" className="mt-5"><AlertDraftForm existing={null} onCancel={() => setShowAlertForm(false)} onSaved={(alert) => void handleAlertSaved(alert)} /></section>}
          {editingAlert && canManageAlerts && <section aria-label="Edit advisory draft" className="mt-5"><AlertDraftForm existing={editingAlert} onCancel={() => setEditingAlert(null)} onSaved={(alert) => void handleAlertSaved(alert)} /></section>}

          <div className="mt-10 grid items-start gap-10">
            <section aria-labelledby="assessments-title" className="min-w-0 rounded-[2rem] border border-coast-line bg-white/55 p-5 sm:p-7 lg:p-8">
              <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
                <div>
                  <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">ASSESSMENTS</p>
                  <h2 className="mt-2 font-display text-3xl tracking-[-0.04em]" id="assessments-title">A clear record of each review</h2>
                  <p className="mt-2 max-w-2xl text-sm leading-6 text-coast-muted">Your drafts and reviews are gathered here so you can pick up where you left off.</p>
                </div>
                {canReadQueue && <span className="rounded-full bg-coast-sage px-3 py-1.5 text-xs font-bold text-coast-deep">Review queue</span>}
              </div>
              {assessmentError && <Notice tone="error">{assessmentError}</Notice>}
              {!canReadAssessments ? <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 text-sm leading-6 text-coast-muted">Your current permissions allow other Coastal Operations actions, but do not include assessment reading.</div>
                : !assessmentError && assessmentItems.length === 0 ? <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 sm:p-8"><h3 className="font-display text-xl">No assessments to show yet</h3><p className="mt-2 text-sm leading-6 text-coast-muted">Saved drafts and submitted reviews will appear here.</p></div>
                  : <div className="grid gap-5">{assessmentItems.map((assessment) => <div key={assessment.assessmentId}>
                    <AssessmentCard assessment={assessment} selected={selectedAssessmentId === assessment.assessmentId} onSelect={() => setSelectedAssessmentId((id) => id === assessment.assessmentId ? null : assessment.assessmentId)} />
                    {selectedAssessmentId === assessment.assessmentId && <div className="mt-4 rounded-3xl border border-coast-line bg-white p-6 sm:p-8">
                      {detailError?.assessmentId === selectedAssessmentId && <Notice tone="error">{detailError.message}</Notice>}
                      {detail?.assessment.assessmentId !== selectedAssessmentId && detailError?.assessmentId !== selectedAssessmentId && <p aria-live="polite" className="text-sm text-coast-muted">Opening the assessment…</p>}
                      {detail?.assessment.assessmentId === selectedAssessmentId && <div className="grid gap-7">
                        <div className="flex flex-wrap items-start justify-between gap-4">
                          <div><p className="text-xs font-extrabold tracking-[0.13em] text-coast-blue">ASSESSMENT DETAILS</p></div>
                          <StatusPill value={detail.assessment.workflowStatus} />
                        </div>
                        {detail.assessment.workflowStatus === 'DRAFT' && <div className="grid gap-3 rounded-2xl bg-coast-sand p-4 sm:p-5">
                          <div><p className="text-xs font-bold text-coast-muted">Assessment period</p><p className="mt-1 text-sm font-bold">{formatDate(detail.assessment.periodStartsAt)} – {formatDate(detail.assessment.periodEndsAt)}</p></div>
                          <p className="text-sm leading-6 text-coast-muted">Coastal context is checked after you submit this draft.</p>
                        </div>}
                        {detail.assessment.workflowStatus !== 'DRAFT' && <div className="grid gap-4 rounded-2xl bg-coast-sand p-4 sm:grid-cols-2 sm:p-5">
                          <div><p className="text-xs font-bold text-coast-muted">Coastal context</p><p className="mt-1 text-sm font-bold">{label(detail.assessment.aiDependencyStatus)}</p></div>
                          <div><p className="text-xs font-bold text-coast-muted">Assessment period</p><p className="mt-1 text-sm font-bold">{formatDate(detail.assessment.periodStartsAt)} – {formatDate(detail.assessment.periodEndsAt)}</p></div>
                        </div>}
                        {detail.assessment.workflowStatus !== 'DRAFT' && detail.assessment.aiDependencyStatus === 'NOT_CONNECTED' && <Notice>Coastal context was recorded, but automated proposals are not available yet. No operational change has been suggested or applied.</Notice>}
                        {detail.assessment.workflowStatus !== 'DRAFT' && detail.assessment.aiDependencyStatus === 'UNAVAILABLE' && <Notice>Coastal context could not be fully checked right now. The assessment remains submitted; refresh later to see the latest information.</Notice>}
                        {detail.assessment.componentDependencies.length > 0 && <div>
                          <h4 className="font-bold">Coastal context checks</h4>
                          <ul className="mt-3 grid gap-2 sm:grid-cols-3">{detail.assessment.componentDependencies.map((dependency, index) => <li className="rounded-2xl border border-coast-line bg-coast-paper p-3" key={`${dependency.service}-${index}`}>
                            <p className="text-sm font-bold">{dependency.service === 'experience-biodiversity' ? 'Coastal experience' : dependency.service === 'marine-safety' ? 'Marine safety' : dependency.service === 'coastal-planner' ? 'Coastal planning' : 'Coastal context'}</p>
                            <div className="mt-2"><StatusPill value={dependency.status} /></div>
                            {dependency.checkedAt && <p className="mt-2 text-xs text-coast-muted">Checked {formatDate(dependency.checkedAt)}</p>}
                          </li>)}</ul>
                        </div>}
                        {canDecideAssessment && detail.assessment.workflowStatus !== 'DRAFT' && <Notice>There is no validated proposal to approve or apply for this assessment yet.</Notice>}
                        {detail.assessment.workflowStatus === 'DRAFT' && <div className="flex flex-wrap gap-3 border-t border-coast-line pt-5">
                          {canUpdateAssessment && <button className="min-h-11 rounded-full border border-coast-line px-5 text-sm font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-coast-blue" onClick={() => { setEditingAssessment(detail.assessment); setShowAssessmentForm(false); setShowAlertForm(false); setEditingAlert(null) }} type="button">Edit draft</button>}
                          {canSubmitAssessment && <button className="min-h-11 rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue" onClick={() => setAssessmentConfirmation({ assessment: detail.assessment, action: 'submit' })} type="button">Submit for review</button>}
                          {canDeleteAssessment && <button className="min-h-11 rounded-full border border-coast-line px-5 text-sm font-bold text-coast-muted hover:bg-coast-sand focus-visible:outline-2 focus-visible:outline-coast-blue" onClick={() => setAssessmentConfirmation({ assessment: detail.assessment, action: 'cancel' })} type="button">Cancel draft</button>}
                        </div>}
                        {detail.decisions.length > 0 && <div><h4 className="font-bold">Recorded decisions</h4><ul className="mt-2 grid gap-2">{detail.decisions.map((decision) => <li className="rounded-2xl bg-coast-sand p-3 text-sm" key={decision.decisionId}><span className="font-bold">{label(decision.decision)}</span><span className="ml-2 text-coast-muted">{formatDate(decision.decidedAt)}</span>{decision.explanation && <p className="mt-1 text-coast-muted">{decision.explanation}</p>}</li>)}</ul></div>}
                        <div className="grid gap-4 lg:grid-cols-2">
                          <div><div className="flex flex-wrap items-center justify-between gap-2"><h4 className="font-bold">Evidence</h4>{canUploadEvidence && ['DRAFT', 'SUBMITTED', 'REVISION_REQUESTED'].includes(detail.assessment.workflowStatus) && detail.evidence.length < 5 && <label className="inline-flex min-h-10 cursor-pointer items-center rounded-full border border-coast-line px-4 text-xs font-bold text-coast-deep hover:bg-coast-sage">Add PNG evidence<input accept="image/png,.png" className="sr-only" disabled={busy} onChange={(event) => { const file = event.target.files?.[0]; if (file) void handleEvidence(file, detail.assessment); event.currentTarget.value = '' }} type="file" /></label>}</div>
                            {!canReadEvidence && <p className="mt-2 text-sm leading-6 text-coast-muted">Evidence details are restricted by your current permissions.</p>}
                            {canReadEvidence && detail.evidence.length === 0 && <p className="mt-2 text-sm text-coast-muted">No evidence images have been added.</p>}
                            {canReadEvidence && <ul className="mt-3 grid gap-2">{detail.evidence.map((evidence) => <li className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-coast-line p-3" key={evidence.evidenceId}><span className="text-xs leading-5 text-coast-muted">PNG · {(evidence.byteLength / 1024).toFixed(0)} KiB · {label(evidence.inspectionStatus)}</span><button className="min-h-9 rounded-full px-3 text-xs font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-coast-blue" disabled={busy} onClick={() => void viewEvidence(detail.assessment.assessmentId, evidence)} type="button">View image</button></li>)}</ul>}
                            {evidencePreview && <figure className="mt-3 rounded-2xl bg-coast-sand p-3"><img alt="Uploaded assessment evidence" className="max-h-80 w-full rounded-xl object-contain" src={evidencePreview.url} /><figcaption className="mt-2 text-xs text-coast-muted">Evidence is served through your authorized BLUEVERSE session.</figcaption></figure>}
                          </div>
                          {(canReadStatus || canReadHistory) && <div><h4 className="font-bold">Operational status and history</h4>{targetError && <p className="mt-2 text-xs leading-5 text-coast-muted">{targetError}</p>}{canReadStatus && targetStatus && <div className="mt-3 flex flex-wrap items-center gap-3 rounded-2xl bg-coast-sage p-4"><StatusPill value={targetStatus.operationalState} /><span className="text-xs text-coast-muted">Updated {formatDate(targetStatus.updatedAt)}</span></div>}{canReadHistory && <ul className="mt-3 grid gap-2">{targetHistory.length === 0 ? <li className="text-sm text-coast-muted">No operational state changes have been recorded.</li> : targetHistory.map((item) => <li className="rounded-2xl border border-coast-line p-3 text-sm" key={item.historyId}><span className="font-bold">{label(item.previousState)} → {label(item.newState)}</span><span className="ml-2 text-xs text-coast-muted">{formatDate(item.createdAt)}</span></li>)}</ul>}</div>}
                        </div>
                      </div>}
                    </div>}
                  </div>)}</div>}
            </section>

            <section aria-labelledby="alerts-title" className="min-w-0 rounded-[2rem] border border-coast-line bg-white/55 p-5 sm:p-7 lg:p-8">
              <div className="mb-6">
                <p className="text-xs font-extrabold tracking-[0.15em] text-coast-teal">ADVISORIES</p>
                <h2 className="mt-2 font-display text-3xl tracking-[-0.04em]" id="alerts-title">Useful updates for the coast</h2>
                <p className="mt-2 max-w-2xl text-sm leading-6 text-coast-muted">Drafts and active updates are shown with the people who can see them and when they apply.</p>
              </div>
              {alertError && <Notice tone="error">{alertError}</Notice>}
              {!canReadAlerts ? <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 text-sm leading-6 text-coast-muted">Your current permissions do not include advisory reading. You may still prepare a draft if you have advisory management access.</div>
                : !alertError && alertItems.length === 0 ? <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 sm:p-8"><h3 className="font-display text-xl">No advisories to show</h3><p className="mt-2 text-sm leading-6 text-coast-muted">Active public updates and your proposed drafts will appear here.</p></div>
                  : <div className="grid gap-5">{alertItems.map((alert) => <article className="rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-[0_12px_36px_rgba(24,57,76,0.04)] sm:p-7" key={alert.alertId}>
                    <div className="flex flex-wrap items-start justify-between gap-3"><div><p className="text-xs font-extrabold tracking-[0.12em] text-coast-muted">{label(alert.targetType)}</p><h3 className="mt-2 font-display text-xl tracking-[-0.03em] sm:text-2xl">{alert.title}</h3></div><StatusPill value={alert.lifecycle} /></div>
                    <p className="mt-3 text-sm leading-6 text-coast-muted">{alert.description}</p>
                    <div className="mt-4 flex flex-wrap items-center gap-2"><StatusPill value={alert.severity} /><span className="rounded-full bg-coast-sand px-3 py-1.5 text-xs font-bold text-coast-muted">{alert.visibility === 'PUBLIC' ? 'For coastal visitors' : 'Operations team'}</span></div>
                    <p className="mt-3 text-xs text-coast-muted">Valid {formatDate(alert.validFrom)} – {formatDate(alert.validUntil)}</p>
                    {(canManageAlerts && alert.lifecycle === 'PROPOSED' || canDecideAlerts && ['PROPOSED', 'ACTIVE'].includes(alert.lifecycle)) && <div className="mt-4 flex flex-wrap gap-2">
                      {canManageAlerts && alert.lifecycle === 'PROPOSED' && <button className="min-h-10 rounded-full border border-coast-line px-4 text-xs font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-coast-blue" onClick={() => { setShowAlertForm(false); setEditingAlert(alert); setShowAssessmentForm(false); setEditingAssessment(null) }} type="button">Edit draft</button>}
                      {canManageAlerts && alert.lifecycle === 'PROPOSED' && <button className="min-h-10 rounded-full border border-coast-line px-4 text-xs font-bold text-coast-muted hover:bg-coast-sand focus-visible:outline-2 focus-visible:outline-coast-blue" onClick={() => setWithdrawalConfirmation(alert)} type="button">Withdraw draft</button>}
                      {canDecideAlerts && alert.lifecycle === 'PROPOSED' && <button className="min-h-10 rounded-full bg-coast-deep px-4 text-xs font-bold text-white hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue" onClick={() => setDecisionConfirmation({ alert, decision: 'PUBLISH' })} type="button">Publish advisory</button>}
                      {canDecideAlerts && alert.lifecycle === 'ACTIVE' && <button className="min-h-10 rounded-full border border-coast-line px-4 text-xs font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-coast-blue" onClick={() => setDecisionConfirmation({ alert, decision: 'RESOLVE' })} type="button">Resolve advisory</button>}
                    </div>}
                  </article>)}</div>}
            </section>
          </div>

          {(canReadStatus || canReadHistory) && <section aria-labelledby="target-lookup-title" className="mt-10 rounded-3xl border border-coast-line bg-coast-pearl p-6 sm:p-8">
            <div className="max-w-2xl"><p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">A CLOSER LOOK</p><h2 className="mt-2 font-display text-2xl tracking-[-0.04em]" id="target-lookup-title">Check a coastal record</h2><p className="mt-2 text-sm leading-6 text-coast-muted">Use the ID shown in the destination, activity, offering, or session details to see its current status and history.</p></div>
            <form className="mt-5 grid gap-4 sm:grid-cols-[minmax(10rem,0.55fr)_minmax(16rem,1fr)_auto] sm:items-end" onSubmit={(event) => void lookupTarget(event)}>
              <label className="text-sm font-bold">Record type<select className={fieldClass()} onChange={(event) => setLookupTargetType(event.target.value as CoastalTargetType)} value={lookupTargetType}>{targetTypes.map((type) => <option key={type} value={type}>{label(type)}</option>)}</select></label>
              <label className="text-sm font-bold">Coastal record ID<input autoComplete="off" className={fieldClass()} maxLength={36} onChange={(event) => setLookupTargetId(event.target.value)} placeholder="Paste the ID from the coastal record details" required value={lookupTargetId} /></label>
              <button className="min-h-12 rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue" type="submit">Check record</button>
            </form>
            {lookupError && <div className="mt-4"><Notice tone="error">{lookupError}</Notice></div>}
            {lookupStatus && <div className="mt-4 flex flex-wrap items-center gap-3 rounded-2xl bg-coast-sage p-4"><span className="text-sm font-bold">Current state</span><StatusPill value={lookupStatus.operationalState} /><span className="text-xs text-coast-muted">Updated {formatDate(lookupStatus.updatedAt)}</span></div>}
            {canReadHistory && lookupStatus && <div className="mt-4"><h3 className="text-sm font-bold">Operational history</h3>{lookupHistory.length === 0 ? <p className="mt-2 text-sm text-coast-muted">No state changes have been recorded for this record.</p> : <ul className="mt-2 grid gap-2 sm:grid-cols-2">{lookupHistory.map((item) => <li className="rounded-2xl border border-coast-line p-3 text-sm" key={item.historyId}><span className="font-bold">{label(item.previousState)} → {label(item.newState)}</span><span className="ml-2 text-xs text-coast-muted">{formatDate(item.createdAt)}</span></li>)}</ul>}</div>}
          </section>}

          {decisionConfirmation && <div className="fixed inset-0 z-[90] flex items-center justify-center bg-coast-ink/45 p-4" role="presentation"><section aria-labelledby="alert-decision-title" aria-modal="true" className="w-full max-w-lg rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-2xl" role="dialog">
            <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">CONFIRM ADVISORY CHANGE</p>
            <h2 className="mt-2 font-display text-2xl" id="alert-decision-title">{decisionConfirmation.decision === 'PUBLISH' ? 'Publish this advisory?' : 'Resolve this advisory?'}</h2>
            <p className="mt-3 text-sm leading-6 text-coast-muted">{decisionConfirmation.decision === 'PUBLISH' ? `“${decisionConfirmation.alert.title}” will become active for its selected audience and validity period.` : `“${decisionConfirmation.alert.title}” will be marked resolved.`}</p>
            {decisionConfirmation.decision === 'PUBLISH' && ['HIGH', 'CRITICAL'].includes(decisionConfirmation.alert.severity) && <p className="mt-4 rounded-2xl bg-coast-sand p-4 text-sm leading-6 text-coast-ink">A different authorized reviewer from the draft creator and linked assessment initiator must publish this {label(decisionConfirmation.alert.severity).toLowerCase()} advisory.</p>}
            <div className="mt-6 flex flex-wrap justify-end gap-3"><button className="min-h-11 rounded-full border border-coast-line px-5 text-sm font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-coast-blue" disabled={busy} onClick={() => setDecisionConfirmation(null)} type="button">Cancel</button><button className="min-h-11 rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:cursor-wait" disabled={busy} onClick={() => void confirmAlertDecision()} type="button">{busy ? 'Updating…' : decisionConfirmation.decision === 'PUBLISH' ? 'Confirm publish' : 'Confirm resolution'}</button></div>
          </section></div>}
          {assessmentConfirmation && <div className="fixed inset-0 z-[90] flex items-center justify-center bg-coast-ink/45 p-4" role="presentation"><section aria-labelledby="assessment-action-title" aria-modal="true" className="w-full max-w-lg rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-2xl" role="dialog">
            <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">ASSESSMENT DRAFT</p>
            <h2 className="mt-2 font-display text-2xl" id="assessment-action-title">{assessmentConfirmation.action === 'submit' ? 'Submit this assessment?' : 'Cancel this draft?'}</h2>
            <p className="mt-3 text-sm leading-6 text-coast-muted">{assessmentConfirmation.action === 'submit' ? 'Submitting closes draft editing and checks the latest coastal context. No automated recommendation or operational change will be created.' : 'This draft will be cancelled and retained in the audit history for authorized reviewers.'}</p>
            <div className="mt-6 flex flex-wrap justify-end gap-3"><button className="min-h-11 rounded-full border border-coast-line px-5 text-sm font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-coast-blue" disabled={busy} onClick={() => setAssessmentConfirmation(null)} type="button">Keep draft</button><button className="min-h-11 rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:cursor-wait" disabled={busy} onClick={() => void confirmAssessmentAction()} type="button">{busy ? 'Updating…' : assessmentConfirmation.action === 'submit' ? 'Submit assessment' : 'Confirm cancellation'}</button></div>
          </section></div>}
          {withdrawalConfirmation && <div className="fixed inset-0 z-[90] flex items-center justify-center bg-coast-ink/45 p-4" role="presentation"><section aria-labelledby="alert-withdrawal-title" aria-modal="true" className="w-full max-w-lg rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-2xl" role="dialog">
            <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">ADVISORY DRAFT</p>
            <h2 className="mt-2 font-display text-2xl" id="alert-withdrawal-title">Withdraw this draft?</h2>
            <p className="mt-3 text-sm leading-6 text-coast-muted">“{withdrawalConfirmation.title}” will be marked withdrawn and kept in the advisory history for managers.</p>
            <div className="mt-6 flex flex-wrap justify-end gap-3"><button className="min-h-11 rounded-full border border-coast-line px-5 text-sm font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-coast-blue" disabled={busy} onClick={() => setWithdrawalConfirmation(null)} type="button">Keep draft</button><button className="min-h-11 rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:cursor-wait" disabled={busy} onClick={() => void confirmAlertWithdrawal()} type="button">{busy ? 'Updating…' : 'Withdraw draft'}</button></div>
          </section></div>}
        </>}
      </div>
    </main>
    <SiteFooter />
  </div>

  return page
}

export default function CoastalOperationsPage() {
  const { user } = useAuthSession()
  const scope = `${user?.id ?? 'signed-out'}:${[...(user?.permissions ?? [])].sort().join(',')}`
  return <CoastalOperationsWorkspace key={scope} user={user} />
}
