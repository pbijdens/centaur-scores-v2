import type { Category, MatchParticipant, ScoreDevice } from './types'

// Lane-assignment helpers for MatchDevicesView: parsing the free-form lane annotation ("1A", "12b"),
// the two sanity checks (one target configuration per lane number, more than one club per device), the
// category-value tag colours, and the printable lane sheet layout (blocks of fixed-height rows, paginated by row).

export type ParsedLane = { number: number | null; letter: string | null }

// "1A" -> { 1, 'A' }, "12" -> { 12, null }; anything else (blank, "X", "B1") has no lane number.
export function parseLane(lane: string | null | undefined): ParsedLane {
  const match = /^\s*(\d+)\s*([A-Za-z]?)\s*$/.exec(lane ?? '')
  if (!match) return { number: null, letter: null }
  return { number: Number(match[1]), letter: match[2] ? match[2].toUpperCase() : null }
}

export type TagColor = { background: string; foreground: string }

// Saturated, mutually distinguishable backgrounds for category-value tags.
const tagPalette = ['#1f77b4', '#ff7f0e', '#2ca02c', '#d62728', '#9467bd', '#8c564b', '#e377c2', '#7f7f7f', '#bcbd22', '#17becf', '#393b79', '#f2c14e']
// Light backgrounds for lane target configurations, so the tags drawn on top stay readable.
const configurationPalette = ['#dbeafe', '#fde68a', '#bbf7d0', '#fecaca', '#e9d5ff', '#fed7aa', '#a5f3fc', '#f5d0fe', '#d9f99d', '#e7e5e4']

// Black or white, whichever contrasts more with the given #rrggbb background (WCAG relative luminance).
export function contrastingForeground(background: string): string {
  const channels = [1, 3, 5].map((offset) => parseInt(background.slice(offset, offset + 2), 16) / 255)
    .map((value) => (value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4))
  const luminance = 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2]
  return luminance > 0.179 ? '#111111' : '#ffffff'
}

// Stable per value: the value's position in its category (ordered by valueId) picks the colour; `offset`
// shifts the palette so two categories shown side by side don't start with the same colours.
export function tagColor(category: Category, valueId: number, offset = 0): TagColor {
  const index = [...category.values].sort((a, b) => a.valueId - b.valueId).findIndex((value) => value.valueId === valueId)
  const background = tagPalette[(Math.max(index, 0) + offset) % tagPalette.length]
  return { background, foreground: contrastingForeground(background) }
}

export function configurationColor(index: number): string {
  return configurationPalette[index % configurationPalette.length]
}

export function valueName(category: Category, valueId: number | undefined): string | null {
  if (valueId === undefined) return null
  return category.values.find((value) => value.valueId === valueId)?.name ?? null
}

// The distinct values of `categoryId` among the participants, ordered by valueId; participants without
// a value count as `null` (listed last) so a lane missing a value is not mistaken for a uniform one.
export function distinctValues(participants: MatchParticipant[], categoryId: string): (number | null)[] {
  const values = new Set(participants.map((participant) => participant.categories[categoryId] ?? null))
  return [...values].sort((a, b) => (a ?? Number.MAX_SAFE_INTEGER) - (b ?? Number.MAX_SAFE_INTEGER))
}

export function configurationKey(values: (number | null)[]): string {
  return values.map((value) => value ?? '-').join('|')
}

// Lane numbers (match-wide, across devices) whose archers don't all share one value of `categoryId`.
export function mixedLaneNumbers(participants: MatchParticipant[], categoryId: string): Set<number> {
  const byLane = new Map<number, MatchParticipant[]>()
  for (const participant of participants) {
    const { number } = parseLane(participant.deviceLane)
    if (number === null || !participant.deviceId) continue
    byLane.set(number, [...(byLane.get(number) ?? []), participant])
  }
  return new Set([...byLane].filter(([, laneParticipants]) => distinctValues(laneParticipants, categoryId).length > 1).map(([number]) => number))
}

// The single shared value when a device has two or more archers who all have the same value of
// `categoryId` (e.g. everyone from one club), otherwise undefined.
export function uniformDeviceValue(deviceParticipants: MatchParticipant[], categoryId: string): number | undefined {
  if (deviceParticipants.length < 2) return undefined
  const values = distinctValues(deviceParticipants, categoryId)
  return values.length === 1 && values[0] !== null ? values[0] : undefined
}

export type LaneSheetRow = { slot: string; participant: MatchParticipant | null }
export type LaneSheetBlock = {
  kind: 'lane' | 'empty' | 'noLane' | 'noDevice'
  deviceId: string | null
  deviceName: string | null
  laneNumber: number | null
  participants: MatchParticipant[]
  rows: LaneSheetRow[]
}
// A block, or the part of it that fits on a page: a block taller than a whole page continues on the next one.
export type LaneSheetSegment = { block: LaneSheetBlock; rows: LaneSheetRow[]; continued: boolean }
export type LaneSheetPage = { segments: LaneSheetSegment[] }

const slotLetter = (index: number) => String.fromCharCode(65 + index)
// Every block on the lane sheet has at least this many rows (a target holds four archers, A-D), so blocks
// have a predictable height; spare rows stay empty for late entries.
export const MIN_LANE_ROWS = 4

function padRows(rows: LaneSheetRow[]): LaneSheetRow[] {
  return [...rows, ...Array.from({ length: Math.max(0, MIN_LANE_ROWS - rows.length) }, () => ({ slot: '', participant: null }))]
}

