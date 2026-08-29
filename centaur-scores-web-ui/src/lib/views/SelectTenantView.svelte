<script lang="ts">
  import { authorizationLabel } from '../authorization'
  import { labelForError } from '../errors'
  import type { TenantAccess } from '../types'

  export let authorizedForTenants: TenantAccess[]
  export let labels: Record<string, string>
  export let onSelectTenant: (tenantId: string) => Promise<void>
  export let onBack: () => void

  let selectingId: string | null = null
  let selectError = ''

  $: sortedTenants = [...authorizedForTenants].sort((a, b) => a.tenantName.localeCompare(b.tenantName))

  async function selectTile(tenantId: string) {
    selectingId = tenantId
    selectError = ''
    try {
      await onSelectTenant(tenantId)
    } catch (error) {
      selectError = labelForError(error, labels, 'selectTenantError')
    } finally {
      selectingId = null
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.home}</button>
<div class="page-intro">
  <div>
    <p class="eyebrow">{labels.switchTenant}</p>
    <h1>{labels.switchTenant}</h1>
  </div>
</div>

{#if selectError}<p class="error">{selectError}</p>{/if}

<div class="pb-tile-grid">
  {#each sortedTenants as item (item.tenantId)}
    <button class="pb-tile" disabled={selectingId !== null} on:click={() => selectTile(item.tenantId)}>
      {#if item.logoUrl}
        <img class="pb-tile-icon" style="width: 28px; height: 28px; object-fit: contain;" src={item.logoUrl} alt="" />
      {:else}
        <span class="pb-tile-icon" aria-hidden="true">🏢</span>
      {/if}
      <span class="pb-tile-title">{item.tenantName}</span>
      <span class="pb-tile-description">{authorizationLabel(item.authorization, labels)}</span>
    </button>
  {/each}
</div>
