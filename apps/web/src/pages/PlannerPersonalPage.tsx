import { useLocation } from 'react-router'
import PlannerPlanPage from './PlannerPlanPage'
import SavedTripsPage from './SavedTripsPage'

// Load both tab destinations together so switching never suspends the page.
export default function PlannerPersonalPage() {
  const { pathname } = useLocation()
  return pathname === '/planner/saved' ? <SavedTripsPage /> : <PlannerPlanPage />
}
