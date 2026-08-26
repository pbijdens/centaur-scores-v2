import { describe, expect, it } from 'vitest'
import { deriveLastName } from './participantName'

describe('deriveLastName', () => {
  it('returns the last word of a full name', () => {
    expect(deriveLastName('Jane Doe')).toBe('Doe')
  })

  it('handles multi-word names', () => {
    expect(deriveLastName('Jean van der Berg')).toBe('Berg')
  })

  it('trims surrounding whitespace and collapses internal whitespace', () => {
    expect(deriveLastName('  Jane   Doe  ')).toBe('Doe')
  })

  it('returns the single word for a one-word name', () => {
    expect(deriveLastName('Cher')).toBe('Cher')
  })

  it('returns an empty string for empty input', () => {
    expect(deriveLastName('   ')).toBe('')
  })
})
