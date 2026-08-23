import { describe, expect, it } from 'vitest'
import { parseTemplateConfiguration } from './templateConfig'

describe('parseTemplateConfiguration', () => {
  it('defaults match length settings for templates saved before they existed', () => {
    const config = parseTemplateConfiguration('{}')

    expect(config.ends).toBe(10)
    expect(config.arrowsPerEnd).toBe(3)
    expect(config.groupEnds).toBeNull()
  })

  it('preserves match length settings', () => {
    const config = parseTemplateConfiguration('{"ends":12,"arrowsPerEnd":6,"groupEnds":3}')

    expect(config.ends).toBe(12)
    expect(config.arrowsPerEnd).toBe(6)
    expect(config.groupEnds).toBe(3)
  })

  it('defaults device names for templates saved before device configuration existed', () => {
    const config = parseTemplateConfiguration('{"categoryOrder":[],"keyboard":[],"disabledKeyRules":[],"scoringRules":[{"type":"total"}],"liveScopes":[]}')

    expect(config.deviceNames).toEqual([])
  })

  it('preserves configured device names in order', () => {
    const config = parseTemplateConfiguration('{"deviceNames":["Lane 1","Lane 2","Finals"]}')

    expect(config.deviceNames).toEqual(['Lane 1', 'Lane 2', 'Finals'])
  })
})