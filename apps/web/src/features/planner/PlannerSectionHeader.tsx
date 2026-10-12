import { Link } from 'react-router'
import { useAuthSession } from '../auth/authSession'
import { hasAllPermissions } from '../authorization/permissions'
import preparationImage from '../../assets/planner/trip-preparation.webp'
import memoriesImage from '../../assets/planner/plan-memories.webp'

export default function PlannerSectionHeader({ section }: { section: 'create' | 'history' }) {
  const { user } = useAuthSession()
  const creating = section === 'create'
  const pages = [
    { id: 'create', label: 'Create a plan', href: '/planner/plan', permission: 'planner.recommendations.create' },
    { id: 'history', label: 'Plan history', href: '/planner/saved', permission: 'planner.itineraries.manage' },
  ]
  return <>
    <nav aria-label="Planning pages" className="mb-6 flex flex-wrap gap-3 border-b border-coast-line pb-4">
      {pages.filter(page => hasAllPermissions(user, [page.permission])).map(page => <Link key={page.id} to={page.href} aria-current={section === page.id ? 'page' : undefined} className={`inline-flex min-h-12 items-center rounded-full border px-6 text-sm font-bold transition focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue ${section === page.id ? 'border-coast-blue bg-coast-blue text-white' : 'border-coast-line text-coast-deep hover:bg-coast-sage'}`}>{page.label}</Link>)}
    </nav>
    <header className="mb-6">
      <p className="text-[10px] font-extrabold uppercase tracking-[0.2em] text-coast-blue">Your coastal planner</p>
      <h1 className="mt-3 font-display text-3xl font-bold tracking-tight sm:text-4xl">{creating ? 'Create your coastal plan.' : 'Your plan history.'}</h1>
      <p className="mt-3 text-sm leading-7 text-coast-muted">{creating ? 'Choose your coast, find experiences that fit, and shape a day at your own pace.' : 'Find your saved drafts, revisit confirmed plans, and look back on days by the coast.'}</p>
    </header>
    <section aria-label={creating ? 'Coastal planning introduction' : 'Plan history introduction'} className="mb-8 overflow-hidden rounded-[2rem] border border-coast-line bg-coast-pearl shadow-sm">
      <div className="relative">
        <img src={creating ? preparationImage : memoriesImage} alt={creating ? 'A day bag, water bottle, sun hat and map ready for a coastal day' : 'A travel journal and coastal photographs overlooking the sea'} width={1536} height={1024} fetchPriority="high" className="h-48 w-full object-cover object-center sm:h-60 lg:h-64" />
        <p className="absolute bottom-4 left-5 rounded-full bg-white/95 px-4 py-2 text-[9px] font-extrabold uppercase tracking-[0.14em] text-coast-blue sm:left-8">BLUEVERSE · Coastal planner</p>
      </div>
      <div className="p-6 sm:p-8">
        <p className="inline-flex rounded-full border border-coast-line px-3 py-2 text-[9px] font-extrabold uppercase tracking-[0.16em] text-coast-blue">{creating ? 'A day worth planning' : 'Good days, kept close'}</p>
        <h2 className="mt-4 font-display text-2xl font-bold tracking-tight sm:text-3xl">{creating ? 'Make room for a great coastal day.' : 'Your coast. Your plans. Your memories.'}</h2>
        <p className="mt-3 max-w-3xl text-sm leading-7 text-coast-muted">{creating ? 'Start with a destination and a little time. Explore current suggestions, save a private draft, and arrange the details before you confirm.' : 'Keep the days you’re dreaming of and the details you’ve worked out in one place. Open a plan to make changes or review conditions before you go.'}</p>
        <p className="mt-5 flex items-center gap-2 border-t border-coast-line pt-4 text-xs leading-6 text-coast-blue"><span aria-hidden="true" className="h-2 w-2 shrink-0 rounded-full bg-coast-teal" />{creating ? 'Your interests · Current conditions · A plan at your pace' : 'Private drafts · Confirmed plans · Past coastal days'}</p>
      </div>
    </section>
  </>
}