// Places archers on their lettered slot (A = 0); archers without a letter, or whose slot is taken, fill
// the first free slot. Returns the slots indexed by position (holes are undefined).
function slotParticipants(participants: MatchParticipant[]): (MatchParticipant | undefined)[] {
  const slots: (MatchParticipant | undefined)[] = []
  const unplaced: MatchParticipant[] = []
  for (const participant of participants) {
    const { letter } = parseLane(participant.deviceLane)
    const index = letter ? letter.charCodeAt(0) - 65 : -1
    if (index >= 0 && !slots[index]) slots[index] = participant
    else unplaced.push(participant)
  }
  for (const participant of unplaced) {
    let index = 0
    while (slots[index]) index++
    slots[index] = participant
  }
  return slots
}

// Builds the printable lane sheet: per device (in device order) one block per lane number, then one
// block for its archers without a lane number; lane numbers missing between the lowest and highest
// get an empty block; archers without a device come last. Every numbered lane block gets as many rows
// as the fullest lane needs, at least MIN_LANE_ROWS, so blocks line up and have the same height (see
// paginateLaneSheet); the other blocks are padded to MIN_LANE_ROWS too.
export function buildLaneSheet(devices: ScoreDevice[], participants: MatchParticipant[]): { blocks: LaneSheetBlock[]; slotCount: number } {
  const blocks: LaneSheetBlock[] = []
  const laneSlots = new Map<LaneSheetBlock, (MatchParticipant | undefined)[]>()

  for (const device of devices) {
    const assigned = participants.filter((participant) => participant.deviceId === device.id)
    const byNumber = new Map<number, MatchParticipant[]>()
    const withoutNumber: MatchParticipant[] = []
    for (const participant of assigned) {
      const { number } = parseLane(participant.deviceLane)
      if (number === null) withoutNumber.push(participant)
      else byNumber.set(number, [...(byNumber.get(number) ?? []), participant])
    }
    for (const number of [...byNumber.keys()].sort((a, b) => a - b)) {
      const block: LaneSheetBlock = { kind: 'lane', deviceId: device.id, deviceName: device.name, laneNumber: number, participants: byNumber.get(number)!, rows: [] }
      laneSlots.set(block, slotParticipants(block.participants))
      blocks.push(block)
    }
    if (withoutNumber.length > 0) {
      blocks.push({ kind: 'noLane', deviceId: device.id, deviceName: device.name, laneNumber: null, participants: withoutNumber, rows: padRows(withoutNumber.map((participant) => ({ slot: participant.deviceLane ?? '', participant }))) })
    }
  }

  // Fill gaps in the lane numbering with empty blocks, each placed right after the last block holding a
  // lower lane number (so it lands within the device that holds its neighbour).
  const numbers = blocks.filter((block) => block.kind === 'lane').map((block) => block.laneNumber!)
  if (numbers.length > 0) {
    const present = new Set(numbers)
    for (let number = Math.min(...numbers) + 1; number < Math.max(...numbers); number++) {
      if (present.has(number)) continue
      let insertAt = 0
      blocks.forEach((block, index) => {
        if ((block.kind === 'lane' || block.kind === 'empty') && block.laneNumber! < number) insertAt = index + 1
      })
      const previous = blocks[insertAt - 1]
      blocks.splice(insertAt, 0, { kind: 'empty', deviceId: previous?.deviceId ?? null, deviceName: previous?.deviceName ?? null, laneNumber: number, participants: [], rows: [] })
    }
  }

  const slotCount = Math.max(MIN_LANE_ROWS, ...[...laneSlots.values()].map((slots) => slots.length))
  for (const block of blocks) {
    if (block.kind !== 'lane' && block.kind !== 'empty') continue
    const slots = laneSlots.get(block) ?? []
    block.rows = Array.from({ length: slotCount }, (_, index) => ({ slot: `${block.laneNumber}${slotLetter(index)}`, participant: slots[index] ?? null }))
  }

  const unassigned = participants.filter((participant) => !participant.deviceId)
  if (unassigned.length > 0) {
    blocks.push({ kind: 'noDevice', deviceId: null, deviceName: null, laneNumber: null, participants: unassigned, rows: padRows(unassigned.map((participant) => ({ slot: '', participant }))) })
  }

  return { blocks, slotCount }
}

// Fills pages of at most `rowsPerPage` rows (every row has the same printed height). A block never splits
// across pages unless it is taller than a whole page (a long list of archers without a lane number or
// device); such a block fills the rest of the page and continues on the next.
export function paginateLaneSheet(blocks: LaneSheetBlock[], rowsPerPage: number): LaneSheetPage[] {
  const capacity = Math.max(1, rowsPerPage)
  const pages: LaneSheetPage[] = []
  let page: LaneSheetPage = { segments: [] }
  let free = capacity
  const newPage = () => {
    if (page.segments.length > 0) pages.push(page)
    page = { segments: [] }
    free = capacity
  }
  for (const block of blocks) {
    const rows = block.rows.length > 0 ? block.rows : [{ slot: '', participant: null }]
    if (rows.length > free && rows.length <= capacity) newPage()
    let start = 0
    while (start < rows.length) {
      if (free === 0) newPage()
      const end = Math.min(rows.length, start + free)
      page.segments.push({ block, rows: rows.slice(start, end), continued: start > 0 })
      free -= end - start
      start = end
    }
  }
  newPage()
  return pages
}
