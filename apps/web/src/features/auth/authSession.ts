import { createContext, useContext } from 'react'
import type { AuthUser } from './auth'
import type { SignedInAccount } from './accountStore'

export type AuthStatus = 'checking' | 'signed-out' | 'signed-in' | 'unavailable'
export type AuthSessionsStatus = 'checking' | 'loaded' | 'unavailable' | 'none'
export const MAX_DEVICE_ACCOUNTS = 5
export const MAX_ACTIVE_SESSIONS = 5

export type AuthSessionContextValue = {
  user: AuthUser | null
  accounts: SignedInAccount[]
  status: AuthStatus
  error: string | null
  switchingAccountId: string | null
  acceptAuthenticatedUser: (user: AuthUser) => void
  switchAccount: (accountId: string) => Promise<void>
  removeAccount: (accountId: string) => Promise<void>
  forgetAccount: (accountId: string) => void
  signOut: () => Promise<void>
  signOutEverywhere: (currentPassword: string) => Promise<void>
  updateUser: (fullName: string) => Promise<AuthUser>
}

export const AuthSessionContext = createContext<AuthSessionContextValue | null>(null)

export function useAuthSession() {
  const context = useContext(AuthSessionContext)
  if (!context) throw new Error('useAuthSession must be used inside AuthSessionProvider.')
  return context
}
