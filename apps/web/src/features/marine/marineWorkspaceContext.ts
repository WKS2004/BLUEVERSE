import { createContext, useContext } from 'react'

// Read this during the initial-data effect, after the workspace layout effect.
// Manual refreshes and mutations retain their usual loading presentation.
export const MarineInitialLoadingContext = createContext<() => boolean>(() => true)

export const useMarineInitialLoadingScreen = () => useContext(MarineInitialLoadingContext)

export function isMarineWorkflowRoute(pathname: string) {
  return pathname === '/marine/conditions' || pathname === '/marine/history' || pathname === '/marine/safety-profiles'
}
