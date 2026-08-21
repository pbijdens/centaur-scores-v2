import type { Competition, Match, Profile, Tenant } from './types'

export const apiBase = import.meta.env.VITE_API_BASE_URL ?? 'http://127.0.0.1:5080'

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
    if (!response.ok) {
      const text = await response.text()
      let message = text
      try {
        const body = JSON.parse(text)
        if (typeof body?.message === 'string') message = body.message
      } catch { /* response body was not JSON */ }
      throw new Error(message)
    }
    return response.status === 204 ? null : response.json()
  }

  fetchMatches(): Promise<Match[]> {
    return this.request('/api/matches')
  }

  fetchCompetitions(): Promise<Competition[]> {
    return this.request('/api/competitions')
  }

  updateMatch(id: string, body: unknown) {
    return this.request(`/api/matches/${id}`, { method: 'PUT', body: JSON.stringify(body) })
  }

  deactivateAllMatches() {
    return this.request('/api/matches/deactivate-all', { method: 'POST' })
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

  deleteChildTenant(id: string) {
    return this.request(`/api/tenants/${id}`, { method: 'DELETE' })
  }
}

export async function fetchTenants(): Promise<Tenant[]> {
  const response = await fetch(`${apiBase}/api/tenants`)
  if (!response.ok) throw new Error('tenant request failed')
  return response.json()
}

export async function login(username: string, password: string, tenantId: string): Promise<{ token: string }> {
  const response = await fetch(`${apiBase}/api/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ username, password, tenantId }) })
  if (!response.ok) throw new Error('login failed')
  return response.json()
}
