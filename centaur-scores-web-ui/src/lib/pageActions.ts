import type { Match } from './types'

// One entry of a page header's action list (see PageHeader.svelte). Every action is listed in the
// page's ⋯ menu; an action with `header` set is additionally shown as a button next to the title, so
// the visible buttons are always the top items of that menu.
export type PageAction = {
  label: string
  // How the action renders next to the title; omit to show it only in the ⋯ menu.
  header?: 'primary' | 'highlight' | 'icon' | 'text'
  // Text for the header button when it differs from the menu label (e.g. an icon glyph).
  headerLabel?: string
  // Highlight buttons render in their "live" colour when active (Activate/Deactivate).
  active?: boolean
  // Navigation target. With `onClick` it is SPA navigation (via navigateOnClick); with `newTab` it
  // always opens in a new tab; on its own it is a plain link.
  href?: string
  newTab?: boolean
  onClick?: () => void
  disabled?: boolean
  danger?: boolean
  // Draws a separator line above this item in the ⋯ menu.
  separatorBefore?: boolean
  // A nested group: a dropdown next to the title, an expandable group in the ⋯ menu.
  children?: PageAction[]
}

// Callbacks App.svelte provides to every match-scoped page so its header can offer the match-level
// actions (activate/deactivate, delete, duplicate, navigate to sibling match pages).
export type MatchHeaderHandlers = {
  onToggleOpen: () => void
  onDeleted: () => void
  onCopied: (copy: Match) => void
  onNavigate: (path: string) => void
}

// Marks the first action of a group so the ⋯ menu draws a separator above it.
export function asGroup(actions: PageAction[]): PageAction[] {
  return actions.map((action, index) => (index === 0 ? { ...action, separatorBefore: true } : action))
}
