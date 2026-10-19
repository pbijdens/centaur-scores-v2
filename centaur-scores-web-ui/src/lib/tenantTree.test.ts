import { describe, expect, it } from 'vitest'
import { flattenTenantTree } from './tenantTree'
import type { TenantAccess } from './types'

function tenant(id: string, parent: string | null = null): TenantAccess {
  return { tenantId: id, tenantName: `Tenant ${id}`, authorization: 'Viewer', parentTenantId: parent, parentTenantName: parent ? `Tenant ${parent}` : null }
}

describe('flattenTenantTree', () => {
  it('orders tenants depth-first with siblings sorted by name', () => {
    const input = [
      tenant('E'), tenant('D2', 'D'), tenant('B3', 'B'), tenant('B2B', 'B2'), tenant('A'), tenant('B2A', 'B2'),
      tenant('D'), tenant('B1', 'B'), tenant('A1', 'A'), tenant('B'), tenant('B2', 'B'), tenant('D1', 'D')
    ]
    expect(flattenTenantTree(input).map((item) => item.tenantId)).toEqual(['A', 'A1', 'B', 'B1', 'B2', 'B2A', 'B2B', 'B3', 'D', 'D1', 'D2', 'E'])
  })

  it('treats a tenant whose parent is not in the list as a root', () => {
    const input = [tenant('Child', 'Home'), tenant('Home', 'OutsideParent')]
    expect(flattenTenantTree(input).map((item) => item.tenantId)).toEqual(['Home', 'Child'])
  })

  it('keeps tenants caught in a parent cycle', () => {
    const input = [tenant('X', 'Y'), tenant('Y', 'X'), tenant('Z')]
    expect(flattenTenantTree(input).map((item) => item.tenantId).sort()).toEqual(['X', 'Y', 'Z'])
  })
})
