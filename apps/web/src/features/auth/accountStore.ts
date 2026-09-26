import type { AuthUser } from './auth'

export type SignedInAccount = Pick<AuthUser, 'id' | 'fullName' | 'email'>

const accountsKey = 'blueverse.signed-in-accounts:v1'
const activeAccountKey = 'blueverse.active-account-id:v1'
const legacyAccountsKey = 'blueverse.signed-in-accounts'
const legacyActiveAccountKey = 'blueverse.active-account-id'

function localStore(): Storage | null {
  try {
    return window.localStorage
  } catch {
    return null
  }
}

export function readSignedInAccounts(): SignedInAccount[] {
  const storage = localStore()
  const raw = storage?.getItem(accountsKey) ?? storage?.getItem(legacyAccountsKey)
  if (!raw) return []

  try {
    const stored: unknown = JSON.parse(raw)
    if (!Array.isArray(stored)) return []
    const accounts = stored
      .filter((account): account is SignedInAccount =>
        typeof account === 'object' && account !== null &&
        'id' in account && typeof account.id === 'string' && account.id.length > 0 &&
        'fullName' in account && typeof account.fullName === 'string' &&
        'email' in account && typeof account.email === 'string')
      .map(({ id, fullName, email }) => ({ id, fullName, email }))
      .slice(0, 5)
    if (storage && !storage.getItem(accountsKey)) {
      storage.setItem(accountsKey, JSON.stringify(accounts))
      storage.removeItem(legacyAccountsKey)
    }
    return accounts
  } catch {
    return []
  }
}

export function rememberSignedInAccount(user: AuthUser): SignedInAccount[] {
  const accounts = readSignedInAccounts()
  const next = [
    { id: user.id, fullName: user.fullName, email: user.email },
    ...accounts.filter((account) => account.id !== user.id),
  ].slice(0, 5)
  try {
    localStore()?.setItem(accountsKey, JSON.stringify(next))
    setPreferredAccountId(user.id)
  } catch {
    // HttpOnly cookies remain the authentication source if browser storage is unavailable.
  }
  return next
}

export function forgetSignedInAccount(userId: string): SignedInAccount[] {
  const next = readSignedInAccounts().filter((account) => account.id !== userId)
  const storage = localStore()
  try {
    storage?.setItem(accountsKey, JSON.stringify(next))
    storage?.removeItem(legacyAccountsKey)
    if (storage?.getItem(activeAccountKey) === userId) {
      if (next[0]) storage.setItem(activeAccountKey, next[0].id)
      else storage.removeItem(activeAccountKey)
    }
  } catch {
    // Best effort: authentication remains controlled by protected cookies.
  }
  return next
}

export function clearSignedInAccounts(): void {
  try {
    localStore()?.removeItem(accountsKey)
    localStore()?.removeItem(activeAccountKey)
    localStore()?.removeItem(legacyAccountsKey)
    localStore()?.removeItem(legacyActiveAccountKey)
  } catch {
    // Best effort: authentication remains controlled by protected cookies.
  }
}

export function getPreferredAccountId(): string | null {
  try {
    const storage = localStore()
    const preferred = storage?.getItem(activeAccountKey) ?? storage?.getItem(legacyActiveAccountKey) ?? null
    if (preferred && storage && !storage.getItem(activeAccountKey)) {
      storage.setItem(activeAccountKey, preferred)
      storage.removeItem(legacyActiveAccountKey)
    }
    return preferred
  } catch {
    return null
  }
}

export function setPreferredAccountId(userId: string): void {
  try {
    localStore()?.setItem(activeAccountKey, userId)
    localStore()?.removeItem(legacyActiveAccountKey)
  } catch {
    // Best effort: authentication remains controlled by protected cookies.
  }
}
