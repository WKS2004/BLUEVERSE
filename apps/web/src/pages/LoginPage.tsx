import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router'
import { AuthApiError, login } from '../features/auth/auth'
import { safePostAuthDestination } from '../features/auth/authNavigation'
import { useAuthSession } from '../features/auth/authSession'
import { preserveAuthLoadingContextForReload } from '../features/loading/backendLoading'
import mangroveLagoon from '../assets/coastal/mangrove-lagoon.jpg'
import signInCove from '../assets/coastal/sign-in-cove.jpg'
import AuthPageLayout from '../components/layout/AuthPageLayout'

const inputClassName = 'min-h-12 w-full rounded-2xl border border-coast-line bg-white px-4 py-3 text-base font-normal text-coast-ink outline-none transition placeholder:text-coast-muted/70 focus:border-coast-blue focus:ring-2 focus:ring-coast-glass'
const submitClassName = 'inline-flex min-h-13 w-full items-center justify-center gap-2 rounded-full bg-coast-deep px-6 text-base font-extrabold text-white shadow-sm transition duration-200 hover:-translate-y-0.5 hover:bg-coast-blue hover:shadow-lg focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-blue active:scale-[0.99] disabled:cursor-wait disabled:opacity-65 motion-reduce:transition-none'

function LoginPage() {
  const { status, acceptAuthenticatedUser } = useAuthSession()
  const location = useLocation()
  const navigate = useNavigate()
  const destination = safePostAuthDestination(location.search)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [rememberMe, setRememberMe] = useState(false)
  const [showPassword, setShowPassword] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (status === 'checking') return
    setError(null)
    setIsSubmitting(true)
    try {
      const auth = await login(email.trim(), password, rememberMe)
      acceptAuthenticatedUser(auth.user)
      navigate(destination, { replace: true })
      preserveAuthLoadingContextForReload('signIn')
      window.location.reload()
    } catch (requestError) {
      setError(requestError instanceof AuthApiError ? requestError.message : 'We couldn’t reach the sign-in service just now. Please try again.')
      setIsSubmitting(false)
    }
  }

  return (
    <AuthPageLayout
      active="login"
      description="Sign in to continue exploring the people, places and marine life connected to the coast."
      eyebrow="WELCOME BACK TO BLUEVERSE"
      photoAlt="A quiet, blue-water cove on Sri Lanka’s southern coastline in soft morning light"
      photoSrc={signInCove}
      mobilePhotoAlt="Mangrove roots meeting a calm lagoon on Sri Lanka’s coast"
      mobilePhotoSrc={mangroveLagoon}
      story="BLUEVERSE brings coastal discovery, marine awareness and local context into one shared experience."
      storyTitle="There is always more to the shore."
      title="Welcome back."
    >
      <form aria-busy={isSubmitting || status === 'checking'} className="mt-8 grid gap-5 sm:mt-9 sm:gap-6" onSubmit={submit}>
        <label className="grid gap-2 text-sm font-bold text-coast-ink sm:gap-2.5 sm:text-base" htmlFor="login-email">
          Email address
          <input autoComplete="username" className={inputClassName} id="login-email" inputMode="email" onChange={(event) => setEmail(event.target.value)} placeholder="you@example.com" required type="email" value={email} />
        </label>

        <label className="grid gap-2 text-sm font-bold text-coast-ink sm:gap-2.5 sm:text-base" htmlFor="login-password">
          Password
          <span className="relative block">
            <input autoComplete="current-password" className={`${inputClassName} pr-16`} id="login-password" onChange={(event) => setPassword(event.target.value)} required type={showPassword ? 'text' : 'password'} value={password} />
            <button aria-label={showPassword ? 'Hide password' : 'Show password'} aria-pressed={showPassword} className="absolute inset-y-0 right-0 rounded px-3 text-xs font-extrabold text-coast-blue underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-[-4px] focus-visible:outline-coast-blue" onClick={() => setShowPassword((shown) => !shown)} type="button">{showPassword ? 'Hide' : 'Show'}</button>
          </span>
        </label>

        <label className="flex min-h-11 items-center gap-3 text-sm text-coast-muted">
          <input checked={rememberMe} className="h-4 w-4 accent-coast-blue" onChange={(event) => setRememberMe(event.target.checked)} type="checkbox" />
          Keep me signed in for 30 days
        </label>

        {error && <p className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm leading-6 text-red-900" role="alert">{error}</p>}

        <button className={submitClassName} disabled={isSubmitting || status === 'checking'} type="submit">
          Sign in
        </button>
      </form>

      <p className="mt-6 text-sm text-coast-muted sm:text-base">New to BLUEVERSE? <Link className="font-extrabold text-coast-deep underline decoration-coast-glass underline-offset-4 transition-colors hover:text-coast-blue" to="/signup">Create an account</Link></p>
    </AuthPageLayout>
  )
}

export default LoginPage
