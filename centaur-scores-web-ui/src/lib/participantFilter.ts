import type { Category, MatchParticipant } from './types'

// Case-insensitive plain-text match against a match participant's name, federation number, or the
// name of any of its category values. An empty/whitespace filter matches everyone.
export function participantMatchesFilter(participant: Pick<MatchParticipant, 'fullName' | 'lastName' | 'federationNumber' | 'categories'>, filter: string, categories: Category[]): boolean {
  const needle = filter.trim().toLocaleLowerCase()
  if (!needle) return true
  const categoryValues = categories
    .map((category) => category.values.find((value) => value.valueId === participant.categories[category.id])?.name)
    .filter((value): value is string => !!value)
  return [participant.fullName, participant.lastName, participant.federationNumber ?? '', ...categoryValues]
    .some((text) => text.toLocaleLowerCase().includes(needle))
}
