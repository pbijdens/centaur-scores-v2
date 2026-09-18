import type { LiveScoringPage } from './types'

// The total number of arrows a full match consists of.
export function totalArrows(page: LiveScoringPage): number {
  const arrowsPerEnd = page.arrowsPerEnd > 0 ? page.arrowsPerEnd : 1
  return page.ends * arrowsPerEnd
}

// A single archer can have fewer (not yet caught up) or more (input mistakes) arrows than the rest,
// so the number of arrows the majority of archers have shot is the representative match progress.
export function typicalArrowsShot(page: LiveScoringPage): number {
  const counts = page.blocks.flatMap((block) => block.entries).map((entry) => entry.arrows)
  if (counts.length === 0) return 0

  const frequency = new Map<number, number>()
  for (const count of counts) frequency.set(count, (frequency.get(count) ?? 0) + 1)

  let typical = counts[0]
  let bestFrequency = 0
  for (const [count, freq] of frequency) {
    if (freq > bestFrequency || (freq === bestFrequency && count > typical)) {
      typical = count
      bestFrequency = freq
    }
  }
  return Math.min(typical, totalArrows(page))
}

// Percent-of-track positions of the group boundaries, for drawing a multi-part progress bar.
// Empty when there's no group configured, or it spans the whole match (nothing to segment).
export function groupBoundaryPercents(page: LiveScoringPage): number[] {
  if (!page.groupEnds || page.groupEnds <= 0 || page.groupEnds >= page.ends || page.ends <= 0) return []
  const boundaries: number[] = []
  for (let end = page.groupEnds; end < page.ends; end += page.groupEnds) {
    boundaries.push((end / page.ends) * 100)
  }
  return boundaries
}
