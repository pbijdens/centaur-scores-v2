<script lang="ts">
  import { navigateOnClick, tenantPath } from '../router'
import type { Tenant } from '../types'

  export let tenants: Tenant[]
  export let labels: Record<string, string>
  export let onOpenTenant: (tenant: Tenant) => void
  export let onCreateTenant: (name: string) => void
  export let onBack: () => void

  let showAddForm = false
  let newTenantName = ''

  function submitAdd() {
    if (!newTenantName.trim()) return
    onCreateTenant(newTenantName.trim())
    newTenantName = ''
    showAddForm = false
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.home}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowTenantAdmin}</p><h1>{labels.tenants}</h1><p class="muted">{labels.tenantsHint}</p></div>
  <button class="primary" on:click={() => (showAddForm = !showAddForm)}>+ {labels.newTenant}</button>
</div>
{#if showAddForm}
  <form class="inline-form" on:submit|preventDefault={submitAdd}>
    <label>{labels.tenantName}<input bind:value={newTenantName} /></label>
    <button class="primary" type="submit" disabled={!newTenantName.trim()}>{labels.save}</button>
  </form>
{/if}
<section class="list-panel">
  {#if tenants.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
  {#each tenants as tenant}
    <a class="list-row" href={tenantPath(tenant.id)} on:click={(event) => navigateOnClick(event, () => onOpenTenant(tenant))}>
      {#if tenant.logoUrl}<img class="tenant-logo-thumb" src={tenant.logoUrl} alt="" />{:else}<span class="management-icon">◇</span>{/if}
      <span><strong>{tenant.name}</strong></span>
      <span class="arrow">→</span>
    </a>
  {/each}
</section>
