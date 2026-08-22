import type { Account, Category, Competition, Match, MatchParticipant, MatchTemplate, ParticipantList, Profile, ScoreDevice, Tenant } from './types'

export const apiBase = import.meta.env.VITE_API_BASE_URL ?? 'http://127.0.0.1:5080'

export type MatchInput = {
  name: string
  date: string
  shortCode?: string | null
  isOpen: boolean
  participantListId?: string | null
  deviceSelectionMode: string
  ends: number
  arrowsPerEnd: number
  groupEnds?: number | null
  allowFreeParticipants: boolean
  keyboardJson: string
  scoringRulesJson: string
}

// Carries the API's stable error `code` so callers can map it to a translated message instead of showing server text.
export class ApiRequestError extends Error {
  code?: string
  constructor(message: string, code?: string) {
    super(message)
    this.code = code
  }
}

async function readApiError(response: Response): Promise<ApiRequestError> {
  const text = await response.text()
  let message = text
  let code: string | undefined
  try {
    const body = JSON.parse(text)
    if (typeof body?.code === 'string') code = body.code
    if (typeof body?.message === 'string') message = body.message
  } catch { /* response body was not JSON */ }
  return new ApiRequestError(message, code)
}

export class ApiClient {
  constructor(
    private getToken: () => string,
    private onUnauthorized: () => void
  ) {}

  private headers() {
    return { Authorization: `Bearer ${this.getToken()}`, 'Content-Type': 'application/json' }
  }

  async request(path: string, options: RequestInit = {}) {
    const response = await fetch(`${apiBase}${path}`, { ...options, headers: { ...this.headers(), ...(options.headers ?? {}) } })
    if (response.status === 401) this.onUnauthorized()
    if (!response.ok) throw await readApiError(response)
    return response.status === 204 ? null : response.json()
  }

  fetchMatches(): Promise<Match[]> {
    return this.request('/api/matches')
  }

  fetchMatch(id: string): Promise<Match> {
    return this.request(`/api/matches/${id}`)
  }

  createMatch(body: MatchInput): Promise<Match> {
    return this.request('/api/matches', { method: 'POST', body: JSON.stringify(body) })
  }

  fetchCompetitions(): Promise<Competition[]> {
    return this.request('/api/competitions')
  }

  updateMatch(id: string, body: MatchInput) {
    return this.request(`/api/matches/${id}`, { method: 'PUT', body: JSON.stringify(body) })
  }

  deleteMatch(id: string) {
    return this.request(`/api/matches/${id}`, { method: 'DELETE' })
  }

  deactivateAllMatches() {
    return this.request('/api/matches/deactivate-all', { method: 'POST' })
  }

  fetchMatchParticipants(matchId: string): Promise<MatchParticipant[]> {
    return this.request(`/api/matches/${matchId}/participants`)
  }

  addMatchParticipant(matchId: string, body: { participantListMemberId?: string | null; lastName: string; fullName: string; federationNumber?: string | null; categories: Record<string, number> }): Promise<MatchParticipant> {
    return this.request(`/api/matches/${matchId}/participants`, { method: 'POST', body: JSON.stringify(body) })
  }

  updateMatchParticipant(matchId: string, participantId: string, body: { participantListMemberId?: string | null; lastName: string; fullName: string; federationNumber?: string | null; categories: Record<string, number> }): Promise<MatchParticipant> {
    return this.request(`/api/matches/${matchId}/participants/${participantId}`, { method: 'PUT', body: JSON.stringify(body) })
  }

  removeMatchParticipant(matchId: string, participantId: string) {
    return this.request(`/api/matches/${matchId}/participants/${participantId}`, { method: 'DELETE' })
  }

  assignParticipantDevice(matchId: string, participantId: string, deviceId: string | null) {
    return this.request(`/api/matches/${matchId}/participants/${participantId}/device`, { method: 'PUT', body: JSON.stringify({ deviceId }) })
  }

