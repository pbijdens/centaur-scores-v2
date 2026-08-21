export type View = 'home' | 'matches' | 'match' | 'competitions' | 'participants' | 'participant-list' | 'participant' | 'categories' | 'category' | 'templates' | 'accounts' | 'account' | 'profile' | 'tenants' | 'tenant'
export type Language = 'en' | 'nl'
export type Tenant = { id: string; name: string; logoUrl?: string | null }
export type Match = { id: string; name: string; date: string; isOpen: boolean; participantCount?: number; participants?: unknown[] }
export type Competition = { id: string; name: string; startDate: string; endDate: string; rounds?: unknown[]; roundCount?: number }
export type NamedItem = { id: string; name: string; description: string; status?: string }
export type Profile = { id: string; username: string; displayName?: string | null; email?: string | null; authorization: string }
export type Account = { id: string; username: string; displayName?: string | null; email?: string | null; authorization: string }
export type CategoryValue = { categoryId: string; valueId: number; name: string }
export type Category = { id: string; name: string; isUsed: boolean; values: CategoryValue[] }
export type ParticipantListMember = { id: string; participantListId: string; lastName: string; fullName: string; federationNumber?: string | null; categories: Record<string, number>; isActive: boolean }
export type ParticipantList = { id: string; name: string; isActive: boolean; members: ParticipantListMember[] }

export type ManagementView = 'templates'
export const managementSections: ManagementView[] = ['templates']

export function isManagementView(view: View): view is ManagementView {
  return (managementSections as View[]).includes(view)
}
