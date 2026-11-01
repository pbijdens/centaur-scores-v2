import { describe, expect, it } from 'vitest'
import { parsePaperSize, printableArea } from './paperSize'

describe('printableArea', () => {
  it('subtracts the margins and swaps the sides in landscape', () => {
    expect(printableArea('A4', 'portrait', 10)).toEqual({ width: 190, height: 277 })
    expect(printableArea('A4', 'landscape', 10)).toEqual({ width: 277, height: 190 })
    expect(printableArea('A3', 'portrait', 6)).toEqual({ width: 285, height: 408 })
  })
})

describe('parsePaperSize', () => {
  it('accepts known sizes and falls back to A4', () => {
    expect(parsePaperSize('Letter')).toBe('Letter')
    expect(parsePaperSize('B5')).toBe('A4')
    expect(parsePaperSize(null)).toBe('A4')
  })
})
