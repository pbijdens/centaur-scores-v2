import { printableArea, type PaperSize, type PrintOrientation } from './paperSize'
import type { CompetitionResultEntry, CompetitionResultGroup, LiveScoringBlock, LiveScoringEntry } from './types'

// Printed results - competition results (see ../documentation/COMPETITION-RESULT-SCREEN.md) and a match's results per
// scope share one report format. The report is a stream of equally high units - a group header or a participant row -
// laid out over two columns in one of two modes:
// - 'stream': notice-board mode; the stream is split in half, the left column holds the first half and the right
//   column the second, each running down across as many pages as needed.
// - 'pages': PDF mode; classic two-column pages read left column, right column, next page.
export type ResultsLayoutMode = 'stream' | 'pages'
export type ResultsDetailLines = 2 | 3
// Multiplies every printed text size; 1.41 (√2) makes A3 paper look like the A4 layout enlarged.
export const resultsFontScales = [1, 1.2, 1.41, 1.7, 2] as const
export type ResultsFontScale = typeof resultsFontScales[number]
export type ResultsLayoutOptions = { mode: ResultsLayoutMode; detailLines: ResultsDetailLines; fontScale: ResultsFontScale }

export type ResultUnit =
  | { kind: 'header'; name: string }
  | { kind: 'entry'; entry: CompetitionResultEntry }
  | { kind: 'match-entry'; entry: LiveScoringEntry }

export type RuleScoreItem = { text: string; struck: boolean; missing: boolean }
export type RuleBlock = { label: string; total: number; scores: RuleScoreItem[] }

export const defaultLayoutOptions: ResultsLayoutOptions = { mode: 'stream', detailLines: 2, fontScale: 1 }

export function flattenResults(groups: CompetitionResultGroup[]): ResultUnit[] {
  return groups
    .filter((group) => group.entries.length > 0)
    .flatMap((group): ResultUnit[] => [{ kind: 'header', name: group.name }, ...group.entries.map((entry): ResultUnit => ({ kind: 'entry', entry }))])
}

export function flattenMatchResults(blocks: LiveScoringBlock[]): ResultUnit[] {
  return blocks
    .filter((block) => block.entries.length > 0)
    .flatMap((block): ResultUnit[] => [{ kind: 'header', name: block.name }, ...block.entries.map((entry): ResultUnit => ({ kind: 'match-entry', entry }))])
}

// Splits the stream roughly in half. Every unit is equally high, so a unit count is a height. The left column never
// ends on a header: when the halfway cut falls right after one, that header moves to the right column. The right
// column may start with the tail of a group (an orphan) - that is accepted.
export function splitInTwo(units: ResultUnit[]): [ResultUnit[], ResultUnit[]] {
  let cut = Math.ceil(units.length / 2)
  while (cut > 0 && units[cut - 1].kind === 'header') cut--
  return [units.slice(0, cut), units.slice(cut)]
}

// PDF mode lays out its pages explicitly instead of with CSS multi-column: Safari ignores `columns` when printing
// and prints a single column. Every unit has a fixed height (ResultUnit.svelte), so the units per column follow from
// the printable height of the chosen paper (inside the pages' 10mm @page margins) minus the report header repeated
// on every page (ResultsReportHeader.svelte, title clamped to two lines) and a safety margin for rounding.
export const PAGE_MARGIN_MM = 10
const MM_PER_PT = 25.4 / 72
const HEADER_MM = 21.5
const SAFETY_MM = 4

export function unitHeightMm(options: ResultsLayoutOptions): number {
  // name line 11pt + detail lines of 8pt + 2 x 1.5pt padding, all scaled
  return (11 + options.detailLines * 8 + 3) * options.fontScale * MM_PER_PT
}

export function unitsPerColumn(paper: PaperSize, orientation: PrintOrientation, options: ResultsLayoutOptions): number {
  const available = printableArea(paper, orientation, PAGE_MARGIN_MM).height - HEADER_MM * options.fontScale - SAFETY_MM
  return Math.max(2, Math.floor(available / unitHeightMm(options)))
}

// Fills pages of two columns of at most `perColumn` units, left column first. Like splitInTwo, a column never ends on
// a header: such a header moves on to the next column.
export function paginate(units: ResultUnit[], perColumn: number): ResultUnit[][][] {
  const columns: ResultUnit[][] = []
  let start = 0
  while (start < units.length) {
    let end = Math.min(start + perColumn, units.length)
    while (end > start + 1 && end < units.length && units[end - 1].kind === 'header') end--
    columns.push(units.slice(start, end))
    start = end
  }
  const pages: ResultUnit[][][] = []
  for (let i = 0; i < columns.length; i += 2) pages.push(columns.slice(i, i + 2))
  return pages
}

export function rankLabel(entry: CompetitionResultEntry): string {
  if (entry.disqualified || !entry.position || entry.position === '-') return '–'
  return entry.needsTieBreaker ? `${entry.position}*` : entry.position
}

// One block per scoring rule, in rule order, listing the value the rule aggregated for every round it covers:
// a dash for a round without a score, strikethrough for a score the rule did not count.
export function ruleBlocks(entry: CompetitionResultEntry): RuleBlock[] {
  return (entry.rules ?? []).map((rule) => ({
    label: rule.name,
    total: rule.total,
    scores: rule.rounds.map((round) => round.value === null
      ? { text: '–', struck: false, missing: true }
      : { text: String(round.value), struck: !round.used, missing: false }),
  }))
}

// Validates the layout options remembered for a results page's print dialog (see printOptions.ts).
export function parseLayoutOptions(stored: Record<string, unknown> | null): ResultsLayoutOptions {
  const mode = stored?.mode
  const lines = stored?.detailLines
  const scale = resultsFontScales.find((value) => value === stored?.fontScale)
  return {
    mode: mode === 'stream' || mode === 'pages' ? mode : defaultLayoutOptions.mode,
    detailLines: lines === 3 || lines === 2 ? lines : defaultLayoutOptions.detailLines,
    fontScale: scale ?? defaultLayoutOptions.fontScale,
  }
}
