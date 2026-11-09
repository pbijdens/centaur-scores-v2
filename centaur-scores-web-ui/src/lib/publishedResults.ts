import { publishedResultsPath } from './router'
import type { PublishedCompetitionEntry, PublishedCompetitionRoundScore } from './types'

// Must stay in sync with the API's PublishedSlugs.Normalize (the API is authoritative; this only previews the link):
// lowercase, diacritics stripped, every run of characters outside [a-z0-9] becomes one dash, no leading/trailing dash.
export const PUBLISHED_SLUG_MAX_LENGTH = 100

export function normalizeSlug(input: string | null | undefined): string {
  if (!input) return ''
  const slug = input
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{Mn}/gu, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
  return slug.length <= PUBLISHED_SLUG_MAX_LENGTH ? slug : slug.slice(0, PUBLISHED_SLUG_MAX_LENGTH).replace(/-+$/, '')
}

// Absolute URL of the public results page for a slug, on this web UI's own origin.
export function publishedResultsUrl(slug: string, origin = typeof window === 'undefined' ? '' : window.location.origin): string {
  return `${origin}${publishedResultsPath(slug)}`
}

// One match score cell: "-" when the archer did not shoot that round, otherwise the score with the f1 points in brackets
// for rounds covered by an f1 rule.
export function formatRoundScore(score: PublishedCompetitionRoundScore | undefined): string {
  if (!score || score.score === null) return '-'
  return score.f1Points === null ? `${score.score}` : `${score.score} (${score.f1Points})`
}

// The competition total: with f1 scoring the total of the counting match scores followed by the f1 points in brackets.
export function formatCompetitionTotal(entry: PublishedCompetitionEntry, usesF1: boolean): string {
  return usesF1 && entry.f1Points !== null ? `${entry.matchScoreTotal} (${entry.f1Points})` : `${entry.total}`
}

export function formatAverage(average: number | null | undefined, language: string): string {
  if (average === null || average === undefined) return '-'
  return average.toLocaleString(language === 'nl' ? 'nl-NL' : 'en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
