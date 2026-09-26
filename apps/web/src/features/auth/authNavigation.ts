export type AuthEntryPath = '/signin' | '/signup'

export type AuthRouteLocation = {
  pathname: string
  search: string
  hash: string
}

const APP_ORIGIN = 'https://blueverse.invalid'
const AUTH_PATHS: readonly string[] = ['/signin', '/signup']

export function safePostAuthDestination(search: string): string {
  const candidate = new URLSearchParams(search).get('returnTo')
  if (!candidate || !candidate.startsWith('/') || candidate.startsWith('//')) return '/'

  try {
    const destination = new URL(candidate, APP_ORIGIN)
    if (destination.origin !== APP_ORIGIN || AUTH_PATHS.includes(destination.pathname)) return '/'
    return `${destination.pathname}${destination.search}${destination.hash}`
  } catch {
    return '/'
  }
}

export function authEntryHref(path: AuthEntryPath, location: AuthRouteLocation): string {
  if (path === '/signup') return path
  const currentPath = `${location.pathname}${location.search}${location.hash}`
  const returnTo = AUTH_PATHS.includes(location.pathname)
    ? safePostAuthDestination(location.search)
    : currentPath
  return authEntryHrefFor(path, returnTo || '/')
}

export function authEntryHrefFor(path: AuthEntryPath, returnTo: string): string {
  if (path === '/signup') return path
  return `${path}?returnTo=${encodeURIComponent(returnTo || '/')}`
}
