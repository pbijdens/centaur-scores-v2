import { describe, expect, it } from 'vitest'
import { groupBoundaryPercents, totalArrows, typicalArrowsShot } from './matchProgress'
import type { LiveScoringBlock, LiveScoringPage } from './types'

function page(overrides: Partial<LiveScoringPage> & { blocks: LiveScoringBlock[] }): LiveScoringPage {
  return { timeout: 15, tenant: 't', matchName: 'm', matchDate: '2026-01-01', ends: 10, arrowsPerEnd: 3, groupEnds: null, ...overrides }
}

function block(arrowsList: number[]): LiveScoringBlock {
  return {
    name: 'block',
    entries: arrowsList.map((arrows, index) => ({ position: index + 1, needsTieBreaker: false, line1: `p${index}`, arrows, score: 0, aboveTarget: false }))
  }
}

describe('totalArrows', () => {
  it('multiplies ends by arrows per end', () => {
    expect(totalArrows(page({ ends: 10, arrowsPerEnd: 3, blocks: [] }))).toBe(30)
  })

  it('falls back to 1 arrow per end when arrowsPerEnd is not set', () => {
    expect(totalArrows(page({ ends: 10, arrowsPerEnd: 0, blocks: [] }))).toBe(10)
  })
})

describe('typicalArrowsShot', () => {
  it('picks the most common arrows-shot value across all entries', () => {
    expect(typicalArrowsShot(page({ blocks: [block([9, 9, 12])] }))).toBe(9)
  })

  it('spans multiple blocks', () => {
    expect(typicalArrowsShot(page({ blocks: [block([9, 9]), block([12])] }))).toBe(9)
  })

  it('breaks ties by picking the larger arrows-shot count', () => {
    expect(typicalArrowsShot(page({ blocks: [block([9, 12])] }))).toBe(12)
  })

  it('clamps to the total arrow count for the match', () => {
    expect(typicalArrowsShot(page({ ends: 5, arrowsPerEnd: 3, blocks: [block([30])] }))).toBe(15)
  })

  it('returns 0 for a match with no entries yet', () => {
    expect(typicalArrowsShot(page({ blocks: [] }))).toBe(0)
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
