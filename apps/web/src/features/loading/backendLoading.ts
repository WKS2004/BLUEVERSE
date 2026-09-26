type Listener = () => void

export type LoadingScreenContext = {
  mode: 'coastal' | 'authentication'
  title: string
  detail: string
  minimumVisibleMs?: number
}

export const COASTAL_LOADING_CONTEXT: LoadingScreenContext = {
  mode: 'coastal',
  title: 'A moment by the water',
  detail: 'Loading BLUEVERSE',
}

export const authTransitionMinimumVisibleMs = 850

export const AUTH_LOADING_CONTEXTS = {
  sessionRestore: {
    mode: 'authentication',
    title: 'Restoring your account',
    detail: 'Checking your BLUEVERSE session securely.',
  },
  signIn: {
    mode: 'authentication',
    title: 'Signing you in',
    detail: 'Verifying your details and opening your account.',
    minimumVisibleMs: authTransitionMinimumVisibleMs,
  },
  registration: {
    mode: 'authentication',
    title: 'Preparing your account',
    detail: 'Setting up your BLUEVERSE profile securely.',
    minimumVisibleMs: authTransitionMinimumVisibleMs,
  },
  switchAccount: {
    mode: 'authentication',
    title: 'Switching account',
    detail: 'Restoring the selected account on this device.',
    minimumVisibleMs: authTransitionMinimumVisibleMs,
  },
  profile: {
    mode: 'authentication',
    title: 'Saving your profile',
    detail: 'Keeping your account details up to date.',
  },
  sessions: {
    mode: 'authentication',
    title: 'Checking your sessions',
    detail: 'Loading the devices connected to your account.',
  },
  password: {
    mode: 'authentication',
    title: 'Securing your account',
    detail: 'Updating your sign-in credentials.',
    minimumVisibleMs: authTransitionMinimumVisibleMs,
  },
  signOut: {
    mode: 'authentication',
    title: 'Signing you out',
    detail: 'Closing this account’s session on this device.',
    minimumVisibleMs: authTransitionMinimumVisibleMs,
  },
  endSession: {
    mode: 'authentication',
    title: 'Ending the session',
    detail: 'Applying your session security request.',
    minimumVisibleMs: authTransitionMinimumVisibleMs,
  },
  deleteAccount: {
    mode: 'authentication',
    title: 'Removing your account',
    detail: 'Finishing your confirmed account request securely.',
    minimumVisibleMs: authTransitionMinimumVisibleMs,
  },
} satisfies Record<string, LoadingScreenContext>

const authenticationContextPriority = new Map<LoadingScreenContext, number>([
  [AUTH_LOADING_CONTEXTS.sessionRestore, 0],
  [AUTH_LOADING_CONTEXTS.profile, 1],
  [AUTH_LOADING_CONTEXTS.sessions, 1],
  [AUTH_LOADING_CONTEXTS.switchAccount, 2],
  [AUTH_LOADING_CONTEXTS.signIn, 3],
  [AUTH_LOADING_CONTEXTS.registration, 3],
  [AUTH_LOADING_CONTEXTS.password, 3],
  [AUTH_LOADING_CONTEXTS.signOut, 3],
  [AUTH_LOADING_CONTEXTS.endSession, 3],
  [AUTH_LOADING_CONTEXTS.deleteAccount, 3],
])

export type AuthLoadingContextKey = keyof typeof AUTH_LOADING_CONTEXTS

const pendingAuthTransitionStorageKey = 'blueverse.pending-auth-transition'
const pendingAuthTransitionLifetimeMs = 2 * 60 * 1000

export function preserveAuthLoadingContextForReload(contextKey: AuthLoadingContextKey) {
  try {
    window.sessionStorage.setItem(
      pendingAuthTransitionStorageKey,
      `${contextKey}|${Date.now()}`,
    )
  } catch {
    // The transition still works in memory if browser storage is unavailable.
  }
}

