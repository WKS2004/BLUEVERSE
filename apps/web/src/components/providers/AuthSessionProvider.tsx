import { useCallback, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import {
  activateAccount,
  AuthApiError,
  getCurrentUser,
  logoutAllDevices,
  refresh,
  removeAccountFromDevice,
  updateCurrentUser,
} from '../../features/auth/auth'
import type { AuthUser } from '../../features/auth/auth'
import {
  forgetSignedInAccount,
  getPreferredAccountId,
  readSignedInAccounts,
  rememberSignedInAccount,
  setPreferredAccountId,
} from '../../features/auth/accountStore'
import type { SignedInAccount } from '../../features/auth/accountStore'
import { AuthSessionContext } from '../../features/auth/authSession'
import type { AuthStatus } from '../../features/auth/authSession'

function isUnauthorized(error: unknown): boolean {
  return error instanceof AuthApiError && error.status === 401
}

async function restoreAccount(userId?: string): Promise<AuthUser> {
  if (userId) await activateAccount(userId)

  try {
    return await getCurrentUser()
  } catch (error) {
    if (!isUnauthorized(error)) throw error
    await refresh()
    return getCurrentUser()
  }
}

export function AuthSessionProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [accounts, setAccounts] = useState<SignedInAccount[]>(() => readSignedInAccounts())
  const [status, setStatus] = useState<AuthStatus>('checking')
  const [error, setError] = useState<string | null>(null)
  const [switchingAccountId, setSwitchingAccountId] = useState<string | null>(null)

  useEffect(() => {
    let active = true

    async function loadSession() {
      const storedAccounts = readSignedInAccounts()
      const preferred = getPreferredAccountId()
      const candidates = [preferred, ...storedAccounts.map((account) => account.id)]
        .filter((id, index, all): id is string => Boolean(id) && all.indexOf(id) === index)
      let restored: AuthUser | null = null

      for (const accountId of candidates) {
        try {
          restored = await restoreAccount(accountId)
          break
        } catch (requestError) {
          if (isUnauthorized(requestError)) {
            forgetSignedInAccount(accountId)
            continue
          }
          if (!active) return
          setUser(null)
          setError(requestError instanceof Error ? requestError.message : 'Your account could not be checked right now.')
          setStatus('unavailable')
          return
        }
      }

      if (!restored) {
        try {
          restored = await restoreAccount()
          // Upgrade an older single-cookie session to the browser account selector.
          await activateAccount(restored.id)
        } catch (requestError) {
          if (!active) return
          setUser(null)
          if (isUnauthorized(requestError)) {
            setError(null)
            setStatus('signed-out')
          } else {
            setError(requestError instanceof Error ? requestError.message : 'Your account could not be checked right now.')
            setStatus('unavailable')
          }
          setAccounts(readSignedInAccounts())
          return
        }
      }

      if (!active || !restored) return
      setUser(restored)
      setAccounts(rememberSignedInAccount(restored))
      setStatus('signed-in')
      setError(null)
    }

    void loadSession()
    return () => { active = false }
  }, [])

  const acceptAuthenticatedUser = useCallback((authenticatedUser: AuthUser) => {
    setUser(authenticatedUser)
    setAccounts(rememberSignedInAccount(authenticatedUser))
    setStatus('signed-in')
    setError(null)
  }, [])

  const switchAccount = useCallback(async (accountId: string) => {
    if (accountId === user?.id || switchingAccountId) return
    setSwitchingAccountId(accountId)
    setError(null)

    try {
      const switchedUser = await restoreAccount(accountId)
      if (switchedUser.id !== accountId) {
        throw new AuthApiError(401, 'That account is no longer available in this browser.')
      }
      setUser(switchedUser)
      setAccounts(rememberSignedInAccount(switchedUser))
      setStatus('signed-in')
    } catch (requestError) {
      if (user) {
        try { await activateAccount(user.id) } catch { /* Keep the account list useful during a service outage. */ }
      }
      throw requestError
    } finally {
      setSwitchingAccountId(null)
    }
  }, [switchingAccountId, user])

  const removeAccount = useCallback(async (accountId: string) => {
    await removeAccountFromDevice(accountId)
    const remaining = forgetSignedInAccount(accountId)
    setAccounts(remaining)

    if (user?.id !== accountId) return

    setUser(null)
    setStatus('checking')
    for (const account of remaining) {
      try {
        const fallbackUser = await restoreAccount(account.id)
        if (fallbackUser.id !== account.id) continue
        setUser(fallbackUser)
        setAccounts(rememberSignedInAccount(fallbackUser))
        setStatus('signed-in')
        return
      } catch (requestError) {
        if (!isUnauthorized(requestError)) {
          setError(requestError instanceof Error ? requestError.message : 'We could not switch to another account just now.')
          setStatus('unavailable')
          return
        }
        forgetSignedInAccount(account.id)
      }
    }

    setAccounts(readSignedInAccounts())
    setStatus('signed-out')
  }, [user])

  const signOut = useCallback(async () => {
    if (!user) return
    await removeAccount(user.id)
  }, [removeAccount, user])

  const signOutEverywhere = useCallback(async (currentPassword: string) => {
    await logoutAllDevices(currentPassword)
    if (!user) return
    const remaining = forgetSignedInAccount(user.id)
    setAccounts(remaining)
    setUser(null)
    setStatus('checking')
    for (const account of remaining) {
      try {
        const fallbackUser = await restoreAccount(account.id)
        if (fallbackUser.id !== account.id) continue
        setUser(fallbackUser)
        setAccounts(rememberSignedInAccount(fallbackUser))
        setStatus('signed-in')
        setError(null)
        return
      } catch (requestError) {
        if (!isUnauthorized(requestError)) {
          setError(requestError instanceof Error ? requestError.message : 'We could not switch to another account just now.')
          setStatus('unavailable')
          return
        }
        forgetSignedInAccount(account.id)
      }
    }
    setAccounts(readSignedInAccounts())
    setStatus('signed-out')
    setError(null)
  }, [user])

  const updateUser = useCallback(async (fullName: string) => {
    const updatedUser = await updateCurrentUser(fullName)
    setUser(updatedUser)
    setAccounts(rememberSignedInAccount(updatedUser))
    return updatedUser
  }, [])

  const forgetAccount = useCallback((accountId: string) => {
    const remaining = forgetSignedInAccount(accountId)
    setAccounts(remaining)
    if (user?.id === accountId) {
      setUser(null)
      setStatus('signed-out')
      setError(null)
      if (remaining[0]) setPreferredAccountId(remaining[0].id)
    }
  }, [user])

  return (
    <AuthSessionContext.Provider value={{
      user,
      accounts,
      status,
      error,
      switchingAccountId,
      acceptAuthenticatedUser,
      switchAccount,
      removeAccount,
      forgetAccount,
      signOut,
      signOutEverywhere,
      updateUser,
    }}>
      {children}
    </AuthSessionContext.Provider>
  )
}
