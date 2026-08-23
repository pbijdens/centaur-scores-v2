import type { ScorekeeperMatch, ScorekeeperMatchParticipant } from './types';

export function keyValue(match: ScorekeeperMatch, keyId: string | null): number {
  if (keyId === null) return 0;
  const key = match.keyboard.find((k) => k.id === keyId);
  return key ? key.value : 0;
}

export function arrowsShot(participant: ScorekeeperMatchParticipant): number {
  return participant.arrowScores.filter((s) => s !== null).length;
}

export function totalScore(match: ScorekeeperMatch, participant: ScorekeeperMatchParticipant): number {
  return participant.arrowScores.reduce((sum, s) => sum + keyValue(match, s), 0);
}

// Split totals per groupEnds ends, only when groupEnds is configured.
export function splitScores(match: ScorekeeperMatch, participant: ScorekeeperMatchParticipant): number[] {
  if (!match.groupEnds || match.groupEnds <= 0) return [];
  const arrowsPerGroup = match.groupEnds * match.arrowsPerEnd;
  const splits: number[] = [];
  for (let start = 0; start < participant.arrowScores.length; start += arrowsPerGroup) {
    const slice = participant.arrowScores.slice(start, start + arrowsPerGroup);
    splits.push(slice.reduce((sum, s) => sum + keyValue(match, s), 0));
  }
  return splits;
}

export function endArrows(match: ScorekeeperMatch, participant: ScorekeeperMatchParticipant, endIndex: number): (string | null)[] {
  const start = endIndex * match.arrowsPerEnd;
  return participant.arrowScores.slice(start, start + match.arrowsPerEnd);
}

export function endTotal(match: ScorekeeperMatch, participant: ScorekeeperMatchParticipant, endIndex: number): number {
  return endArrows(match, participant, endIndex).reduce((sum, s) => sum + keyValue(match, s), 0);
}

export function runningTotalThroughEnd(match: ScorekeeperMatch, participant: ScorekeeperMatchParticipant, endIndex: number): number {
  const arrows = participant.arrowScores.slice(0, (endIndex + 1) * match.arrowsPerEnd);
  return arrows.reduce((sum, s) => sum + keyValue(match, s), 0);
}

export function groupRunningTotal(match: ScorekeeperMatch, participant: ScorekeeperMatchParticipant, endIndex: number): number {
  if (!match.groupEnds || match.groupEnds <= 0) {
    return runningTotalThroughEnd(match, participant, endIndex);
  }
  const groupStartEnd = Math.floor(endIndex / match.groupEnds) * match.groupEnds;
  const arrowsStart = groupStartEnd * match.arrowsPerEnd;
  const arrowsEnd = (endIndex + 1) * match.arrowsPerEnd;
  const arrows = participant.arrowScores.slice(arrowsStart, arrowsEnd);
  return arrows.reduce((sum, s) => sum + keyValue(match, s), 0);
}

export function availableKeys(match: ScorekeeperMatch, participant: ScorekeeperMatchParticipant) {
  if (!participant.availableKeyIDs) return match.keyboard;
  const allowed = new Set(participant.availableKeyIDs);
  return match.keyboard.filter((k) => allowed.has(k.id));
}

export function firstNullIndexInEnd(match: ScorekeeperMatch, participant: ScorekeeperMatchParticipant, endIndex: number): number | null {
  const start = endIndex * match.arrowsPerEnd;
  for (let i = start; i < start + match.arrowsPerEnd; i++) {
    if (participant.arrowScores[i] === null) return i;
  }
  return null;
}
