<script lang="ts">
  import PageHeader from '../PageHeader.svelte'
  import type { ApiClient } from '../api'
  import { labelForError } from '../errors'
  import type { DefaultScopeSettings } from '../types'

  export let api: ApiClient
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onSaved: () => void

  let settings: DefaultScopeSettings | null = null
  let defaultNarrowcastScope = ''
  let scorekeeperBaseUrl = ''

  // What scorekeeperUrl() falls back to when no override is set, shown in the hint.
  const defaultScorekeeperBaseUrl = `${window.location.origin}/scores`

  async function loadSettings() {
    const [scopeSettings, tenant] = await Promise.all([api.fetchDefaultScopeSettings(), api.fetchCurrentTenant()])
    defaultNarrowcastScope = scopeSettings.tenantValue ?? ''
    scorekeeperBaseUrl = tenant.scorekeeperBaseUrl ?? ''
    settings = scopeSettings
  }
  loadSettings()

  async function save() {
    try {
      settings = await api.updateDefaultNarrowcastScope(defaultNarrowcastScope.trim() || null)
      defaultNarrowcastScope = settings.tenantValue ?? ''
      const tenant = await api.updateScorekeeperBaseUrl(scorekeeperBaseUrl.trim() || null)
      scorekeeperBaseUrl = tenant.scorekeeperBaseUrl ?? ''
      alert(labels.tenantSettingsSaved)
      onSaved()
    } catch (error) {
      alert(labelForError(error, labels, 'tenantSettingsSaveError'))
    }
  }
</script>

<PageHeader eyebrow={labels.tenantSettings} title={labels.tenantSettings} backLabel={labels.home} {onBack} />
{#if settings}
  <section class="panel">
    <form id="tenant-settings-form" on:submit|preventDefault={save}>
      <label>{labels.defaultScopeLabel}<input bind:value={defaultNarrowcastScope} placeholder={settings.effectiveValue} /></label>
      <p class="muted">{labels.defaultScopeHint}</p>
      <p class="muted">{labels.defaultScopeEffective.replace('{scope}', settings.effectiveValue)}</p>
      <label>{labels.scorekeeperBaseUrlLabel}<input type="url" bind:value={scorekeeperBaseUrl} placeholder={defaultScorekeeperBaseUrl} /></label>
      <p class="muted">{labels.scorekeeperBaseUrlHint.replace('{default}', defaultScorekeeperBaseUrl)}</p>
    </form>
  </section>
  <div class="sticky-actions">
    <button class="primary" type="submit" form="tenant-settings-form">{labels.save}</button>
  </div>
{/if}
