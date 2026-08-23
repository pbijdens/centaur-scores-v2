import { get } from 'svelte/store';
import { ApiError, NetworkError, ScorekeeperApi } from './api';
import { mergeMatchData } from './matchService';
import { apiBase, conflicts, matchData, navigate, pendingUpdates, screen, syncStatus } from './stores';
import type { ParticipantScoreUpdates, PendingParticipantUpdates, ScoreConflictEntry } from './types';

const POLL_INTERVAL_MS = 60_000;
const RETRY_INTERVAL_MS = 8_000;

let pollTimer: ReturnType<typeof setInterval> | null = null;
let retryTimer: ReturnType<typeof setInterval> | null = null;
let syncing = false;

function getApi(): ScorekeeperApi | null {
  const base = get(apiBase);
  return base ? new ScorekeeperApi(base) : null;
}

export async function fetchMatchInfo(): Promise<boolean> {
  const api = getApi();
  if (!api) return false;
  try {
    const next = await api.getMatchInfo();
    const merged = mergeMatchData(next, get(pendingUpdates));
    matchData.set(merged);
    const current = get(screen);
    if (current.name === 'no-active-match' || current.name === 'loading') {
      navigate({ name: 'home' }, { resetStack: true });
    }
    return true;
  } catch (err) {
    if (err instanceof ApiError && (err.status === 404 || err.status === 409)) {
      navigate({ name: 'no-active-match' }, { resetStack: true });
      return false;
    }
    // network error: keep showing whatever we had (cached state), just skip this tick
    return false;
  }
}

function buildScorePayload(): ParticipantScoreUpdates[] {
  const pending = get(pendingUpdates);
  const payload: ParticipantScoreUpdates[] = [];
  for (const [matchParticipantId, edits] of Object.entries(pending)) {
    const updates = Object.entries(edits).map(([indexStr, edit]) => ({
      index: Number(indexStr),
      old: edit.old,
      new: edit.new,
    }));
    if (updates.length > 0) {
      payload.push({ matchParticipantId, updates });
    }
  }
  return payload;
}

export async function flushPendingScores(): Promise<void> {
  const api = getApi();
  if (!api || syncing) return;
  const payload = buildScorePayload();
  if (payload.length === 0) {
    syncStatus.set('idle');
    return;
  }

  syncing = true;
  syncStatus.set('syncing');
  try {
    await api.putScores(payload);
    // Everything we sent was applied: drop exactly the entries we sent,
    // unless the user edited them again while the request was in flight.
    pendingUpdates.update((pending) => {
      const next: typeof pending = { ...pending };
      for (const item of payload) {
        const sentIndexes = new Set(item.updates.map((u) => u.index));
        const current = next[item.matchParticipantId];
        if (!current) continue;
        const remaining: PendingParticipantUpdates = {};
        for (const [indexStr, edit] of Object.entries(current)) {
          const index = Number(indexStr);
          const sentEdit = item.updates.find((u) => u.index === index);
          if (sentIndexes.has(index) && sentEdit && sentEdit.new === edit.new) {
            continue; // synced, drop it
          }
          remaining[index] = edit;
        }
        if (Object.keys(remaining).length > 0) {
          next[item.matchParticipantId] = remaining;
        } else {
          delete next[item.matchParticipantId];
        }
      }
      return next;
    });
    conflicts.set(null);
    syncStatus.set(get(pendingUpdates) && Object.keys(get(pendingUpdates)).length > 0 ? 'pending' : 'idle');
  } catch (err) {
    if (err instanceof ApiError && err.status === 409 && err.code === 'UPDATE_SCORE_CONFLICT' && err.conflicts) {
      handleConflictResponse(payload, err.conflicts);
    } else if (err instanceof NetworkError) {
      syncStatus.set('error');
    } else {
      syncStatus.set('error');
    }
  } finally {
    syncing = false;
  }
}

function handleConflictResponse(sent: ParticipantScoreUpdates[], entries: ScoreConflictEntry[]): void {
  const conflictedByParticipant = new Map(entries.map((e) => [e.matchParticipantId, e]));

  pendingUpdates.update((pending) => {
    const next: typeof pending = { ...pending };
    for (const item of sent) {
      const entry = conflictedByParticipant.get(item.matchParticipantId);
      const current = next[item.matchParticipantId];
      if (!current) continue;

      if (!entry) {
        // This participant's updates were applied fine; drop the sent ones.
        const remaining: PendingParticipantUpdates = {};
        for (const [indexStr, edit] of Object.entries(current)) {
          const index = Number(indexStr);
          const wasSent = item.updates.some((u) => u.index === index);
          if (!wasSent) remaining[index] = edit;
        }
        if (Object.keys(remaining).length > 0) next[item.matchParticipantId] = remaining;
        else delete next[item.matchParticipantId];
        continue;
      }

      if (entry.error === 'PARTICIPANT_CONFLICT') {
        // No longer assigned to this device: leave every pending edit for
        // them untouched so the conflict screen can offer to discard them.
        continue;
      }

      // SCORE_CONFLICT: drop the non-conflicting sent indexes, keep the
      // conflicting ones pending so the user can resolve them.
      const conflictIndexes = new Set(entry.conflicts.map((c) => c.index));
      const remaining: PendingParticipantUpdates = {};
      for (const [indexStr, edit] of Object.entries(current)) {
        const index = Number(indexStr);
        const wasSent = item.updates.some((u) => u.index === index);
        if (wasSent && !conflictIndexes.has(index)) continue; // applied fine
        remaining[index] = edit;
      }
      if (Object.keys(remaining).length > 0) next[item.matchParticipantId] = remaining;
      else delete next[item.matchParticipantId];
    }
    return next;
  });

  syncStatus.set('error');
  conflicts.set(entries);
}

