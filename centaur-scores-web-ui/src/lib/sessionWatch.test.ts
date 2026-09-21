import { describe, expect, it } from 'vitest'
import { decodeJwtExpirySeconds, isSessionWatchActive, isTokenExpired, makeExpiryCache, type SessionWatchState } from './sessionWatch'

function makeToken(payload: Record<string, unknown>): string {
  const encode = (value: object) => btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
  return `${encode({ alg: 'none', typ: 'JWT' })}.${encode(payload)}.signature`
}

const baseState: SessionWatchState = {
  token: 'a.b.c',
  loggedIn: true,
  sessionReady: true,
  tenantAccessError: false,
  view: 'home'
}

describe('decodeJwtExpirySeconds', () => {
  it('reads the exp claim out of a well-formed token', () => {
    expect(decodeJwtExpirySeconds(makeToken({ exp: 1234567890 }))).toBe(1234567890)
  })

  it('returns null when the token has no exp claim', () => {
    expect(decodeJwtExpirySeconds(makeToken({ sub: 'account-1' }))).toBeNull()
  })

  it('returns null for a malformed token', () => {
    expect(decodeJwtExpirySeconds('not-a-jwt')).toBeNull()
    expect(decodeJwtExpirySeconds('')).toBeNull()
    expect(decodeJwtExpirySeconds('only.one')).toBeNull()
  })
})

describe('isTokenExpired', () => {
  it('is false when there is no token at all', () => {
    expect(isTokenExpired('')).toBe(false)
  })

  it('is false while the token is still within its lifetime', () => {
    const token = makeToken({ exp: 2_000_000_000 })
    expect(isTokenExpired(token, 1_000_000_000_000)).toBe(false)
  })

  it('is true once the token has passed its exp claim', () => {
    const token = makeToken({ exp: 1_000 })
    expect(isTokenExpired(token, 1_000_001 * 1000)).toBe(true)
  })

  it('is false for an old token that never had a decodable expiry', () => {
    expect(isTokenExpired('garbage-token')).toBe(false)
  })
})

describe('makeExpiryCache', () => {
  it('memoizes the decoded expiry for repeated calls with the same token', () => {
    const expiryMsFor = makeExpiryCache()
    const token = makeToken({ exp: 42 })
    expect(expiryMsFor(token)).toBe(42_000)
    expect(expiryMsFor(token)).toBe(42_000)
  })

  it('recomputes when the token changes', () => {
    const expiryMsFor = makeExpiryCache()
    expect(expiryMsFor(makeToken({ exp: 1 }))).toBe(1_000)
    expect(expiryMsFor(makeToken({ exp: 2 }))).toBe(2_000)
  })
})

describe('isSessionWatchActive', () => {
  it('is active for an ordinary authenticated management view', () => {
    expect(isSessionWatchActive(baseState)).toBe(true)
  })

  it('is inactive while not logged in, session not ready, or tenant access is denied', () => {
    expect(isSessionWatchActive({ ...baseState, loggedIn: false })).toBe(false)
    expect(isSessionWatchActive({ ...baseState, sessionReady: false })).toBe(false)
    expect(isSessionWatchActive({ ...baseState, tenantAccessError: true })).toBe(false)
  })

  it('is inactive for result, QR, and print pages, which run unattended without anyone to dismiss a popup', () => {
    expect(isSessionWatchActive({ ...baseState, view: 'match-results-scope' })).toBe(false)
    expect(isSessionWatchActive({ ...baseState, view: 'competition-results' })).toBe(false)
    expect(isSessionWatchActive({ ...baseState, view: 'match-qr' })).toBe(false)
    expect(isSessionWatchActive({ ...baseState, view: 'match-print' })).toBe(false)
  })

  // The narrowcast display runs on unattended kiosks with no login of its own - it must never be
  // redirected to /login or shown a popup, regardless of whatever stale token happens to be sitting
  // in that browser's localStorage from a previous, unrelated session on the same machine.
  describe('the narrowcast path', () => {
    it('is inactive with no token present at all', () => {
      expect(isSessionWatchActive({ ...baseState, view: 'narrowcast', token: '' })).toBe(false)
    })

    it('is inactive with an old, already-expired token left on the machine', () => {
      const expiredToken = makeToken({ exp: 1 })
      expect(isTokenExpired(expiredToken, Date.now())).toBe(true)
      expect(isSessionWatchActive({ ...baseState, view: 'narrowcast', token: expiredToken })).toBe(false)
    })

    it('is inactive even with a currently-valid token', () => {
      const validToken = makeToken({ exp: 9_999_999_999 })
      expect(isSessionWatchActive({ ...baseState, view: 'narrowcast', token: validToken, loggedIn: true, sessionReady: true })).toBe(false)
    })
  })
})
