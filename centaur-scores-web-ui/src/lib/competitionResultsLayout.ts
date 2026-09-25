import type { CompetitionResultEntry, CompetitionResultGroup } from './types'

// Printed competition results (see ../documentation/COMPETITION-RESULT-SCREEN.md). The report is a stream of equally
// high units - a group header or a participant row - laid out over two columns in one of two modes:
// - 'stream': notice-board mode; the stream is split in half, the left column holds the first half and the right
//   column the second, each running down across as many pages as needed.
// - 'pages': PDF mode; classic two-column pages read left column, right column, next page.
export type ResultsLayoutMode = 'stream' | 'pages'
export type ResultsDetailLines = 2 | 3
export type ResultsLayoutOptions = { mode: ResultsLayoutMode; detailLines: ResultsDetailLines }

export type ResultUnit =
  | { kind: 'header'; name: string }
  | { kind: 'entry'; entry: CompetitionResultEntry }

export type RuleScoreItem = { text: string; struck: boolean; missing: boolean }
export type RuleBlock = { label: string; total: number; scores: RuleScoreItem[] }

export const defaultLayoutOptions: ResultsLayoutOptions = { mode: 'stream', detailLines: 2 }

export function flattenResults(groups: CompetitionResultGroup[]): ResultUnit[] {
  return groups
    .filter((group) => group.entries.length > 0)
    .flatMap((group): ResultUnit[] => [{ kind: 'header', name: group.name }, ...group.entries.map((entry): ResultUnit => ({ kind: 'entry', entry }))])
}

// Splits the stream roughly in half. Every unit is equally high, so a unit count is a height. The left column never
// ends on a header: when the halfway cut falls right after one, that header moves to the right column. The right
// column may start with the tail of a group (an orphan) - that is accepted.
export function splitInTwo(units: ResultUnit[]): [ResultUnit[], ResultUnit[]] {
  let cut = Math.ceil(units.length / 2)
  while (cut > 0 && units[cut - 1].kind === 'header') cut--
  return [units.slice(0, cut), units.slice(cut)]
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

export function parseLayoutOptions(search: string): ResultsLayoutOptions {
  const params = new URLSearchParams(search)
  const mode = params.get('layout')
  const lines = params.get('lines')
  return {
    mode: mode === 'stream' || mode === 'pages' ? mode : defaultLayoutOptions.mode,
    detailLines: lines === '3' ? 3 : lines === '2' ? 2 : defaultLayoutOptions.detailLines,
  }
}

export function layoutOptionsSearch(options: ResultsLayoutOptions): string {
  return `?${new URLSearchParams({ layout: options.mode, lines: String(options.detailLines) }).toString()}`
}
