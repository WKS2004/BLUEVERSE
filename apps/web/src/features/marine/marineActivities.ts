import { useEffect, useState } from 'react'
import { getMarineActivities } from './marineApi'
import { useMarineInitialLoadingScreen } from './marineWorkspaceContext'

export type MarineActivityReference = { id: string; name: string; activityType: string }

export function useMarineActivities() {
  const showInitialLoadingScreen = useMarineInitialLoadingScreen()
  const [activities, setActivities] = useState<MarineActivityReference[]>([])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    let active = true
    getMarineActivities(attempt > 0 || showInitialLoadingScreen()).then(rows => {
      if (active) { setActivities(rows); setError(null); setLoading(false) }
    }).catch(reason => {
      if (active) { setActivities([]); setError(reason instanceof Error ? reason.message : 'Activities could not be loaded.'); setLoading(false) }
    })
    return () => { active = false }
  }, [attempt, showInitialLoadingScreen])
  return { activities, error, loading, retry: () => { setLoading(true); setAttempt(value => value + 1) } }
}
