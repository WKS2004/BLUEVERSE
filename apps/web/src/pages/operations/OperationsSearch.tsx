import { useCallback, useEffect, useId, useMemo, useRef, useState } from 'react'
import type { RecordQuery } from '../../features/coastalOperations/operationsApi'
import OperationsIcon from './OperationsIcon'

type Filters = { tab: string; search: string; targetType: string; recordId: string; targetId: string; advanced: boolean; severity: string; visibility: string }
const defaults: Filters = { tab: 'All', search: '', targetType: '', recordId: '', targetId: '', advanced: false, severity: '', visibility: '' }
const uuid = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/
function queryFor(filters: Filters, kind: 'assessments' | 'alerts'): RecordQuery {
  return {
    ...(filters.search.trim() && { search: filters.search.trim() }),
    ...(filters.targetType && { targetType: filters.targetType }),
    ...(filters.advanced && filters.recordId.trim() && { recordId: filters.recordId.trim() }),
    ...(filters.advanced && filters.targetId.trim() && { targetId: filters.targetId.trim() }),
    ...(kind === 'assessments' ? {
      ...(filters.tab === 'Drafts' && { workflowStatus: 'DRAFT', onlyMine: true }),
      ...(filters.tab === 'Published' && { publishedOnly: true }),
      ...(filters.tab === 'History' && { includeCancelled: true }),
    } : {
      ...(filters.tab === 'Drafts' && { lifecycle: 'PROPOSED' }),
      ...(filters.tab === 'Active' && { lifecycle: 'ACTIVE' }),
      ...(filters.tab === 'History' && { history: true }),
      ...(filters.severity && { severity: filters.severity }),
      ...(filters.visibility && { visibility: filters.visibility }),
    }),
  }
}

