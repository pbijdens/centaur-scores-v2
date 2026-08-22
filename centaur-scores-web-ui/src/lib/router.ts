import type { View } from './types'

export type Route = { view: View; matchId?: string; tenantId?: string; scope?: string; accountId?: string; categoryId?: string; listId?: string; memberId?: string; templateId?: string; participantId?: string; competitionId?: string; invalid?: boolean }

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
  if (section === 'narrowcast') {
    return segments[1] && segments[2] ? { view: 'narrowcast', tenantId: segments[1], scope: segments[2] } : { view: 'home', invalid: true }
  }
  if (section === 'matches') {
    if (!segments[1]) return { view: 'matches' }
    if (segments[2] === 'edit') return { view: 'match-metadata', matchId: segments[1] }
    if (segments[2] === 'devices') return { view: 'match-devices', matchId: segments[1] }
    if (segments[2] === 'qr') return { view: 'match-qr', matchId: segments[1] }
    if (segments[2] === 'participants' && segments[3]) return { view: 'match-participant', matchId: segments[1], participantId: segments[3] }
    return { view: 'match', matchId: segments[1] }
  }
  if (section === 'competitions') {
    if (!segments[1]) return { view: 'competitions' }
    if (segments[2] === 'results') return { view: 'competition-results', competitionId: segments[1] }
    return { view: 'competition', competitionId: segments[1] }
  }
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
  if (section === 'templates') {
    return segments[1] ? { view: 'template', templateId: segments[1] } : { view: 'templates' }
  }
  return { view: 'home', invalid: true }
}
