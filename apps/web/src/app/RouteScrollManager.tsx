import { useEffect } from 'react'
import { useLocation } from 'react-router'

export default function RouteScrollManager() {
  const { pathname, hash } = useLocation()

  useEffect(() => {
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
