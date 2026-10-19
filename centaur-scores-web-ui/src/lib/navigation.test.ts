import { describe, expect, it } from 'vitest'
import { navSectionFor } from './navigation'

describe('navSectionFor', () => {
  it('maps match and competition sub-pages to their top-level section', () => {
    expect(navSectionFor('matches')).toBe('matches')
    expect(navSectionFor('match-participant-scores')).toBe('matches')
    expect(navSectionFor('competition')).toBe('competitions')
  })

  it('maps "More" pages to the path of their overview tile', () => {
    expect(navSectionFor('category')).toBe('categories')
    expect(navSectionFor('participant')).toBe('participants')
    expect(navSectionFor('personal-best-log')).toBe('personal-best')
    expect(navSectionFor('account')).toBe('accounts')
  })

  it('returns null for pages outside the navigation', () => {
    expect(navSectionFor('profile')).toBeNull()
    expect(navSectionFor('select-tenant')).toBeNull()
  })
})
