import type { TenantAccess } from './types'

// Orders the account's authorized tenants as a depth-first flattened tree: each tenant is followed
// directly by its descendants, siblings sorted by name. A tenant whose parent is not in the list (the
// account's home tenant, or any orphan) is treated as a root.
export function flattenTenantTree(tenants: TenantAccess[]): TenantAccess[] {
  const ids = new Set(tenants.map((item) => item.tenantId))
  const byName = (a: TenantAccess, b: TenantAccess) => a.tenantName.localeCompare(b.tenantName)
  const childrenOf = new Map<string, TenantAccess[]>()
  const roots: TenantAccess[] = []
  for (const tenant of tenants) {
    const parentId = tenant.parentTenantId
    if (parentId && ids.has(parentId) && parentId !== tenant.tenantId) childrenOf.set(parentId, [...(childrenOf.get(parentId) ?? []), tenant])
    else roots.push(tenant)
  }
  const result: TenantAccess[] = []
  const visited = new Set<string>()
  const visit = (tenant: TenantAccess) => {
    if (visited.has(tenant.tenantId)) return
    visited.add(tenant.tenantId)
    result.push(tenant)
    for (const child of [...(childrenOf.get(tenant.tenantId) ?? [])].sort(byName)) visit(child)
  }
  for (const root of roots.sort(byName)) visit(root)
  // A parent cycle leaves tenants unreachable from any root; append them rather than dropping them.
  for (const tenant of [...tenants].sort(byName)) visit(tenant)
  return result
}
