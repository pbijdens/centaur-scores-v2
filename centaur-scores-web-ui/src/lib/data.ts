import { writable } from 'svelte/store'
import type { ApiClient } from './api'
import type { Account, Category, Competition, DefaultScopeSettings, MatchListItem, MatchTemplate, ParticipantListSummary, PersonalBestStatus, Profile, Tenant } from './types'

export const matches = writable<MatchListItem[]>([])
export const competitions = writable<Competition[]>([])
export const profile = writable<Profile | null>(null)
export const currentTenant = writable<Tenant | null>(null)
export const defaultScopeSettings = writable<DefaultScopeSettings | null>(null)
export const childTenants = writable<Tenant[]>([])
export const accounts = writable<Account[]>([])
export const categories = writable<Category[]>([])
export const participantLists = writable<ParticipantListSummary[]>([])
export const templates = writable<MatchTemplate[]>([])
export const personalBestStatus = writable<PersonalBestStatus | null>(null)

// Fetched and resolved before loadData(), since every call below requires an already-selected tenant.
export async function loadProfile(api: ApiClient): Promise<Profile> {
  const result = await api.fetchProfile()
  profile.set(result)
  return result
}

export async function loadData(api: ApiClient) {
  matches.set(await api.fetchMatches())
  competitions.set(await api.fetchCompetitions())
  currentTenant.set(await api.fetchCurrentTenant())
  defaultScopeSettings.set(await api.fetchDefaultScopeSettings())
  categories.set(await api.fetchCategories())
  participantLists.set(await api.fetchParticipantLists())
  templates.set(await api.fetchTemplates())
  personalBestStatus.set(await api.fetchPersonalBestStatus())
}

export async function refreshPersonalBestStatus(api: ApiClient) {
  personalBestStatus.set(await api.fetchPersonalBestStatus())
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

export async function refreshDefaultScopeSettings(api: ApiClient) {
  defaultScopeSettings.set(await api.fetchDefaultScopeSettings())
}
