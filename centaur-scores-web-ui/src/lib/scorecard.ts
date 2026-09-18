import type { ArrowScore, Match } from './types'

// Shared scoring math for anything that renders a participant's arrows/ends - the compact and full
// scorecards, and the match-participant summary stats. Mirrors how the sibling
// centaur-scores-mobile-web-scoring app centralizes the identical math in its own lib/scoring.ts.

function sumBetweenEnds(scores: ArrowScore[], firstEnd: number, lastEnd: number): number {
  return scores.filter((score) => score.end >= firstEnd && score.end <= lastEnd).reduce((sum, score) => sum + score.value, 0)
}

function effectiveGroupEnds(match: Match): number {
  return match.groupEnds && match.groupEnds > 0 ? match.groupEnds : match.ends
}

export function scoreFor(scores: ArrowScore[] | undefined, end: number, arrow: number): ArrowScore | undefined {
  return (scores ?? []).find((score) => score.end === end && score.arrow === arrow)
}

export function totalScore(scores: ArrowScore[] | undefined): number {
  return (scores ?? []).reduce((sum, score) => sum + score.value, 0)
}

export function arrowsShotCount(scores: ArrowScore[] | undefined): number {
  return (scores ?? []).length
}

export function endTotal(scores: ArrowScore[] | undefined, end: number): number {
  return sumBetweenEnds(scores ?? [], end, end)
}

export function runningTotal(scores: ArrowScore[] | undefined, end: number): number {
  return sumBetweenEnds(scores ?? [], 1, end)
}

export function groupRunningTotal(match: Match, scores: ArrowScore[] | undefined, end: number): number {
  const groupEnds = effectiveGroupEnds(match)
  const firstEnd = Math.floor((end - 1) / groupEnds) * groupEnds + 1
  return sumBetweenEnds(scores ?? [], firstEnd, end)
}

// True when this match actually splits into more than one group of ends - i.e. there's something to segment.
export function hasGroups(match: Match): boolean {
  return !!match.groupEnds && match.groupEnds > 0 && match.groupEnds < match.ends
}

// True for the last end of a completed group (not the match's very last end) - used to draw a divider.
export function isGroupDivider(match: Match, end: number): boolean {
  return hasGroups(match) && end % (match.groupEnds as number) === 0 && end < match.ends
}

// One subtotal per group-of-ends block (final if the group is complete, partial if still in progress).
export function groupTotals(match: Match, scores: ArrowScore[] | undefined): number[] {
  const groupEnds = effectiveGroupEnds(match)
  const totals: number[] = []
  for (let groupStart = 1; groupStart <= match.ends; groupStart += groupEnds) {
    const groupEnd = Math.min(groupStart + groupEnds - 1, match.ends)
    totals.push(sumBetweenEnds(scores ?? [], groupStart, groupEnd))
  }
  return totals
}
