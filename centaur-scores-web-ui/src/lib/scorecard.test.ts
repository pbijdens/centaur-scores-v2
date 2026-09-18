import { describe, expect, it } from 'vitest'
import { arrowsShotCount, endTotal, groupRunningTotal, groupTotals, hasGroups, isGroupDivider, runningTotal, scoreFor, totalScore } from './scorecard'
import type { ArrowScore, Match } from './types'

function match(overrides: Partial<Match> = {}): Match {
  return {
    id: 'm', name: 'Match', date: '2026-01-01', isOpen: true, deviceSelectionMode: 'list-and-free',
    ends: 6, arrowsPerEnd: 3, groupEnds: null, allowFreeParticipants: true, keyboardJson: '{}', scoringRulesJson: '[]',
    ...overrides
  }
}

function score(end: number, arrow: number, value: number): ArrowScore {
  return { id: `${end}-${arrow}`, matchParticipantId: 'p', end, arrow, keyId: String(value), value }
}

describe('scoreFor', () => {
  it('finds the entry for a given end/arrow', () => {
    const scores = [score(1, 1, 9), score(1, 2, 7)]
    expect(scoreFor(scores, 1, 2)?.value).toBe(7)
    expect(scoreFor(scores, 1, 3)).toBeUndefined()
  })

  it('treats undefined scores as empty', () => {
    expect(scoreFor(undefined, 1, 1)).toBeUndefined()
  })
})

describe('totalScore / arrowsShotCount', () => {
  it('sums every scored arrow regardless of end', () => {
    const scores = [score(1, 1, 9), score(1, 2, 7), score(2, 1, 10)]
    expect(totalScore(scores)).toBe(26)
    expect(arrowsShotCount(scores)).toBe(3)
  })

  it('returns 0 for no scores yet', () => {
    expect(totalScore(undefined)).toBe(0)
    expect(arrowsShotCount(undefined)).toBe(0)
  })
})

describe('endTotal / runningTotal', () => {
  const scores = [score(1, 1, 9), score(1, 2, 7), score(1, 3, 10), score(2, 1, 5)]

  it('sums only the given end', () => {
    expect(endTotal(scores, 1)).toBe(26)
    expect(endTotal(scores, 2)).toBe(5)
    expect(endTotal(scores, 3)).toBe(0)
  })

  it('sums every end up to and including the given one', () => {
    expect(runningTotal(scores, 1)).toBe(26)
    expect(runningTotal(scores, 2)).toBe(31)
  })
})

describe('hasGroups / isGroupDivider', () => {
  it('is false when no groupEnds is configured', () => {
    expect(hasGroups(match({ groupEnds: null }))).toBe(false)
  })

  it('is false when groupEnds spans the whole match', () => {
    expect(hasGroups(match({ ends: 6, groupEnds: 6 }))).toBe(false)
  })

  it('is true when groupEnds splits the match into more than one group', () => {
    expect(hasGroups(match({ ends: 6, groupEnds: 3 }))).toBe(true)
  })

  it('marks the last end of a completed group, but not the match\'s final end', () => {
    const m = match({ ends: 6, groupEnds: 3 })
    expect(isGroupDivider(m, 3)).toBe(true)
    expect(isGroupDivider(m, 1)).toBe(false)
    expect(isGroupDivider(m, 6)).toBe(false)
  })
})

describe('groupRunningTotal', () => {
  it('resets at each group boundary', () => {
    const m = match({ ends: 6, groupEnds: 3 })
    const scores = [score(1, 1, 10), score(2, 1, 10), score(3, 1, 10), score(4, 1, 5)]
    expect(groupRunningTotal(m, scores, 3)).toBe(30)
    expect(groupRunningTotal(m, scores, 4)).toBe(5)
  })

  it('behaves like runningTotal when there are no groups', () => {
    const m = match({ ends: 6, groupEnds: null })
    const scores = [score(1, 1, 10), score(2, 1, 5)]
    expect(groupRunningTotal(m, scores, 2)).toBe(runningTotal(scores, 2))
  })
})

describe('groupTotals', () => {
  it('returns one subtotal per group-of-ends block', () => {
    const m = match({ ends: 6, groupEnds: 3 })
    const scores = [score(1, 1, 10), score(2, 1, 10), score(3, 1, 10), score(4, 1, 5), score(5, 1, 5)]
    expect(groupTotals(m, scores)).toEqual([30, 10])
  })

  it('returns a single total for the whole match when there are no groups', () => {
    const m = match({ ends: 4, groupEnds: null })
    const scores = [score(1, 1, 10), score(4, 1, 5)]
    expect(groupTotals(m, scores)).toEqual([15])
  })
})
