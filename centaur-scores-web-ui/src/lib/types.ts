export type View = 'home' | 'matches' | 'match' | 'match-metadata' | 'match-devices' | 'match-qr' | 'match-participant' | 'competitions' | 'participants' | 'participant-list' | 'participant' | 'categories' | 'category' | 'templates' | 'template' | 'accounts' | 'account' | 'profile' | 'tenants' | 'tenant' | 'narrowcast'
export type Language = 'en' | 'nl'
export type Tenant = { id: string; name: string; logoUrl?: string | null }
export type Match = {
  id: string
  name: string
  date: string
  shortCode?: string | null
  isOpen: boolean
  participantListId?: string | null
  deviceSelectionMode: string
  ends: number
  arrowsPerEnd: number
  groupEnds?: number | null
  allowFreeParticipants: boolean
  keyboardJson: string
  scoringRulesJson: string
  participantCount?: number
  participants?: MatchParticipant[]
  devices?: ScoreDevice[]
  liveScopes?: LiveScoreScope[]
}
export type MatchParticipant = {
  id: string
  matchId: string
  participantListMemberId?: string | null
  lastName: string
  fullName: string
  federationNumber?: string | null
  categories: Record<string, number>
  deviceId?: string | null
  deviceOrder?: number | null
  scores?: ArrowScore[]
}
export type ArrowScore = { id: string; matchParticipantId: string; end: number; arrow: number; keyId: string; value: number }
export type ScoreDevice = { id: string; matchId: string; name: string; sortOrder?: number }
export type LiveScoreScope = { id: string; matchId: string; scope: string; groupByCategoryIdsJson: string; includeAverage: boolean; includeGroupScores: boolean; includeEqualizers: boolean; includePersonalBest: boolean }
export type LiveScoringMatch = { id: string; date: string; name: string }
export type LiveScoringEntry = { position: number; needsTieBreaker: boolean; line1: string; line2?: string | null; line3?: string | null; average?: number | null; arrows: number; score: number }
export type LiveScoringBlock = { name: string; entries: LiveScoringEntry[] }
export type LiveScoringPage = { timeout: number; logo?: string | null; tenant: string; matchName: string; matchDate: string; blocks: LiveScoringBlock[] }
export type Competition = { id: string; name: string; startDate: string; endDate: string; rounds?: unknown[]; roundCount?: number }
export type Profile = { id: string; username: string; displayName?: string | null; email?: string | null; authorization: string }
export type Account = { id: string; username: string; displayName?: string | null; email?: string | null; authorization: string }
export type CategoryValue = { categoryId: string; valueId: number; name: string }
export type Category = { id: string; name: string; isUsed: boolean; values: CategoryValue[] }
export type ParticipantListMember = { id: string; participantListId: string; lastName: string; fullName: string; federationNumber?: string | null; categories: Record<string, number>; isActive: boolean }
export type ParticipantList = { id: string; name: string; isActive: boolean; members: ParticipantListMember[] }
export type MatchTemplate = { id: string; name: string; participantListId?: string | null; allowFreeParticipants: boolean; deviceSelectionMode: string; configurationJson: string }

export type KeyboardKeyColor = 'Yellow' | 'Red' | 'Blue' | 'Black' | 'White'
export type KeyboardKey = { keyId: string; label: string; color: KeyboardKeyColor; value: number }
export type DisabledKeyRule = { categoryId: string; valueId: number; disabledKeyIds: string[] }
export type ScoringRule = { type: 'total' | 'countKey'; keyId?: string }
export type LiveScopeConfig = { scope: string; groupByCategoryIds: string[]; includeAverage: boolean; includeGroupScores: boolean; includeEqualizers: boolean; includePersonalBest: boolean }
export type TemplateConfiguration = {
  categoryOrder: string[]
  deviceNames: string[]
  keyboard: KeyboardKey[]
  disabledKeyRules: DisabledKeyRule[]
  scoringRules: ScoringRule[]
  liveScopes: LiveScopeConfig[]
}

