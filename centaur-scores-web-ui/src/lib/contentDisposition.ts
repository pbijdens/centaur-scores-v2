// Reads the download filename from a Content-Disposition header. Prefers the RFC 5987 `filename*`
// form (`filename*=UTF-8''name%20with%20spaces.zip`, which ASP.NET adds next to the plain one), then the
// plain `filename=` (quoted or unquoted), and only takes that one parameter - not the rest of the header.
export function filenameFromDisposition(disposition: string | null | undefined, fallback: string): string {
  const header = disposition ?? ''

  const extended = /filename\*\s*=\s*([^']*)'[^']*'([^;]+)/i.exec(header)
  if (extended) {
    try {
      const name = decodeURIComponent(extended[2].trim().replace(/^"|"$/g, ''))
      if (name) return name
    } catch {
      // malformed percent-encoding: fall back to the plain filename
    }
  }

  const plain = /filename\s*=\s*(?:"([^"]*)"|([^;]*))/i.exec(header)
  const name = (plain?.[1] ?? plain?.[2] ?? '').trim()
  return name || fallback
}
