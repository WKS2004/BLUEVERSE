import { useEffect, useState } from 'react'
import { getAlertAudit, getAssessmentAudit } from '../../features/coastalOperations/operationsApi'
import type { OperationsAudit } from '../../features/coastalOperations/operationsApi'

const actionNames: Record<string, string> = { CREATED: 'Created a draft', DRAFT_UPDATED: 'Updated the draft', UPDATED: 'Updated the record', SUBMITTED: 'Published for assessment', CANCELLED: 'Cancelled the draft', DRAFT_WITHDRAWN: 'Withdrew the draft', WITHDRAWN: 'Withdrew the record', PUBLISH: 'Published the advisory', RESOLVE: 'Resolved the advisory', UPLOADED: 'Attached an evidence image', REMOVED: 'Removed the draft evidence image', EXPIRED: 'Expired the retained content', DECISION_APPROVE: 'Approved the recommendation', DECISION_REJECT: 'Rejected the recommendation', DECISION_REQUEST_REVISION: 'Requested a revision' }
const fieldNames: Record<string, string> = { AiDependencyStatus: 'Context availability', AiDispatchOutcome: 'Review delivery status', AiDispatchRetryable: 'Retry available', TargetId: 'Related coastal record', SourceWorkflowId: 'Related coastal plan', PeriodStartsAt: 'Period starts', PeriodEndsAt: 'Period ends', ValidFrom: 'Valid from', ValidUntil: 'Valid until', TimeZoneId: 'Time zone', WorkflowStatus: 'Assessment status', InspectionStatus: 'Image status', ByteLength: 'Image size (bytes)', AppliedOperationalState: 'Applied coastal state', WorkflowStatusAfterDecision: 'Status after decision' }
function fieldName(value: string) { return fieldNames[value] || value.replace(/([a-z])([A-Z])/g, '$1 $2') }
function fieldValue(value: string | null, field: string) {
  if (value === null || value === '') return 'Not set'
  if (/At$|^ValidFrom$|^ValidUntil$/.test(field) && !Number.isNaN(Date.parse(value))) return new Date(value).toLocaleString()
  if (['TargetType', 'WorkflowStatus', 'Lifecycle', 'Severity', 'Visibility', 'InspectionStatus', 'Decision', 'AppliedOperationalState', 'WorkflowStatusAfterDecision'].includes(field)) return value.toLowerCase().replaceAll('_', ' ').replace(/\b\w/g, (letter) => letter.toUpperCase())
  return value
}
export default function OperationsActivity({ kind, id, revision, showReference = false }: { kind: 'assessment' | 'alert'; id: string; revision: number; showReference?: boolean }) {
  const [items, setItems] = useState<OperationsAudit[]>([])
  const [cursor, setCursor] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let current = true
    const read = kind === 'assessment' ? getAssessmentAudit : getAlertAudit
    void read(id).then((page) => { if (current) { setItems(page.items); setCursor(page.nextCursor) } })
      .catch((reason: unknown) => { if (current) setError(reason instanceof Error ? reason.message : 'Activity could not be loaded.') })
      .finally(() => { if (current) setLoading(false) })
    return () => { current = false }
  }, [kind, id, revision, retry])
  async function more() {
    if (!cursor || loading) return
    setLoading(true); setError(null)
    try {
      const page = await (kind === 'assessment' ? getAssessmentAudit : getAlertAudit)(id, cursor)
      setItems((previous) => [...previous, ...page.items]); setCursor(page.nextCursor)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Activity could not be loaded.') }
    finally { setLoading(false) }
  }
  return <section aria-label="Record activity" className="mt-5 rounded-2xl border border-coast-line p-4">
    <h4 className="font-bold">Record activity</h4>
    <p className="mt-1 text-xs leading-5 text-coast-muted">Draft changes, publication and decisions, newest first. Times are shown in your local time zone.</p>
    {error && <p className="mt-3 text-sm" role="alert">{error} <button className="underline" onClick={() => { setError(null); setItems([]); setCursor(null); setLoading(true); setRetry((value) => value + 1) }} type="button">Retry activity</button></p>}
    {!loading && !error && items.length === 0 && <p className="mt-3 text-sm text-coast-muted">No activity recorded yet.</p>}
    <ol className="mt-3 grid gap-3">{items.map((item) => <li className="border-l-2 border-coast-teal pl-3" key={item.auditId}>
      <p className="text-sm font-bold">{item.summary || actionNames[item.action] || item.action.toLowerCase().replaceAll('_', ' ')}</p>
      {!showReference && <p className="mt-1 text-sm">{item.actorName || (item.actorId === '00000000-0000-0000-0000-000000000000' ? 'System' : 'Team member (name not recorded)')} · {item.actorRoles?.length ? item.actorRoles.join(', ') : 'Role not recorded'}</p>}
      {item.recordTitle && <p className="mt-1 text-xs text-coast-muted">{item.recordTitle}</p>}
      <time className="text-xs text-coast-muted" dateTime={item.createdAt}>{new Date(item.createdAt).toLocaleString()}</time>
      {item.changes?.length ? <dl className="mt-3 grid gap-2 rounded-xl bg-coast-sand/50 p-3">{item.changes.map((change, index) => <div key={`${change.field}:${index}`}><dt className="text-xs font-bold">{fieldName(change.field)}</dt><dd className="mt-1 whitespace-pre-wrap break-words text-xs leading-5 text-coast-muted"><span className="font-semibold">Before:</span> {fieldValue(change.before, change.field)}<br /><span className="font-semibold">After:</span> {fieldValue(change.after, change.field)}</dd></div>)}</dl> : <p className="mt-2 text-xs text-coast-muted">Field details were not recorded for this event.</p>}
      {showReference && <details className="mt-3 rounded-xl border border-coast-line bg-coast-pearl px-3 py-2 text-xs text-coast-muted"><summary className="cursor-pointer font-semibold text-coast-deep">Reference details</summary><p className="mt-2">Correlation reference</p><code className="mt-1 block break-all rounded-lg bg-coast-sand px-2 py-1">{item.correlationId}</code></details>}
    </li>)}</ol>
    {loading && <p aria-live="polite" className="mt-3 text-sm">Loading activity…</p>}
    {cursor && <button className="mt-3 min-h-11 rounded-full border border-coast-line px-4 text-sm font-bold" disabled={loading} onClick={() => void more()} type="button">Load earlier activity</button>}
  </section>
}
