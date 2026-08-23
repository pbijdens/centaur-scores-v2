import { get, writable } from 'svelte/store';
import { readJson, writeJson } from './storage';
import type { Language, PendingUpdates, Screen, ScorekeeperMatch, SyncStatus, ScoreConflictEntry } from './types';

function persisted<T>(key: string, initial: T) {
  const store = writable<T>(readJson(key, initial));
  store.subscribe((value) => writeJson(key, value));
  return store;
}

export const apiBase = persisted<string | null>('apiBase', null);
export const language = persisted<Language>('language', 'NL');
export const screen = persisted<Screen>('screen', { name: 'loading' });
export const matchData = persisted<ScorekeeperMatch | null>('matchData', null);
export const pendingUpdates = persisted<PendingUpdates>('pendingUpdates', {});

export const syncStatus = writable<SyncStatus>('idle');
export const conflicts = writable<ScoreConflictEntry[] | null>(null);
export const busy = writable<boolean>(false);

const historyStack: Screen[] = [];

export function navigate(next: Screen, opts: { replace?: boolean; resetStack?: boolean } = {}): void {
  screen.update((current) => {
    if (opts.resetStack) {
      historyStack.length = 0;
    } else if (!opts.replace) {
      historyStack.push(current);
    }
    return next;
  });
  if (!opts.replace) {
    try {
      history.pushState({ depth: historyStack.length }, '');
    } catch {
      // ignore environments without history support
    }
  }
}

export function goToParent(): void {
  const parent = historyStack.pop();
  screen.set(parent ?? { name: 'home' });
}

export function resetHistoryStack(): void {
  historyStack.length = 0;
}

/** Moves the score-card screen to the previous/next participant (direction -1/+1),
 * cycling around the participant list. Shared by the header's Previous/Next
 * buttons and ScoreCardView's swipe gesture so both stay in sync. */
export function switchToAdjacentParticipant(direction: number): void {
  const current = get(screen);
  const match = get(matchData);
  if (!match || current.name !== 'score-card' || match.participants.length < 2) return;
  const idx = match.participants.findIndex((p) => p.matchParticipantId === current.matchParticipantId);
  if (idx === -1) return;
  const count = match.participants.length;
  const nextIdx = (idx + direction + count) % count;
  navigate({ name: 'score-card', matchParticipantId: match.participants[nextIdx].matchParticipantId }, { replace: true });
}
