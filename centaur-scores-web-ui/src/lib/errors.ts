import { ApiRequestError } from './api'

// Maps a coded API error to a translated label, falling back to a generic message for unknown/network errors.
export function labelForError(error: unknown, labels: Record<string, string>, fallbackKey: string): string {
  if (error instanceof ApiRequestError && error.code) {
    const key = `error_${error.code}`
    if (labels[key]) return labels[key]
  }
  return labels[fallbackKey]
}
