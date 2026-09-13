<script lang="ts">
  import type { ApiClient } from '../api'
  import type { DefaultScopeSettings } from '../types'

  export let api: ApiClient
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onSaved: () => void

  let settings: DefaultScopeSettings | null = null
  let defaultNarrowcastScope = ''

  async function loadSettings() {
    settings = await api.fetchDefaultScopeSettings()
    defaultNarrowcastScope = settings.tenantValue ?? ''
  }
  loadSettings()

  async function save() {
    try {
      settings = await api.updateDefaultNarrowcastScope(defaultNarrowcastScope.trim() || null)
      defaultNarrowcastScope = settings.tenantValue ?? ''
      alert(labels.defaultScopeSaved)
      onSaved()
    } catch {
      alert(labels.defaultScopeSaveError)
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.home}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowTenantSettings}</p><h1>{labels.tenantSettings}</h1></div>
</div>
{#if settings}
  <section class="panel">
    <form id="tenant-settings-form" on:submit|preventDefault={save}>
      <label>{labels.defaultScopeLabel}<input bind:value={defaultNarrowcastScope} placeholder={settings.effectiveValue} /></label>
      <p class="muted">{labels.defaultScopeHint}</p>
      <p class="muted">{labels.defaultScopeEffective.replace('{scope}', settings.effectiveValue)}</p>
    </form>
  </section>
  <div class="sticky-actions">
    <button class="primary" type="submit" form="tenant-settings-form">{labels.save}</button>
  </div>
{/if}
