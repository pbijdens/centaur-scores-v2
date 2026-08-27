import { describe, expect, it } from 'vitest'
import { deriveLastName, memberCategoryLabel, memberDetailLabel, memberDisplayLabel } from './participantName'
import type { Category } from './types'

const categories: Category[] = [
  { id: 'age', name: 'Age', isUsed: true, values: [{ categoryId: 'age', valueId: 1, name: 'Junior' }] },
  { id: 'bow', name: 'Bow', isUsed: true, values: [{ categoryId: 'bow', valueId: 2, name: 'Recurve' }] },
]

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

describe('memberCategoryLabel', () => {
  it('joins matching category value names in category order', () => {
    const member = { categories: { age: 1, bow: 2 } }
    expect(memberCategoryLabel(categories, member)).toBe('Junior / Recurve')
  })

  it('skips categories with no matching value', () => {
    const member = { categories: { age: 1 } }
    expect(memberCategoryLabel(categories, member)).toBe('Junior')
  })
})

describe('memberDetailLabel', () => {
  it('precedes the category values with the federation number', () => {
    const member = { fullName: 'Jane Doe', lastName: 'Doe', categories: { age: 1, bow: 2 }, federationNumber: '12345' }
    expect(memberDetailLabel(categories, member)).toBe('12345 / Junior / Recurve')
  })

  it('omits the federation number when absent', () => {
    const member = { fullName: 'Jane Doe', lastName: 'Doe', categories: { age: 1, bow: 2 }, federationNumber: null }
    expect(memberDetailLabel(categories, member)).toBe('Junior / Recurve')
  })
})

describe('memberDisplayLabel', () => {
  it('wraps the detail label in parentheses after the name', () => {
    const member = { fullName: 'Jane Doe', lastName: 'Doe', federationNumber: '12345', categories: { age: 1, bow: 2 } }
    expect(memberDisplayLabel(categories, member)).toBe('Jane Doe (12345 / Junior / Recurve)')
  })

  it('falls back to the last name when full name is blank', () => {
    const member = { fullName: '', lastName: 'Doe', federationNumber: null, categories: {} }
    expect(memberDisplayLabel(categories, member)).toBe('Doe')
  })
})
