import { describe, expect, it } from 'vitest'
import { flattenMatchResults, flattenResults, paginate, parseLayoutOptions, rankLabel, ruleBlocks, splitInTwo, unitsPerColumn, type ResultUnit } from './resultsLayout'
import type { CompetitionResultEntry, LiveScoringEntry } from './types'

function entry(name: string, overrides: Partial<CompetitionResultEntry> = {}): CompetitionResultEntry {
  return { position: '1', needsTieBreaker: false, name, disqualified: false, total: 0, roundScores: {}, ruleScores: {}, rules: [], ...overrides }
}

const header = (name: string): ResultUnit => ({ kind: 'header', name })
const row = (name: string): ResultUnit => ({ kind: 'entry', entry: entry(name) })
const shape = (units: ResultUnit[]) => units.map((unit) => unit.kind === 'header' ? `H:${unit.name}` : unit.kind === 'entry' ? unit.entry.name : unit.entry.line1)

describe('flattenResults', () => {
  it('emits a header before each group and skips empty groups', () => {
    const units = flattenResults([
      { name: 'Recurve', entries: [entry('a'), entry('b')] },
      { name: 'Empty', entries: [] },
      { name: 'Compound', entries: [entry('c')] },
    ])
    expect(shape(units)).toEqual(['H:Recurve', 'a', 'b', 'H:Compound', 'c'])
  })
})

describe('flattenMatchResults', () => {
  const matchEntry = (line1: string): LiveScoringEntry => ({ position: 1, needsTieBreaker: false, line1, arrows: 3, score: 27, aboveTarget: false })

  it('emits a header before each scope block and skips empty blocks', () => {
    const units = flattenMatchResults([
      { name: 'Recurve', entries: [matchEntry('a'), matchEntry('b')] },
      { name: 'Empty', entries: [] },
      { name: 'Compound', entries: [matchEntry('c')] },
    ])
    expect(shape(units)).toEqual(['H:Recurve', 'a', 'b', 'H:Compound', 'c'])
    expect(units[1].kind).toBe('match-entry')
  })
})

describe('splitInTwo', () => {
  it('splits an even stream in half', () => {
    const [left, right] = splitInTwo([header('A'), row('a1'), row('a2'), row('a3')])
    expect(shape(left)).toEqual(['H:A', 'a1'])
    expect(shape(right)).toEqual(['a2', 'a3'])
  })

  it('gives the extra unit of an odd stream to the left column', () => {
    const [left, right] = splitInTwo([header('A'), row('a1'), row('a2'), row('a3'), row('a4')])
    expect(shape(left)).toEqual(['H:A', 'a1', 'a2'])
    expect(shape(right)).toEqual(['a3', 'a4'])
  })

  it('moves a header that would end the left column to the right column', () => {
    const [left, right] = splitInTwo([header('A'), row('a1'), header('B'), row('b1'), row('b2'), row('b3')])
    expect(shape(left)).toEqual(['H:A', 'a1'])
    expect(shape(right)).toEqual(['H:B', 'b1', 'b2', 'b3'])
  })

  it('handles an empty stream', () => {
    expect(splitInTwo([])).toEqual([[], []])
  })
})

describe('paginate', () => {
  it('fills two columns per page, left column first', () => {
    const pages = paginate([header('A'), row('a1'), row('a2'), row('a3'), row('a4'), row('a5'), row('a6')], 3)
    expect(pages.map((page) => page.map(shape))).toEqual([
      [['H:A', 'a1', 'a2'], ['a3', 'a4', 'a5']],
      [['a6']],
    ])
  })

  it('never ends a column on a header', () => {
    const pages = paginate([header('A'), row('a1'), header('B'), row('b1'), row('b2')], 3)
    expect(pages.map((page) => page.map(shape))).toEqual([[['H:A', 'a1'], ['H:B', 'b1', 'b2']]])
  })

  it('returns no pages for an empty report', () => {
    expect(paginate([], 3)).toEqual([])
  })
})

describe('unitsPerColumn', () => {
  const options = { mode: 'pages', detailLines: 2, fontScale: 1 } as const

  it('fits fewer units in landscape and with larger fonts', () => {
    expect(unitsPerColumn('A4', 'portrait', options)).toBe(23)
    expect(unitsPerColumn('A4', 'landscape', options)).toBe(15)
    expect(unitsPerColumn('A4', 'portrait', { ...options, detailLines: 3 })).toBeLessThan(23)
    expect(unitsPerColumn('A4', 'portrait', { ...options, fontScale: 2 })).toBeLessThan(12)
  })

  it('follows the paper size', () => {
    expect(unitsPerColumn('Letter', 'portrait', options)).toBe(22)
    expect(unitsPerColumn('A3', 'portrait', options)).toBe(35)
    expect(unitsPerColumn('A5', 'portrait', options)).toBe(15)
    // A3 at 141% holds about as many units as A4 at 100% (the safety margin does not scale with the font).
    expect(unitsPerColumn('A3', 'portrait', { ...options, fontScale: 1.41 })).toBe(24)
  })
})

describe('rankLabel', () => {
  it('shows the position, marked when a tie-breaker is needed', () => {
    expect(rankLabel(entry('a', { position: '3' }))).toBe('3')
    expect(rankLabel(entry('a', { position: '3', needsTieBreaker: true }))).toBe('3*')
  })

  it('shows a dash when disqualified or unranked', () => {
    expect(rankLabel(entry('a', { position: '-', disqualified: true }))).toBe('–')
    expect(rankLabel(entry('a', { position: null }))).toBe('–')
  })
})

describe('ruleBlocks', () => {
  it('labels each rule and marks missing and unused round values', () => {
    const blocks = ruleBlocks(entry('a', {
      rules: [
        { name: '18 meter', aggregation: 'total', total: 190, rounds: [{ roundId: 'r1', value: 100, used: true }, { roundId: 'r2', value: null, used: false }, { roundId: 'r3', value: 90, used: true }] },
        { name: '25 meter', aggregation: 'f1', total: 12, rounds: [{ roundId: 'r1', value: 12, used: true }, { roundId: 'r2', value: 8, used: false }] },
      ],
    }))
    expect(blocks).toEqual([
      { label: '18 meter', total: 190, scores: [{ text: '100', struck: false, missing: false }, { text: '–', struck: false, missing: true }, { text: '90', struck: false, missing: false }] },
      { label: '25 meter', total: 12, scores: [{ text: '12', struck: false, missing: false }, { text: '8', struck: true, missing: false }] },
    ])
  })

  it('tolerates a document without the rules breakdown', () => {
    expect(ruleBlocks({ ...entry('a'), rules: undefined as never })).toEqual([])
  })
})

describe('layout options', () => {
  it('keeps valid stored options', () => {
    expect(parseLayoutOptions({ mode: 'pages', detailLines: 3, fontScale: 1.41 })).toEqual({ mode: 'pages', detailLines: 3, fontScale: 1.41 })
  })

  it('falls back to defaults for missing or invalid values', () => {
    expect(parseLayoutOptions(null)).toEqual({ mode: 'stream', detailLines: 2, fontScale: 1 })
    expect(parseLayoutOptions({ mode: 'bogus', detailLines: 7, fontScale: 3 })).toEqual({ mode: 'stream', detailLines: 2, fontScale: 1 })
  })
})
