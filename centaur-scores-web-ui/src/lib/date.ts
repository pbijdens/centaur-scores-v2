export function formatLocalDate(value: string, language: string): string {
  return new Intl.DateTimeFormat(language === 'nl' ? 'nl-NL' : 'en-GB', { day: 'numeric', month: 'short', year: 'numeric' }).format(new Date(`${value.slice(0, 10)}T12:00:00`))
}
