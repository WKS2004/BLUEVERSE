import { withLoadingScreen } from '../loading/backendLoading'

export type CoastalTargetType = 'DESTINATION' | 'ACTIVITY' | 'OFFERING' | 'SESSION'

export type ComponentDependency = {
  service: string
  status: string
  attempts: number
  retries: number
  retryable: boolean
  checkedAt: string | null
}

export type Assessment = {
  assessmentId: string
  workflowId: string
  targetType: CoastalTargetType
  targetId: string
  sourceWorkflowId: string | null
  periodStartsAt: string
  periodEndsAt: string
  objective: string
  workflowStatus: string
  aiDependencyStatus: string
  aiDispatchOutcome: string
  aiDispatchRetryable: boolean
  componentDependencies: ComponentDependency[]
  version: number
  createdAt: string
  updatedAt: string
}

export type Evidence = {
  evidenceId: string
  assessmentVersion: number
  mediaType: string
  byteLength: number
  contentSha256: string
  inspectionStatus: string
  uploadedAt: string
  expiresAt: string
}

export type AssessmentDetail = {
  assessment: Assessment
  decisions: Array<{
    decisionId: string
    decision: string
    workflowStatus: string
    decidedAt: string
    explanation: string | null
  }>
  evidence: Evidence[]
}

export type CoastalPage<T> = { items: T[]; nextCursor: string | null }

export type CoastalAlert = {
  alertId: string
  targetType: CoastalTargetType
  targetId: string
  assessmentId: string | null
  title: string
  description: string
  severity: string
  visibility: string
  lifecycle: string
  validFrom: string
  validUntil: string
  version: number
  createdAt: string
  updatedAt: string
}

export type OperationalStatus = {
  targetType: CoastalTargetType
  targetId: string
  operationalState: string
  stateVersion: number
  updatedAt: string
}

export type OperationalHistoryItem = {
  historyId: string
  previousState: string
  newState: string
  assessmentId: string
  decisionId: string
  createdAt: string
}

export class CoastalOperationsApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
    this.name = 'CoastalOperationsApiError'
  }
}

function friendlyMessage(status: number) {
  if (status === 401) return 'Your session needs to be refreshed. Sign in again, then retry.'
  if (status === 403) return 'Your current permissions do not allow this action.'
  if (status === 404) return 'This coastal record is unavailable or outside your access.'
  if (status === 409) return 'This record changed while you were viewing it. Refresh and try again.'
  if (status === 413) return 'That image is larger than the 5 MiB limit.'
  if (status === 415) return 'Choose a PNG image to attach as evidence.'
  if (status === 422) return 'The service could not accept those details. Review the form and try again.'
  if (status === 503) return 'Coastal operations is temporarily unavailable. Try again shortly.'
  return 'We could not complete that coastal operations request. Please try again.'
}

