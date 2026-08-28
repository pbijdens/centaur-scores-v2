<script lang="ts">
  import type { ApiClient } from '../api'
  import DropdownMenu from '../DropdownMenu.svelte'
  import { labelForError } from '../errors'
  import type { PersonalBestImportConflict, PersonalBestImportResult, PersonalBestStatus } from '../types'

  export let api: ApiClient
  export let status: PersonalBestStatus | null
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onNavigate: (path: string) => void
  export let onChanged: () => void

  let actionError = ''
  let fileInput: HTMLInputElement
  let importing = false
  let importResult: PersonalBestImportResult | null = null
  let importError = ''
  let resolveError = ''
  let resolving = false
  let decisions: Record<string, 'deleteOffending' | 'ignoreImported'> = {}
  let exportError = ''

  function conflictKey(conflict: PersonalBestImportConflict): string {
    return `${conflict.federationNumber}|${conflict.discipline}|${conflict.matchClassifier}`
  }

  $: actionableConflicts = importResult?.conflicts.filter((conflict) => conflict.actionable) ?? []
  $: infoConflicts = importResult?.conflicts.filter((conflict) => !conflict.actionable) ?? []
  $: canSubmitResolutions = actionableConflicts.length > 0 && actionableConflicts.every((conflict) => decisions[conflictKey(conflict)])

  async function enable() {
    actionError = ''
    try {
      await api.enablePersonalBest()
      onChanged()
    } catch (error) {
      actionError = labelForError(error, labels, 'personalBestEnableError')
    }
  }

  async function disable() {
    actionError = ''
    if (!confirm(labels.disablePersonalBestConfirm)) return
    try {
      await api.disablePersonalBest()
      onChanged()
    } catch (error) {
      actionError = labelForError(error, labels, 'personalBestDisableError')
    }
  }

  async function importFile(event: Event) {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    input.value = ''
    if (!file) return
    importError = ''
    importResult = null
    decisions = {}
    importing = true
    try {
      importResult = await api.importPersonalBestList(file)
    } catch (error) {
      importError = labelForError(error, labels, 'personalBestImportError')
    } finally {
      importing = false
    }
  }

  async function submitResolutions() {
    if (!importResult?.batchId) return
    resolveError = ''
    resolving = true
    try {
      await api.resolvePersonalBestConflicts(
        importResult.batchId,
        actionableConflicts.map((conflict) => ({
          federationNumber: conflict.federationNumber,
          discipline: conflict.discipline,
          matchClassifier: conflict.matchClassifier,
          action: decisions[conflictKey(conflict)]
        }))
      )
      importResult = { ...importResult, batchId: null, conflicts: infoConflicts }
    } catch (error) {
      resolveError = labelForError(error, labels, 'personalBestConflictResolveError')
    } finally {
      resolving = false
    }
  }

  async function exportUpdates() {
    exportError = ''
    try {
      const { blob, filename } = await api.downloadPersonalBestExport()
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = filename
      link.click()
      URL.revokeObjectURL(url)
    } catch (error) {
      exportError = labelForError(error, labels, 'personalBestExportError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.home}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowPersonalBest}</p><h1>{labels.personalBest}</h1></div>
  <div class="match-header-actions">
    <DropdownMenu ariaLabel={labels.matchActions} buttonClass="actions-trigger" align="right">
      <svelte:fragment slot="trigger">⋯</svelte:fragment>
      {#if status?.enabled}
        <button class="menu-item" on:click={() => onNavigate('/personal-best/classifiers')}>{labels.classifiersLabel}</button>
        <button class="menu-item" on:click={() => onNavigate('/personal-best/disciplines')}>{labels.disciplinesLabel}</button>
        <button class="menu-item" on:click={() => onNavigate('/personal-best/export-configuration')}>{labels.exportConfigurationLabel}</button>
        <button class="menu-item" on:click={() => onNavigate('/personal-best/import-configuration')}>{labels.importConfigurationLabel}</button>
        <hr class="menu-separator" />
        <button class="menu-item menu-item-danger" on:click={disable}>{labels.disablePersonalBest}</button>
      {:else}
        <button class="menu-item" on:click={enable}>{labels.enablePersonalBest}</button>
      {/if}
    </DropdownMenu>
  </div>
</div>
{#if actionError}<p class="error">{actionError}</p>{/if}

{#if !status?.enabled}
  <p class="muted">{labels.personalBestNotEnabled}</p>
{:else}
  <div class="pb-tile-grid">
    <button class="pb-tile" disabled={importing} on:click={() => fileInput.click()}>
      <span class="pb-tile-icon" aria-hidden="true">📥</span>
      <span class="pb-tile-title">{labels.importPersonalBestList}</span>
      <span class="pb-tile-description">{labels.importPersonalBestListDescription}</span>
    </button>
    <button class="pb-tile" on:click={exportUpdates}>
      <span class="pb-tile-icon" aria-hidden="true">📤</span>
      <span class="pb-tile-title">{labels.exportPersonalBestUpdates}</span>
      <span class="pb-tile-description">{labels.exportPersonalBestUpdatesDescription}</span>
    </button>
    <button class="pb-tile" on:click={() => onNavigate('/personal-best/log')}>
      <span class="pb-tile-icon" aria-hidden="true">🏆</span>
      <span class="pb-tile-title">{labels.viewPersonalBests}</span>
      <span class="pb-tile-description">{labels.personalBestLogHint}</span>
    </button>
  </div>
  <input type="file" accept=".xlsx" bind:this={fileInput} on:change={importFile} hidden />
  {#if exportError}<p class="error">{exportError}</p>{/if}
  {#if importError}<p class="error">{importError}</p>{/if}
  {#if importResult}
    <section class="panel section-gap">
      <p class="success">{labels.personalBestImportSummary.replace('{newArchers}', String(importResult.newArchers)).replace('{newRegistrations}', String(importResult.newRegistrations))}</p>
      {#if importResult.warnings.length > 0}
        <ul class="import-warnings">
          {#each importResult.warnings as warning}<li>{warning}</li>{/each}
        </ul>
      {/if}
      {#if infoConflicts.length > 0}
        <p class="muted">{labels.personalBestConflictCannotInsert}</p>
        <ul class="import-warnings">
          {#each infoConflicts as conflict}<li>{conflict.federationNumber} — {conflict.discipline} / {conflict.matchClassifier}</li>{/each}
        </ul>
      {/if}
      {#if actionableConflicts.length > 0}
        <h2>{labels.personalBestConflictsTitle}</h2>
        {#each actionableConflicts as conflict}
          <div class="list-row personal-best-conflict-row">
            <span><strong>{conflict.federationNumber}</strong> — {conflict.discipline} / {conflict.matchClassifier} {labels.personalBestConflictHigher}</span>
            <label><input type="radio" name={conflictKey(conflict)} value="deleteOffending" bind:group={decisions[conflictKey(conflict)]} /> {labels.personalBestConflictDeleteOffending}</label>
            <label><input type="radio" name={conflictKey(conflict)} value="ignoreImported" bind:group={decisions[conflictKey(conflict)]} /> {labels.personalBestConflictIgnoreImported}</label>
          </div>
        {/each}
        {#if resolveError}<p class="error">{resolveError}</p>{/if}
        <button class="primary" disabled={!canSubmitResolutions || resolving} on:click={submitResolutions}>{labels.personalBestConflictSubmit}</button>
      {/if}
    </section>
  {/if}
{/if}

<style>
  .personal-best-conflict-row {
    flex-direction: column;
    align-items: flex-start;
    gap: 6px;
  }

  .pb-tile-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
    gap: 16px;
    margin-top: 16px;
  }

  .pb-tile {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: 6px;
    text-align: left;
    background: var(--paper);
    border: 1px solid var(--line);
    padding: 20px;
    box-shadow: 0 2px 6px rgba(20, 33, 15, .06);
  }

  .pb-tile:hover:not(:disabled),
  .pb-tile:focus-visible {
    border-color: var(--green);
    background: var(--neutral);
  }

  .pb-tile:disabled {
    opacity: .5;
    cursor: default;
  }

  .pb-tile-icon {
    font-size: 28px;
    line-height: 1;
  }

  .pb-tile-title {
    font-weight: 700;
    font-size: 15px;
    color: var(--ink);
  }

  .pb-tile-description {
    font-size: 12.5px;
    font-weight: 400;
    color: var(--muted);
    line-height: 1.4;
  }
</style>
