<script lang="ts">
  import type { ApiClient } from '../api'
  import { formatLocalDate } from '../date'
  import type { Language, PersonalBestLogRow } from '../types'

  export let api: ApiClient
  export let language: Language
  export let labels: Record<string, string>
  export let onBack: () => void

  let rows: PersonalBestLogRow[] = []
  let federationNumberFilter = ''
  let nameFilter = ''
  let disciplineFilter = ''
  let matchClassifierFilter = ''
  let dateFilter = ''
  let scoreFilter = ''

  async function load() {
    rows = await api.fetchPersonalBestLog()
  }
  load()

  function includes(value: string, filter: string): boolean {
    return value.toLowerCase().includes(filter.trim().toLowerCase())
  }

  $: filteredRows = rows.filter(
    (row) =>
      includes(row.federationNumber, federationNumberFilter) &&
      includes(row.name, nameFilter) &&
      includes(row.discipline, disciplineFilter) &&
      includes(row.matchClassifier, matchClassifierFilter) &&
      includes(row.date, dateFilter) &&
      includes(String(row.score), scoreFilter)
  )
</script>

<button class="back-link" on:click={onBack}>← {labels.personalBest}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowPersonalBestLog}</p><h1>{labels.viewPersonalBests}</h1><p class="muted">{labels.personalBestLogHint}</p></div>
</div>

<div class="table-scroll">
  <table class="data-table">
    <thead>
      <tr>
        <th>{labels.federationNumberLabel}</th>
        <th>{labels.fullNameLabel}</th>
        <th>{labels.disciplineLabel}</th>
        <th>{labels.matchClassifierLabel}</th>
        <th>{labels.dateLabel}</th>
        <th>{labels.scoreLabel}</th>
      </tr>
      <tr class="filter-row">
        <th><input aria-label={labels.filterLabel} placeholder={labels.filterLabel} bind:value={federationNumberFilter} /></th>
        <th><input aria-label={labels.filterLabel} placeholder={labels.filterLabel} bind:value={nameFilter} /></th>
        <th><input aria-label={labels.filterLabel} placeholder={labels.filterLabel} bind:value={disciplineFilter} /></th>
        <th><input aria-label={labels.filterLabel} placeholder={labels.filterLabel} bind:value={matchClassifierFilter} /></th>
        <th><input aria-label={labels.filterLabel} placeholder={labels.filterLabel} bind:value={dateFilter} /></th>
        <th><input aria-label={labels.filterLabel} placeholder={labels.filterLabel} bind:value={scoreFilter} /></th>
      </tr>
    </thead>
    <tbody>
      {#each filteredRows as row}
        <tr>
          <td>{row.federationNumber}</td>
          <td>{row.name}</td>
          <td>{row.discipline}</td>
          <td>{row.matchClassifier}</td>
          <td>{formatLocalDate(row.date, language)}</td>
          <td>{row.score}</td>
        </tr>
      {/each}
    </tbody>
  </table>
  {#if filteredRows.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
</div>

<style>
  .table-scroll {
    overflow-x: auto;
  }

  .data-table {
    width: 100%;
    border-collapse: collapse;
  }

  .data-table th,
  .data-table td {
    text-align: left;
    padding: 8px 10px;
    border-bottom: 1px solid var(--line);
    white-space: nowrap;
  }

  .filter-row input {
    width: 100%;
    box-sizing: border-box;
  }
</style>
