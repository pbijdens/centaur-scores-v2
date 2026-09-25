import { describe, expect, it } from 'vitest'
import { flattenResults, layoutOptionsSearch, parseLayoutOptions, rankLabel, ruleBlocks, splitInTwo, type ResultUnit } from './competitionResultsLayout'
import type { CompetitionResultEntry } from './types'

function entry(name: string, overrides: Partial<CompetitionResultEntry> = {}): CompetitionResultEntry {
  return { position: '1', needsTieBreaker: false, name, disqualified: false, total: 0, roundScores: {}, ruleScores: {}, rules: [], ...overrides }
}

const header = (name: string): ResultUnit => ({ kind: 'header', name })
const row = (name: string): ResultUnit => ({ kind: 'entry', entry: entry(name) })
const shape = (units: ResultUnit[]) => units.map((unit) => unit.kind === 'header' ? `H:${unit.name}` : unit.entry.name)

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
  it('round-trips through the query string', () => {
    expect(parseLayoutOptions(layoutOptionsSearch({ mode: 'pages', detailLines: 3 }))).toEqual({ mode: 'pages', detailLines: 3 })
  })

  it('falls back to defaults for missing or invalid values', () => {
    expect(parseLayoutOptions('')).toEqual({ mode: 'stream', detailLines: 2 })
    expect(parseLayoutOptions('?layout=bogus&lines=7')).toEqual({ mode: 'stream', detailLines: 2 })
  })
})
