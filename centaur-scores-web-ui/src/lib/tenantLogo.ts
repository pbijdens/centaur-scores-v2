const maxLogoBytes = 256 * 1024
const preferredAspectRatio = 1
const acceptedTypes = ['image/svg+xml', 'image/png']

export type LogoValidation = { dataUrl: string; aspectWarning: boolean }

export function validateLogoFile(file: File): string | null {
  if (!acceptedTypes.includes(file.type)) return 'Logo must be an SVG or PNG image.'
  if (file.size > maxLogoBytes) return 'Logo must be 256KB or smaller.'
  return null
}

export function readLogoFile(file: File): Promise<LogoValidation> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onerror = () => reject(new Error('Unable to read the selected file.'))
    reader.onload = () => {
      const dataUrl = reader.result as string
      const image = new Image()
      image.onload = () => {
        const ratio = image.naturalWidth / image.naturalHeight
        resolve({ dataUrl, aspectWarning: !Number.isFinite(ratio) || Math.abs(ratio - preferredAspectRatio) > 0.3 })
      }
      image.onerror = () => resolve({ dataUrl, aspectWarning: false })
      image.src = dataUrl
    }
    reader.readAsDataURL(file)
  })
}
