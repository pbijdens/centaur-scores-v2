import { describe, expect, it } from 'vitest'
import { resolveRoute } from './router'

describe('resolveRoute', () => {
  it('resolves a narrowcast route with tenant and scope', () => {
    expect(resolveRoute('/narrowcast/tenant-id/club-finals')).toEqual({
      view: 'narrowcast',
      tenantId: 'tenant-id',
      scope: 'club-finals'
    })
  })

  it('rejects a narrowcast route without a tenant or scope', () => {
    expect(resolveRoute('/narrowcast/club-finals')).toEqual({ view: 'home', invalid: true })
  })
})