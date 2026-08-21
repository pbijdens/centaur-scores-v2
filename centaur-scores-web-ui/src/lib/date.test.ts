import { describe, expect, it } from 'vitest'
import { formatLocalDate } from './date'

describe('formatLocalDate', () => {
  it('keeps date-only values on the selected calendar date', () => {
    expect(formatLocalDate('2026-08-21', 'en')).toContain('21')
    expect(formatLocalDate('2026-08-21', 'nl')).toContain('21')
  })
})