function idempotencyKey() {
  if (typeof globalThis.crypto?.randomUUID === 'function') return globalThis.crypto.randomUUID()
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`
}

async function readJson<T>(response: Response): Promise<T> {
  const content = await response.text()
  let payload: unknown
  if (content.trim()) {
    try { payload = JSON.parse(content) as unknown } catch { payload = undefined }
  }

  if (!response.ok) throw new CoastalOperationsApiError(response.status, friendlyMessage(response.status))
  if (payload === undefined || payload === null) {
    throw new CoastalOperationsApiError(response.status, 'The service returned a response that could not be read.')
  }
  return payload as T
}

function jsonOptions(method: string, body?: unknown, key?: string): RequestInit {
  return {
    method,
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
      ...(key ? { 'Idempotency-Key': key } : {}),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  }
}

function publicApiOptions(init: RequestInit = {}): RequestInit {
  return {
    ...init,
    credentials: 'include',
    headers: { Accept: 'application/json', ...(init.headers as Record<string, string> | undefined) },
  }
}

async function request<T>(send: () => Promise<Response>): Promise<T> {
  return withLoadingScreen(async () => {
    let response: Response
    try {
      response = await send()
    } catch {
      throw new CoastalOperationsApiError(0, 'We could not reach coastal operations. Check your connection and retry.')
    }
    return readJson<T>(response)
  })
}

export function listAssessments() {
  return request<CoastalPage<Assessment>>(() => fetch('/api/operations/assessments?pageSize=100', publicApiOptions()))
}

export function createAssessment(input: {
  targetType: CoastalTargetType
  targetId: string
  sourceWorkflowId?: string
  periodStartsAt: string
  periodEndsAt: string
  objective: string
}) {
  return request<Assessment>(() => fetch('/api/operations/assessments', publicApiOptions(jsonOptions('POST', {
    ...input,
    sourceWorkflowId: input.sourceWorkflowId || null,
  }, idempotencyKey()))))
}

export function getAssessmentDetail(assessmentId: string) {
  return request<AssessmentDetail>(() => fetch('/api/operations/assessments/{assessmentId:guid}'.replace('{assessmentId:guid}', encodeURIComponent(assessmentId)), publicApiOptions()))
}

export function uploadAssessmentEvidence(assessmentId: string, image: File) {
  const body = new FormData()
  body.set('Image', image, image.name)
  return request<Evidence>(() => fetch('/api/operations/assessments/{assessmentId:guid}/evidence'.replace('{assessmentId:guid}', encodeURIComponent(assessmentId)), publicApiOptions({
    method: 'POST', credentials: 'include', headers: { Accept: 'application/json' }, body,
  })))
}

export async function getEvidenceImage(assessmentId: string, evidenceId: string): Promise<Blob> {
  return withLoadingScreen(async () => {
    let response: Response
    try {
      response = await fetch('/api/operations/assessments/{assessmentId:guid}/evidence/{evidenceId:guid}'
        .replace('{assessmentId:guid}', encodeURIComponent(assessmentId))
        .replace('{evidenceId:guid}', encodeURIComponent(evidenceId)), {
        credentials: 'include', headers: { Accept: 'image/png' },
      })
    } catch {
      throw new CoastalOperationsApiError(0, 'We could not reach coastal operations. Check your connection and retry.')
    }
    if (!response.ok) throw new CoastalOperationsApiError(response.status, friendlyMessage(response.status))
    if (!response.headers.get('content-type')?.toLowerCase().startsWith('image/png')) {
      throw new CoastalOperationsApiError(response.status, 'The evidence image could not be displayed safely.')
    }
    return response.blob()
  })
}

export function listAlerts() {
  return request<CoastalPage<CoastalAlert>>(() => fetch('/api/operations/alerts?pageSize=100', publicApiOptions()))
}

export function createAlertDraft(input: {
  targetType: CoastalTargetType
  targetId: string
  assessmentId?: string
  title: string
  description: string
  severity: string
  visibility: string
  validFrom: string
  validUntil: string
}) {
  return request<CoastalAlert>(() => fetch('/api/operations/alerts', publicApiOptions(jsonOptions('POST', {
    ...input,
    assessmentId: input.assessmentId || null,
  }))))
}

export function updateAlertDraft(alertId: string, input: {
  expectedVersion: number
  title: string
  description: string
  severity: string
  visibility: string
  validFrom: string
  validUntil: string
}) {
  return request<CoastalAlert>(() => fetch('/api/operations/alerts/{alertId:guid}'.replace('{alertId:guid}', encodeURIComponent(alertId)), publicApiOptions(jsonOptions('PATCH', input))))
}

export function decideAlert(alertId: string, decision: 'PUBLISH' | 'RESOLVE', expectedVersion: number) {
  return request(() => fetch('/api/operations/alerts/{alertId:guid}/decisions'.replace('{alertId:guid}', encodeURIComponent(alertId)), publicApiOptions(jsonOptions('POST', {
    decision,
    expectedVersion,
  }, idempotencyKey()))))
}

export function getTargetStatus(targetType: CoastalTargetType, targetId: string) {
  return request<OperationalStatus>(() => fetch('/api/operations/targets/{targetType}/{targetId:guid}/status'
    .replace('{targetType}', encodeURIComponent(targetType))
    .replace('{targetId:guid}', encodeURIComponent(targetId)), publicApiOptions()))
}

export function getTargetHistory(targetType: CoastalTargetType, targetId: string) {
  return request<CoastalPage<OperationalHistoryItem>>(() => fetch('/api/operations/targets/{targetType}/{targetId:guid}/history?pageSize=25'
    .replace('{targetType}', encodeURIComponent(targetType))
    .replace('{targetId:guid}', encodeURIComponent(targetId)), publicApiOptions()))
}
