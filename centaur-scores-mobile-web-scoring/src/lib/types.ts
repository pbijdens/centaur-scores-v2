// Types matching the CentaurScores public scorekeeper API
// (see ../../../documentation/PUBLIC-API-DESIGN.md)

export type KeyColor = 'Yellow' | 'Red' | 'Blue' | 'Black' | 'White';

export interface CategoryValue {
  id: number;
  name: string;
}

export interface ScoreKeeperCategory {
  id: string;
  name: string;
  values: CategoryValue[];
}

export interface ScorekeeperKey {
  id: string;
  label: string;
  value: number;
  color: KeyColor;
}

export interface ParticipantCategoryValue {
  id: string;
  name: string;
  value: string;
}

export interface ScorekeeperMatchParticipant {
  federationNumber: string | null;
  name: string;
  info: string | null;
  categories: ParticipantCategoryValue[];
  matchParticipantId: string;
  tenantParticipantId: string | null;
  availableKeyIDs: string[] | null;
  arrowScores: (string | null)[];
}

export interface ScorekeeperMatch {
  device: string;
  match: string;
  ends: number;
  arrowsPerEnd: number;
  groupEnds: number | null;
  categories: ScoreKeeperCategory[];
  allowModifyParticipants: boolean;
  allowCustomParticipants: boolean;
  keyboard: ScorekeeperKey[];
  participants: ScorekeeperMatchParticipant[];
}

export interface ScoreKeeperParticipantInfo {
  matchParticipantId: string | null;
  tenantParticipantId: string | null;
  federationNumber: string | null;
  name: string;
  info: string | null;
  categories: ParticipantCategoryValue[];
}

export interface ParticipantOptionsResponse {
  unassigned: ScoreKeeperParticipantInfo[];
  assigned: ScoreKeeperParticipantInfo[];
  potential: ScoreKeeperParticipantInfo[];
}

// Payload shape used when PUTting the participants list back to the server.
export interface ScorekeeperParticipantUpdate {
  federationNumber: string | null;
  name: string | null;
  info: string | null;
  categories: ParticipantCategoryValue[] | null;
  matchParticipantId: string | null;
  tenantParticipantId: string | null;
  availableKeyIDs: null;
  arrowScores: null;
}

export interface ScoreUpdate {
  index: number;
  old: string | null;
  new: string | null;
}

export interface ParticipantScoreUpdates {
  matchParticipantId: string;
  updates: ScoreUpdate[];
}

export type ScoreConflictErrorCode = 'SCORE_CONFLICT' | 'PARTICIPANT_CONFLICT';

export interface ScoreConflict {
  index: number;
  current: string | null;
  old: string | null;
  new: string | null;
}

export interface ScoreConflictEntry {
  matchParticipantId: string;
  error: ScoreConflictErrorCode;
  conflicts: ScoreConflict[];
}

export interface TimeResponse {
  time: string;
}

export type ApiErrorCode =
  | 'MATCH_NO_LONGER_ACTIVE'
  | 'PARTICIPANT_LIST_FIXED'
  | 'CUSTOM_PARTICIPANT_NOT_ALLOWED'
  | 'PARTICIPANT_UPDATE_NOT_ALLOWED'
  | 'UPDATE_SCORE_CONFLICT'
  | string;

// Application-level state

export type Language = 'NL' | 'EN';

export type SyncStatus = 'idle' | 'pending' | 'syncing' | 'error';

export type Screen =
  | { name: 'loading' }
  | { name: 'no-active-match' }
  | { name: 'home' }
  | { name: 'add-participant' }
  | { name: 'edit-participant'; matchParticipantId: string }
  | { name: 'score-card'; matchParticipantId: string };

// index -> pending edit for that arrow
export type PendingParticipantUpdates = Record<number, { old: string | null; new: string | null }>;
export type PendingUpdates = Record<string, PendingParticipantUpdates>;
