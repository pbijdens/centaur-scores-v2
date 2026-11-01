import { parseLane } from './laneSheet'
import type { MatchParticipant, ScoreDevice } from './types'

// The quick lane editor (MatchLaneEditorView): archers are dropped onto the four slots (A-D) of numbered lanes,
// and lanes onto devices. Nothing about lanes is stored on the server except each archer's own `deviceLane`
// ("11A") and `deviceId`: a lane belongs to a device because its archers do. This module rebuilds the editor
// from those fields and computes the per-device archer order after a change.

export const LANE_LETTERS = ['A', 'B', 'C', 'D'] as const
export type LaneLetter = typeof LANE_LETTERS[number]

export type LaneEditorState = {
  // Lanes 1..laneCount exist; a lane without an entry in laneDevice is not on a device.
  laneCount: number
  laneDevice: Record<number, string | null>
  // "11A" -> participant id.
  slots: Record<string, string>
}

export function slotKey(lane: number, letter: string): string {
  return `${lane}${letter}`
}

// A lane slot an archer can sit on: a lane number of 1 or more and a letter A-D.
export function editorSlot(lane: string | null | undefined): { lane: number; letter: LaneLetter } | null {
  const { number, letter } = parseLane(lane)
  if (number === null || number < 1 || !letter || !(LANE_LETTERS as readonly string[]).includes(letter)) return null
  return { lane: number, letter: letter as LaneLetter }
}

// Places every archer whose lane is a valid slot. Archers on a device are placed first (in device order, then
// their order on the device), so a lane goes to the first device found on it. An archer loses their slot (and
// stays unplaced) when the slot is taken, or when the lane already belongs to another device - or, for an
// archer without a device, belongs to a device at all. Lanes 1 up to the highest lane in use all exist.
export function buildLaneEditorState(participants: MatchParticipant[], devices: ScoreDevice[]): LaneEditorState {
  const deviceIndex = new Map(devices.map((device, index) => [device.id, index]))
  const rank = (participant: MatchParticipant) => (participant.deviceId ? deviceIndex.get(participant.deviceId) ?? devices.length : devices.length + 1)
  const ordered = [...participants].sort((a, b) =>
    rank(a) - rank(b) ||
    (a.deviceOrder ?? Number.MAX_SAFE_INTEGER) - (b.deviceOrder ?? Number.MAX_SAFE_INTEGER) ||
    (a.fullName || a.lastName).localeCompare(b.fullName || b.lastName))

  const state: LaneEditorState = { laneCount: 0, laneDevice: {}, slots: {} }
  for (const participant of ordered) {
    const slot = editorSlot(participant.deviceLane)
    if (!slot) continue
    const deviceId = participant.deviceId && deviceIndex.has(participant.deviceId) ? participant.deviceId : null
    const key = slotKey(slot.lane, slot.letter)
    if (state.slots[key]) continue
    if (slot.lane in state.laneDevice && state.laneDevice[slot.lane] !== deviceId) continue
    state.laneDevice[slot.lane] = deviceId
    state.slots[key] = participant.id
    state.laneCount = Math.max(state.laneCount, slot.lane)
  }
  return state
}

export function deviceOfLane(state: LaneEditorState, lane: number): string | null {
  return state.laneDevice[lane] ?? null
}

// The slot an archer sits on, if any.
export function slotOf(state: LaneEditorState, participantId: string): { lane: number; letter: LaneLetter } | null {
  for (const [key, id] of Object.entries(state.slots)) {
    if (id === participantId) return editorSlot(key)
  }
  return null
}

// The archers on a lane, by slot.
export function laneParticipantIds(state: LaneEditorState, lane: number): string[] {
  return LANE_LETTERS.map((letter) => state.slots[slotKey(lane, letter)]).filter((id): id is string => !!id)
}

// The order of a device's archers: those on its lanes by lane and slot, then the device's other archers
// (no valid lane) in their current order. Every archer assigned to the device is included, as the API requires.
export function deviceParticipantOrder(state: LaneEditorState, participants: MatchParticipant[], deviceId: string): string[] {
  const onDevice = participants.filter((participant) => participant.deviceId === deviceId)
  const ids = new Set(onDevice.map((participant) => participant.id))
  const placed: string[] = []
  for (let lane = 1; lane <= state.laneCount; lane++) {
    if (deviceOfLane(state, lane) !== deviceId) continue
    for (const id of laneParticipantIds(state, lane)) if (ids.has(id)) placed.push(id)
  }
  const rest = onDevice
    .filter((participant) => !placed.includes(participant.id))
    .sort((a, b) => (a.deviceOrder ?? Number.MAX_SAFE_INTEGER) - (b.deviceOrder ?? Number.MAX_SAFE_INTEGER))
    .map((participant) => participant.id)
  return [...placed, ...rest]
}
