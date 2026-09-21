import type {
  Language,
  PendingSignatures,
  PendingUpdates,
  ScoreKeeperParticipantInfo,
  ScorekeeperMatch,
  ScorekeeperMatchParticipant,
  ScorekeeperParticipantUpdate,
} from './types';
import { clearAppState } from './storage';
import { apiBase, language, matchData, pendingSignatures, pendingUpdates, screen } from './stores';
import { get } from 'svelte/store';

export function readStartupParams(): { apiBase: string | null; language: Language | null } {
  const params = new URLSearchParams(window.location.search);
  const apiParam = params.get('api');
  const langParam = params.get('language');
  const lang = langParam && langParam.toUpperCase() === 'EN' ? 'EN' : langParam ? 'NL' : null;
  return { apiBase: apiParam, language: lang };
}

/** Applies the ?api=&language= startup params (if present), resetting local
 * application state whenever the API base actually changes. */
export function initializeFromStartupParams(): void {
  const { apiBase: newBase, language: newLang } = readStartupParams();

  if (newBase && newBase !== get(apiBase)) {
    clearAppState();
    matchData.set(null);
    pendingUpdates.set({});
    pendingSignatures.set({});
    screen.set({ name: 'loading' });
    apiBase.set(newBase);
  }

  if (newLang) {
    language.set(newLang);
  }
}

/** Merges freshly fetched match data into the store, keeping any not-yet
 * synchronized local edits (scores and/or a signature) so a background poll
 * never clobbers them. */
export function mergeMatchData(next: ScorekeeperMatch, pending: PendingUpdates, pendingSigned: PendingSignatures): ScorekeeperMatch {
  const mergedParticipants = next.participants.map((participant) => {
    let merged = participant;

    const participantPending = pending[participant.matchParticipantId];
    if (participantPending && Object.keys(participantPending).length > 0) {
      const arrowScores = [...merged.arrowScores];
      for (const [indexStr, edit] of Object.entries(participantPending)) {
        arrowScores[Number(indexStr)] = edit.new;
      }
      merged = { ...merged, arrowScores };
    }

    const pendingSignature = pendingSigned[participant.matchParticipantId];
    if (pendingSignature) {
      merged = {
        ...merged,
        signed: true,
        archerSignatureDataUrl: pendingSignature.archerSignatureDataUrl,
        markerSignatureDataUrl: pendingSignature.markerSignatureDataUrl,
      };
    }

    return merged;
  });
  return { ...next, participants: mergedParticipants };
}

export function toParticipantPayload(p: ScorekeeperMatchParticipant): ScorekeeperParticipantUpdate {
  if (p.tenantParticipantId) {
    return {
      tenantParticipantId: p.tenantParticipantId,
      matchParticipantId: null,
      federationNumber: null,
      name: null,
      categories: null,
      info: null,
      availableKeyIDs: null,
      arrowScores: null,
    };
  }
  return {
    tenantParticipantId: null,
    matchParticipantId: p.matchParticipantId,
    federationNumber: p.federationNumber,
    name: p.name,
    categories: p.categories,
    info: null,
    availableKeyIDs: null,
    arrowScores: null,
  };
}

export function optionToParticipantPayload(o: ScoreKeeperParticipantInfo): ScorekeeperParticipantUpdate {
  if (o.tenantParticipantId) {
    return {
      tenantParticipantId: o.tenantParticipantId,
      matchParticipantId: null,
      federationNumber: null,
      name: null,
      categories: null,
      info: null,
      availableKeyIDs: null,
      arrowScores: null,
    };
  }
  return {
    tenantParticipantId: null,
    matchParticipantId: o.matchParticipantId,
    federationNumber: o.federationNumber,
    name: o.name,
    categories: o.categories,
    info: null,
    availableKeyIDs: null,
    arrowScores: null,
  };
}

export interface CustomParticipantFields {
  federationNumber: string | null;
  name: string;
  categories: { id: string; name: string; value: string }[];
}

export function customParticipantPayload(
  fields: CustomParticipantFields,
  existingMatchParticipantId: string | null = null,
): ScorekeeperParticipantUpdate {
  return {
    tenantParticipantId: null,
    matchParticipantId: existingMatchParticipantId,
    federationNumber: fields.federationNumber || null,
    name: fields.name,
    categories: fields.categories,
    info: null,
    availableKeyIDs: null,
    arrowScores: null,
  };
}
