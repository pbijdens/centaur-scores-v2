import type { View } from './types'

export type NavLink = { path: string; icon: string; title: string; description: string }

// Maps every view to the top-level header item it belongs to: 'home', 'matches', 'competitions', or
// the path of one of the "More" items (the same paths as the overview's quick-link tiles). Views that
// belong to none of them (profile, tenant switcher, standalone pages) map to null.
export function navSectionFor(view: View): string | null {
  if (view === 'home') return 'home'
  if (view === 'matches' || view.startsWith('match')) return 'matches'
  if (view.startsWith('competition')) return 'competitions'
  if (view === 'categories' || view === 'category') return 'categories'
  if (view === 'participants' || view === 'participant-list' || view === 'participant') return 'participants'
  if (view === 'templates' || view === 'template') return 'templates'
  if (view.startsWith('personal-best')) return 'personal-best'
  if (view === 'tenant-settings') return 'tenant-settings'
  if (view === 'accounts' || view === 'account') return 'accounts'
  if (view === 'tenants' || view === 'tenant') return 'tenants'
  if (view === 'backup-restore') return 'backup-restore'
  return null
}
