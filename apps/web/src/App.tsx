import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import './App.css'
import {
  AuthApiError,
  getCurrentUser,
  getSessions,
  login,
  logoutAllDevices,
  logoutCurrentDevice,
  refresh,
} from './auth'
import type { AuthSession, AuthUser } from './auth'

function App() {
  const [isLoginRoute, setIsLoginRoute] = useState(window.location.pathname === '/login')

  useEffect(() => {
    const handleNavigation = () => setIsLoginRoute(window.location.pathname === '/login')
    window.addEventListener('popstate', handleNavigation)
    return () => window.removeEventListener('popstate', handleNavigation)
  }, [])

  return isLoginRoute ? <LoginPage /> : <HomePage />
}

function LoginPage() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [rememberMe, setRememberMe] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      await login(email, password, rememberMe)
      window.location.assign('/')
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Sign-in failed.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="auth-shell">
      <section className="auth-card" aria-labelledby="login-heading">
        <p className="eyebrow">BLUEVERSE / AUTH</p>
        <h1 id="login-heading">Welcome back.</h1>
        <p className="muted">Sign in to manage your account sessions securely.</p>
        <form onSubmit={submit} className="auth-form">
          <label>
            Email
            <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} required autoComplete="email" />
          </label>
          <label>
            Password
            <input type="password" value={password} onChange={(event) => setPassword(event.target.value)} required autoComplete="current-password" />
          </label>
          <label className="checkbox-row">
            <input type="checkbox" checked={rememberMe} onChange={(event) => setRememberMe(event.target.checked)} />
            Keep this session for 30 days
          </label>
          {error && <p className="error" role="alert">{error}</p>}
          <button type="submit" disabled={isSubmitting}>{isSubmitting ? 'Signing in…' : 'Sign in'}</button>
        </form>
        <a className="text-link" href="/">Back to home</a>
      </section>
    </main>
  )
}

function HomePage() {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [sessions, setSessions] = useState<AuthSession[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  async function loadAccount() {
    setIsLoading(true)
    setError(null)
    try {
      let currentUser: AuthUser
      try {
        currentUser = await getCurrentUser()
      } catch (requestError) {
        if (!(requestError instanceof AuthApiError) || requestError.status !== 401) throw requestError
        await refresh()
        currentUser = await getCurrentUser()
      }
      setUser(currentUser)
      setSessions(await getSessions())
    } catch (requestError) {
      setUser(null)
      if (requestError instanceof AuthApiError && requestError.status !== 401) {
        setError(requestError.message)
      }
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    const task = window.setTimeout(() => void loadAccount(), 0)
    return () => window.clearTimeout(task)
  }, [])

  async function signOut(scope: 'current' | 'all') {
    setError(null)
    try {
      if (scope === 'current') await logoutCurrentDevice()
      else await logoutAllDevices()
      setUser(null)
      setSessions([])
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Sign-out failed.')
    }
  }

  if (isLoading) return <main className="page-shell"><p className="muted">Checking your session…</p></main>

  if (!user) {
    return (
      <main className="page-shell">
        <section className="hero-card">
          <p className="eyebrow">BLUEVERSE</p>
          <h1>One secure gateway for every workflow.</h1>
          <p className="muted">Auth sessions expire predictably, refresh securely, and can be managed per device.</p>
          {error && <p className="error" role="alert">{error}</p>}
          <a className="button-link" href="/login">Sign in</a>
        </section>
      </main>
    )
  }

  return (
    <main className="page-shell">
      <section className="hero-card account-header">
        <div>
          <p className="eyebrow">SIGNED IN</p>
          <h1>{user.fullName}</h1>
          <p className="muted">{user.email}</p>
        </div>
        <div className="button-group">
          <button type="button" onClick={() => void signOut('current')}>Log out this device</button>
          <button type="button" className="secondary" onClick={() => void signOut('all')}>Log out everywhere</button>
        </div>
      </section>
      <section className="sessions-card" aria-labelledby="sessions-heading">
        <div className="section-heading">
          <div>
            <p className="eyebrow">SESSION MANAGEMENT</p>
            <h2 id="sessions-heading">Active sessions</h2>
          </div>
          <span className="count-badge">{sessions.length} / 5</span>
        </div>
        {error && <p className="error" role="alert">{error}</p>}
        <ul className="session-list">
          {sessions.map((session) => (
            <li key={session.id}>
              <div>
                <strong>{session.isCurrent ? 'This browser' : 'Signed-in device'}</strong>
                <span>{session.rememberMe ? '30-day session' : '1-day session'} · expires {new Date(session.expiresAt).toLocaleString()}</span>
              </div>
              {session.isCurrent && <span className="current-badge">Current</span>}
            </li>
          ))}
        </ul>
      </section>
    </main>
  )
}

export default App
