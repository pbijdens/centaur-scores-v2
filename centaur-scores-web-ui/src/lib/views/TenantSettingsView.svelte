<script lang="ts">
  import type { ApiClient } from '../api'
  import type { DefaultScopeSettings } from '../types'

  export let api: ApiClient
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onSaved: () => void

  let settings: DefaultScopeSettings | null = null
  let defaultNarrowcastScope = ''
  let saveMessage = ''
  let saveError = ''

  async function loadSettings() {
    settings = await api.fetchDefaultScopeSettings()
    defaultNarrowcastScope = settings.tenantValue ?? ''
  }
  loadSettings()

  async function save() {
    saveMessage = ''
    saveError = ''
    try {
      settings = await api.updateDefaultNarrowcastScope(defaultNarrowcastScope.trim() || null)
      defaultNarrowcastScope = settings.tenantValue ?? ''
      saveMessage = labels.defaultScopeSaved
      onSaved()
    } catch {
      saveError = labels.defaultScopeSaveError
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.home}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowTenantSettings}</p><h1>{labels.tenantSettings}</h1></div>
</div>
{#if settings}
  <section class="panel">
    <form on:submit|preventDefault={save}>
      <label>{labels.defaultScopeLabel}<input bind:value={defaultNarrowcastScope} placeholder={settings.effectiveValue} /></label>
      <p class="muted">{labels.defaultScopeHint}</p>
      <p class="muted">{labels.defaultScopeEffective.replace('{scope}', settings.effectiveValue)}</p>
      {#if saveError}<p class="error">{saveError}</p>{/if}
      {#if saveMessage}<p class="success">{saveMessage}</p>{/if}
      <button class="primary" type="submit">{labels.save}</button>
    </form>
  </section>
{/if}
