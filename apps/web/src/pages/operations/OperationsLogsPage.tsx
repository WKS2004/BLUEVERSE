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
import logsHero from '../../assets/coastal/operations-logs-hero.webp'

function LogsWorkspace({ user }: { user: AuthUser | null }) {
  const [view, setView] = useSearchParams()
  const grants = new Set(user?.permissions.map((grant) => grant.toLowerCase()))
  const audit = grants.has(permissions.auditRead)
  const assessmentsAllowed = audit && (grants.has(permissions.assessmentRead) || grants.has(permissions.assessmentQueueRead))
  const alertsAllowed = audit && Object.values(permissions).some((grant) => grant.startsWith('operations.alert.') && grants.has(grant))
  const requestedKind = view.get('kind')
  const kind = requestedKind === 'alerts' && alertsAllowed || !assessmentsAllowed ? 'alerts' : 'assessments'
  const [categoryDirection, setCategoryDirection] = useState<'forward' | 'back' | null>(null)
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
    if (requestedKind !== kind) setView({ kind }, { replace: true })
  }, [requestedKind, setView, kind])
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
  function switchKind(value: 'assessments' | 'alerts') {
    if (value === kind) return
    setCategoryDirection(value === 'alerts' ? 'forward' : 'back')
    setView({ kind: value })
    setQuery({})
    setRecords([])
    resetPage()
  }
  if (['create', 'edit', 'detail'].includes(view.get('view') || '')) return <CoastalOperationsPage section={view.get('kind') === 'alerts' ? 'alerts' : 'assessments'} origin="logs" />
  return <div className="flex min-h-screen flex-col bg-coast-paper font-sans text-coast-ink">
    <SiteHeader active="operations" />
    <main className="mx-auto grid w-full max-w-[90rem] flex-1 grid-cols-1 content-start gap-y-5 px-0 pt-0 pb-6 lg:grid-cols-[15rem_minmax(0,1fr)] lg:items-start lg:gap-x-8 lg:gap-y-0 lg:px-6 lg:pb-0">
      {allowed && <div aria-label="Log record category" className="sticky top-[76px] z-20 flex flex-wrap gap-3 border-b border-coast-line bg-coast-paper px-4 py-3 backdrop-blur sm:px-8 lg:col-start-2 lg:row-start-1 lg:px-0">
            {assessmentsAllowed && <button aria-pressed={kind === 'assessments'} className={`min-h-11 rounded-full px-5 font-bold ${kind === 'assessments' ? 'bg-coast-deep text-white' : 'border border-coast-line'}`} onClick={() => switchKind('assessments')} type="button">Assessment logs</button>}
            {alertsAllowed && <button aria-pressed={kind === 'alerts'} className={`min-h-11 rounded-full px-5 font-bold ${kind === 'alerts' ? 'bg-coast-deep text-white' : 'border border-coast-line'}`} onClick={() => switchKind('alerts')} type="button">Alert logs</button>}
          </div>}
      <AccountAreaNavigation active="operations" className={`order-2 px-4 sm:px-8 lg:order-none lg:col-start-1 lg:row-start-1 ${allowed ? 'lg:row-span-2' : 'lg:row-span-1'}`} />
      <div className={`order-3 min-w-0 px-4 sm:px-8 lg:order-none lg:px-0 lg:col-start-2 ${allowed ? 'lg:row-start-2' : 'lg:row-start-1'}`}>
        {!allowed ? <p role="alert">Your permissions do not allow access to Coastal Operations logs.</p> : <>
          <section aria-labelledby="operations-logs-title" className="relative isolate mb-5 mt-6 overflow-hidden rounded-[2rem] bg-coast-deep text-white shadow-sm">
            <img alt="Coastal stewards comparing shoreline maps and field notes at a table" className="absolute inset-0 -z-20 h-full w-full object-cover opacity-60" src={logsHero} />
            <div aria-hidden="true" className="absolute inset-0 -z-10 bg-gradient-to-r from-coast-deep via-coast-deep/90 to-coast-deep/35" />
            <div className="max-w-3xl px-5 py-8 sm:px-8 sm:py-10 lg:px-10 lg:py-12">
              <p className="text-[11px] font-extrabold tracking-[0.18em] text-coast-glass">COASTAL OPERATIONS · ACTIVITY</p>
              <h1 className="mt-3 font-display text-4xl leading-tight tracking-[-0.05em] sm:text-5xl" id="operations-logs-title">Every coastal decision, accounted for.</h1>
              <p className="mt-4 max-w-2xl text-sm leading-6 text-white/85 sm:text-base sm:leading-7">Follow the history of assessment drafts, published reviews and advisories—who changed them and when.</p>
            </div>
          </section>
          <section key={kind} aria-label="Coastal Operations logs" data-coastal-direction={categoryDirection} className="coastal-category-panel rounded-3xl border border-coast-line bg-coast-pearl p-5 sm:p-8">
            <OperationsSearch key={kind} kind={kind} canManage loading={loading} onChange={(value) => { setQuery(value); resetPage() }} />
            {error && <p className="mb-4 text-sm text-red-900" role="alert">{error} <button className="underline" onClick={() => { beginRead(); setRetry((value) => value + 1) }} type="button">Retry logs</button></p>}
            {!loading && !error && records.length === 0 && <div className="py-8"><h2 className="font-display text-xl">{kind === 'assessments' ? 'No assessments to show yet' : 'No advisories to show'}</h2><p className="mt-2 text-sm text-coast-muted">{kind === 'assessments' ? 'Saved drafts and submitted reviews will appear here.' : 'Active public updates and your proposed drafts will appear here.'}</p></div>}
            <div aria-label="Log records" aria-busy={loading} className="grid gap-4">
              {records.map((record) => {
                const isAssessment = 'assessmentId' in record && !('alertId' in record)
                const id = isAssessment ? (record as Assessment).assessmentId : (record as CoastalAlert).alertId
                return <OperationsRecordCard key={id} record={record} onOpen={() => setView({ view: 'detail', kind, id })} activity={<>
                  <button aria-expanded={expanded === id} className="mt-3 min-h-11 rounded-full border border-coast-line px-4 text-sm font-bold" onClick={() => setExpanded(expanded === id ? null : id)} type="button">{expanded === id ? 'Hide activity' : 'View activity'}</button>
                  {expanded === id && <OperationsActivity key={id} kind={isAssessment ? 'assessment' : 'alert'} id={id} revision={record.version} showReference />}
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
