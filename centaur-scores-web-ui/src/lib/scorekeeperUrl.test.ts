import { describe, expect, it } from 'vitest'
import { scorekeeperUrl } from './api'

describe('scorekeeperUrl', () => {
  it('points at /scores on the web UI origin when the tenant has no override', () => {
    const url = new URL(scorekeeperUrl('t1', 'm1', 'd1', 'nl'))
    expect(url.pathname).toBe('/scores')
    expect(url.searchParams.get('api')).toMatch(/\/scorekeeper\/t1\/m1\/d1$/)
    expect(url.searchParams.get('language')).toBe('NL')
  })

  it('uses the tenant override as the scorekeeper base URL, keeping its own path and query', () => {
    const url = new URL(scorekeeperUrl('t1', 'm1', 'd1', 'en', 'https://scores.example.com/app/?x=1'))
    expect(url.origin).toBe('https://scores.example.com')
    expect(url.pathname).toBe('/app/')
    expect(url.searchParams.get('x')).toBe('1')
    expect(url.searchParams.get('api')).toMatch(/\/scorekeeper\/t1\/m1\/d1$/)
    expect(url.searchParams.get('language')).toBe('EN')
  })

  it('ignores an empty override', () => {
    expect(new URL(scorekeeperUrl('t1', 'm1', 'd1', 'en', '')).pathname).toBe('/scores')
  })
})
