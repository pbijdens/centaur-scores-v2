import { writable } from 'svelte/store';
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
