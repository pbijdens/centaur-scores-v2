import type { View } from './types'

// Views that render without ever requiring a fresh session: narrowcast displays and result
// pages run unattended on kiosks/printers/public screens with no one there to dismiss a popup
// or be redirected, and match-qr shares that "standalone, no chrome" treatment (see CLAUDE.md).
const VIEWS_WITHOUT_SESSION_WATCH = new Set<View>(['narrowcast', 'match-qr', 'match-print', 'match-results-scope', 'competition-results'])

export type SessionWatchState = {
  token: string
  loggedIn: boolean
  sessionReady: boolean
  tenantAccessError: boolean
  view: View
}

export function isSessionWatchActive(state: SessionWatchState): boolean {
  return Boolean(state.token) && state.loggedIn && state.sessionReady && !state.tenantAccessError && !VIEWS_WITHOUT_SESSION_WATCH.has(state.view)
}

export function decodeJwtExpirySeconds(token: string): number | null {
  const payload = token.split('.')[1]
  if (!payload) return null
  try {
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/').padEnd(payload.length + (4 - payload.length % 4) % 4, '=')
    const claims = JSON.parse(atob(base64))
    return typeof claims.exp === 'number' ? claims.exp : null
  } catch {
    return null
  }
}

export function isTokenExpired(token: string, nowMs: number = Date.now()): boolean {
  if (!token) return false
  const expirySeconds = decodeJwtExpirySeconds(token)
  return expirySeconds !== null && nowMs >= expirySeconds * 1000
}

// Decoding a JWT is cheap, but the point of the exercise is to do it once per token rather than
// on every 15-second poll tick - the returned function memoizes the last token it saw.
export function makeExpiryCache(): (token: string) => number | null {
  let cachedToken: string | null = null
  let cachedExpiryMs: number | null = null
  return (token: string) => {
    if (token !== cachedToken) {
      cachedToken = token
      const expirySeconds = decodeJwtExpirySeconds(token)
      cachedExpiryMs = expirySeconds === null ? null : expirySeconds * 1000
    }
    return cachedExpiryMs
  }
}
