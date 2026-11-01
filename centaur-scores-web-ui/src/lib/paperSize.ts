// The paper a printable page is laid out for. Pages that compute their own pagination or sizes in mm (results in
// PDF mode, scorecards, the lane sheet) take the sheet's dimensions from here instead of assuming A4. The browser
// is not told the size (no `@page { size }`, which would hide its orientation switch - except on the portrait-only
// lane sheet), so the same paper has to be picked in the browser's print dialog.
//
// The choice belongs to the printer rather than to a match or competition, so it is remembered once for all pages.

export const paperSizes = ['A4', 'A3', 'A5', 'Letter', 'Legal'] as const
export type PaperSize = typeof paperSizes[number]
export type PrintOrientation = 'portrait' | 'landscape'

// Portrait width x height in mm.
const dimensions: Record<PaperSize, { width: number; height: number; label: string }> = {
  A4: { width: 210, height: 297, label: 'A4 (210 × 297 mm)' },
  A3: { width: 297, height: 420, label: 'A3 (297 × 420 mm)' },
  A5: { width: 148, height: 210, label: 'A5 (148 × 210 mm)' },
  Letter: { width: 215.9, height: 279.4, label: 'Letter (8.5 × 11 in)' },
  Legal: { width: 215.9, height: 355.6, label: 'Legal (8.5 × 14 in)' },
}

export const defaultPaperSize: PaperSize = 'A4'
const storageKey = 'centaur-print-paper-size'

export function paperLabel(paper: PaperSize): string {
  return dimensions[paper].label
}

// The area inside the page margins, in mm, for the given orientation.
export function printableArea(paper: PaperSize, orientation: PrintOrientation, marginMm: number): { width: number; height: number } {
  const { width, height } = dimensions[paper]
  const [w, h] = orientation === 'portrait' ? [width, height] : [height, width]
  return { width: w - 2 * marginMm, height: h - 2 * marginMm }
}

export function parsePaperSize(value: unknown): PaperSize {
  return paperSizes.find((paper) => paper === value) ?? defaultPaperSize
}

export function loadPaperSize(): PaperSize {
  try {
    return parsePaperSize(localStorage.getItem(storageKey))
  } catch {
    return defaultPaperSize
  }
}

export function savePaperSize(paper: PaperSize) {
  try {
    localStorage.setItem(storageKey, paper)
  } catch {
    // localStorage unavailable - the choice just won't persist.
  }
}
