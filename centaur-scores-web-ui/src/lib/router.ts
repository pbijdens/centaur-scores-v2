import type { View } from './types'
import { isManagementView } from './types'

export type Route = { view: View; matchId?: string; invalid?: boolean }

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
  if (isManagementView(section as View)) return { view: section as View }
  return { view: 'home', invalid: true }
}
