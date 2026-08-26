import { writable } from 'svelte/store'
import { fetchTenants, type ApiClient } from './api'
import type { Account, Category, Competition, Match, MatchTemplate, ParticipantList, Profile, Tenant } from './types'

export const tenants = writable<Tenant[]>([])
export const matches = writable<Match[]>([])
export const competitions = writable<Competition[]>([])
export const profile = writable<Profile | null>(null)
export const currentTenant = writable<Tenant | null>(null)
export const childTenants = writable<Tenant[]>([])
export const accounts = writable<Account[]>([])
export const categories = writable<Category[]>([])
export const participantLists = writable<ParticipantList[]>([])
export const templates = writable<MatchTemplate[]>([])

export async function loadTenants(): Promise<Tenant[]> {
  const result = await fetchTenants()
  tenants.set(result)
  return result
}

export async function loadData(api: ApiClient) {
  matches.set(await api.fetchMatches())
  competitions.set(await api.fetchCompetitions())
  profile.set(await api.fetchProfile())
  currentTenant.set(await api.fetchCurrentTenant())
  categories.set(await api.fetchCategories())
  participantLists.set(await api.fetchParticipantLists())
  templates.set(await api.fetchTemplates())
}

export function resetMatchesAndCompetitions() {
  matches.set([])
  competitions.set([])
}

export async function loadMatchesList(api: ApiClient) {
  matches.set(await api.fetchMatches())
}

export async function loadCompetitionsList(api: ApiClient) {
  competitions.set(await api.fetchCompetitions())
}

export async function loadAccounts(api: ApiClient) {
  accounts.set(await api.fetchAccounts())
}

export async function loadChildTenants(api: ApiClient) {
  childTenants.set(await api.fetchChildTenants())
}

export async function refreshCategories(api: ApiClient) {
  categories.set(await api.fetchCategories())
}

export async function refreshParticipantLists(api: ApiClient) {
  participantLists.set(await api.fetchParticipantLists())
}

export async function refreshTemplates(api: ApiClient) {
  templates.set(await api.fetchTemplates())
}
