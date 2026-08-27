import { describe, expect, it } from 'vitest'
import { resolveRoute } from './router'

describe('resolveRoute', () => {
  it('resolves a narrowcast route with a scope', () => {
    expect(resolveRoute('/narrowcast/club-finals')).toEqual({
      view: 'narrowcast',
      scope: 'club-finals'
    })
  })

  it('rejects a narrowcast route without a scope', () => {
    expect(resolveRoute('/narrowcast')).toEqual({ view: 'home', invalid: true })
  })
})