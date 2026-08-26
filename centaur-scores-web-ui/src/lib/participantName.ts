export function deriveLastName(fullName: string): string {
  const trimmed = fullName.trim()
  if (!trimmed) return ''
  const parts = trimmed.split(/\s+/)
  return parts[parts.length - 1]
}
