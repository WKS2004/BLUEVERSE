import { useEffect, useState } from 'react'
import { useAuthSession } from '../../features/auth/authSession'
import type { AuthUser } from '../../features/auth/auth'
import { listAssessmentLogRecords, listAlertLogRecords } from '../../features/coastalOperations/operationsApi'
import type { Assessment, CoastalAlert, RecordQuery } from '../../features/coastalOperations/operationsApi'
import { coastalOperationsPermissions as permissions } from '../../features/coastalOperations/permissions'
import SiteHeader from '../../components/layout/SiteHeader'
import SiteFooter from '../../components/layout/SiteFooter'
import AccountAreaNavigation from '../../components/account/AccountAreaNavigation'
import OperationsSearch from './OperationsSearch'
import { useSearchParams } from 'react-router'
import CoastalOperationsPage from './CoastalOperationsPage'
import OperationsRecordCard from './OperationsRecordCard'
import OperationsPagination from './OperationsPagination'
import OperationsActivity from './OperationsActivity'

function LogsWorkspace({ user }: { user: AuthUser | null }) {
  const [view, setView] = useSearchParams()
  const grants = new Set(user?.permissions.map((grant) => grant.toLowerCase()))
  const audit = grants.has(permissions.auditRead)
  const assessmentsAllowed = audit && (grants.has(permissions.assessmentRead) || grants.has(permissions.assessmentQueueRead))
  const alertsAllowed = audit && Object.values(permissions).some((grant) => grant.startsWith('operations.alert.') && grants.has(grant))
  const [kind, setKind] = useState<'assessments' | 'alerts'>(assessmentsAllowed ? 'assessments' : 'alerts')
  const [query, setQuery] = useState<RecordQuery>({})
  const [size, setSize] = useState(25)
  const [cursors, setCursors] = useState<Array<string | undefined>>([undefined])
  const [page, setPage] = useState(0)
  const [next, setNext] = useState<string | null>(null)
  const [records, setRecords] = useState<Array<Assessment | CoastalAlert>>([])
  const [expanded, setExpanded] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  const allowed = kind === 'assessments' ? assessmentsAllowed : alertsAllowed
  const cursor = cursors[page]
  useEffect(() => {
    if (!allowed) return
    const controller = new AbortController()
    let current = true
    const read = kind === 'assessments' ? listAssessmentLogRecords : listAlertLogRecords
    void read({ ...query, pageSize: size, cursor }, controller.signal)
      .then((result) => { if (current) { setRecords(result.items); setNext(result.nextCursor); setExpanded(null) } })
      .catch((reason: unknown) => { if (current) setError(reason instanceof Error ? reason.message : 'Logs could not be loaded. Please retry.') })
      .finally(() => { if (current) setLoading(false) })
    return () => { current = false; controller.abort() }
  }, [allowed, kind, query, size, cursor, retry])
  function beginRead() { setLoading(true); setError(null) }
  function resetPage() { beginRead(); setPage(0); setCursors([undefined]); setNext(null); setExpanded(null) }
  function switchKind(value: 'assessments' | 'alerts') { setKind(value); setQuery({}); setRecords([]); resetPage() }
  if (['create', 'edit', 'detail'].includes(view.get('view') || '')) return <CoastalOperationsPage section={view.get('kind') === 'alerts' ? 'alerts' : 'assessments'} origin="logs" />
  return <div className="flex min-h-screen flex-col bg-coast-paper font-sans text-coast-ink">
    <SiteHeader active="operations" />
    <main className="mx-auto grid w-full max-w-[90rem] flex-1 grid-cols-1 content-start gap-6 px-4 py-6 sm:px-8 sm:py-10 lg:grid-cols-[15rem_minmax(0,1fr)] lg:items-start lg:gap-8 lg:px-6 lg:py-0">
      <AccountAreaNavigation active="operations" />
      <div className="min-w-0 lg:py-10">
        {!allowed ? <p role="alert">Your permissions do not allow access to Coastal Operations logs.</p> : <>
          <div aria-label="Log record category" className="sticky top-20 z-20 flex flex-wrap gap-3 border-b border-coast-line bg-coast-paper/95 py-3 backdrop-blur">
            {assessmentsAllowed && <button aria-pressed={kind === 'assessments'} className={`min-h-11 rounded-full px-5 font-bold ${kind === 'assessments' ? 'bg-coast-deep text-white' : 'border border-coast-line'}`} onClick={() => switchKind('assessments')} type="button">Assessment logs</button>}
            {alertsAllowed && <button aria-pressed={kind === 'alerts'} className={`min-h-11 rounded-full px-5 font-bold ${kind === 'alerts' ? 'bg-coast-deep text-white' : 'border border-coast-line'}`} onClick={() => switchKind('alerts')} type="button">Alert logs</button>}
          </div>
          <section aria-label="Coastal Operations logs" className="mt-6 rounded-3xl border border-coast-line bg-coast-pearl p-5 sm:p-8">
            <h1 className="mb-2 font-display text-3xl">Coastal Operations logs</h1>
            <p className="mb-6 text-sm text-coast-muted">Follow retained drafts, published records and inactive records through their recorded activity.</p>
            <OperationsSearch key={kind} kind={kind} canManage loading={loading} onChange={(value) => { setQuery(value); resetPage() }} />
            {error && <p className="mb-4 text-sm text-red-900" role="alert">{error} <button className="underline" onClick={() => { beginRead(); setRetry((value) => value + 1) }} type="button">Retry logs</button></p>}
            {!loading && !error && records.length === 0 && <div className="py-8"><h2 className="font-display text-xl">{kind === 'assessments' ? 'No assessments to show yet' : 'No advisories to show'}</h2><p className="mt-2 text-sm text-coast-muted">{kind === 'assessments' ? 'Saved drafts and submitted reviews will appear here.' : 'Active public updates and your proposed drafts will appear here.'}</p></div>}
            <div aria-label="Log records" aria-busy={loading} className="grid gap-4">
              {records.map((record) => {
                const isAssessment = 'assessmentId' in record && !('alertId' in record)
                const id = isAssessment ? (record as Assessment).assessmentId : (record as CoastalAlert).alertId
                return <OperationsRecordCard key={id} record={record} onOpen={() => setView({ view: 'detail', kind, id })} activity={<>
                  <button aria-expanded={expanded === id} className="mt-3 min-h-11 rounded-full border border-coast-line px-4 text-sm font-bold" onClick={() => setExpanded(expanded === id ? null : id)} type="button">{expanded === id ? 'Hide activity' : 'View activity'}</button>
                  {expanded === id && <OperationsActivity key={id} kind={isAssessment ? 'assessment' : 'alert'} id={id} revision={record.version} />}
                </>} />
              })}
            </div>
            <OperationsPagination kind={kind} size={size} count={records.length} page={page} busy={loading} onSize={(value) => { setSize(value); resetPage() }} previous={page > 0 ? () => { beginRead(); setPage(page - 1) } : undefined} next={next && !error ? () => { beginRead(); setCursors([...cursors.slice(0, page + 1), next]); setPage(page + 1) } : undefined} />
          </section>
        </>}
      </div>
    </main>
    <SiteFooter />
  </div>
}

export default function OperationsLogsPage() {
  const { user } = useAuthSession()
  return <LogsWorkspace key={`${user?.id}:${user?.permissions.slice().sort().join(',')}`} user={user} />
}
