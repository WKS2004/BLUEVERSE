import { useEffect, useRef, useState, useSyncExternalStore } from 'react'
import {
  COASTAL_LOADING_CONTEXT,
  getLoadingScreenContextSnapshot,
  getLoadingScreenSnapshot,
  shouldWashLoadingScreenAway,
  subscribeToLoadingScreen,
} from '../../features/loading/backendLoading'

function BlueverseLoadingMark() {
  return (
    <div aria-hidden="true" className="relative grid h-32 w-32 place-items-center sm:h-36 sm:w-36">
      <span className="blueverse-loader-halo absolute inset-0 rounded-full border border-coast-blue/10" />
      <span className="blueverse-loader-ring absolute inset-2 rounded-full p-[2px]">
        <span className="block h-full w-full rounded-full bg-coast-paper" />
      </span>
      <span className="blueverse-loader-pulse relative grid h-[4.5rem] w-[4.5rem] place-items-center overflow-hidden rounded-full shadow-[0_12px_36px_rgba(32,92,121,0.16)] sm:h-[5rem] sm:w-[5rem]">
        <svg className="h-full w-full" fill="none" viewBox="0 0 44 44">
          <defs>
            <linearGradient id="blueverse-loading-water" x1="0" x2="1" y1="0" y2="1">
              <stop offset="0" stopColor="#347b9b" />
              <stop offset="1" stopColor="#69a9a9" />
            </linearGradient>
            <clipPath id="blueverse-loading-mark-clip">
              <circle cx="22" cy="22" r="20" />
            </clipPath>
          </defs>
          <circle cx="22" cy="22" r="21" fill="#f7f6f0" stroke="#205c79" strokeWidth="1.5" />
          <g clipPath="url(#blueverse-loading-mark-clip)">
            <rect className="blueverse-loader-fill" fill="url(#blueverse-loading-water)" height="22" width="44" x="0" y="22" />
          </g>
          <path d="M7 25c5.2 0 5.2-4.2 10.4-4.2S22.6 25 27.8 25 33 20.8 38 20.8M7 31c5.2 0 5.2-4.2 10.4-4.2S22.6 31 27.8 31 33 26.8 38 26.8" stroke="#205c79" strokeLinecap="round" strokeWidth="1.7" />
          <circle cx="28.5" cy="13" r="3" fill="#43858a" />
        </svg>
      </span>
    </div>
  )
}

export default function BackendLoadingScreen() {
  const isLoading = useSyncExternalStore(
    subscribeToLoadingScreen,
    getLoadingScreenSnapshot,
    () => false,
  )
  const context = useSyncExternalStore(
    subscribeToLoadingScreen,
    getLoadingScreenContextSnapshot,
    () => COASTAL_LOADING_CONTEXT,
  )
  const [isExiting, setIsExiting] = useState(false)
  const startedAt = useRef<number | null>(null)
  const exitTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    if (isLoading) {
      if (exitTimer.current) clearTimeout(exitTimer.current)
      exitTimer.current = null
      startedAt.current ??= performance.now()
      const resetTimer = setTimeout(() => setIsExiting(false), 0)
      return () => clearTimeout(resetTimer)
    }

    if (startedAt.current === null) return
    const duration = performance.now() - startedAt.current
    startedAt.current = null
    if (!shouldWashLoadingScreenAway(duration)) return

    const revealFrame = requestAnimationFrame(() => setIsExiting(true))
    exitTimer.current = setTimeout(() => {
      exitTimer.current = null
      setIsExiting(false)
    }, 900)
    return () => cancelAnimationFrame(revealFrame)
  }, [isLoading])

  useEffect(() => () => {
    if (exitTimer.current) clearTimeout(exitTimer.current)
  }, [])

  if (!isLoading && !isExiting) return null

  return (
    <div
      aria-label={`${context.title}. ${context.detail}`}
      className="blueverse-loader-backdrop fixed inset-0 z-[100] grid place-items-center px-5"
      data-exiting={isExiting || undefined}
      data-loading-mode={context.mode}
      role="status"
      aria-live="polite"
    >
      <div className="blueverse-loader-content flex flex-col items-center text-center" data-exiting={isExiting || undefined}>
        <BlueverseLoadingMark />
        {context.mode === 'authentication' && (
          <div className="mt-5 inline-flex items-center gap-2 rounded-full border border-coast-glass/80 bg-white/75 px-3.5 py-2 text-[10px] font-extrabold tracking-[0.18em] text-coast-deep shadow-sm">
            <svg aria-hidden="true" className="h-4 w-4 text-coast-teal" fill="none" viewBox="0 0 20 20">
              <path d="M10 2.25 16 4.5v4.3c0 4.1-2.35 6.9-6 8.95-3.65-2.05-6-4.85-6-8.95V4.5l6-2.25Z" stroke="currentColor" strokeLinejoin="round" strokeWidth="1.5" />
              <path d="m7.3 9.8 1.7 1.7 3.7-4" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="1.5" />
            </svg>
            SECURE ACCOUNT TRANSITION
          </div>
        )}
        <p className="mt-6 font-display text-lg font-semibold tracking-[-0.02em] text-coast-deep sm:text-xl">{context.title}</p>
        <p className="mt-1.5 max-w-sm text-sm leading-6 text-coast-muted">{context.detail}</p>
      </div>
    </div>
  )
}
