export default function OperationsIcon({ name, spinning = false }: { name: 'search' | 'filters' | 'reset' | 'back' | 'loading'; spinning?: boolean }) {
  const paths = {
    search: 'm21 21-5-5M18 10.5a7.5 7.5 0 1 1-15 0 7.5 7.5 0 0 1 15 0',
    filters: 'M4 7h16M4 17h16M8 4v6M16 14v6',
    reset: 'M3 10a9 9 0 1 1 2 8M3 4v6h6',
    back: 'm11 5-7 7 7 7M4 12h16',
    loading: 'M21 12a9 9 0 1 1-9-9',
  }
  return <svg aria-hidden="true" className={`h-5 w-5 shrink-0 ${spinning ? 'motion-safe:animate-spin' : ''}`} fill="none" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="1.8" viewBox="0 0 24 24"><path d={paths[name]} /></svg>
}
