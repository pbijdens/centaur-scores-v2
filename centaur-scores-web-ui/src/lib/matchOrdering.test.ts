import { describe, expect, it } from 'vitest'
import { partitionMatches } from './matchOrdering'

describe('partitionMatches', () => {
  const matches = [
    { id: 'past-old', isOpen: false, date: '2026-01-01' },
    { id: 'future-late', isOpen: false, date: '2026-12-01' },
    { id: 'open', isOpen: true, date: '2026-03-01' },
    { id: 'today', isOpen: false, date: '2026-10-19' },
    { id: 'past-recent', isOpen: false, date: '2026-10-18' },
    { id: 'future-soon', isOpen: false, date: '2026-10-25' }
  ]

  it('splits open, upcoming (soonest first) and past (most recent first)', () => {
    const result = partitionMatches(matches, '2026-10-19')
    expect(result.open.map((item) => item.id)).toEqual(['open'])
    expect(result.upcoming.map((item) => item.id)).toEqual(['today', 'future-soon', 'future-late'])
    expect(result.past.map((item) => item.id)).toEqual(['past-recent', 'past-old'])
  })
})
