import type { View } from './types'
import { isManagementView } from './types'

export type Route = { view: View; matchId?: string; tenantId?: string; accountId?: string; categoryId?: string; listId?: string; memberId?: string; invalid?: boolean }

export function navigateTo(path: string, replace = false) {
  const normalizedPath = path || '/'
  if (replace) history.replaceState({}, '', normalizedPath)
  else if (location.pathname !== normalizedPath) history.pushState({}, '', normalizedPath)
  return resolveRoute(normalizedPath)
}

export function resolveRoute(path = location.pathname): Route {
  const segments = path.split('/').filter(Boolean)
  const section = segments[0]
  if (!section) return { view: 'home' }
  if (section === 'matches') {
    return segments[1] ? { view: 'match', matchId: segments[1] } : { view: 'matches' }
  }
  if (section === 'competitions') return { view: 'competitions' }
  if (section === 'profile') return { view: 'profile' }
  if (section === 'tenants') {
    return segments[1] ? { view: 'tenant', tenantId: segments[1] } : { view: 'tenants' }
  }
  if (section === 'accounts') {
    return segments[1] ? { view: 'account', accountId: segments[1] } : { view: 'accounts' }
  }
  if (section === 'categories') {
    return segments[1] ? { view: 'category', categoryId: segments[1] } : { view: 'categories' }
  }
  if (section === 'participants') {
    if (!segments[1]) return { view: 'participants' }
    if (segments[2] === 'members' && segments[3]) return { view: 'participant', listId: segments[1], memberId: segments[3] }
    return { view: 'participant-list', listId: segments[1] }
  }
  if (isManagementView(section as View)) return { view: section as View }
  return { view: 'home', invalid: true }
}
