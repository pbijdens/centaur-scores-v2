import type { NamedItem } from './types'

export function managementItems(kind: string): NamedItem[] {
  if (kind === 'participants') return [{ id: 'participants', name: 'Participant lists', description: 'Active and inactive lists with member data', status: 'ACTIVE' }]
  if (kind === 'categories') return [{ id: 'categories', name: 'Match categories', description: 'Values used to group participants and results', status: 'READY' }]
  if (kind === 'templates') return [{ id: 'templates', name: 'Match templates', description: 'Reusable match configuration and scoring keyboards', status: 'READY' }]
  return [{ id: 'accounts', name: 'Tenant accounts', description: 'Users, roles and delegated access', status: 'ADMIN' }]
}
