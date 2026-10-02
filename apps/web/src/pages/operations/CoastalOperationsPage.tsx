import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import OperationsRecordCard from './OperationsRecordCard'
import OperationsPagination from './OperationsPagination'
import type { ReactNode } from 'react'
import OperationsActivity from './OperationsActivity'
import OperationsSearch from './OperationsSearch'
import { AssessmentDraftForm, AlertDraftForm } from './OperationsDraftForms'
import assessmentHero from '../../assets/coastal/assessment-hero.png'
import alertsHero from '../../assets/coastal/alerts-hero.png'
import OperationsIcon from './OperationsIcon'
import { hasAllPermissions } from '../../features/authorization/permissions'
import type { AuthUser } from '../../features/auth/auth'
import { useAuthSession } from '../../features/auth/authSession'
import {
  CoastalOperationsApiError,
  cancelAssessmentDraft,
  decideAlert,
  getAssessmentDetail,
  getEvidenceImage,
  getTargetHistory,
  getTargetStatus,
  listAlerts,
  listAlertLogRecords,
  listAssessments,
  submitAssessmentDraft,
  uploadAssessmentEvidence,
  removeAssessmentEvidence,
  withdrawAlertDraft,
} from '../../features/coastalOperations/operationsApi'
import type {
  Assessment,
  AssessmentDetail,
  CoastalAlert,
  Evidence,
  OperationalHistoryItem,
  OperationalStatus,
  RecordQuery,
} from '../../features/coastalOperations/operationsApi'
import { coastalNavigationLinks, coastalOperationsPermissions, hasCoastalOperationsAccess } from '../../features/coastalOperations/permissions'
import AccountAreaNavigation from '../../components/account/AccountAreaNavigation'
import SiteFooter from '../../components/layout/SiteFooter'
import SiteHeader from '../../components/layout/SiteHeader'

const maxEvidenceBytes = 5 * 1024 * 1024

function label(value: string | null | undefined) {
  if (!value) return 'Not recorded'
  if (value.toUpperCase() === 'PROPOSED') return 'Draft'
  if (value.toUpperCase() === 'SUBMITTED') return 'Published for assessment'
  return value.toLowerCase().split('_').map((part) => part.charAt(0).toUpperCase() + part.slice(1)).join(' ')
}

function formatDate(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.valueOf()) ? 'Time not available' : new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

function StatusPill({ value }: { value: string }) {
  const normalized = value.toUpperCase()
  const positive = ['AVAILABLE', 'OPEN', 'ACTIVE', 'PUBLISHED', 'SUCCEEDED', 'APPROVED'].includes(normalized)
  const quiet = ['DRAFT', 'NOT_CONNECTED', 'NOT_REQUESTED', 'NOT_STARTED', 'SUBMITTED', 'PROPOSED', 'UNKNOWN', 'PENDING_APPROVAL', 'CANCELLED', 'WITHDRAWN', 'EXPIRED', 'RESOLVED', 'LOW', 'MODERATE'].includes(normalized)
  const styles = positive ? 'bg-coast-sage text-coast-deep' : quiet ? 'bg-coast-sand text-coast-muted' : 'bg-red-50 text-red-900'
  return <span className={`inline-flex min-h-7 items-center rounded-full px-3 text-xs font-bold ${styles}`}>{label(value)}</span>
}

function Notice({ tone = 'info', children }: { tone?: 'info' | 'success' | 'error'; children: ReactNode }) {
  const styles = tone === 'error' ? 'border-red-200 bg-red-50 text-red-900' : tone === 'success' ? 'border-coast-glass bg-coast-sage text-coast-deep' : 'border-coast-line bg-coast-sand text-coast-ink'
  return <p className={`rounded-2xl border px-4 py-3 text-sm leading-6 ${styles}`} role={tone === 'error' ? 'alert' : 'status'}>{children}</p>
}

