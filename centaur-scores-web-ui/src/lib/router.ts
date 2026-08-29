import type { View } from './types'

export type Route = { view: View; matchId?: string; tenantId?: string; scope?: string; accountId?: string; categoryId?: string; listId?: string; memberId?: string; templateId?: string; participantId?: string; competitionId?: string; invalid?: boolean }

export function matchPath(matchId: string): string { return `/matches/${matchId}` }
export function matchEditPath(matchId: string): string { return `/matches/${matchId}/edit` }
export function matchDevicesPath(matchId: string): string { return `/matches/${matchId}/devices` }
export function matchQrPath(matchId: string): string { return `/matches/${matchId}/qr` }
export function matchResultsPath(matchId: string, scope: string): string { return `/matches/${matchId}/results/${encodeURIComponent(scope)}` }
export function matchParticipantPath(matchId: string, participantId: string): string { return `/matches/${matchId}/participants/${participantId}` }
export function competitionPath(competitionId: string): string { return `/competitions/${competitionId}` }
export function competitionResultsPath(competitionId: string): string { return `/competitions/${competitionId}/results` }
export function tenantPath(tenantId: string): string { return `/tenants/${tenantId}` }
export function accountPath(accountId: string): string { return `/accounts/${accountId}` }
export function categoryPath(categoryId: string): string { return `/categories/${categoryId}` }
export function participantListPath(listId: string): string { return `/participants/${listId}` }
export function participantMemberPath(listId: string, memberId: string): string { return `/participants/${listId}/members/${memberId}` }
export function templatePath(templateId: string): string { return `/templates/${templateId}` }
export function narrowcastPath(scope: string): string { return `/narrowcast/${encodeURIComponent(scope)}` }

// Lets list-row-style <a href> elements keep native right-click/middle-click/ctrl-click
// "open in new tab" while a plain left click still does client-side SPA navigation.
export function navigateOnClick(event: MouseEvent, action: () => void): void {
  if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return
  event.preventDefault()
  action()
}

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
  if (section === 'login') return { view: 'home' }
  if (section === 'narrowcast') {
    return segments[1] ? { view: 'narrowcast', scope: decodeURIComponent(segments[1]) } : { view: 'home', invalid: true }
  }
  if (section === 'matches') {
    if (!segments[1]) return { view: 'matches' }
    if (segments[2] === 'edit') return { view: 'match-metadata', matchId: segments[1] }
    if (segments[2] === 'devices') return { view: 'match-devices', matchId: segments[1] }
    if (segments[2] === 'qr') return { view: 'match-qr', matchId: segments[1] }
    if (segments[2] === 'results' && segments[3]) return { view: 'match-results-scope', matchId: segments[1], scope: decodeURIComponent(segments[3]) }
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
  if (section === 'personal-best') {
    if (segments[1] === 'classifiers') return { view: 'personal-best-classifiers' }
    if (segments[1] === 'disciplines') return { view: 'personal-best-disciplines' }
    if (segments[1] === 'export-configuration') return { view: 'personal-best-export-config' }
    if (segments[1] === 'import-configuration') return { view: 'personal-best-import-config' }
    if (segments[1] === 'log') return { view: 'personal-best-log' }
    return { view: 'personal-best' }
  }
  return { view: 'home', invalid: true }
}
