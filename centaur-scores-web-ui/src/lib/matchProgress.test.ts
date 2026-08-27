import { describe, expect, it } from 'vitest'
import { groupBoundaryPercents, typicalEndsCompleted } from './matchProgress'
import type { LiveScoringBlock, LiveScoringPage } from './types'

function page(overrides: Partial<LiveScoringPage> & { blocks: LiveScoringBlock[] }): LiveScoringPage {
  return { timeout: 15, tenant: 't', matchName: 'm', matchDate: '2026-01-01', ends: 10, arrowsPerEnd: 3, groupEnds: null, ...overrides }
}

function block(arrowsList: number[]): LiveScoringBlock {
  return {
    name: 'block',
    entries: arrowsList.map((arrows, index) => ({ position: index + 1, needsTieBreaker: false, line1: `p${index}`, arrows, score: 0 }))
  }
}

describe('typicalEndsCompleted', () => {
  it('picks the most common ends-completed value across all entries', () => {
    // arrowsPerEnd 3: 9 arrows -> 3 ends, 12 arrows -> 4 ends
    expect(typicalEndsCompleted(page({ blocks: [block([9, 9, 12])] }))).toBe(3)
  })

  it('spans multiple blocks', () => {
    expect(typicalEndsCompleted(page({ blocks: [block([9, 9]), block([12])] }))).toBe(3)
  })

  it('breaks ties by picking the larger ends-completed count', () => {
    expect(typicalEndsCompleted(page({ blocks: [block([9, 12])] }))).toBe(4)
  })

  it('clamps to the match end count', () => {
    expect(typicalEndsCompleted(page({ ends: 5, blocks: [block([30])] }))).toBe(5)
  })

  it('returns 0 for a match with no entries yet', () => {
    expect(typicalEndsCompleted(page({ blocks: [] }))).toBe(0)
  })
})

describe('groupBoundaryPercents', () => {
  it('returns a boundary for each group split', () => {
    const boundaries = groupBoundaryPercents(page({ ends: 30, groupEnds: 10, blocks: [] }))
    expect(boundaries).toHaveLength(2)
    expect(boundaries[0]).toBeCloseTo(100 / 3)
    expect(boundaries[1]).toBeCloseTo(200 / 3)
  })

  it('returns nothing when there is no group configured', () => {
    expect(groupBoundaryPercents(page({ ends: 30, groupEnds: null, blocks: [] }))).toEqual([])
  })

  it('returns nothing when the group spans the whole match', () => {
    expect(groupBoundaryPercents(page({ ends: 30, groupEnds: 30, blocks: [] }))).toEqual([])
  })
})
