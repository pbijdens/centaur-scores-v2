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

  it('resolves the select-tenant route', () => {
    expect(resolveRoute('/select-tenant')).toEqual({ view: 'select-tenant' })
  })

  it('resolves the personal best control, configuration, and log routes', () => {
    expect(resolveRoute('/personal-best')).toEqual({ view: 'personal-best' })
    expect(resolveRoute('/personal-best/classifiers')).toEqual({ view: 'personal-best-classifiers' })
    expect(resolveRoute('/personal-best/disciplines')).toEqual({ view: 'personal-best-disciplines' })
    expect(resolveRoute('/personal-best/export-configuration')).toEqual({ view: 'personal-best-export-config' })
    expect(resolveRoute('/personal-best/import-configuration')).toEqual({ view: 'personal-best-import-config' })
    expect(resolveRoute('/personal-best/log')).toEqual({ view: 'personal-best-log' })
  })
})