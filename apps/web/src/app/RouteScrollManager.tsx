import { useEffect, useRef } from 'react'
import { useLocation } from 'react-router'
import { isMarineWorkflowRoute } from '../features/marine/marineWorkspaceContext'

export default function RouteScrollManager() {
  const { pathname, hash } = useLocation()
  const previousPath = useRef<string | null>(null)

  useEffect(() => {
    const previous = previousPath.current
    previousPath.current = pathname
    if (!hash && previous && isMarineWorkflowRoute(previous) && isMarineWorkflowRoute(pathname)) {
      const start = document.getElementById('marine-workflow-start')
      if (start && start.getBoundingClientRect().top < 76) {
        start.scrollIntoView({ block: 'start', behavior: 'instant' })
      }
      return
    }
    if (!hash) {
      window.scrollTo({ top: 0, behavior: 'instant' })
      return
    }

    const frame = window.requestAnimationFrame(() => {
      document.getElementById(hash.slice(1))?.scrollIntoView({
        behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth',
      })
    })

    return () => window.cancelAnimationFrame(frame)
  }, [pathname, hash])

  return null
}
