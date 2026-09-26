import { useState } from 'react'
import { Link, useLocation } from 'react-router'
import { hasAnyPermission } from '../../features/authorization/permissions'
import { useAuthSession } from '../../features/auth/authSession'

type Area = 'profile' | 'dashboard' | 'admin'
type NavigationGroup = { id: Area; label: string; href: string; children: { label: string; href: string }[] }

function groupsFor(permissions: string[]): NavigationGroup[] {
  const groups: NavigationGroup[] = [
    {
      id: 'profile', label: 'Profile', href: '/profile', children: [
        { label: 'Personal details', href: '/profile#personal-details' },
        { label: 'Change password', href: '/profile#change-password' },
        { label: 'Login sessions', href: '/profile#sessions' },
      ],
    },
    {
      id: 'dashboard', label: 'Dashboard', href: '/dashboard', children: [
        { label: 'Account overview', href: '/dashboard#overview' },
        { label: 'Coastal focus', href: '/dashboard#coastal-focus' },
      ],
    },
  ]
  const links = [
    ...(hasAnyPermission({ permissions }, ['auth.permission.read']) ? [{ label: 'Permissions', href: '/admin/permissions' }] : []),
    ...(hasAnyPermission({ permissions }, ['auth.role.read']) ? [{ label: 'Roles', href: '/admin/roles' }] : []),
    ...(hasAnyPermission({ permissions }, ['auth.user.read']) ? [{ label: 'User accounts', href: '/admin/users' }] : []),
  ]
  if (links.length) groups.push({ id: 'admin', label: 'Administration', href: '/admin', children: links })
  return groups
}

function Subnav({ group, desktop = false }: { group: NavigationGroup; desktop?: boolean }) {
  const menuId = `${group.id}-subnav-${desktop ? 'desktop' : 'mobile'}`
  return <ul className={`${desktop ? 'ml-3 mt-2 border-l border-coast-line pl-3' : 'mt-2 flex flex-wrap gap-1.5 border-t border-coast-line pt-2'}`} id={menuId}>
    {group.children.map((child) => <li key={child.href}><Link className={`inline-flex min-h-9 items-center rounded-full ${desktop ? 'w-full px-3' : 'px-3'} text-xs font-semibold text-coast-muted transition-colors hover:bg-coast-sage hover:text-coast-deep focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue`} to={child.href}>{child.label}</Link></li>)}
  </ul>
}

function Disclosure({ group, expanded, onToggle, desktop = false, showChildren = true }: { group: NavigationGroup; expanded: boolean; onToggle: () => void; desktop?: boolean; showChildren?: boolean }) {
  const location = useLocation()
  const menuId = `${group.id}-subnav-${desktop ? 'desktop' : 'mobile'}`
  return <>
    <div className={`flex min-w-0 items-center ${desktop ? 'rounded-2xl' : 'shrink-0 rounded-full'} ${expanded ? 'bg-coast-sage/80' : ''}`}>
      <Link aria-current={location.pathname === group.href ? 'page' : undefined} className={`flex min-h-10 min-w-0 items-center ${desktop ? 'flex-1 rounded-l-2xl px-4 text-sm' : 'rounded-l-full px-4 text-sm'} font-bold text-coast-deep transition-colors hover:bg-coast-sage focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue`} to={group.href}>{group.label}</Link>
      <button aria-controls={menuId} aria-expanded={expanded} aria-label={`${expanded ? 'Collapse' : 'Expand'} ${group.label} options`} className={`inline-flex h-10 w-10 shrink-0 items-center justify-center ${desktop ? 'rounded-r-2xl' : 'rounded-r-full'} text-coast-muted transition-colors hover:bg-coast-sage hover:text-coast-blue focus-visible:z-10 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue`} onClick={onToggle} type="button">
        <svg aria-hidden="true" className={`h-4 w-4 transition-transform duration-200 motion-reduce:transition-none ${expanded ? 'rotate-180' : ''}`} viewBox="0 0 16 16" fill="none"><path d="m3.5 6 4.5 4 4.5-4" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" /></svg>
      </button>
    </div>
    {expanded && showChildren && <Subnav group={group} desktop={desktop} />}
  </>
}

export default function AccountAreaNavigation({ active }: { active: Area }) {
  const { user } = useAuthSession()
  const groups = groupsFor(user?.permissions ?? [])
  const [expanded, setExpanded] = useState<Area | null>(active)

  function toggle(area: Area) {
    setExpanded((current) => current === area ? null : area)
  }

  return <aside aria-label="Account navigation" className="lg:sticky lg:top-[76px] lg:z-10 lg:h-[calc(100dvh-76px)] lg:max-h-[calc(100dvh-76px)] lg:w-full lg:self-start lg:overflow-y-auto lg:border-r lg:border-coast-line lg:bg-coast-paper/95 lg:px-4 lg:pb-6 lg:pt-6">
    <nav aria-label="Profile, dashboard and administration">
      <div className="rounded-3xl border border-coast-line bg-white/80 p-2 shadow-sm lg:hidden">
        <div className="flex min-w-0 gap-1 overflow-x-auto">
          {groups.map((group) => <div className="shrink-0" key={group.id}><Disclosure group={group} expanded={expanded === group.id} onToggle={() => toggle(group.id)} showChildren={false} /></div>)}
        </div>
        {expanded && groups.some((group) => group.id === expanded) && <div className="px-2 pb-1"><Subnav group={groups.find((group) => group.id === expanded)!} /></div>}
      </div>

      <div className="hidden rounded-3xl border border-coast-line bg-white/80 p-3 shadow-sm lg:block">
        <p className="px-4 pb-2 pt-2 text-[10px] font-extrabold tracking-[0.16em] text-coast-muted">YOUR SPACE</p>
        <div className="grid gap-1">
          {groups.map((group) => <Disclosure desktop group={group} expanded={expanded === group.id} key={group.id} onToggle={() => toggle(group.id)} />)}
        </div>
      </div>
    </nav>
  </aside>
}
