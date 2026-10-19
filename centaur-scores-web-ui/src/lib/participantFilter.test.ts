import { describe, expect, it } from 'vitest'
import { participantMatchesFilter } from './participantFilter'
import type { Category } from './types'

const categories: Category[] = [
  { id: 'bow', name: 'Bow', isUsed: true, values: [{ categoryId: 'bow', valueId: 1, name: 'Recurve' }, { categoryId: 'bow', valueId: 2, name: 'Compound' }] },
  { id: 'age', name: 'Age', isUsed: true, values: [{ categoryId: 'age', valueId: 1, name: 'Senior' }] }
]
const participant = { fullName: 'Robin Archer', lastName: 'Archer', federationNumber: 'NL-12345', categories: { bow: 2, age: 1 } }

describe('participantMatchesFilter', () => {
  it('matches everyone on an empty filter', () => {
    expect(participantMatchesFilter(participant, '  ', categories)).toBe(true)
  })

  it('matches the name case-insensitively', () => {
    expect(participantMatchesFilter(participant, 'rOBIN arch', categories)).toBe(true)
  })

  it('matches the federation number', () => {
    expect(participantMatchesFilter(participant, 'nl-123', categories)).toBe(true)
  })

  it('matches any category value name', () => {
    expect(participantMatchesFilter(participant, 'COMPOUND', categories)).toBe(true)
    expect(participantMatchesFilter(participant, 'senior', categories)).toBe(true)
  })

  it('does not match an unrelated string or an unselected category value', () => {
    expect(participantMatchesFilter(participant, 'recurve', categories)).toBe(false)
    expect(participantMatchesFilter(participant, 'zzz', categories)).toBe(false)
  })
})
