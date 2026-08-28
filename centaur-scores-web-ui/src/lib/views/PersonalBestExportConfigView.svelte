<script lang="ts">
  import type { ApiClient } from '../api'
  import { labelForError } from '../errors'
  import RowActions from '../RowActions.svelte'
  import type { PersonalBestExportColumn, PersonalBestExportField, PersonalBestExportMode, PersonalBestDateFormat } from '../types'

  export let api: ApiClient
  export let labels: Record<string, string>
  export let onBack: () => void

  let exportMode: PersonalBestExportMode = 'all'
  let exportTableName = ''
  let exportColumns: PersonalBestExportColumn[] = []
  let exportConfigError = ''
  let exportConfigMessage = ''

  async function loadExportConfig() {
    const config = await api.fetchPersonalBestExportConfig()
    exportMode = config.exportMode
    exportTableName = config.tableName
    exportColumns = config.columns.map((column) => ({ ...column }))
  }
  loadExportConfig()

  function addExportColumn() {
    exportColumns = [...exportColumns, { columnName: '', field: 'federationNumber' as PersonalBestExportField, dateFormat: null }]
  }

  function removeExportColumn(index: number) {
    exportColumns = exportColumns.filter((_, i) => i !== index)
  }

  function moveExportColumn(index: number, delta: number) {
    const target = index + delta
    if (target < 0 || target >= exportColumns.length) return
    const next = [...exportColumns]
    ;[next[index], next[target]] = [next[target], next[index]]
    exportColumns = next
  }

  function onExportFieldChange(index: number) {
    const column = exportColumns[index]
    if (column.field !== 'date' && column.field !== 'exportDate') exportColumns[index] = { ...column, dateFormat: null }
    else if (!column.dateFormat) exportColumns[index] = { ...column, dateFormat: 'ymd' as PersonalBestDateFormat }
  }

  async function saveExportConfig() {
    exportConfigError = ''
    exportConfigMessage = ''
    try {
      const saved = await api.savePersonalBestExportConfig({ exportMode, tableName: exportTableName, columns: exportColumns })
      exportMode = saved.exportMode
      exportTableName = saved.tableName
      exportColumns = saved.columns.map((column) => ({ ...column }))
      exportConfigMessage = labels.exportConfigSaved
    } catch (error) {
      exportConfigError = labelForError(error, labels, 'exportConfigSaveError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.personalBest}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowPersonalBestConfig}</p><h1>{labels.exportConfigurationLabel}</h1></div>
</div>

<section class="panel">
  <label>{labels.exportModeLabel}
    <select bind:value={exportMode}>
      <option value="all">{labels.exportModeAll}</option>
      <option value="changesSinceLastImport">{labels.exportModeChangesSinceLastImport}</option>
    </select>
  </label>
  <label>{labels.exportTableNameLabel}<input bind:value={exportTableName} /></label>
  <h3>{labels.exportColumnsLabel}</h3>
  {#each exportColumns as column, index}
    <div class="list-row export-column-row">
      <label>{labels.exportColumnNameLabel}<input bind:value={column.columnName} /></label>
      <label>{labels.exportColumnFieldLabel}
        <select bind:value={column.field} on:change={() => onExportFieldChange(index)}>
          <option value="federationNumber">{labels.exportFieldFederationNumber}</option>
          <option value="fullName">{labels.exportFieldFullName}</option>
          <option value="discipline">{labels.exportFieldDiscipline}</option>
          <option value="matchClassifier">{labels.exportFieldMatchClassifier}</option>
          <option value="score">{labels.exportFieldScore}</option>
          <option value="date">{labels.exportFieldDate}</option>
          <option value="exportDate">{labels.exportFieldExportDate}</option>
        </select>
      </label>
      {#if column.field === 'date' || column.field === 'exportDate'}
        <label>{labels.exportColumnDateFormatLabel}
          <select bind:value={column.dateFormat}>
            <option value="ymd">{labels.dateFormatYmd}</option>
            <option value="dmy">{labels.dateFormatDmy}</option>
            <option value="mdy">{labels.dateFormatMdy}</option>
          </select>
        </label>
      {/if}
      <RowActions {labels} canMoveUp={index > 0} canMoveDown={index < exportColumns.length - 1} onMoveUp={() => moveExportColumn(index, -1)} onMoveDown={() => moveExportColumn(index, 1)} onDelete={() => removeExportColumn(index)} />
    </div>
  {/each}
  <button type="button" class="primary" on:click={addExportColumn}>+ {labels.addExportColumn}</button>
  {#if exportConfigError}<p class="error">{exportConfigError}</p>{/if}
  {#if exportConfigMessage}<p class="success">{exportConfigMessage}</p>{/if}
  <button type="button" class="primary" on:click={saveExportConfig}>{labels.saveExportConfig}</button>
</section>

<style>
  .export-column-row {
    flex-wrap: wrap;
    align-items: flex-end;
  }
</style>
