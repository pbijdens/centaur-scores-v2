export type View = 'home' | 'matches' | 'match' | 'match-metadata' | 'match-devices' | 'match-qr' | 'match-results-scope' | 'match-participant' | 'competitions' | 'competition' | 'competition-results' | 'participants' | 'participant-list' | 'participant' | 'categories' | 'category' | 'templates' | 'template' | 'accounts' | 'account' | 'profile' | 'tenants' | 'tenant' | 'narrowcast' | 'personal-best' | 'personal-best-classifiers' | 'personal-best-disciplines' | 'personal-best-export-config' | 'personal-best-import-config' | 'personal-best-log'
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
  personalBestClassifier?: string | null
  participants?: MatchParticipant[]
  devices?: ScoreDevice[]
  liveScopes?: LiveScoreScope[]
}
// Shape of GET /api/matches: unlike Match (a single match's full detail), this never carries the
// (potentially huge) participants/devices collections - just aggregate counts for list rendering.
export type MatchListItem = {
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
  participantCount: number
  unlistedParticipantCount: number
  liveScopes: LiveScoreScope[]
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
export type LiveScoringEntry = { position: number; needsTieBreaker: boolean; line1: string; line2?: string | null; average?: number | null; arrows: number; score: number; aboveTarget: boolean }
export type LiveScoringBlock = { name: string; entries: LiveScoringEntry[] }
export type LiveScoringPage = { timeout: number; logo?: string | null; tenant: string; matchName: string; matchDate: string; ends: number; arrowsPerEnd: number; groupEnds?: number | null; blocks: LiveScoringBlock[] }
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
// Shape of GET /api/participant-lists: unlike ParticipantList (one list's full detail, with members),
// this never carries the (potentially huge) members collection - just counts for list rendering.
export type ParticipantListSummary = { id: string; name: string; isActive: boolean; memberCount: number; activeMemberCount: number }
export type MatchTemplate = { id: string; name: string; participantListId?: string | null; allowFreeParticipants: boolean; deviceSelectionMode: string; configurationJson: string; personalBestClassifier?: string | null }

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

// Personal Best tracking
export type PersonalBestStatus = { enabled: boolean; ownedHere: boolean; owningTenantId?: string | null }
export type PersonalBestDisciplineValue = { tenantId: string; tenantName: string; categoryId: string; categoryName: string; valueId: number; valueName: string }
export type PersonalBestDiscipline = { id: string; name: string; values: PersonalBestDisciplineValue[] }
export type PersonalBestAvailableValue = { tenantId: string; tenantName: string; categoryId: string; categoryName: string; valueId: number; valueName: string; takenByDisciplineId?: string | null }
export type PersonalBestDisciplineValueRef = { tenantId: string; categoryId: string; valueId: number }
export type PersonalBestExportField = 'federationNumber' | 'fullName' | 'discipline' | 'matchClassifier' | 'score' | 'date' | 'exportDate'
export type PersonalBestExportMode = 'all' | 'changesSinceLastImport'
export type PersonalBestDateFormat = 'ymd' | 'dmy' | 'mdy'
export type PersonalBestExportColumn = { columnName: string; field: PersonalBestExportField; dateFormat?: PersonalBestDateFormat | null }
export type PersonalBestExportConfig = { exportMode: PersonalBestExportMode; tableName: string; columns: PersonalBestExportColumn[] }
export type PersonalBestImportConfig = {
  tableName: string
  dateColumn: string
  federationNumberColumn: string
  nameColumn: string
  disciplineColumn: string
  matchClassifierColumn: string
  scoreColumn: string
  updateDateColumn: string
}
export type PersonalBestImportConflict = { federationNumber: string; discipline: string; matchClassifier: string; conflictType: 'cannotInsertLowerScore' | 'archerHasHigherScore'; actionable: boolean }
export type PersonalBestImportResult = { newArchers: number; newRegistrations: number; warnings: string[]; batchId?: string | null; conflicts: PersonalBestImportConflict[] }
export type PersonalBestConflictResolution = { federationNumber: string; discipline: string; matchClassifier: string; action: 'deleteOffending' | 'ignoreImported' }
export type PersonalBestLogRow = { federationNumber: string; name: string; discipline: string; matchClassifier: string; date: string; score: number }

