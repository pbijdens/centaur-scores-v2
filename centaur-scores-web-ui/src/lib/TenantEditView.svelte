<script lang="ts">
  import type { ApiClient } from './api'
  import { readLogoFile, validateLogoFile } from './tenantLogo'
  import type { Tenant } from './types'

  export let api: ApiClient
  export let tenantId: string
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onDeleted: () => void

  let tenant: Tenant | null = null
  let name = ''
  let logoUrl: string | null | undefined = null
  let saveMessage = ''
  let saveError = ''
  let logoWarning = ''

  async function loadTenant() {
    tenant = await api.fetchChildTenant(tenantId)
    name = tenant.name
    logoUrl = tenant.logoUrl
  }
  loadTenant()

  async function onLogoSelected(event: Event) {
    logoWarning = ''
    saveError = ''
    const file = (event.currentTarget as HTMLInputElement).files?.[0]
    if (!file) return
    const validationError = validateLogoFile(file)
    if (validationError) { saveError = validationError; return }
    const { dataUrl, aspectWarning } = await readLogoFile(file)
    logoUrl = dataUrl
    if (aspectWarning) logoWarning = labels.tenantLogoAspectWarning
  }

  async function save() {
    saveMessage = ''
    saveError = ''
    try {
      tenant = await api.updateChildTenant(tenantId, { name, logoUrl })
      saveMessage = labels.tenantSaved
    } catch {
      saveError = labels.tenantSaveError
    }
  }

  async function remove() {
    if (!tenant) return
    const message = labels.deleteTenantConfirm.replace('{name}', tenant.name)
    if (!confirm(message)) return
    await api.deleteChildTenant(tenantId)
    onDeleted()
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.tenants}</button>
{#if tenant}
  <div class="page-intro"><div><p class="eyebrow">{labels.eyebrowSubTenant}</p><h1>{tenant.name}</h1></div></div>
  <section class="panel">
    <form on:submit|preventDefault={save}>
      <label>{labels.tenantName}<input bind:value={name} /></label>
      <label>{labels.tenantLogo}
        {#if logoUrl}<img class="tenant-logo-preview" src={logoUrl} alt="" />{/if}
        <input type="file" accept="image/svg+xml,image/png" on:change={onLogoSelected} />
      </label>
      <p class="muted">{labels.tenantLogoHint}</p>
      {#if logoWarning}<p class="error">{logoWarning}</p>{/if}
      {#if saveError}<p class="error">{saveError}</p>{/if}
      {#if saveMessage}<p class="success">{saveMessage}</p>{/if}
      <button class="primary" type="submit">{labels.save}</button>
    </form>
  </section>
  <button class="danger-button" on:click={remove}>{labels.deleteTenant}</button>
{/if}
