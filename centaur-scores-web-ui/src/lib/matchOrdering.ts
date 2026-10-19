import type { MatchListItem } from './types'

export function localToday(): string {
  const now = new Date()
  return new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 10)
}

// The single ordering used by the matches list (and the overview's "coming up" panel, which shows the
// head of `upcoming`): open matches first, then planned matches soonest first, then past matches most
// recent first. `today` is a local YYYY-MM-DD date.
export function partitionMatches<T extends Pick<MatchListItem, 'isOpen' | 'date'>>(matches: T[], today: string): { open: T[]; upcoming: T[]; past: T[] } {
  return {
    open: matches.filter((match) => match.isOpen),
    upcoming: matches.filter((match) => !match.isOpen && match.date.slice(0, 10) >= today).sort((a, b) => a.date.localeCompare(b.date)),
    past: matches.filter((match) => !match.isOpen && match.date.slice(0, 10) < today).sort((a, b) => b.date.localeCompare(a.date))
  }
}