  enterScore(matchId: string, participantId: string, body: { end: number; arrow: number; keyId: string; value: number }) {
    return this.request(`/api/matches/${matchId}/participants/${participantId}/scores`, { method: 'POST', body: JSON.stringify(body) })
  }

  fetchMatchResults(matchId: string) {
    return this.request(`/api/matches/${matchId}/results`)
  }

  addDevice(matchId: string, body: { name: string }): Promise<ScoreDevice> {
    return this.request(`/api/matches/${matchId}/devices`, { method: 'POST', body: JSON.stringify(body) })
  }

  reorderDevices(matchId: string, deviceIds: string[]) {
    return this.request(`/api/matches/${matchId}/devices/order`, { method: 'PUT', body: JSON.stringify({ deviceIds }) })
  }

  reorderDeviceParticipants(matchId: string, deviceId: string, participantIds: string[]) {
    return this.request(`/api/matches/${matchId}/devices/${deviceId}/participants/order`, { method: 'PUT', body: JSON.stringify({ participantIds }) })
  }

  deleteDevice(matchId: string, deviceId: string) {
    return this.request(`/api/matches/${matchId}/devices/${deviceId}`, { method: 'DELETE' })
  }

  addLiveScope(matchId: string, body: { scope: string; groupByCategoryIds: string[]; includeAverage: boolean; includeGroupScores: boolean; includeEqualizers: boolean; includePersonalBest: boolean }) {
    return this.request(`/api/matches/${matchId}/live-scopes`, { method: 'POST', body: JSON.stringify(body) })
  }

  deleteLiveScope(matchId: string, scopeId: string) {
    return this.request(`/api/matches/${matchId}/live-scopes/${scopeId}`, { method: 'DELETE' })
  }