export default function OperationsSearch({ kind, canManage, onChange, loading = false, active = true }: { kind: 'assessments' | 'alerts'; canManage: boolean; onChange: (query: RecordQuery) => void; loading?: boolean; active?: boolean }) {
  const [filters, setFilters] = useState<Filters>(defaults)
  const [open, setOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const panelId = useId()
  const callback = useRef(onChange)
  const sent = useRef('{}')
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null)
  useEffect(() => { callback.current = onChange }, [onChange])
  const apply = useCallback((next: Filters) => {
    if (timer.current) clearTimeout(timer.current)
    if (next.advanced && [next.recordId, next.targetId].some((id) => id.trim() && !uuid.test(id.trim()))) {
      setError('Enter a valid record ID in Advanced search.'); return
    }
    setError(null)
    const query = queryFor(next, kind)
    const key = JSON.stringify(query)
    if (sent.current === key) return
    sent.current = key
    callback.current(query)
  }, [kind])
  const key = useMemo(() => JSON.stringify(queryFor(filters, kind)), [filters, kind])
  useEffect(() => {
    if (active && key !== sent.current) timer.current = setTimeout(() => apply(filters), 500)
    return () => { if (timer.current) clearTimeout(timer.current) }
  }, [key, filters, apply, active])
  function update(patch: Partial<Filters>, immediate = false) {
    const next = { ...filters, ...patch }; setFilters(next)
    if (immediate) apply(next)
  }
  const tabs = kind === 'assessments' ? ['All', 'Drafts', 'Published', ...(canManage ? ['History'] : [])] : ['All', ...(canManage ? ['Drafts'] : []), 'Active', ...(canManage ? ['History'] : [])]
  const input = 'mt-2 min-h-11 w-full rounded-xl border border-coast-line bg-white px-3 text-sm focus-visible:outline-2 focus-visible:outline-coast-blue'
  const icon = 'inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-full hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue'
  const activeFilters = [filters.targetType, filters.severity, filters.visibility, filters.advanced && (filters.recordId || filters.targetId)].filter(Boolean).length
  if (!active) return null
  return <section aria-label={`Search ${kind}`} className="mb-6">
    <h2 className="font-display text-2xl">Find your {kind === 'assessments' ? 'coastal reviews' : 'coastal updates'}</h2>
    <div aria-label={`${kind} lifecycle`} className="mt-3 flex flex-wrap gap-2">{tabs.map((value) => <button aria-pressed={filters.tab === value} className={`min-h-11 rounded-full px-4 text-sm font-bold ${filters.tab === value ? 'bg-coast-deep text-white' : 'border border-coast-line hover:bg-coast-sage'}`} key={value} onClick={() => update({ tab: value }, true)} type="button">{value}</button>)}</div>
    <form className="mt-4" noValidate onSubmit={(event) => { event.preventDefault(); apply(filters) }}>
      <div className="flex min-w-0 items-center gap-0.5 rounded-2xl border border-coast-line bg-white p-1.5 focus-within:border-coast-blue">
        <label className="sr-only" htmlFor={`${panelId}-search`}>Search records</label>
        <input autoComplete="off" className="min-h-11 min-w-0 flex-1 rounded-xl bg-transparent px-2 text-sm outline-none sm:px-3" id={`${panelId}-search`} maxLength={160} onChange={(event) => update({ search: event.target.value })} placeholder={kind === 'assessments' ? 'Search the Assessments' : 'Search the Alerts'} type="search" value={filters.search} />
        <button aria-controls={panelId} aria-expanded={open} aria-label={activeFilters ? `Filters (${activeFilters} active)` : 'Filters'} className={`${icon} ${open || activeFilters ? 'bg-coast-sage' : ''}`} onClick={() => setOpen(!open)} title="Filters" type="button"><OperationsIcon name="filters" /></button>
        <button aria-label="Reset filters" className={icon} onClick={() => { setFilters(defaults); setOpen(false); setError(null); apply(defaults) }} title="Reset filters" type="button"><OperationsIcon name="reset" /></button>
        <button aria-label="Search" className={`${icon} bg-coast-deep text-white hover:bg-coast-blue`} title="Search now" type="submit"><OperationsIcon name={loading ? 'loading' : 'search'} spinning={loading} /></button>
      </div>
      <span aria-live="polite" className="sr-only" role="status">{loading ? 'Searching records' : ''}</span>
      <div className="mt-3 rounded-2xl border border-coast-line bg-coast-paper p-4" hidden={!open} id={panelId}>
        <div className="grid gap-4 sm:grid-cols-2">
          <label className="text-sm font-bold">Coastal record type<select className={input} onChange={(event) => update({ targetType: event.target.value }, true)} value={filters.targetType}><option value="">All record types</option>{['DESTINATION', 'ACTIVITY', 'OFFERING', 'SESSION'].map((value) => <option key={value}>{value}</option>)}</select></label>
          {kind === 'alerts' && <><label className="text-sm font-bold">Severity<select className={input} onChange={(event) => update({ severity: event.target.value }, true)} value={filters.severity}><option value="">All severities</option>{['LOW', 'MODERATE', 'HIGH', 'CRITICAL'].map((value) => <option key={value}>{value}</option>)}</select></label>{canManage && <label className="text-sm font-bold">Audience<select className={input} onChange={(event) => update({ visibility: event.target.value }, true)} value={filters.visibility}><option value="">All audiences</option><option value="PUBLIC">Coastal visitors</option><option value="OPERATIONS">Operations team</option></select></label>}</>}
          <details className="sm:col-span-2" open={filters.advanced} onToggle={(event) => { if (event.currentTarget.open !== filters.advanced) update({ advanced: event.currentTarget.open }, true) }}><summary className="cursor-pointer text-sm font-bold">Advanced search</summary><div className="mt-3 grid gap-3 sm:grid-cols-2"><label className="text-sm font-bold">Assessment or alert ID<input className={input} disabled={!filters.advanced} maxLength={36} onChange={(event) => update({ recordId: event.target.value })} value={filters.recordId} /></label><label className="text-sm font-bold">Coastal record ID<input className={input} disabled={!filters.advanced} maxLength={36} onChange={(event) => update({ targetId: event.target.value })} value={filters.targetId} /></label><p className="text-xs text-coast-muted sm:col-span-2">IDs appear beside titles and creation dates in the search results.</p></div></details>
        </div>
      </div>
      {error && <p className="mt-2 text-sm text-red-900" role="alert">{error}</p>}
    </form>
  </section>
}
