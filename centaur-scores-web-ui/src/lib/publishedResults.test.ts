import { describe, expect, it } from 'vitest'
import { formatAverage, formatCompetitionTotal, formatRoundScore, normalizeSlug, publishedResultsUrl, PUBLISHED_SLUG_MAX_LENGTH } from './publishedResults'
import type { PublishedCompetitionEntry } from './types'

describe('normalizeSlug', () => {
  it.each([
    ['Club Champs 2026!', 'club-champs-2026'],
    ['  --Één   Twee--  ', 'een-twee'],
    ['a/b_c.d', 'a-b-c-d'],
    ['!!!', ''],
    ['', '']
  ])('normalizes %j to %j (same as the API)', (input, expected) => {
    expect(normalizeSlug(input)).toBe(expected)
  })

  it('truncates without leaving a trailing dash', () => {
    const slug = normalizeSlug(`${'a'.repeat(PUBLISHED_SLUG_MAX_LENGTH - 1)} bcd`)
    expect(slug.length).toBeLessThanOrEqual(PUBLISHED_SLUG_MAX_LENGTH)
    expect(slug.endsWith('-')).toBe(false)
  })
})

describe('published results formatting', () => {
  const entry: PublishedCompetitionEntry = { position: '1', name: 'A', average: 9, total: 22, matchScoreTotal: 540, f1Points: 22, rounds: [] }

  it('builds the public url on the given origin', () => {
    expect(publishedResultsUrl('winter-cup', 'https://scores.example.com')).toBe('https://scores.example.com/results/winter-cup')
  })

  it('shows "-" for a round not shot and f1 points in brackets', () => {
    expect(formatRoundScore(undefined)).toBe('-')
    expect(formatRoundScore({ roundId: 'r', score: null, f1Points: null, counts: false })).toBe('-')
    expect(formatRoundScore({ roundId: 'r', score: 270, f1Points: null, counts: true })).toBe('270')
    expect(formatRoundScore({ roundId: 'r', score: 270, f1Points: 12, counts: true })).toBe('270 (12)')
  })

  it('shows the match score total with f1 points in brackets only for f1 competitions', () => {
    expect(formatCompetitionTotal(entry, true)).toBe('540 (22)')
    expect(formatCompetitionTotal(entry, false)).toBe('22')
  })

  it('formats averages with two decimals per language', () => {
    expect(formatAverage(9, 'en')).toBe('9.00')
    expect(formatAverage(8.5, 'nl')).toBe('8,50')
    expect(formatAverage(null, 'en')).toBe('-')
  })
})
