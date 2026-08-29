<script lang="ts">
  import type { ApiClient } from '../api'
  import { labelForError } from '../errors'
  import type { RestoreBackupResult } from '../types'

  export let api: ApiClient
  export let labels: Record<string, string>
  export let onBack: () => void

  let includeSubTenants = false
  let exporting = false
  let exportError = ''

  let fileInput: HTMLInputElement
  let restoring = false
  let restoreError = ''
  let restoreResult: RestoreBackupResult | null = null

  async function createBackup() {
    exportError = ''
    exporting = true
    try {
      const { blob, filename } = await api.downloadBackupExport(includeSubTenants)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = filename
      link.click()
      URL.revokeObjectURL(url)
    } catch (error) {
      exportError = labelForError(error, labels, 'backupExportError')
    } finally {
      exporting = false
    }
  }

  function pickRestoreFile() {
    if (!confirm(labels.restoreBackupConfirm)) return
    fileInput.click()
  }

  async function restoreFile(event: Event) {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    input.value = ''
    if (!file) return
    restoreError = ''
    restoreResult = null
    restoring = true
    try {
      restoreResult = await api.restoreBackup(file)
    } catch (error) {
      restoreError = labelForError(error, labels, 'restoreBackupError')
    } finally {
      restoring = false
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.home}</button>
<div class="page-intro">
  <div>
    <p class="eyebrow">{labels.eyebrowBackupRestore}</p>
    <h1>{labels.backupRestore}</h1>
    <p class="muted">{labels.backupRestoreHint}</p>
  </div>
</div>

<div class="pb-tile-grid">
  <div class="pb-tile">
    <span class="pb-tile-icon" aria-hidden="true">📦</span>
    <span class="pb-tile-title">{labels.createBackup}</span>
    <span class="pb-tile-description">{labels.createBackupDescription}</span>
    <label class="checkbox-label"><input type="checkbox" bind:checked={includeSubTenants} /> {labels.includeSubTenants}</label>
    <button class="primary" disabled={exporting} on:click={createBackup}>{labels.createBackup}</button>
  </div>
  <button class="pb-tile" disabled={restoring} on:click={pickRestoreFile}>
    <span class="pb-tile-icon" aria-hidden="true">♻️</span>
    <span class="pb-tile-title">{labels.restoreBackup}</span>
    <span class="pb-tile-description">{labels.restoreBackupDescription}</span>
  </button>
</div>
<input type="file" accept=".zip" bind:this={fileInput} on:change={restoreFile} hidden />

{#if exportError}<p class="error">{exportError}</p>{/if}
{#if restoreError}<p class="error">{restoreError}</p>{/if}
{#if restoreResult}
  <section class="panel result-panel">
    <p class="success">{labels.restoreBackupSuccess.replace('{name}', restoreResult.newTenantName)}</p>
    {#if restoreResult.warnings.length > 0}
      <p class="muted">{labels.restoreBackupWarningsTitle}</p>
      <ul>
        {#each restoreResult.warnings as warning}<li>{warning}</li>{/each}
      </ul>
    {/if}
  </section>
{/if}

<style>
  .result-panel {
    margin-top: 16px;
  }

  .pb-tile .checkbox-label {
    margin-top: 4px;
  }

  .pb-tile .primary {
    margin-top: 8px;
  }
</style>
