import type { DisabledKeyRule, KeyboardKey, ScoringRule } from './types'

export type MatchKeyboardConfig = { categoryOrder: string[]; keyboard: KeyboardKey[]; disabledKeyRules: DisabledKeyRule[] }

const defaultKeyboardColors = ['Yellow', 'Yellow', 'Red', 'Red', 'Blue', 'Blue', 'Black', 'Black', 'White', 'White'] as const
const defaultKeyboard: KeyboardKey[] = [...Array.from({ length: 10 }, (_, index) => {
  const value = 10 - index
  return { keyId: String(value), label: String(value), color: defaultKeyboardColors[index], value }
}), { keyId: 'M', label: 'M', color: 'White' as const, value: 0 }]
const emptyKeyboardConfig: MatchKeyboardConfig = { categoryOrder: [], keyboard: defaultKeyboard, disabledKeyRules: [] }
const defaultScoringRules: ScoringRule[] = [{ type: 'total' }]

export const defaultMatchKeyboardJson = JSON.stringify(emptyKeyboardConfig)
export const defaultMatchScoringRulesJson = JSON.stringify(defaultScoringRules)

export function parseMatchKeyboardConfig(json: string): MatchKeyboardConfig {
  try {
    const parsed = JSON.parse(json)
    return {
      categoryOrder: Array.isArray(parsed.categoryOrder) ? parsed.categoryOrder : [],
      keyboard: Array.isArray(parsed.keyboard) ? parsed.keyboard : [],
      disabledKeyRules: Array.isArray(parsed.disabledKeyRules) ? parsed.disabledKeyRules : []
    }
  } catch {
    return structuredClone(emptyKeyboardConfig)
  }
}

export function parseMatchScoringRules(json: string): ScoringRule[] {
  try {
    const parsed = JSON.parse(json)
    return Array.isArray(parsed) && parsed.length > 0 ? parsed : structuredClone(defaultScoringRules)
  } catch {
    return structuredClone(defaultScoringRules)
  }
}