  async downloadMatchExport(matchId: string): Promise<{ blob: Blob; filename: string }> {
    const response = await fetch(`${apiBase}/api/matches/${matchId}/export.csv`, { headers: this.headers() })
    if (!response.ok) throw await readApiError(response)
    const disposition = response.headers.get('content-disposition') ?? ''
    const filenameMatch = /filename="?([^"]+)"?/.exec(disposition)
    return { blob: await response.blob(), filename: filenameMatch?.[1] ?? 'export.csv' }
  }

  fetchProfile(): Promise<Profile> {
    return this.request('/api/auth/me')
  }

  updateProfile(body: { displayName?: string | null; email?: string | null }): Promise<Profile> {
    return this.request('/api/auth/me', { method: 'PUT', body: JSON.stringify(body) })
  }

  changePassword(currentPassword: string, newPassword: string) {
    return this.request('/api/auth/change-password', { method: 'POST', body: JSON.stringify({ currentPassword, newPassword }) })
  }

  fetchCurrentTenant(): Promise<Tenant> {
    return this.request('/api/tenants/current')
  }

  fetchChildTenants(): Promise<Tenant[]> {
    return this.request('/api/tenants/children')
  }

  fetchChildTenant(id: string): Promise<Tenant> {
    return this.request(`/api/tenants/children/${id}`)
  }

  createChildTenant(body: { name: string; logoUrl?: string | null; parentTenantId: string }): Promise<Tenant> {
    return this.request('/api/tenants', { method: 'POST', body: JSON.stringify(body) })
  }

  updateChildTenant(id: string, body: { name: string; logoUrl?: string | null }): Promise<Tenant> {
    return this.request(`/api/tenants/children/${id}`, { method: 'PUT', body: JSON.stringify(body) })
  }

  fetchAccounts(): Promise<Account[]> {
    return this.request('/api/accounts')
  }

  createAccount(body: { username: string }): Promise<Account> {
    return this.request('/api/accounts', { method: 'POST', body: JSON.stringify(body) })
  }

  fetchAccount(id: string): Promise<Account> {
    return this.request(`/api/accounts/${id}`)
  }

  updateAccount(id: string, body: { username: string; password?: string; displayName?: string | null; email?: string | null; authorization: string }): Promise<Account> {
    return this.request(`/api/accounts/${id}`, { method: 'PUT', body: JSON.stringify(body) })
  }

  deleteChildTenant(id: string) {
    return this.request(`/api/tenants/${id}`, { method: 'DELETE' })
  }

  fetchCategories(): Promise<Category[]> {
    return this.request('/api/categories')
  }

  createCategory(name: string): Promise<Category> {
    return this.request('/api/categories', { method: 'POST', body: JSON.stringify({ name }) })
  }

  addCategoryValue(categoryId: string, valueId: number, name: string) {
    return this.request(`/api/categories/${categoryId}/values`, { method: 'POST', body: JSON.stringify({ valueId, name }) })
  }

  updateCategoryValue(categoryId: string, valueId: number, name: string) {
    return this.request(`/api/categories/${categoryId}/values/${valueId}`, { method: 'PUT', body: JSON.stringify({ name }) })
  }

  deleteCategoryValue(categoryId: string, valueId: number) {
    return this.request(`/api/categories/${categoryId}/values/${valueId}`, { method: 'DELETE' })
  }

  deleteCategory(categoryId: string) {
    return this.request(`/api/categories/${categoryId}`, { method: 'DELETE' })
  }

  fetchParticipantLists(): Promise<ParticipantList[]> {
    return this.request('/api/participant-lists?includeInactive=true')
  }

  createParticipantList(body: { name: string; isActive: boolean }): Promise<ParticipantList> {
    return this.request('/api/participant-lists', { method: 'POST', body: JSON.stringify(body) })
  }

  updateParticipantList(id: string, body: { name: string; isActive: boolean }): Promise<ParticipantList> {
    return this.request(`/api/participant-lists/${id}`, { method: 'PUT', body: JSON.stringify(body) })
  }

  addParticipantMember(listId: string, body: { lastName: string; fullName: string; federationNumber?: string | null; categories: Record<string, number>; isActive: boolean }) {
    return this.request(`/api/participant-lists/${listId}/members`, { method: 'POST', body: JSON.stringify(body) })
  }

  updateParticipantMember(listId: string, memberId: string, body: { lastName: string; fullName: string; federationNumber?: string | null; categories: Record<string, number>; isActive: boolean }) {
    return this.request(`/api/participant-lists/${listId}/members/${memberId}`, { method: 'PUT', body: JSON.stringify(body) })
  }

  deleteParticipantMember(listId: string, memberId: string) {
    return this.request(`/api/participant-lists/${listId}/members/${memberId}`, { method: 'DELETE' })
  }

  fetchTemplates(): Promise<MatchTemplate[]> {
    return this.request('/api/match-templates')
  }

  createTemplate(body: { name: string; participantListId?: string | null; allowFreeParticipants: boolean; deviceSelectionMode: string; configurationJson: string }): Promise<MatchTemplate> {
    return this.request('/api/match-templates', { method: 'POST', body: JSON.stringify(body) })
  }

  updateTemplate(id: string, body: { name: string; participantListId?: string | null; allowFreeParticipants: boolean; deviceSelectionMode: string; configurationJson: string }): Promise<MatchTemplate> {
    return this.request(`/api/match-templates/${id}`, { method: 'PUT', body: JSON.stringify(body) })
  }

  deleteTemplate(id: string) {
    return this.request(`/api/match-templates/${id}`, { method: 'DELETE' })
  }
}

export async function fetchTenants(): Promise<Tenant[]> {
  const response = await fetch(`${apiBase}/api/tenants`)
  if (!response.ok) throw new Error('tenant request failed')
  return response.json()
}

export async function login(username: string, password: string, tenantId: string): Promise<{ token: string }> {
  const response = await fetch(`${apiBase}/api/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ username, password, tenantId }) })
  if (!response.ok) throw await readApiError(response)
  return response.json()
}

export function scorekeeperUrl(tenantId: string, matchId: string, deviceId: string): string {
  return `${apiBase}/scorekeeper/${tenantId}/${matchId}/${deviceId}`
}
