import { describe, expect, it } from 'vitest'
import { filenameFromDisposition } from './contentDisposition'

describe('filenameFromDisposition', () => {
  it('reads only the filename when ASP.NET also sends filename*', () => {
    const header = "attachment; filename=centaur-backup-Root-Tenant-20261031-231047.zip; filename*=UTF-8''centaur-backup-Root-Tenant-20261031-231047.zip"
    expect(filenameFromDisposition(header, 'backup.zip')).toBe('centaur-backup-Root-Tenant-20261031-231047.zip')
  })

  it('decodes the UTF-8 filename* form', () => {
    expect(filenameFromDisposition("attachment; filename=Uitslag_.csv; filename*=UTF-8''Uitslag%20%C3%A9%C3%A9n.csv", 'x.csv')).toBe('Uitslag één.csv')
  })

  it('reads quoted and unquoted plain filenames', () => {
    expect(filenameFromDisposition('attachment; filename="lanes 1.xlsx"', 'x')).toBe('lanes 1.xlsx')
    expect(filenameFromDisposition('attachment; filename=lanes.xlsx', 'x')).toBe('lanes.xlsx')
    expect(filenameFromDisposition('attachment; filename=lanes.xlsx; size=10', 'x')).toBe('lanes.xlsx')
  })

  it('falls back when there is no filename', () => {
    expect(filenameFromDisposition('', 'backup.zip')).toBe('backup.zip')
    expect(filenameFromDisposition(null, 'backup.zip')).toBe('backup.zip')
    expect(filenameFromDisposition('attachment', 'backup.zip')).toBe('backup.zip')
  })
})
