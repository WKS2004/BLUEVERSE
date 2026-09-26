import { Component, useEffect, useLayoutEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { BrowserRouter } from 'react-router'
import { AuthSessionProvider } from '../components/providers/AuthSessionProvider'
import BackendLoadingScreen from '../components/feedback/BackendLoadingScreen'
import { ServerErrorPage } from '../pages/GlobalErrorPage'
import { useAuthSession } from '../features/auth/authSession'
import {
  AUTH_LOADING_CONTEXTS,
  beginLoadingScreen,
  consumeAuthLoadingContextAfterReload,
} from '../features/loading/backendLoading'
import RouteScrollManager from './RouteScrollManager'
import AppRoutes from './routes'

const accountTransitionNoticeKey = 'blueverse.auth-transition-notice'

class GlobalAppErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false }

  static getDerivedStateFromError() {
    return { failed: true }
  }

  render() {
    if (this.state.failed) {
      return <ServerErrorPage onRetry={() => this.setState({ failed: false })} />
    }
    return this.props.children
  }
}

function AccountTransitionNotice() {
  const [notice, setNotice] = useState<string | null>(() => {
    try {
      return window.sessionStorage.getItem(accountTransitionNoticeKey)
    } catch {
      // Session storage is optional; account transitions still complete safely.
      return null
    }
  })

  useEffect(() => {
    if (!notice) return
    try {
      window.sessionStorage.removeItem(accountTransitionNoticeKey)
    } catch {
      // The notice can still be dismissed in memory.
    }
  }, [notice])

  if (!notice) return null

  return (
    <div className="fixed inset-x-4 top-20 z-[80] mx-auto max-w-2xl" role="status">
      <div className="flex items-start justify-between gap-4 rounded-2xl border border-coast-line bg-coast-pearl px-5 py-4 text-sm leading-6 text-coast-deep shadow-lg">
        <p>{notice}</p>
        <button aria-label="Dismiss account update" className="shrink-0 rounded-full px-2 font-bold text-coast-muted hover:bg-coast-sage hover:text-coast-deep" onClick={() => setNotice(null)} type="button">Dismiss</button>
      </div>
    </div>
  )
}

function AccountScopedRoutes() {
  const { user } = useAuthSession()

  return (
    <div className="contents" key={user?.id ?? 'signed-out'}>
      <RouteScrollManager />
      <AppRoutes />
    </div>
  )
}

function AuthSessionLoadingState() {
  const { status } = useAuthSession()
  const [transitionContext] = useState(consumeAuthLoadingContextAfterReload)

  useLayoutEffect(() => {
    if (status !== 'checking') return
    return beginLoadingScreen(transitionContext ?? AUTH_LOADING_CONTEXTS.sessionRestore)
  }, [status, transitionContext])

  return null
}

export default function App() {
  return (
    <AuthSessionProvider>
      <BrowserRouter>
        <GlobalAppErrorBoundary>
          <AuthSessionLoadingState />
          <BackendLoadingScreen />
          <AccountTransitionNotice />
          <AccountScopedRoutes />
        </GlobalAppErrorBoundary>
      </BrowserRouter>
    </AuthSessionProvider>
  )
}
