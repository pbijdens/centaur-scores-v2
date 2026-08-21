export type View = 'home' | 'matches' | 'match' | 'competitions' | 'participants' | 'participant-list' | 'participant' | 'categories' | 'category' | 'templates' | 'template' | 'accounts' | 'account' | 'profile' | 'tenants' | 'tenant'
export type Language = 'en' | 'nl'
export type Tenant = { id: string; name: string; logoUrl?: string | null }
export type Match = { id: string; name: string; date: string; isOpen: boolean; participantCount?: number; participants?: unknown[] }
export type Competition = { id: string; name: string; startDate: string; endDate: string; rounds?: unknown[]; roundCount?: number }
export type Profile = { id: string; username: string; displayName?: string | null; email?: string | null; authorization: string }
export type Account = { id: string; username: string; displayName?: string | null; email?: string | null; authorization: string }
export type CategoryValue = { categoryId: string; valueId: number; name: string }
export type Category = { id: string; name: string; isUsed: boolean; values: CategoryValue[] }
export type ParticipantListMember = { id: string; participantListId: string; lastName: string; fullName: string; federationNumber?: string | null; categories: Record<string, number>; isActive: boolean }
export type ParticipantList = { id: string; name: string; isActive: boolean; members: ParticipantListMember[] }
export type MatchTemplate = { id: string; name: string; participantListId?: string | null; participantSelectionMode: string; configurationJson: string }

export type KeyboardKeyColor = 'Yellow' | 'Red' | 'Blue' | 'Black' | 'White'
// The 'delete/wipe' key is represented by keyId/color/value all null.
export type KeyboardKey = { keyId: string | null; label: string; color: KeyboardKeyColor | null; value: number | null }
export type DisabledKeyRule = { categoryId: string; valueId: number; disabledKeyIds: string[] }
export type ScoringRule = { type: 'total' | 'countKey'; keyId?: string }
export type LiveScopeConfig = { scope: string; groupByCategoryIds: string[]; includeAverage: boolean; includeGroupScores: boolean; includeEqualizers: boolean; includePersonalBest: boolean }
export type TemplateConfiguration = {
  categoryOrder: string[]
  keyboard: KeyboardKey[]
  disabledKeyRules: DisabledKeyRule[]
  scoringRules: ScoringRule[]
  liveScopes: LiveScopeConfig[]
}

