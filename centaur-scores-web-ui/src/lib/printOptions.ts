// Remembers the options of a page's print dialog per printed entity (match, competition, ...) in
// localStorage, so the next print of the same thing starts from the previous answers. A per-browser
// convenience, not shared data - hence not stored through the API.

export function printOptionsKey(kind: string, id: string): string {
  return `centaur-print-${kind}-${id}`
}

// Returns whatever was stored (parsed JSON), or null when nothing usable is stored; `parse` turns that
// into a complete, validated options object.
export function loadPrintOptions<T>(key: string, parse: (stored: Record<string, unknown> | null) => T): T {
  try {
    const raw = localStorage.getItem(key)
    const stored = raw ? JSON.parse(raw) : null
    return parse(stored && typeof stored === 'object' && !Array.isArray(stored) ? stored : null)
  } catch {
    return parse(null)
  }
}

export function savePrintOptions<T>(key: string, options: T) {
  try {
    localStorage.setItem(key, JSON.stringify(options))
  } catch {
    // localStorage unavailable (private browsing, quota) - the options just won't persist.
  }
}
