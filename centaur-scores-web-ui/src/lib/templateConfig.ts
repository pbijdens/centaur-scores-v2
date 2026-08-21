import type { TemplateConfiguration } from './types'

export const keyboardColors = ['Yellow', 'Red', 'Blue', 'Black', 'White'] as const

export const deviceSelectionModes = ['restricted', 'list', 'list-and-free'] as const

export const emptyTemplateConfiguration: TemplateConfiguration = {
  categoryOrder: [],
  keyboard: [],
  disabledKeyRules: [],
  scoringRules: [{ type: 'total' }],
  liveScopes: []
}

export const defaultTemplateConfigurationJson = JSON.stringify(emptyTemplateConfiguration)

export function parseTemplateConfiguration(json: string): TemplateConfiguration {
  try {
    const parsed = JSON.parse(json)
    return {
      categoryOrder: Array.isArray(parsed.categoryOrder) ? parsed.categoryOrder : [],
      keyboard: Array.isArray(parsed.keyboard) ? parsed.keyboard : [],
      disabledKeyRules: Array.isArray(parsed.disabledKeyRules) ? parsed.disabledKeyRules : [],
      scoringRules: Array.isArray(parsed.scoringRules) && parsed.scoringRules.length > 0 ? parsed.scoringRules : [{ type: 'total' }],
      liveScopes: Array.isArray(parsed.liveScopes) ? parsed.liveScopes : []
    }
  } catch {
    return structuredClone(emptyTemplateConfiguration)
  }
}
