import { describe, expect, it } from 'vitest'
import { buildLaneEditorState, deviceParticipantOrder, editorSlot, laneParticipantIds, slotOf } from './laneEditor'
import type { MatchParticipant, ScoreDevice } from './types'

function participant(id: string, deviceId: string | null, lane: string | null, deviceOrder: number | null = null): MatchParticipant {
  return { id, matchId: 'm', lastName: id, fullName: id, categories: {}, deviceId, deviceLane: lane, deviceOrder, signed: false }
}

const devices: ScoreDevice[] = [
  { id: 'd1', matchId: 'm', name: 'Device 1', sortOrder: 0 },
  { id: 'd2', matchId: 'm', name: 'Device 2', sortOrder: 1 }
]

describe('editorSlot', () => {
  it('accepts lane numbers with a letter A-D only', () => {
    expect(editorSlot('11a')).toEqual({ lane: 11, letter: 'A' })
    expect(editorSlot('3D')).toEqual({ lane: 3, letter: 'D' })
    expect(editorSlot('3E')).toBeNull()
    expect(editorSlot('3')).toBeNull()
    expect(editorSlot('0A')).toBeNull()
    expect(editorSlot(null)).toBeNull()
  })
})

describe('buildLaneEditorState', () => {
  it('puts archers with a device on that device lane, and fills lanes up to the highest', () => {
    const state = buildLaneEditorState([participant('a', 'd1', '11A'), participant('b', null, '12B')], devices)
    expect(state.laneCount).toBe(12)
    expect(state.laneDevice).toEqual({ 11: 'd1', 12: null })
    expect(state.slots).toEqual({ '11A': 'a', '12B': 'b' })
  })

  it('gives a lane to the first device on it; archers of another device on that lane lose their slot', () => {
    const state = buildLaneEditorState([participant('late', 'd2', '11B'), participant('first', 'd1', '11A')], devices)
    expect(state.laneDevice[11]).toBe('d1')
    expect(slotOf(state, 'first')).toEqual({ lane: 11, letter: 'A' })
    expect(slotOf(state, 'late')).toBeNull()
  })

  it('places archers on a device before archers without one', () => {
    const state = buildLaneEditorState([participant('free', null, '5A'), participant('placed', 'd2', '5C')], devices)
    expect(state.laneDevice[5]).toBe('d2')
    expect(slotOf(state, 'free')).toBeNull()
  })

  it('leaves the second archer on an occupied slot unplaced, by device order', () => {
    const state = buildLaneEditorState([participant('second', 'd1', '2A', 1), participant('first', 'd1', '2A', 0)], devices)
    expect(state.slots['2A']).toBe('first')
    expect(slotOf(state, 'second')).toBeNull()
  })

  it('ignores lanes outside A-D and archers without a lane', () => {
    const state = buildLaneEditorState([participant('e', 'd1', '4E'), participant('none', 'd1', null)], devices)
    expect(state).toEqual({ laneCount: 0, laneDevice: {}, slots: {} })
  })
})

describe('deviceParticipantOrder', () => {
  it('orders a device by its lanes and slots, then its archers without a lane', () => {
    const participants = [participant('x', 'd1', null, 0), participant('2a', 'd1', '2A', 1), participant('1c', 'd1', '1C', 2), participant('1a', 'd1', '1A', 3), participant('other', 'd2', '3A')]
    const state = buildLaneEditorState(participants, devices)
    expect(laneParticipantIds(state, 1)).toEqual(['1a', '1c'])
    expect(deviceParticipantOrder(state, participants, 'd1')).toEqual(['1a', '1c', '2a', 'x'])
  })
})
