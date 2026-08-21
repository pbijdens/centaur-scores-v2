import type { NamedItem } from './types'

export function managementItems(kind: string): NamedItem[] {
  if (kind === 'templates') return [{ id: 'templates', name: 'Match templates', description: 'Reusable match configuration and scoring keyboards', status: 'READY' }]
  return []
}
