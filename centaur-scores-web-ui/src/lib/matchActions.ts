import type { ApiClient, MatchInput } from './api'
import type { Match } from './types'

// Saves a downloaded blob through a temporary <a download> link.
export function saveBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  link.click()
  URL.revokeObjectURL(url)
}

// Creates a closed copy of `match` with the same settings, devices and live scopes, and - when
// includeParticipants is set - the same participants on the same (newly created) devices.
export async function duplicateMatch(api: ApiClient, match: Match, name: string, includeParticipants: boolean): Promise<Match> {
  const copyInput: MatchInput = {
    name,
    date: match.date,
    shortCode: match.shortCode,
    isOpen: false,
    participantListId: match.participantListId,
    deviceSelectionMode: match.deviceSelectionMode,
    signatureMode: match.signatureMode,
    ends: match.ends,
    arrowsPerEnd: match.arrowsPerEnd,
    groupEnds: match.groupEnds,
    allowFreeParticipants: match.allowFreeParticipants,
    keyboardJson: match.keyboardJson,
    scoringRulesJson: match.scoringRulesJson,
    personalBestClassifier: match.personalBestClassifier
  }
  const copy = await api.createMatch(copyInput)

  const deviceIdMap = new Map<string, string>()
  for (const device of [...(match.devices ?? [])].sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))) {
    const newDevice = await api.addDevice(copy.id, { name: device.name })
    deviceIdMap.set(device.id, newDevice.id)
  }

  for (const scope of match.liveScopes ?? []) {
    await api.addLiveScope(copy.id, { scope: scope.scope, groupByCategoryIds: JSON.parse(scope.groupByCategoryIdsJson || '[]'), includeAverage: scope.includeAverage, includeGroupScores: scope.includeGroupScores, includeEqualizers: scope.includeEqualizers, includePersonalBest: scope.includePersonalBest, displayCategoryIds: JSON.parse(scope.displayCategoryIdsJson || '[]') })
  }

  if (includeParticipants) {
    for (const participant of match.participants ?? []) {
      const newParticipant = await api.addMatchParticipant(copy.id, { participantListMemberId: participant.participantListMemberId, lastName: participant.lastName, fullName: participant.fullName, federationNumber: participant.federationNumber, categories: participant.categories })
      const newDeviceId = participant.deviceId ? deviceIdMap.get(participant.deviceId) : null
      if (newDeviceId) await api.assignParticipantDevice(copy.id, newParticipant.id, newDeviceId)
    }
  }

  return copy
}
