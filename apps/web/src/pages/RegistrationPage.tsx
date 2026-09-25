import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { AuthApiError, register } from '../features/auth/auth'
import { authEntryHrefFor } from '../features/auth/authNavigation'
import { MAX_DEVICE_ACCOUNTS, useAuthSession } from '../features/auth/authSession'
import { preserveAuthLoadingContextForReload } from '../features/loading/backendLoading'
import coastalWalk from '../assets/coastal/coastal-walk.jpg'
import coastalGuide from '../assets/coastal/registration-coastal-guide.jpg'
import AuthPageLayout from '../components/layout/AuthPageLayout'

const inputClassName = 'min-h-12 w-full min-w-0 rounded-2xl border border-coast-line bg-white px-4 py-3 text-base font-normal text-coast-ink outline-none transition placeholder:text-coast-muted/70 focus:border-coast-blue focus:ring-2 focus:ring-coast-glass'
const submitClassName = 'inline-flex min-h-13 w-full items-center justify-center gap-2 rounded-full bg-coast-deep px-6 text-base font-extrabold text-white shadow-sm transition duration-200 hover:-translate-y-0.5 hover:bg-coast-blue hover:shadow-lg focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-coast-blue active:scale-[0.99] disabled:cursor-wait disabled:opacity-65 motion-reduce:transition-none'

function RegistrationPage() {
  const { status, accounts, acceptAuthenticatedUser } = useAuthSession()
  const navigate = useNavigate()
  const destination = '/profile'
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [rememberMe, setRememberMe] = useState(false)
  const [showPassword, setShowPassword] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const passwordsMatch = confirmPassword.length > 0 && password === confirmPassword
  const accountLimitReached = status === 'signed-in' && accounts.length >= MAX_DEVICE_ACCOUNTS

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (status === 'checking') return
    if (accountLimitReached) {
      setError('This device already has five signed-in accounts. Remove one from the account menu before creating another.')
      return
    }
    setError(null)

    if (!fullName.trim()) {
      setError('Enter your name to create an account.')
      return
    }
    if (password !== confirmPassword) {
      setError('Your passwords do not match yet.')
      return
    }

    setIsSubmitting(true)
    try {
      const auth = await register(fullName.trim(), email.trim(), password, rememberMe)
      acceptAuthenticatedUser(auth.user)
      navigate('/profile', { replace: true })
      preserveAuthLoadingContextForReload('registration')
      window.location.reload()
    } catch (requestError) {
      setError(requestError instanceof AuthApiError ? requestError.message : 'We couldn’t reach the sign-up service just now. Please try again.')
      setIsSubmitting(false)
    }
  }

  return (
    <AuthPageLayout
      active="register"
      description="Create your account and explore the coast through its places, marine life and the people who call it home."
      eyebrow="START WITH CURIOSITY"
      photoAlt="A local coastal guide talking with visitors along a quiet Sri Lankan shoreline"
      photoSrc={coastalGuide}
      mobilePhotoAlt="A small group walking together beside the blue Sri Lankan shoreline"
      mobilePhotoSrc={coastalWalk}
      story="A shared view brings visitors, local teams and people caring for the shoreline closer together."
      storyTitle="A shared coast starts with us."
      title="Find your place by the sea."
    >
      <form aria-busy={isSubmitting || status === 'checking'} className="mt-8 grid gap-5 sm:mt-9 sm:gap-6" onSubmit={submit}>
        {accountLimitReached && <p className="rounded-2xl bg-coast-sand px-4 py-3 text-sm leading-6 text-coast-deep" role="status">Five accounts are already saved on this device. Remove one from the account menu before adding another.</p>}
        <label className="grid min-w-0 gap-2 text-sm font-bold text-coast-ink sm:gap-2.5 sm:text-base" htmlFor="register-name">
          Full name
          <input autoComplete="name" className={inputClassName} id="register-name" maxLength={100} onChange={(event) => setFullName(event.target.value)} placeholder="Your name" required value={fullName} />
        </label>

        <label className="grid min-w-0 gap-2 text-sm font-bold text-coast-ink sm:gap-2.5 sm:text-base" htmlFor="register-email">
          Email address
          <input autoComplete="email" className={inputClassName} id="register-email" inputMode="email" onChange={(event) => setEmail(event.target.value)} placeholder="you@example.com" required type="email" value={email} />
        </label>

        <label className="grid min-w-0 gap-2 text-sm font-bold text-coast-ink sm:gap-2.5 sm:text-base" htmlFor="register-password">
          Password
          <span className="relative block">
            <input autoComplete="new-password" className={`${inputClassName} pr-16`} id="register-password" minLength={8} onChange={(event) => setPassword(event.target.value)} required type={showPassword ? 'text' : 'password'} value={password} />
            <button aria-label={showPassword ? 'Hide password' : 'Show password'} aria-pressed={showPassword} className="absolute inset-y-0 right-0 rounded px-3 text-xs font-extrabold text-coast-blue underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-[-4px] focus-visible:outline-coast-blue" onClick={() => setShowPassword((shown) => !shown)} type="button">{showPassword ? 'Hide' : 'Show'}</button>
          </span>
          <span className="text-xs font-normal leading-5 text-coast-muted">Use at least 8 characters.</span>
        </label>

        <label className="grid min-w-0 gap-2 text-sm font-bold text-coast-ink sm:gap-2.5 sm:text-base" htmlFor="register-confirm-password">
          Confirm password
          <input aria-invalid={confirmPassword.length > 0 && !passwordsMatch} autoComplete="new-password" className={inputClassName} id="register-confirm-password" onChange={(event) => setConfirmPassword(event.target.value)} required type={showPassword ? 'text' : 'password'} value={confirmPassword} />
          {confirmPassword.length > 0 && <span aria-live="polite" className={`text-xs font-semibold ${passwordsMatch ? 'text-coast-teal' : 'text-red-800'}`}>{passwordsMatch ? 'Passwords match.' : 'Passwords do not match yet.'}</span>}
        </label>

        <label className="flex min-h-11 items-center gap-3 text-sm text-coast-muted">
          <input checked={rememberMe} className="h-4 w-4 accent-coast-blue" onChange={(event) => setRememberMe(event.target.checked)} type="checkbox" />
          Keep me signed in for 30 days
        </label>

        {error && <p className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm leading-6 text-red-900" role="alert">{error}</p>}

        <button className={submitClassName} disabled={isSubmitting || status === 'checking' || accountLimitReached} type="submit">
          Create account
        </button>
      </form>

      <p className="mt-6 text-sm text-coast-muted sm:text-base">Already part of BLUEVERSE? <Link className="font-extrabold text-coast-deep underline decoration-coast-glass underline-offset-4 transition-colors hover:text-coast-blue" to={authEntryHrefFor('/signin', destination)}>Sign in</Link></p>
    </AuthPageLayout>
  )
}

export default RegistrationPage
