export type View = 'home' | 'matches' | 'match' | 'competitions' | 'participants' | 'categories' | 'templates' | 'accounts' | 'profile'
export type Language = 'en' | 'nl'
export type Tenant = { id: string; name: string; logoUrl?: string }
export type Match = { id: string; name: string; date: string; isOpen: boolean; participantCount?: number; participants?: unknown[] }
export type Competition = { id: string; name: string; startDate: string; endDate: string; rounds?: unknown[]; roundCount?: number }
export type NamedItem = { id: string; name: string; description: string; status?: string }
export type Profile = { id: string; username: string; displayName?: string | null; email?: string | null; authorization: string }

export type ManagementView = 'participants' | 'categories' | 'templates' | 'accounts'
export const managementSections: ManagementView[] = ['participants', 'categories', 'templates', 'accounts']

export function isManagementView(view: View): view is ManagementView {
  return (managementSections as View[]).includes(view)
}
