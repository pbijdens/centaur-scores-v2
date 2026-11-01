// The two categories picked for the lane check on the devices page (MatchDevicesView): one that should be the
// same for every archer on a lane number (e.g. target face) and one that should differ between the archers on a
// device (e.g. club). Remembered per match on this browser; picked and shown on both the devices page and the
// quick lane editor.

import { parseMatchKeyboardConfig } from './matchConfig'
import type { Category, Match } from './types'

export type LaneCheck = { laneCategoryId: string; deviceCategoryId: string }

const storageKey = (matchId: string) => `centaur-lane-check-${matchId}`

export function loadLaneCheck(matchId: string): LaneCheck {
  try {
    const stored = JSON.parse(localStorage.getItem(storageKey(matchId)) ?? '{}')
    return { laneCategoryId: String(stored.laneCategoryId ?? ''), deviceCategoryId: String(stored.deviceCategoryId ?? '') }
  } catch {
    return { laneCategoryId: '', deviceCategoryId: '' }
  }
}

// The categories to pick from: the match's own categories in match order, or all of them when the match has none.
export function laneCheckCategories(match: Match, allCategories: Category[]): Category[] {
  const ordered = parseMatchKeyboardConfig(match.keyboardJson).categoryOrder
    .map((id) => allCategories.find((category) => category.id === id))
    .filter((category): category is Category => !!category)
  return ordered.length > 0 ? ordered : allCategories
}

export function saveLaneCheck(matchId: string, check: LaneCheck) {
  try {
    localStorage.setItem(storageKey(matchId), JSON.stringify(check))
  } catch {
    // localStorage unavailable - the picks just won't persist.
  }
}
