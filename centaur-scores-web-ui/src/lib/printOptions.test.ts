import { beforeEach, describe, expect, it, vi } from 'vitest'
import { loadPrintOptions, printOptionsKey, savePrintOptions } from './printOptions'

type Options = { lines: number }
const parse = (stored: Record<string, unknown> | null): Options => ({ lines: typeof stored?.lines === 'number' ? stored.lines : 2 })

describe('print options', () => {
  const key = printOptionsKey('test', 'abc')

  // Tests run in Node, without a browser localStorage.
  beforeEach(() => {
    const store = new Map<string, string>()
    vi.stubGlobal('localStorage', {
      getItem: (name: string) => store.get(name) ?? null,
      setItem: (name: string, value: string) => store.set(name, value)
    })
  })

  it('builds one key per kind and entity', () => {
    expect(key).toBe('centaur-print-test-abc')
  })

  it('round-trips saved options', () => {
    savePrintOptions(key, { lines: 3 })
    expect(loadPrintOptions(key, parse)).toEqual({ lines: 3 })
  })

  it('hands the parser null when nothing usable is stored', () => {
    expect(loadPrintOptions(key, parse)).toEqual({ lines: 2 })
    localStorage.setItem(key, 'not json')
    expect(loadPrintOptions(key, parse)).toEqual({ lines: 2 })
    localStorage.setItem(key, '[1, 2]')
    expect(loadPrintOptions(key, parse)).toEqual({ lines: 2 })
  })
})