/** SCORE_CONFLICT resolution for a single arrow.
 *  - "theirs": discard our pending edit and adopt the server's value.
 *  - "mine": keep our edit, but re-request it with `old` set to the value
 *    the server told us it actually holds, so the next sync attempt applies. */
export function resolveScoreConflict(matchParticipantId: string, index: number, resolution: 'mine' | 'theirs', serverValue: string | null): void {
  if (resolution === 'theirs') {
    pendingUpdates.update((pending) => {
      const next = { ...pending };
      const forParticipant = { ...(next[matchParticipantId] ?? {}) };
      delete forParticipant[index];
      if (Object.keys(forParticipant).length > 0) next[matchParticipantId] = forParticipant;
      else delete next[matchParticipantId];
      return next;
    });
    matchData.update((match) => {
      if (!match) return match;
      return {
        ...match,
        participants: match.participants.map((p) => {
          if (p.matchParticipantId !== matchParticipantId) return p;
          const arrowScores = [...p.arrowScores];
          arrowScores[index] = serverValue;
          return { ...p, arrowScores };
        }),
      };
    });
  } else {
    pendingUpdates.update((pending) => {
      const next = { ...pending };
      const forParticipant = { ...(next[matchParticipantId] ?? {}) };
      const edit = forParticipant[index];
      if (edit) {
        forParticipant[index] = { ...edit, old: serverValue };
      }
      next[matchParticipantId] = forParticipant;
      return next;
    });
  }

  removeResolvedConflict(matchParticipantId, index);
  if (resolution === 'mine') {
    void flushPendingScores();
  } else {
    refreshSyncStatusAfterResolution();
  }
}

/** PARTICIPANT_CONFLICT resolution: the participant is no longer assigned to
 * this device, so the only option is to drop every pending edit for them. */
export function discardParticipantConflict(matchParticipantId: string): void {
  pendingUpdates.update((pending) => {
    const next = { ...pending };
    delete next[matchParticipantId];
    return next;
  });
  conflicts.update((current) => {
    if (!current) return current;
    const updated = current.filter((entry) => entry.matchParticipantId !== matchParticipantId);
    return updated.length > 0 ? updated : null;
  });
  refreshSyncStatusAfterResolution();
}

function removeResolvedConflict(matchParticipantId: string, index: number): void {
  conflicts.update((current) => {
    if (!current) return current;
    const updated = current
      .map((entry) => {
        if (entry.matchParticipantId !== matchParticipantId) return entry;
        return { ...entry, conflicts: entry.conflicts.filter((c) => c.index !== index) };
      })
      .filter((entry) => entry.error === 'PARTICIPANT_CONFLICT' || entry.conflicts.length > 0);
    return updated.length > 0 ? updated : null;
  });
}

/** After a conflict is resolved without a fresh sync attempt (discard /
 * "use theirs"), the sync icon must stop showing the stale error state. */
function refreshSyncStatusAfterResolution(): void {
  if (get(conflicts)) return; // other conflicts still need resolving
  if (Object.keys(get(pendingUpdates)).length > 0) {
    void flushPendingScores();
  } else {
    syncStatus.set('idle');
  }
}

export function recordScoreEdit(matchParticipantId: string, index: number, previousValue: string | null, newValue: string | null): void {
  pendingUpdates.update((pending) => {
    const next = { ...pending };
    const forParticipant = { ...(next[matchParticipantId] ?? {}) };
    const existing = forParticipant[index];
    forParticipant[index] = { old: existing ? existing.old : previousValue, new: newValue };
    next[matchParticipantId] = forParticipant;
    return next;
  });
  syncStatus.update((s) => (s === 'syncing' ? s : 'pending'));
  void flushPendingScores();
}

export function startBackgroundSync(): void {
  stopBackgroundSync();
  void fetchMatchInfo();
  void flushPendingScores();
  pollTimer = setInterval(() => void fetchMatchInfo(), POLL_INTERVAL_MS);
  retryTimer = setInterval(() => void flushPendingScores(), RETRY_INTERVAL_MS);
}

export function stopBackgroundSync(): void {
  if (pollTimer) clearInterval(pollTimer);
  if (retryTimer) clearInterval(retryTimer);
  pollTimer = null;
  retryTimer = null;
}

export function forceSync(): void {
  void flushPendingScores();
}
