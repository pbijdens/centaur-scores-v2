<script lang="ts">
  import type { ApiClient } from '../api'
  import { labelForError } from '../errors'

  export let api: ApiClient
  export let labels: Record<string, string>
  export let onBack: () => void

  let importTableName = ''
  let importDateColumn = ''
  let importFederationNumberColumn = ''
  let importNameColumn = ''
  let importDisciplineColumn = ''
  let importMatchClassifierColumn = ''
  let importScoreColumn = ''
  let importUpdateDateColumn = ''
  let importConfigError = ''
  let importConfigMessage = ''

  async function loadImportConfig() {
    const config = await api.fetchPersonalBestImportConfig()
    importTableName = config.tableName
    importDateColumn = config.dateColumn
    importFederationNumberColumn = config.federationNumberColumn
    importNameColumn = config.nameColumn
    importDisciplineColumn = config.disciplineColumn
    importMatchClassifierColumn = config.matchClassifierColumn
    importScoreColumn = config.scoreColumn
    importUpdateDateColumn = config.updateDateColumn
  }
  loadImportConfig()

  async function saveImportConfig() {
    importConfigError = ''
    importConfigMessage = ''
    try {
      await api.savePersonalBestImportConfig({
        tableName: importTableName,
        dateColumn: importDateColumn,
        federationNumberColumn: importFederationNumberColumn,
        nameColumn: importNameColumn,
        disciplineColumn: importDisciplineColumn,
        matchClassifierColumn: importMatchClassifierColumn,
        scoreColumn: importScoreColumn,
        updateDateColumn: importUpdateDateColumn
      })
      importConfigMessage = labels.importConfigSaved
    } catch (error) {
      importConfigError = labelForError(error, labels, 'importConfigSaveError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.personalBest}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowPersonalBestConfig}</p><h1>{labels.importConfigurationLabel}</h1></div>
</div>

<section class="panel">
  <label>{labels.importTableNameLabel}<input bind:value={importTableName} /></label>
  <label>{labels.importDateColumnLabel}<input bind:value={importDateColumn} /></label>
  <label>{labels.importFederationNumberColumnLabel}<input bind:value={importFederationNumberColumn} /></label>
  <label>{labels.importNameColumnLabel}<input bind:value={importNameColumn} /></label>
  <label>{labels.importDisciplineColumnLabel}<input bind:value={importDisciplineColumn} /></label>
  <label>{labels.importMatchClassifierColumnLabel}<input bind:value={importMatchClassifierColumn} /></label>
  <label>{labels.importScoreColumnLabel}<input bind:value={importScoreColumn} /></label>
  <label>{labels.importUpdateDateColumnLabel}<input bind:value={importUpdateDateColumn} /></label>
  {#if importConfigError}<p class="error">{importConfigError}</p>{/if}
  {#if importConfigMessage}<p class="success">{importConfigMessage}</p>{/if}
  <button type="button" class="primary" on:click={saveImportConfig}>{labels.saveImportConfig}</button>
</section>
