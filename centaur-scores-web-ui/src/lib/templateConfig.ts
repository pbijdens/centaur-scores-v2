import type { KeyboardKey, TemplateConfiguration } from './types'

export const keyboardColors = ['Yellow', 'Red', 'Blue', 'Black', 'White'] as const

export const deviceSelectionModes = ['restricted', 'list', 'list-and-free'] as const

const defaultKeyboardColors = ['Yellow', 'Yellow', 'Red', 'Red', 'Blue', 'Blue', 'Black', 'Black', 'White', 'White'] as const
const defaultKeyboard: KeyboardKey[] = [...Array.from({ length: 10 }, (_, index) => {
  const value = 10 - index
  return { keyId: String(value), label: String(value), color: defaultKeyboardColors[index], value }
}), { keyId: 'M', label: 'M', color: 'White' as const, value: 0 }]

export const emptyTemplateConfiguration: TemplateConfiguration = {
  ends: 10,
  arrowsPerEnd: 3,
  groupEnds: null,
  categoryOrder: [],
  deviceNames: [],
  keyboard: defaultKeyboard,
  disabledKeyRules: [],
  scoringRules: [{ type: 'total' }],
  liveScopes: [{ scope: 'all', groupByCategoryIds: [], includeAverage: true, includeGroupScores: false, includeEqualizers: true, includePersonalBest: false }]
}

export const defaultTemplateConfigurationJson = JSON.stringify(emptyTemplateConfiguration)

export function parseTemplateConfiguration(json: string): TemplateConfiguration {
  try {
    const parsed = JSON.parse(json)
    return {
      ends: typeof parsed.ends === 'number' && parsed.ends >= 1 ? parsed.ends : 10,
      arrowsPerEnd: typeof parsed.arrowsPerEnd === 'number' && parsed.arrowsPerEnd >= 1 ? parsed.arrowsPerEnd : 3,
      groupEnds: typeof parsed.groupEnds === 'number' && parsed.groupEnds >= 1 ? parsed.groupEnds : null,
      categoryOrder: Array.isArray(parsed.categoryOrder) ? parsed.categoryOrder : [],
      deviceNames: Array.isArray(parsed.deviceNames) ? parsed.deviceNames.filter((name: unknown): name is string => typeof name === 'string') : [],
      keyboard: Array.isArray(parsed.keyboard) ? parsed.keyboard : [],
      disabledKeyRules: Array.isArray(parsed.disabledKeyRules) ? parsed.disabledKeyRules : [],
      scoringRules: Array.isArray(parsed.scoringRules) && parsed.scoringRules.length > 0 ? parsed.scoringRules : [{ type: 'total' }],
      liveScopes: Array.isArray(parsed.liveScopes) ? parsed.liveScopes : []
    }
  } catch {
    return structuredClone(emptyTemplateConfiguration)
  }
}
