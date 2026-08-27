import type { Category } from './types'

export function deriveLastName(fullName: string): string {
  const trimmed = fullName.trim()
  if (!trimmed) return ''
  const parts = trimmed.split(/\s+/)
  return parts[parts.length - 1]
}

type CategorizedMember = { categories: Record<string, number> }
type NamedMember = CategorizedMember & { fullName: string; lastName: string; federationNumber?: string | null }

export function memberCategoryLabel(categories: Category[], member: CategorizedMember): string {
  return categories
    .map((category) => category.values.find((value) => value.valueId === member.categories[category.id])?.name)
    .filter((value): value is string => !!value)
    .join(' / ')
}

export function memberDetailLabel(categories: Category[], member: NamedMember): string {
  const categoryText = memberCategoryLabel(categories, member)
  return [member.federationNumber, categoryText].filter((part): part is string => !!part).join(' / ')
}

export function memberDisplayLabel(categories: Category[], member: NamedMember): string {
  const name = member.fullName || member.lastName
  const detail = memberDetailLabel(categories, member)
  return detail ? `${name} (${detail})` : name
}
