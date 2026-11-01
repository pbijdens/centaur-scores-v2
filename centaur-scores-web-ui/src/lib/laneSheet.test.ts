import { describe, expect, it } from 'vitest'
import { buildLaneSheet, contrastingForeground, paginateLaneSheet, mixedLaneNumbers, parseLane, tagColor, uniformDeviceValue } from './laneSheet'
import type { Category, MatchParticipant, ScoreDevice } from './types'

function participant(id: string, deviceId: string | null, lane: string | null, categories: Record<string, number> = {}): MatchParticipant {
  return { id, matchId: 'm', lastName: id, fullName: id, categories, deviceId, deviceLane: lane, signed: false }
}

const devices: ScoreDevice[] = [
  { id: 'd1', matchId: 'm', name: 'Tablet 1', sortOrder: 0 },
  { id: 'd2', matchId: 'm', name: 'Tablet 2', sortOrder: 1 }
]

describe('parseLane', () => {
  it('splits number and letter', () => {
    expect(parseLane('1A')).toEqual({ number: 1, letter: 'A' })
    expect(parseLane(' 12b ')).toEqual({ number: 12, letter: 'B' })
    expect(parseLane('7')).toEqual({ number: 7, letter: null })
  })

  it('has no number for blank or non-numeric lanes', () => {
    expect(parseLane(null)).toEqual({ number: null, letter: null })
    expect(parseLane('')).toEqual({ number: null, letter: null })
    expect(parseLane('B1')).toEqual({ number: null, letter: null })
  })
})

describe('colours', () => {
  it('picks a readable foreground', () => {
    expect(contrastingForeground('#ffffff')).toBe('#111111')
    expect(contrastingForeground('#000000')).toBe('#ffffff')
  })

  it('gives each value of a category its own colour', () => {
    const category: Category = { id: 'c', name: 'Club', isUsed: true, values: [{ categoryId: 'c', valueId: 2, name: 'B' }, { categoryId: 'c', valueId: 1, name: 'A' }] }
    expect(tagColor(category, 1).background).not.toBe(tagColor(category, 2).background)
    expect(tagColor(category, 1)).toEqual(tagColor(category, 1))
  })
})

describe('checks', () => {
  it('flags lane numbers with more than one target value, across devices', () => {
    const participants = [
      participant('a', 'd1', '1A', { t: 1 }),
      participant('b', 'd2', '1B', { t: 2 }),
      participant('c', 'd1', '2A', { t: 1 }),
      participant('d', 'd1', '2B', { t: 1 }),
      participant('e', 'd1', '3A', { t: 1 }),
      participant('f', 'd1', '3B', {})
    ]
    expect([...mixedLaneNumbers(participants, 't')].sort()).toEqual([1, 3])
  })

  it('reports a device where everyone shares one value', () => {
    expect(uniformDeviceValue([participant('a', 'd1', null, { c: 4 }), participant('b', 'd1', null, { c: 4 })], 'c')).toBe(4)
    expect(uniformDeviceValue([participant('a', 'd1', null, { c: 4 }), participant('b', 'd1', null, { c: 5 })], 'c')).toBeUndefined()
    expect(uniformDeviceValue([participant('a', 'd1', null, { c: 4 })], 'c')).toBeUndefined()
  })
})

describe('buildLaneSheet', () => {
  it('fills skipped lanes and gives every lane block the same slots', () => {
    const participants = [
      participant('1a', 'd1', '1A'), participant('1b', 'd1', '1B'), participant('1c', 'd1', '1C'),
      participant('3a', 'd1', '3A'), participant('3b', 'd1', '3B'),
      participant('5c', 'd2', '5C'),
      participant('6a', 'd2', '6A'), participant('6b', 'd2', '6B')
    ]
    const { blocks, slotCount } = buildLaneSheet(devices, participants)
    expect(slotCount).toBe(4)
    expect(blocks.map((block) => block.laneNumber)).toEqual([1, 2, 3, 4, 5, 6])
    expect(blocks.map((block) => block.kind)).toEqual(['lane', 'empty', 'lane', 'empty', 'lane', 'lane'])
    expect(blocks.map((block) => block.rows.map((row) => row.slot).join(' '))).toEqual(['1A 1B 1C 1D', '2A 2B 2C 2D', '3A 3B 3C 3D', '4A 4B 4C 4D', '5A 5B 5C 5D', '6A 6B 6C 6D'])
    expect(blocks[4].rows.map((row) => row.participant?.id ?? null)).toEqual([null, null, '5c', null])
    expect(blocks[3].deviceId).toBe('d1')
  })

  it('lists archers without a lane number per device, and archers without a device last', () => {
    const participants = [participant('a', 'd1', '1A'), participant('b', 'd1', null), participant('c', null, null), participant('d', 'd1', '1')]
    const { blocks } = buildLaneSheet(devices, participants)
    expect(blocks.map((block) => block.kind)).toEqual(['lane', 'noLane', 'noDevice'])
    expect(blocks[0].rows.map((row) => row.participant?.id)).toEqual(['a', 'd', undefined, undefined])
    expect(blocks[1].rows).toHaveLength(4)
    expect(blocks[1].participants.map((item) => item.id)).toEqual(['b'])
    expect(blocks[2].participants.map((item) => item.id)).toEqual(['c'])
  })
})

describe('paginateLaneSheet', () => {
  const ids = (page: { segments: { block: { laneNumber: number | null }; rows: unknown[]; continued: boolean }[] }) =>
    page.segments.map((segment) => `${segment.block.laneNumber ?? '-'}:${segment.rows.length}${segment.continued ? '+' : ''}`)

  it('gives lanes with five archers five rows, so four of them share a page', () => {
    const participants = Array.from({ length: 5 }, (_, lane) => ['A', 'B', 'C', 'D', 'E'].map((letter) => participant(`${lane}${letter}`, 'd1', `${lane + 1}${letter}`))).flat()
    const pages = paginateLaneSheet(buildLaneSheet(devices, participants).blocks, 24)
    expect(pages.map(ids)).toEqual([['1:5', '2:5', '3:5', '4:5'], ['5:5']])
  })

  it('fits six lanes of four archers on a page and never splits a lane', () => {
    const participants = Array.from({ length: 7 }, (_, lane) => ['A', 'B', 'C', 'D'].map((letter) => participant(`${lane}${letter}`, lane < 6 ? 'd1' : 'd2', `${lane + 1}${letter}`))).flat()
    const { blocks } = buildLaneSheet(devices, participants)
    const pages = paginateLaneSheet(blocks, 24)
    expect(pages.map(ids)).toEqual([['1:4', '2:4', '3:4', '4:4', '5:4', '6:4'], ['7:4']])
  })

  it('moves a lane that does not fit to the next page', () => {
    const participants = Array.from({ length: 3 }, (_, lane) => ['A', 'B', 'C'].map((letter) => participant(`${lane}${letter}`, 'd1', `${lane + 1}${letter}`))).flat()
    const pages = paginateLaneSheet(buildLaneSheet(devices, participants).blocks, 10)
    expect(pages.map(ids)).toEqual([['1:4', '2:4'], ['3:4']])
  })

  it('continues a block taller than a page on the next page', () => {
    const participants = [participant('a', 'd1', '1A'), ...Array.from({ length: 9 }, (_, index) => participant(`x${index}`, null, null))]
    const pages = paginateLaneSheet(buildLaneSheet(devices, participants).blocks, 5)
    expect(pages.map(ids)).toEqual([['1:4', '-:1'], ['-:5+'], ['-:3+']])
  })
})