export function consumeAuthLoadingContextAfterReload(): LoadingScreenContext | null {
  try {
    const pending = window.sessionStorage.getItem(pendingAuthTransitionStorageKey)
    window.sessionStorage.removeItem(pendingAuthTransitionStorageKey)
    if (!pending) return null

    const separator = pending.lastIndexOf('|')
    if (separator < 0) return null
    const contextKey = pending.slice(0, separator)
    const savedAt = Number(pending.slice(separator + 1))
    const age = Date.now() - savedAt
    if (!Number.isFinite(savedAt) || age < 0 || age > pendingAuthTransitionLifetimeMs) return null
    if (!Object.prototype.hasOwnProperty.call(AUTH_LOADING_CONTEXTS, contextKey)) return null

    return AUTH_LOADING_CONTEXTS[contextKey as AuthLoadingContextKey]
  } catch {
    return null
  }
}

const listeners = new Set<Listener>()
const settleDelayMs = 160
export const slowLoadingThresholdMs = 2000

const activeRequests = new Map<number, LoadingScreenContext>()
let nextRequestId = 0
let isVisible = false
let currentContext = COASTAL_LOADING_CONTEXT
let minimumVisibleUntil = 0
let settleTimer: ReturnType<typeof setTimeout> | null = null

function emit() {
  for (const listener of listeners) listener()
}

function setSnapshot(visible: boolean, context: LoadingScreenContext) {
  if (isVisible === visible && currentContext === context) return
  isVisible = visible
  currentContext = context
  emit()
}

function latestContext() {
  const contexts = Array.from(activeRequests.values())
  let selectedAuthContext: LoadingScreenContext | undefined
  let selectedPriority = -1

  for (const context of contexts) {
    if (context.mode !== 'authentication') continue

    const priority = authenticationContextPriority.get(context) ?? 1
    if (priority >= selectedPriority) {
      selectedAuthContext = context
      selectedPriority = priority
    }
  }

  const selectedContext = selectedAuthContext ?? contexts.at(-1) ?? COASTAL_LOADING_CONTEXT
  if (isVisible && currentContext.mode === 'authentication') {
    const displayedPriority = authenticationContextPriority.get(currentContext) ?? 1
    const activePriority = selectedContext.mode === 'authentication'
      ? authenticationContextPriority.get(selectedContext) ?? 1
      : -1
    if (displayedPriority > activePriority) return currentContext
  }

  return selectedContext
}

export function beginLoadingScreen(context: LoadingScreenContext = COASTAL_LOADING_CONTEXT) {
  const requestId = ++nextRequestId
  activeRequests.set(requestId, context)
  const startedAt = performance.now()
  if (context.minimumVisibleMs) {
    minimumVisibleUntil = Math.max(minimumVisibleUntil, startedAt + context.minimumVisibleMs)
  }

  if (settleTimer) {
    clearTimeout(settleTimer)
    settleTimer = null
  }

  setSnapshot(true, latestContext())

  let finished = false
  return () => {
    if (finished) return
    finished = true
    activeRequests.delete(requestId)

    if (activeRequests.size > 0) {
      setSnapshot(true, latestContext())
      return
    }

    if (isVisible && !settleTimer) {
      const minimumRemainingMs = Math.max(0, minimumVisibleUntil - performance.now())
      settleTimer = setTimeout(() => {
        settleTimer = null
        if (activeRequests.size !== 0) return
        minimumVisibleUntil = 0
        setSnapshot(false, COASTAL_LOADING_CONTEXT)
      }, Math.max(settleDelayMs, minimumRemainingMs))
    }
  }
}

export function subscribeToLoadingScreen(listener: Listener) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function getLoadingScreenSnapshot() {
  return isVisible
}

export function getLoadingScreenContextSnapshot() {
  return currentContext
}

export function shouldWashLoadingScreenAway(durationMs: number) {
  return Number.isFinite(durationMs) && durationMs >= slowLoadingThresholdMs
}

export function withLoadingScreen<T>(
  operation: () => Promise<T>,
  context: LoadingScreenContext = COASTAL_LOADING_CONTEXT,
): Promise<T> {
  const finish = beginLoadingScreen(context)
  return Promise.resolve().then(operation).finally(finish)
}