function CoastalOperationsWorkspace({ user, section, origin }: { user: AuthUser | null; section: 'assessments' | 'alerts' | 'all'; origin?: 'logs' }) {
  const [url, setUrl] = useSearchParams()
  const navigate = useNavigate()
  const [intent] = useState(() => ({ view: url.get('view'), id: url.get('id') }))
  const [restoring, setRestoring] = useState(intent.view === 'edit')
  const [restoreError, setRestoreError] = useState<string | null>(() => intent.view === 'create' && !(section === 'alerts' ? hasAllPermissions(user, [coastalOperationsPermissions.alertCreate]) || hasAllPermissions(user, [coastalOperationsPermissions.alertManage]) : hasAllPermissions(user, [coastalOperationsPermissions.assessmentCreate])) ? 'Your current access does not allow creating this draft. Return to the records to continue.' : null)
  const cancelRestoration = useRef(false)
  const [pageSize, setPageSize] = useState(25)
  const [pageIndex, setPageIndex] = useState(0)
  const [pageCursors, setPageCursors] = useState<Array<string | undefined>>([undefined])
  const [activeAssessmentActivity, setActiveAssessmentActivity] = useState<string | null>(null)
  const [query, setQuery] = useState<RecordQuery>({})
  const [assessmentCursor, setAssessmentCursor] = useState<string | null>(null)
  const [alertCursor, setAlertCursor] = useState<string | null>(null)
  const [recordsLoading, setRecordsLoading] = useState(true)
  const requestGeneration = useRef(0)
  const [selectedAlertId, setSelectedAlertId] = useState<string | null>(intent.view === 'detail' && section === 'alerts' ? intent.id : null)
  const [selectedAlert, setSelectedAlert] = useState<CoastalAlert | null>(null)
  const [alertDetailError, setAlertDetailError] = useState<string | null>(null)
  const workspaceRef = useRef<HTMLDivElement>(null)
  const originRef = useRef<HTMLElement | null>(null)
  const originLabel = useRef<string | null>(null)
  const [activeAlertActivity, setActiveAlertActivity] = useState<string | null>(null)
  const showAssessments = section !== 'alerts'
  const showAlerts = section !== 'assessments'
  const [assessmentItems, setAssessmentItems] = useState<Assessment[]>([])
  const [assessmentError, setAssessmentError] = useState<string | null>(null)
  const [alertItems, setAlertItems] = useState<CoastalAlert[]>([])
  const [alertError, setAlertError] = useState<string | null>(null)
  const [selectedAssessmentId, setSelectedAssessmentId] = useState<string | null>(intent.view === 'detail' && section !== 'alerts' ? intent.id : null)
  const [detail, setDetail] = useState<AssessmentDetail | null>(null)
  const [detailError, setDetailError] = useState<{ assessmentId: string; message: string } | null>(null)
  const [targetStatus, setTargetStatus] = useState<OperationalStatus | null>(null)
  const [targetHistory, setTargetHistory] = useState<OperationalHistoryItem[]>([])
  const [targetError, setTargetError] = useState<string | null>(null)
  const [notice, setNotice] = useState<{ text: string; tone: 'success' | 'error' } | null>(null)
  const [busy, setBusy] = useState(false)
  const [refreshKey, setRefreshKey] = useState(0)
  const [showAssessmentForm, setShowAssessmentForm] = useState(intent.view === 'create' && section !== 'alerts' && hasAllPermissions(user, [coastalOperationsPermissions.assessmentCreate]))
  const [editingAssessment, setEditingAssessment] = useState<Assessment | null>(null)
  const [showAlertForm, setShowAlertForm] = useState(intent.view === 'create' && section === 'alerts' && (hasAllPermissions(user, [coastalOperationsPermissions.alertCreate]) || hasAllPermissions(user, [coastalOperationsPermissions.alertManage])))
  const [editingAlert, setEditingAlert] = useState<CoastalAlert | null>(null)
  const [assessmentConfirmation, setAssessmentConfirmation] = useState<{ assessment: Assessment; action: 'submit' | 'cancel' } | null>(null)
  const [decisionConfirmation, setDecisionConfirmation] = useState<{ alert: CoastalAlert; decision: 'PUBLISH' | 'RESOLVE' } | null>(null)
  const [withdrawalConfirmation, setWithdrawalConfirmation] = useState<CoastalAlert | null>(null)
  const [evidenceRemoval, setEvidenceRemoval] = useState<{ assessment: Assessment; evidence: Evidence } | null>(null)
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
  const has = (permission: string) => hasAllPermissions(user, [permission])
  const canManageAlerts = [coastalOperationsPermissions.alertManage, coastalOperationsPermissions.alertDecide, coastalOperationsPermissions.alertCreate, coastalOperationsPermissions.alertUpdate, coastalOperationsPermissions.alertDelete, coastalOperationsPermissions.alertPublish, coastalOperationsPermissions.alertResolve].some(has)
  const canReadAlerts = has(coastalOperationsPermissions.alertRead) || canManageAlerts
  const canCreateAlerts = has(coastalOperationsPermissions.alertCreate) || has(coastalOperationsPermissions.alertManage)
  const canUpdateAlerts = has(coastalOperationsPermissions.alertUpdate) || has(coastalOperationsPermissions.alertManage)
  const canDeleteAlerts = has(coastalOperationsPermissions.alertDelete) || has(coastalOperationsPermissions.alertManage)
  const canPublishAlerts = has(coastalOperationsPermissions.alertPublish) || has(coastalOperationsPermissions.alertDecide)
  const canResolveAlerts = has(coastalOperationsPermissions.alertResolve) || has(coastalOperationsPermissions.alertDecide)
  const canReadAudit = has(coastalOperationsPermissions.auditRead)
  const hasAccess = hasCoastalOperationsAccess(user)

  useEffect(() => () => {
    if (evidencePreview) URL.revokeObjectURL(evidencePreview.url)
  }, [evidencePreview])

  useEffect(() => {
    let current = true
    requestGeneration.current++
    const controller = new AbortController()
    const requests: Promise<void>[] = []
    if (canReadAssessments && showAssessments) requests.push(listAssessments({ ...query, pageSize, cursor: pageCursors[pageIndex] }, { quiet: true, signal: controller.signal }).then((page) => {
      if (current) { setAssessmentError(null); setAssessmentItems(page.items); setAssessmentCursor(page.nextCursor) }
    }).catch((error: unknown) => { if (current) setAssessmentError(error instanceof Error ? error.message : 'Assessments could not be loaded. Please retry.') }))
    if (canReadAlerts && showAlerts) requests.push(listAlerts({ ...query, pageSize, cursor: pageCursors[pageIndex] }, { quiet: true, signal: controller.signal }).then((page) => {
      if (current) { setAlertError(null); setAlertItems(page.items); setAlertCursor(page.nextCursor) }
    }).catch((error: unknown) => { if (current) setAlertError(error instanceof Error ? error.message : 'Alerts could not be loaded. Please retry.') }))
    void Promise.all(requests).finally(() => { if (current) setRecordsLoading(false) })
    return () => { current = false; controller.abort() }
  }, [canReadAssessments, canReadAlerts, showAssessments, showAlerts, query, refreshKey, pageSize, pageIndex, pageCursors])

  function resetPagination() { setPageIndex(0); setPageCursors([undefined]); setAssessmentCursor(null); setAlertCursor(null) }
  function nextPage(kind: 'assessments' | 'alerts') {
    const next = kind === 'assessments' ? assessmentCursor : alertCursor
    if (!next || recordsLoading) return
    setRecordsLoading(true); setPageCursors([...pageCursors.slice(0, pageIndex + 1), next]); setPageIndex(pageIndex + 1)
  }
  function pagination(kind: 'assessments' | 'alerts') { return <OperationsPagination kind={kind} size={pageSize} count={kind === 'assessments' ? assessmentItems.length : alertItems.length} page={pageIndex} busy={recordsLoading} onSize={(value) => { setRecordsLoading(true); setPageSize(value); resetPagination() }} previous={pageIndex > 0 ? () => { setRecordsLoading(true); setPageIndex(pageIndex - 1) } : undefined} next={(kind === 'assessments' ? assessmentCursor && !assessmentError : alertCursor && !alertError) ? () => nextPage(kind) : undefined} /> }

  function changeQuery(next: RecordQuery) {
    resetPagination()
    requestGeneration.current++
    setAssessmentCursor(null); setAlertCursor(null)
    setSelectedAssessmentId(null); setDetail(null); setActiveAlertActivity(null); setRecordsLoading(true)
    setAssessmentError(null); setAlertError(null); setQuery(next)
  }

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
        canReadStatus && targetId !== '00000000-0000-0000-0000-000000000000' ? getTargetStatus(targetType, targetId) : Promise.resolve(null),
        canReadHistory && targetId !== '00000000-0000-0000-0000-000000000000' ? getTargetHistory(targetType, targetId) : Promise.resolve(null),
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

  useEffect(() => {
    if (intent.view !== 'edit') return
    let current = true
    const validId = intent.id && /^[0-9a-f]{8}-[0-9a-f-]{27}$/i.test(intent.id)
    const read = async () => {
      if (!validId || (section === 'alerts' ? !canUpdateAlerts || !canReadAlerts : !canUpdateAssessment || !canReadAssessments)) throw new Error('This draft cannot be edited with your current access. Return to the records to continue.')
      if (section === 'alerts') {
        const page = await listAlerts({ recordId: intent.id! }, { quiet: true })
        const draft = page.items.find((item) => item.alertId === intent.id)
        if (!draft || draft.lifecycle !== 'PROPOSED') throw new Error('This advisory is no longer an editable draft. Return to Alerts for its current details.')
        if (current && !cancelRestoration.current) setEditingAlert(draft)
      } else {
        const result = await getAssessmentDetail(intent.id!)
        if (result.assessment.workflowStatus !== 'DRAFT') throw new Error('This assessment is no longer an editable draft. Return to Assessments for its current details.')
        if (current && !cancelRestoration.current) setEditingAssessment(result.assessment)
      }
    }
    void read().catch((reason: unknown) => { if (current && !cancelRestoration.current) setRestoreError(reason instanceof Error ? reason.message : 'The draft could not be restored. Return to the records and retry.') }).finally(() => { if (current && !cancelRestoration.current) setRestoring(false) })
    return () => { current = false }
  }, [intent, section, canUpdateAssessment, canUpdateAlerts, canReadAlerts, canReadAssessments])

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

  async function confirmEvidenceRemoval() {
    if (!evidenceRemoval) return
    setBusy(true)
    try {
      await removeAssessmentEvidence(evidenceRemoval.assessment.assessmentId, evidenceRemoval.evidence.evidenceId, evidenceRemoval.assessment.version)
      if (evidencePreview) { URL.revokeObjectURL(evidencePreview.url); setEvidencePreview(null) }
      setNotice({ text: 'The image was removed from this draft. Its activity record is retained.', tone: 'success' })
      setEvidenceRemoval(null); setRefreshKey((value) => value + 1)
    } catch (error) { setNotice({ text: error instanceof Error ? error.message : 'The image could not be removed. Refresh and retry.', tone: 'error' }); setEvidenceRemoval(null) }
    finally { setBusy(false) }
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
    setSelectedAssessmentId(canReadAssessments ? assessment.assessmentId : null)
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
    setSelectedAlert(null); setAlertDetailError(null); setSelectedAlertId(alert.alertId)
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

  const formOpen = !!(showAssessmentForm || editingAssessment || showAlertForm || editingAlert)
  const focused = restoring || !!restoreError || formOpen || !!selectedAssessmentId || !!selectedAlertId
  const backToAlerts = section === 'alerts' || showAlertForm || !!editingAlert || !!selectedAlertId
  const viewKey = formOpen ? `form:${editingAssessment?.assessmentId ?? editingAlert?.alertId ?? (showAlertForm ? 'alert' : 'assessment')}` : selectedAssessmentId ?? selectedAlertId ?? 'list'
  useEffect(() => {
    if (restoring || restoreError) return
    const value = new URLSearchParams()
    if (origin === 'logs') value.set('kind', section === 'alerts' ? 'alerts' : 'assessments')
    if (editingAssessment || editingAlert) { value.set('view', 'edit'); value.set('id', (editingAssessment?.assessmentId ?? editingAlert?.alertId)!) }
    else if (formOpen) value.set('view', 'create')
    else if (selectedAssessmentId || selectedAlertId) { value.set('view', 'detail'); value.set('id', (selectedAssessmentId ?? selectedAlertId)!) }
    if (url.toString() !== value.toString()) setUrl(value, { replace: true })
  }, [restoring, restoreError, editingAssessment, editingAlert, formOpen, selectedAssessmentId, selectedAlertId, origin, section, url, setUrl])
  function rememberOrigin() {
    if (!focused && document.activeElement instanceof HTMLElement) { originRef.current = document.activeElement; originLabel.current = document.activeElement.textContent }
  }
  function backToList() {
    cancelRestoration.current = true
    if (origin === 'logs') { navigate('/operations/logs'); return }
    setRestoreError(null); setRestoring(false)
    setShowAssessmentForm(false); setEditingAssessment(null); setShowAlertForm(false); setEditingAlert(null)
    setSelectedAssessmentId(null); setSelectedAlertId(null); setSelectedAlert(null); setActiveAlertActivity(null)
    if (evidencePreview) { URL.revokeObjectURL(evidencePreview.url); setEvidencePreview(null) }
  }
  useEffect(() => {
    if (viewKey === 'list') {
      const origin = originRef.current?.isConnected ? originRef.current : [...(workspaceRef.current?.querySelectorAll<HTMLElement>('button') ?? [])].find((button) => button.textContent === originLabel.current)
      origin?.focus({ preventScroll: true }); return
    }
    const heading = workspaceRef.current?.querySelector<HTMLElement>('[data-workspace-heading]')
    heading?.focus({ preventScroll: true }); workspaceRef.current?.scrollIntoView?.({ block: 'start' })
  }, [viewKey])
  useEffect(() => {
    if (!selectedAlertId || !canReadAlerts) return
    let current = true
    const controller = new AbortController()
    void (origin === 'logs' ? listAlertLogRecords({ recordId: selectedAlertId }, controller.signal) : listAlerts({ recordId: selectedAlertId }, { quiet: true, signal: controller.signal })).then((page) => {
      if (!current) return
      const record = page.items.find((item) => item.alertId === selectedAlertId)
      if (record) { setSelectedAlert(record); setAlertDetailError(null) }
      else { setSelectedAlert(null); setAlertDetailError('This advisory is unavailable or outside your current access.') }
    }).catch((error: unknown) => { if (current) { setSelectedAlert(null); setAlertDetailError(error instanceof Error ? error.message : 'We could not open this advisory. Retry.') } })
    return () => { current = false; controller.abort() }
  }, [selectedAlertId, canReadAlerts, refreshKey, origin])
  function renderAssessmentDetails() { return <div className="mt-4 rounded-3xl border border-coast-line bg-white p-6 sm:p-8">
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
                          {canSubmitAssessment && <button className="min-h-11 rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue" onClick={() => setAssessmentConfirmation({ assessment: detail.assessment, action: 'submit' })} type="button">{section === 'all' ? 'Submit for review' : 'Publish assessment'}</button>}
                          {canDeleteAssessment && <button className="min-h-11 rounded-full border border-red-200 px-5 text-sm font-bold text-red-800 hover:bg-red-50 focus-visible:outline-2 focus-visible:outline-red-700" onClick={() => setAssessmentConfirmation({ assessment: detail.assessment, action: 'cancel' })} type="button">Cancel draft</button>}
                        </div>}
                        {canReadAudit && <OperationsActivity key={`${detail.assessment.assessmentId}:${refreshKey}`} id={detail.assessment.assessmentId} kind="assessment" revision={refreshKey} />}
                        {detail.decisions.length > 0 && <div><h4 className="font-bold">Recorded decisions</h4><ul className="mt-2 grid gap-2">{detail.decisions.map((decision) => <li className="rounded-2xl bg-coast-sand p-3 text-sm" key={decision.decisionId}><span className="font-bold">{label(decision.decision)}</span><span className="ml-2 text-coast-muted">{formatDate(decision.decidedAt)}</span>{decision.explanation && <p className="mt-1 text-coast-muted">{decision.explanation}</p>}</li>)}</ul></div>}
                        <div className="grid gap-4 lg:grid-cols-2">
                          <div><div className="flex flex-wrap items-center justify-between gap-2"><h4 className="font-bold">Evidence</h4>{canUploadEvidence && detail.assessment.workflowStatus === 'DRAFT' && detail.evidence.length < 5 && <label className="inline-flex min-h-10 cursor-pointer items-center rounded-full border border-coast-line px-4 text-xs font-bold text-coast-deep hover:bg-coast-sage">Add PNG evidence<input accept="image/png,.png" className="sr-only" disabled={busy} onChange={(event) => { const file = event.target.files?.[0]; if (file) void handleEvidence(file, detail.assessment); event.currentTarget.value = '' }} type="file" /></label>}</div>
                            {!canReadEvidence && <p className="mt-2 text-sm leading-6 text-coast-muted">Evidence details are restricted by your current permissions.</p>}
                            {canReadEvidence && detail.evidence.length === 0 && <p className="mt-2 text-sm text-coast-muted">No evidence images have been added.</p>}
                            {canReadEvidence && <ul className="mt-3 grid gap-2">{detail.evidence.map((evidence) => <li className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-coast-line p-3" key={evidence.evidenceId}><span className="text-xs leading-5 text-coast-muted">PNG · {(evidence.byteLength / 1024).toFixed(0)} KiB · {label(evidence.inspectionStatus)}</span><button className="min-h-9 rounded-full px-3 text-xs font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-coast-blue" disabled={busy} onClick={() => void viewEvidence(detail.assessment.assessmentId, evidence)} type="button">View image</button>{canUploadEvidence && detail.assessment.workflowStatus === 'DRAFT' && <button className="min-h-9 rounded-full border border-red-200 px-3 text-xs font-bold text-red-800 hover:bg-red-50 focus-visible:outline-2 focus-visible:outline-red-700" disabled={busy} onClick={() => setEvidenceRemoval({ assessment: detail.assessment, evidence })} type="button">Remove image</button>}</li>)}</ul>}
                            {evidencePreview && <figure className="mt-3 rounded-2xl bg-coast-sand p-3"><img alt="Uploaded assessment evidence" className="max-h-80 w-full rounded-xl object-contain" src={evidencePreview.url} /><figcaption className="mt-2 text-xs text-coast-muted">Evidence is served through your authorized BLUEVERSE session.</figcaption></figure>}
                          </div>
                          {(canReadStatus || canReadHistory) && <div><h4 className="font-bold">Operational status and history</h4>{targetError && <p className="mt-2 text-xs leading-5 text-coast-muted">{targetError}</p>}{canReadStatus && targetStatus && <div className="mt-3 flex flex-wrap items-center gap-3 rounded-2xl bg-coast-sage p-4"><StatusPill value={targetStatus.operationalState} /><span className="text-xs text-coast-muted">Updated {formatDate(targetStatus.updatedAt)}</span></div>}{canReadHistory && <ul className="mt-3 grid gap-2">{targetHistory.length === 0 ? <li className="text-sm text-coast-muted">No operational state changes have been recorded.</li> : targetHistory.map((item) => <li className="rounded-2xl border border-coast-line p-3 text-sm" key={item.historyId}><span className="font-bold">{label(item.previousState)} → {label(item.newState)}</span><span className="ml-2 text-xs text-coast-muted">{formatDate(item.createdAt)}</span></li>)}</ul>}</div>}
                        </div>
                      </div>}
                    </div> }
  function renderAlertCard(alert: CoastalAlert) { return <OperationsRecordCard key={alert.alertId} record={alert} onOpen={selectedAlertId === alert.alertId ? undefined : () => { rememberOrigin(); setSelectedAlert(null); setAlertDetailError(null); setSelectedAlertId(alert.alertId) }} activity={canReadAudit && <><button className="mt-4 min-h-11 rounded-full border border-coast-line px-4 text-sm font-bold" onClick={() => setActiveAlertActivity((value) => value === alert.alertId ? null : alert.alertId)} type="button">{activeAlertActivity === alert.alertId ? 'Hide activity' : 'View activity'}</button>{activeAlertActivity === alert.alertId && <OperationsActivity key={`${alert.alertId}:${refreshKey}`} id={alert.alertId} kind="alert" revision={refreshKey} />}</>}>
                    {(canUpdateAlerts && alert.lifecycle === 'PROPOSED' || canDeleteAlerts && alert.lifecycle === 'PROPOSED' || canPublishAlerts && alert.lifecycle === 'PROPOSED' || canResolveAlerts && alert.lifecycle === 'ACTIVE') && <div className="mt-4 flex flex-wrap gap-2">
                      {canUpdateAlerts && alert.lifecycle === 'PROPOSED' && <button className="min-h-10 rounded-full border border-coast-line px-4 text-xs font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-coast-blue" onClick={() => { setShowAlertForm(false); setEditingAlert(alert); setShowAssessmentForm(false); setEditingAssessment(null) }} type="button">Edit draft</button>}
                      {canDeleteAlerts && alert.lifecycle === 'PROPOSED' && <button className="min-h-10 rounded-full border border-red-200 px-4 text-xs font-bold text-red-800 hover:bg-red-50 focus-visible:outline-2 focus-visible:outline-red-700" onClick={() => setWithdrawalConfirmation(alert)} type="button">Withdraw draft</button>}
                      {canPublishAlerts && alert.lifecycle === 'PROPOSED' && <button className="min-h-10 rounded-full bg-coast-deep px-4 text-xs font-bold text-white hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue" onClick={() => setDecisionConfirmation({ alert, decision: 'PUBLISH' })} type="button">Publish advisory</button>}
                      {canResolveAlerts && alert.lifecycle === 'ACTIVE' && <button className="min-h-10 rounded-full border border-coast-line px-4 text-xs font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-coast-blue" onClick={() => setDecisionConfirmation({ alert, decision: 'RESOLVE' })} type="button">Resolve advisory</button>}
                    </div>}
  </OperationsRecordCard> }

  const page = <div className="flex min-h-screen flex-col bg-coast-paper font-sans text-coast-ink" id="top">
    <SiteHeader active="operations" />
    <main className="mx-auto grid w-full max-w-[90rem] flex-1 grid-cols-1 content-start gap-6 px-4 py-6 sm:px-8 sm:py-10 lg:grid-cols-[15rem_minmax(0,1fr)] lg:items-start lg:gap-8 lg:px-6 lg:py-0">
      <AccountAreaNavigation active="operations" />
      <div className="min-w-0 scroll-mt-24 lg:py-10" ref={workspaceRef}>
        {!focused && <nav aria-label="Coastal record sections" className="sticky top-20 z-20 mb-5 flex gap-3 border-b border-coast-line bg-coast-paper/95 py-3 backdrop-blur">
          {coastalNavigationLinks(user).filter((item) => item.label !== 'Logs').map((item) => <Link key={item.href} aria-current={(section === 'alerts' ? item.label === 'Alerts' : item.label === 'Assessments') ? 'page' : undefined} className={`min-h-11 rounded-full px-5 py-3 font-bold ${(section === 'alerts' ? item.label === 'Alerts' : item.label === 'Assessments') ? 'bg-coast-deep text-white' : 'border border-coast-line'}`} to={item.href}>{item.label}</Link>)}
        </nav>}
        {!focused && <>
        <div className="grid gap-5"><section aria-labelledby="operations-title" className="relative isolate overflow-hidden rounded-[2rem] bg-coast-deep text-white shadow-sm">
          <img alt={section === 'alerts' ? 'A coastal steward guiding visitors toward a beach access path' : 'Coastal field workers inspecting a beach access path and dune vegetation'} className="absolute inset-0 -z-20 h-full w-full object-cover opacity-60" src={section === 'alerts' ? alertsHero : assessmentHero} />
          <div aria-hidden="true" className="absolute inset-0 -z-10 bg-gradient-to-r from-coast-deep via-coast-deep/90 to-coast-deep/35" />
          <div className="max-w-3xl px-5 py-8 sm:px-8 sm:py-10 lg:px-10 lg:py-12">
            <p className="text-[11px] font-extrabold tracking-[0.18em] text-coast-glass">COASTAL CARE</p>
            <h1 className="mt-3 font-display text-4xl leading-tight tracking-[-0.05em] sm:text-5xl" id="operations-title">{section === 'alerts' ? 'Clear updates. Safer coastal days.' : 'Look after the places we share.'}</h1>
            <p className="mt-4 max-w-2xl text-sm leading-6 text-white/85 sm:text-base sm:leading-7">{section === 'alerts' ? 'Prepare a clear notice, choose who should see it, and publish when the details are ready. Follow each update through resolution.' : 'Start with a coastal review draft. Gather evidence, publish it for assessment, and follow the recommendation and human review when available.'}</p>
          </div>
        </section></div>


        </>}
        {focused && <div className="mb-6">
          <button className="mb-5 inline-flex min-h-11 items-center gap-2 rounded-full px-3 text-sm font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-coast-blue" disabled={busy} onClick={backToList} type="button"><OperationsIcon name="back" />Back to {origin === 'logs' ? 'Logs' : backToAlerts ? 'Alerts' : 'Assessments'}</button>
          <h1 className="font-display text-3xl tracking-[-0.04em] sm:text-4xl" data-workspace-heading tabIndex={-1}>{formOpen ? (editingAssessment || editingAlert ? 'Edit your draft' : backToAlerts ? 'Prepare a coastal advisory' : 'New assessment') : selectedAssessmentId ? detail?.assessment.title || 'Assessment details' : selectedAlert?.title || 'Advisory details'}</h1>
        </div>}
        {!hasAccess ? <section className="mt-6 rounded-3xl border border-coast-line bg-coast-pearl p-6 sm:p-9">
          <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">ACCOUNT ACCESS</p>
          <h2 className="mt-3 font-display text-3xl tracking-[-0.04em]">This workspace is not available to your account.</h2>
          <p className="mt-3 max-w-xl text-sm leading-6 text-coast-muted">Coastal Operations access is granted through your account permissions. If you need access, contact your BLUEVERSE administrator.</p>
        </section> : <>
          {notice && <div className="mt-5"><Notice tone={notice.tone}>{notice.text}</Notice></div>}
          {!focused && <section className="mt-7 grid gap-5 xl:grid-cols-[minmax(0,1fr)_auto] xl:items-center">
            <div>
              <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">OPERATIONS WORKSPACE</p>
              <h2 className="mt-2 font-display text-3xl tracking-[-0.045em] sm:text-4xl">{section === 'all' ? 'Assessments and advisories' : section === 'alerts' ? 'Alerts' : 'Assessments'}</h2>
              <p className="mt-2 max-w-2xl text-sm leading-6 text-coast-muted">{section === 'alerts' ? 'Keep notices clear, current, and useful for the people who need them.' : 'Pick up a saved draft or follow a published review and its coastal context.'}</p>
            </div>
            <div className="flex flex-wrap gap-3">
              {showAssessments && canCreateAssessment && <button className="inline-flex min-h-11 items-center justify-center rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white shadow-sm transition hover:-translate-y-0.5 hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue" onClick={() => { rememberOrigin(); setSelectedAssessmentId(null); setSelectedAlertId(null); setShowAssessmentForm((shown) => !shown); setEditingAssessment(null); setShowAlertForm(false); setEditingAlert(null) }} type="button">{showAssessmentForm && !editingAssessment ? 'Close assessment form' : 'New assessment'}</button>}
              {showAlerts && canCreateAlerts && <button className="inline-flex min-h-11 items-center justify-center rounded-full border border-coast-line bg-coast-pearl px-5 text-sm font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue" onClick={() => { rememberOrigin(); setSelectedAssessmentId(null); setSelectedAlertId(null); setEditingAlert(null); setShowAlertForm((shown) => !shown); setShowAssessmentForm(false); setEditingAssessment(null) }} type="button">{showAlertForm ? 'Close alert form' : 'Prepare an advisory'}</button>}
              <button aria-label="Refresh coastal operations" className="inline-flex min-h-11 items-center justify-center rounded-full border border-coast-line bg-white px-4 text-sm font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:cursor-wait" disabled={busy} onClick={() => { setRecordsLoading(true); setRefreshKey((key) => key + 1) }} type="button">Refresh</button>
            </div>
          </section>}

          {restoring && <p role="status" className="py-6">Restoring the current draft…</p>}
          {restoreError && <Notice tone="error">{restoreError}</Notice>}
          {(showAssessmentForm || editingAssessment) && (editingAssessment ? canUpdateAssessment : canCreateAssessment) && <section aria-label={editingAssessment ? 'Edit assessment draft' : 'Create assessment'} className="mt-8"><AssessmentDraftForm key={editingAssessment?.assessmentId ?? 'new'} existing={editingAssessment} onCancel={() => { setShowAssessmentForm(false); setEditingAssessment(null) }} onSaved={(assessment) => void handleAssessmentSaved(assessment)} /></section>}
          {showAlertForm && canCreateAlerts && <section aria-label="Create advisory" className="mt-5"><AlertDraftForm existing={null} onCancel={() => setShowAlertForm(false)} onSaved={(alert) => void handleAlertSaved(alert)} /></section>}
          {editingAlert && canUpdateAlerts && <section aria-label="Edit advisory draft" className="mt-5"><AlertDraftForm existing={editingAlert} onCancel={() => setEditingAlert(null)} onSaved={(alert) => void handleAlertSaved(alert)} /></section>}

          <div hidden={focused} className={focused ? 'hidden' : 'mt-7 grid items-start gap-7'}>
            {showAssessments && <section aria-label="Assessment records" className="min-w-0 rounded-[2rem] border border-coast-line bg-white/55 p-5 sm:p-7 lg:p-8">
              <OperationsSearch kind="assessments" canManage={canReadQueue} loading={recordsLoading} active={!focused} onChange={changeQuery} />
              {assessmentError && <Notice tone="error">{assessmentError} <button className="underline" onClick={() => { setRecordsLoading(true); setRefreshKey((key) => key + 1) }} type="button">Retry assessments</button></Notice>}
              {!canReadAssessments ? <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 text-sm leading-6 text-coast-muted">Your current permissions allow other Coastal Operations actions, but do not include assessment reading.</div>
                : !recordsLoading && !assessmentError && assessmentItems.length === 0 ? <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 sm:p-8"><h3 className="font-display text-xl">No assessments to show yet</h3><p className="mt-2 text-sm leading-6 text-coast-muted">Saved drafts and submitted reviews will appear here.</p></div>
                  : <div className="grid gap-5">{assessmentItems.map((assessment) => <div key={assessment.assessmentId}>
                    <OperationsRecordCard record={assessment} onOpen={() => { rememberOrigin(); setSelectedAssessmentId(assessment.assessmentId) }} activity={canReadAudit && <><button className="mt-3 min-h-11 rounded-full border border-coast-line px-4 text-sm font-bold" onClick={() => setActiveAssessmentActivity(activeAssessmentActivity === assessment.assessmentId ? null : assessment.assessmentId)} type="button">{activeAssessmentActivity === assessment.assessmentId ? 'Hide activity' : 'View activity'}</button>{activeAssessmentActivity === assessment.assessmentId && <OperationsActivity kind="assessment" id={assessment.assessmentId} revision={refreshKey} />}</>} />
                  </div>)}</div>}
              {canReadAssessments && pagination('assessments')}
            </section>}

            {showAlerts && <section aria-label="Alert records" className="min-w-0 rounded-[2rem] border border-coast-line bg-white/55 p-5 sm:p-7 lg:p-8">
              <OperationsSearch kind="alerts" canManage={canManageAlerts} loading={recordsLoading} active={!focused} onChange={changeQuery} />
              {alertError && <Notice tone="error">{alertError} <button className="underline" onClick={() => { setRecordsLoading(true); setRefreshKey((key) => key + 1) }} type="button">Retry alerts</button></Notice>}
              {!canReadAlerts ? <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 text-sm leading-6 text-coast-muted">Your current permissions do not include advisory reading. You may still prepare a draft if you have advisory management access.</div>
                : !recordsLoading && !alertError && alertItems.length === 0 ? <div className="rounded-3xl border border-coast-line bg-coast-pearl p-6 sm:p-8"><h3 className="font-display text-xl">No advisories to show</h3><p className="mt-2 text-sm leading-6 text-coast-muted">Active public updates and your proposed drafts will appear here.</p></div>
                  : <div className="grid gap-5">{alertItems.map((alert) => renderAlertCard(alert))}</div>}
              {canReadAlerts && pagination('alerts')}
            </section>}
          </div>
          {!formOpen && selectedAssessmentId && <section aria-label="Assessment details">
            {detail?.assessment.assessmentId === selectedAssessmentId && <p className="mb-5 text-sm leading-7 text-coast-muted">{detail.assessment.objective}</p>}
            {renderAssessmentDetails()}
          </section>}
          {!formOpen && selectedAlertId && <section aria-label="Advisory details">
            {alertDetailError && <Notice tone="error">{alertDetailError} <button className="underline" onClick={() => { setRecordsLoading(true); setRefreshKey((key) => key + 1) }} type="button">Retry advisory</button></Notice>}
            {!selectedAlert && !alertDetailError && <p role="status">Opening the advisory…</p>}
            {selectedAlert?.alertId === selectedAlertId && renderAlertCard(selectedAlert)}
          </section>}

          {evidenceRemoval && <div className="fixed inset-0 z-[90] flex items-center justify-center bg-coast-ink/45 p-4" role="presentation"><section aria-labelledby="remove-evidence-title" aria-modal="true" className="w-full max-w-lg rounded-3xl border border-red-200 bg-coast-pearl p-6 shadow-2xl" role="dialog">
            <h2 className="font-display text-2xl text-red-900" id="remove-evidence-title">Remove this draft image?</h2>
            <p className="mt-3 text-sm leading-6">The image will no longer be attached. The upload and removal remain in the activity history.</p>
            <div className="mt-6 flex flex-wrap justify-end gap-3"><button className="min-h-11 rounded-full border border-coast-line px-5 text-sm font-bold" disabled={busy} onClick={() => setEvidenceRemoval(null)} type="button">Keep image</button><button className="min-h-11 rounded-full bg-red-700 px-5 text-sm font-bold text-white hover:bg-red-800 focus-visible:outline-2 focus-visible:outline-red-700" disabled={busy} onClick={() => void confirmEvidenceRemoval()} type="button">{busy ? 'Removing…' : 'Confirm removal'}</button></div>
          </section></div>}
          {decisionConfirmation && <div className="fixed inset-0 z-[90] flex items-center justify-center bg-coast-ink/45 p-4" role="presentation"><section aria-labelledby="alert-decision-title" aria-modal="true" className="w-full max-w-lg rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-2xl" role="dialog">
            <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">CONFIRM ADVISORY CHANGE</p>
            <h2 className="mt-2 font-display text-2xl" id="alert-decision-title">{decisionConfirmation.decision === 'PUBLISH' ? 'Publish this advisory?' : 'Resolve this advisory?'}</h2>
            <p className="mt-3 text-sm leading-6 text-coast-muted">{decisionConfirmation.decision === 'PUBLISH' ? `“${decisionConfirmation.alert.title}” will become active for its selected audience and validity period.` : `“${decisionConfirmation.alert.title}” will be marked resolved.`}</p>
            {decisionConfirmation.decision === 'PUBLISH' && ['HIGH', 'CRITICAL'].includes(decisionConfirmation.alert.severity) && <p className="mt-4 rounded-2xl bg-coast-sand p-4 text-sm leading-6 text-coast-ink">A different authorized reviewer from the draft creator and linked assessment initiator must publish this {label(decisionConfirmation.alert.severity).toLowerCase()} advisory.</p>}
            <div className="mt-6 flex flex-wrap justify-end gap-3"><button className="min-h-11 rounded-full border border-red-200 px-5 text-sm font-bold text-red-800 hover:bg-red-50 focus-visible:outline-2 focus-visible:outline-red-700" disabled={busy} onClick={() => setDecisionConfirmation(null)} type="button">Cancel</button><button className="min-h-11 rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue disabled:cursor-wait" disabled={busy} onClick={() => void confirmAlertDecision()} type="button">{busy ? 'Updating…' : decisionConfirmation.decision === 'PUBLISH' ? 'Confirm publish' : 'Confirm resolution'}</button></div>
          </section></div>}
          {assessmentConfirmation && <div className="fixed inset-0 z-[90] flex items-center justify-center bg-coast-ink/45 p-4" role="presentation"><section aria-labelledby="assessment-action-title" aria-modal="true" className="w-full max-w-lg rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-2xl" role="dialog">
            <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">ASSESSMENT DRAFT</p>
            <h2 className="mt-2 font-display text-2xl" id="assessment-action-title">{assessmentConfirmation.action === 'submit' ? (section === 'all' ? 'Submit this assessment?' : 'Publish this assessment?') : 'Cancel this draft?'}</h2>
            <p className="mt-3 text-sm leading-6 text-coast-muted">{assessmentConfirmation.action === 'submit' ? 'Publishing closes draft editing and checks the latest coastal context. The assessment is queued for the assessment agent when connected. Publication does not approve a recommendation or change coastal access.' : 'This draft will be cancelled and retained in the audit history for authorized reviewers.'}</p>
            <div className="mt-6 flex flex-wrap justify-end gap-3"><button className="min-h-11 rounded-full border border-coast-line px-5 text-sm font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-coast-blue" disabled={busy} onClick={() => setAssessmentConfirmation(null)} type="button">Keep draft</button><button className={`min-h-11 rounded-full px-5 text-sm font-extrabold text-white focus-visible:outline-2 focus-visible:outline-offset-3 disabled:cursor-wait ${assessmentConfirmation.action === 'cancel' ? 'bg-red-700 hover:bg-red-800 focus-visible:outline-red-700' : 'bg-coast-deep hover:bg-coast-blue focus-visible:outline-coast-blue'}`} disabled={busy} onClick={() => void confirmAssessmentAction()} type="button">{busy ? 'Updating…' : assessmentConfirmation.action === 'submit' ? (section === 'all' ? 'Submit assessment' : 'Confirm publication') : 'Confirm cancellation'}</button></div>
          </section></div>}
          {withdrawalConfirmation && <div className="fixed inset-0 z-[90] flex items-center justify-center bg-coast-ink/45 p-4" role="presentation"><section aria-labelledby="alert-withdrawal-title" aria-modal="true" className="w-full max-w-lg rounded-3xl border border-coast-line bg-coast-pearl p-6 shadow-2xl" role="dialog">
            <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">ADVISORY DRAFT</p>
            <h2 className="mt-2 font-display text-2xl" id="alert-withdrawal-title">Withdraw this draft?</h2>
            <p className="mt-3 text-sm leading-6 text-coast-muted">“{withdrawalConfirmation.title}” will be marked withdrawn and kept in the advisory history for managers.</p>
            <div className="mt-6 flex flex-wrap justify-end gap-3"><button className="min-h-11 rounded-full border border-coast-line px-5 text-sm font-bold text-coast-deep hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-coast-blue" disabled={busy} onClick={() => setWithdrawalConfirmation(null)} type="button">Keep draft</button><button className="min-h-11 rounded-full bg-red-700 px-5 text-sm font-extrabold text-white hover:bg-red-800 focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-red-700 disabled:cursor-wait" disabled={busy} onClick={() => void confirmAlertWithdrawal()} type="button">{busy ? 'Updating…' : 'Withdraw draft'}</button></div>
          </section></div>}
        </>}
      </div>
    </main>
    <SiteFooter />
  </div>

  return page
}

export default function CoastalOperationsPage({ section = 'all', origin }: { section?: 'assessments' | 'alerts' | 'all'; origin?: 'logs' }) {
  const { user } = useAuthSession()
  const scope = `${user?.id ?? 'signed-out'}:${[...(user?.permissions ?? [])].sort().join(',')}`
  return <CoastalOperationsWorkspace key={`${scope}:${section}`} user={user} section={section} origin={origin} />
}
