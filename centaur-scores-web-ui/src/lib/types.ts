export type View = 'home' | 'matches' | 'match' | 'match-metadata' | 'match-devices' | 'match-qr' | 'match-results-scope' | 'match-participant' | 'competitions' | 'competition' | 'competition-results' | 'participants' | 'participant-list' | 'participant' | 'categories' | 'category' | 'templates' | 'template' | 'accounts' | 'account' | 'profile' | 'tenants' | 'tenant' | 'narrowcast'
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
export type CompetitionRoundMatch = { id: string; competitionRoundId: string; matchId: string }
export type CompetitionRound = { id: string; competitionId: string; order: number; shortName: string; longName: string; matches?: CompetitionRoundMatch[] }
export type CompetitionScoreRule = { id: string; competitionId: string; name: string; roundIdsJson: string; highestScores: number; minimumScores: number; aggregation: 'total' | 'f1'; sortOrder?: number }
export type Competition = { id: string; name: string; startDate: string; endDate: string; groupByCategoryIdsJson: string; rounds?: CompetitionRound[]; scoringRules?: CompetitionScoreRule[] }
export type CompetitionResultScore = { value: number; used: boolean }
export type CompetitionResultEntry = { position: string | null; needsTieBreaker: boolean; name: string; disqualified: boolean; total: number | null; roundScores: Record<string, CompetitionResultScore>; ruleScores: Record<string, number> }
export type CompetitionResultGroup = { name: string; entries: CompetitionResultEntry[] }
export type CompetitionResultRound = { id: string; shortName: string; longName: string; order: number }
export type CompetitionResultsDocument = { competitionName: string; startDate: string; endDate: string; rounds: CompetitionResultRound[]; groups: CompetitionResultGroup[] }
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
  ends: number
  arrowsPerEnd: number
  groupEnds: number | null
  categoryOrder: string[]
  deviceNames: string[]
  keyboard: KeyboardKey[]
  disabledKeyRules: DisabledKeyRule[]
  scoringRules: ScoringRule[]
  liveScopes: LiveScopeConfig[]
}

