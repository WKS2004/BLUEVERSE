import type { ReactNode } from 'react'
import type { Assessment, CoastalAlert } from '../../features/coastalOperations/operationsApi'

function human(value: string) {
  if (value === 'PROPOSED') return 'Draft'
  if (value === 'SUBMITTED') return 'Published for assessment'
  return value.toLowerCase().replaceAll('_', ' ').replace(/\b\w/g, (letter) => letter.toUpperCase())
}
function date(value: string) {
  return Number.isNaN(Date.parse(value)) ? 'Time not available' : new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}
export default function OperationsRecordCard({ record, onOpen, activity, children }: { record: Assessment | CoastalAlert; onOpen?: () => void; activity?: ReactNode; children?: ReactNode }) {
  const assessment = !('alertId' in record)
  const id = assessment ? record.assessmentId : record.alertId
  const content = <>
    <div className="flex flex-wrap items-start justify-between gap-3"><div><p className="text-xs font-bold tracking-wide text-coast-muted">{human(record.targetType)}</p><h3 className="mt-2 font-display text-xl sm:text-2xl">{record.title || (assessment ? record.objective : 'Coastal notice')}</h3></div><span className="rounded-full bg-coast-sand px-3 py-1 text-xs font-bold">{human(assessment ? record.workflowStatus : record.lifecycle)}</span></div>
    {!assessment && <><p className="mt-3 text-sm leading-6 text-coast-muted">{record.description}</p><p className="mt-2 text-xs font-bold text-coast-muted">{human(record.severity)} · {record.visibility === 'PUBLIC' ? 'For coastal visitors' : 'Operations team'}</p></>}
    <p className="mt-3 break-all text-xs leading-6 text-coast-muted">ID: {id}<br />{assessment ? 'Period:' : 'Valid'} {date(assessment ? record.periodStartsAt : record.validFrom)} – {date(assessment ? record.periodEndsAt : record.validUntil)}<br />Created {date(record.createdAt)}<br />Updated {date(record.updatedAt)}</p>
    {onOpen && <p className="mt-3 text-xs font-semibold text-coast-deep">{assessment ? 'Open review details' : 'Open advisory details'} <span aria-hidden="true">→</span></p>}
  </>
  return <article className="rounded-2xl border border-coast-line bg-white p-5">
    {onOpen ? <button className="block w-full text-left hover:text-coast-blue focus-visible:outline-2 focus-visible:outline-coast-blue" onClick={onOpen} type="button">{content}</button> : content}
    {children}{activity}
  </article>
}
