import { get } from 'svelte/store';
import { ApiError, NetworkError, ScorekeeperApi } from './api';
import { mergeMatchData } from './matchService';
import { apiBase, conflicts, matchData, navigate, pendingSignatures, pendingUpdates, screen, syncStatus } from './stores';
import type { ParticipantScoreUpdates, PendingParticipantUpdates, ScoreConflictEntry, SignRequest } from './types';

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
    const merged = mergeMatchData(next, get(pendingUpdates), get(pendingSignatures));
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

      if (entry.error === 'SCORECARD_SIGNED') {
        // The card was signed (by this device or elsewhere) while an edit was
        // still queued - it can never be applied now, and there's nothing to
        // "resolve" (no conflict dialog entry, unlike PARTICIPANT_CONFLICT):
        // drop every pending edit for this participant unconditionally. The
        // next poll already reflects signed=true, and the read-only lock
        // prevents queuing any further edits for them.
        delete next[item.matchParticipantId];
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

  // SCORECARD_SIGNED entries are resolved automatically above (nothing left
  // pending, nothing for the user to choose) - ConflictDialog only knows how
  // to render PARTICIPANT_CONFLICT and SCORE_CONFLICT shapes.
  const dialogEntries = entries.filter((e) => e.error !== 'SCORECARD_SIGNED');
  conflicts.set(dialogEntries.length > 0 ? dialogEntries : null);
  if (dialogEntries.length > 0) {
    syncStatus.set('error');
  } else {
    syncStatus.set(Object.keys(get(pendingUpdates)).length > 0 ? 'pending' : 'idle');
  }
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

// --- Scorecard signing (see documentation/SIGNING-SCORECARDS.md) ---
//
// Unlike scores, signing has no conflict resolution - a queued signature is
// simply retried on the same 8s timer until it lands, and a SCORECARD_SIGNED
// response (e.g. a raced duplicate send) is treated as success, not a
// failure to keep retrying, since the end state is already correct.

let signaturesSyncing = false;

export function recordSignature(matchParticipantId: string, body: SignRequest): void {
  pendingSignatures.update((pending) => ({ ...pending, [matchParticipantId]: body }));
  matchData.update((match) => {
    if (!match) return match;
    return {
      ...match,
      participants: match.participants.map((p) =>
        p.matchParticipantId === matchParticipantId
          ? { ...p, signed: true, archerSignatureDataUrl: body.archerSignatureDataUrl, markerSignatureDataUrl: body.markerSignatureDataUrl }
          : p,
      ),
    };
  });
  void flushPendingSignatures();
}

export async function flushPendingSignatures(): Promise<void> {
  const api = getApi();
  const pending = get(pendingSignatures);
  if (!api || signaturesSyncing || Object.keys(pending).length === 0) return;

  signaturesSyncing = true;
  try {
    // Snapshot so a signature queued mid-flush isn't dropped from the store
    // we're iterating, and isn't double-sent either (it's simply picked up
    // on the next retry tick).
    const entries = Object.entries(pending);
    for (const [matchParticipantId, body] of entries) {
      try {
        await api.postSign(matchParticipantId, body);
        pendingSignatures.update((current) => {
          const next = { ...current };
          delete next[matchParticipantId];
          return next;
        });
      } catch (err) {
        if (err instanceof ApiError && err.code === 'SCORECARD_SIGNED') {
          pendingSignatures.update((current) => {
            const next = { ...current };
            delete next[matchParticipantId];
            return next;
          });
        }
        // Otherwise leave it queued; the next retry tick will try again.
      }
    }
  } finally {
    signaturesSyncing = false;
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
  void flushPendingSignatures();
  pollTimer = setInterval(() => void fetchMatchInfo(), POLL_INTERVAL_MS);
  retryTimer = setInterval(() => {
    void flushPendingScores();
    void flushPendingSignatures();
  }, RETRY_INTERVAL_MS);
}

export function stopBackgroundSync(): void {
  if (pollTimer) clearInterval(pollTimer);
  if (retryTimer) clearInterval(retryTimer);
  pollTimer = null;
  retryTimer = null;
}

export function forceSync(): void {
  void flushPendingScores();
  void flushPendingSignatures();
}
