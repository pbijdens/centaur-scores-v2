import { describe, expect, it } from 'vitest'
import { parseTemplateConfiguration } from './templateConfig'

describe('parseTemplateConfiguration', () => {
  it('defaults device names for templates saved before device configuration existed', () => {
    const config = parseTemplateConfiguration('{"categoryOrder":[],"keyboard":[],"disabledKeyRules":[],"scoringRules":[{"type":"total"}],"liveScopes":[]}')

    expect(config.deviceNames).toEqual([])
  })

  it('preserves configured device names in order', () => {
    const config = parseTemplateConfiguration('{"deviceNames":["Lane 1","Lane 2","Finals"]}')

    expect(config.deviceNames).toEqual(['Lane 1', 'Lane 2', 'Finals'])
  })
})