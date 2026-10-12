import { NavLink } from 'react-router'
import { marineWorkflowLabels } from '../../features/marine/marineWorkflowLabels'

const linkClass = ({ isActive }: { isActive: boolean }) =>
  `inline-flex min-h-11 items-center justify-center rounded-full px-5 text-sm font-bold transition duration-200 motion-reduce:transition-none focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue ${isActive ? 'bg-coast-deep text-white' : 'text-coast-deep hover:bg-coast-sage'}`

export default function MarineWorkflowNavigation() {
  return <div className="sticky top-[76px] z-40 mb-6 bg-coast-paper/95 py-3 backdrop-blur-sm" id="marine-workflow-navigation">
    <nav aria-label="Marine workflows" className="flex flex-wrap gap-2 rounded-3xl border border-coast-line bg-white p-2">
    <NavLink className={linkClass} to="/marine/conditions">{marineWorkflowLabels.conditions}</NavLink>
    <NavLink className={linkClass} to="/marine/history">{marineWorkflowLabels.history}</NavLink>
    <NavLink className={linkClass} to="/marine/safety-profiles">{marineWorkflowLabels.profiles}</NavLink>
    </nav>
  </div>
}
