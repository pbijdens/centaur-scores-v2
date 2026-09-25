<script lang="ts">
  import { onMount } from 'svelte'
  import type { ApiClient } from '../api'
  import CompetitionResultsReportHeader from '../CompetitionResultsReportHeader.svelte'
  import CompetitionResultUnit from '../CompetitionResultUnit.svelte'
  import { flattenResults, layoutOptionsSearch, parseLayoutOptions, splitInTwo, type ResultsLayoutMode } from '../competitionResultsLayout'
  import type { CompetitionResultsDocument, Language } from '../types'

  export let api: ApiClient
  export let competitionId: string
  export let tenantLogoUrl: string | null | undefined = undefined
  export let language: Language
  export let labels: Record<string, string>

  let document_: CompetitionResultsDocument | null = null
  let loadError = false

  // The layout choice lives in the query string so a printed-results link reproduces it.
  const initialOptions = parseLayoutOptions(location.search)
  let mode: ResultsLayoutMode = initialOptions.mode
  let extraLine = initialOptions.detailLines === 3

  $: detailLines = extraLine ? 3 as const : 2 as const
  $: history.replaceState(history.state, '', `${location.pathname}${layoutOptionsSearch({ mode, detailLines })}`)
  $: units = document_ ? flattenResults(document_.groups) : []
  $: columns = splitInTwo(units)

  onMount(async () => {
    try {
      document_ = await api.fetchCompetitionResults(competitionId)
    } catch {
      loadError = true
    }
  })
</script>

<svelte:head>
  <title>{labels.resultsPageTitle}{document_ ? ` - ${document_.competitionName}` : ''}</title>
  <style>
    @page {
      margin: 10mm;
    }
  </style>
</svelte:head>

<div class="results-toolbar">
  <label>
    {labels.resultsLayoutLabel}
    <select bind:value={mode}>
      <option value="stream">{labels.resultsLayoutStream}</option>
      <option value="pages">{labels.resultsLayoutPages}</option>
    </select>
  </label>
  <label class="checkbox-label"><input type="checkbox" bind:checked={extraLine} /> {labels.resultsExtraDetailLine}</label>
  <button class="primary" on:click={() => window.print()}>{labels.printButton}</button>
</div>

{#if loadError}
  <p class="error">{labels.resultsLoadError}</p>
{:else if !document_}
  <p class="muted">…</p>
{:else}
  <div class="report" style="--detail-lines: {detailLines}">
    {#if mode === 'stream'}
      <CompetitionResultsReportHeader
        competitionName={document_.competitionName}
        startDate={document_.startDate}
        endDate={document_.endDate}
        logoUrl={tenantLogoUrl}
        {language}
        {labels}
      />
      <div class="stream-columns">
        {#each columns as column}
          <div class="stream-column">
            {#each column as unit}<CompetitionResultUnit {unit} />{/each}
          </div>
        {/each}
      </div>
    {:else}
      <!-- PDF mode repeats the report header on every printed page: browsers repeat a table's <thead> when the table
           breaks across pages, so the header and the column flow share one single-cell table. -->
      <table class="page-table">
        <thead>
          <tr>
            <td>
              <CompetitionResultsReportHeader
                competitionName={document_.competitionName}
                startDate={document_.startDate}
                endDate={document_.endDate}
                logoUrl={tenantLogoUrl}
                {language}
                {labels}
              />
            </td>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td>
              <div class="page-columns">
                {#each units as unit}<CompetitionResultUnit {unit} />{/each}
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    {/if}
  </div>
{/if}

<style>
  :global(body) {
    background: #fff;
  }

  .results-toolbar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 12px 20px;
    padding: 16px;
  }

  .results-toolbar label {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .results-toolbar .primary {
    margin-left: auto;
  }

  @media print {
    .results-toolbar {
      display: none;
    }
  }

  /* On screen, preview roughly an A4 portrait page's content width. */
  .report {
    max-width: 190mm;
    margin: 0 auto;
    padding: 0 16px 40px;
    font-family: 'DM Sans', sans-serif;
    color: #000;
  }

  @media print {
    .report {
      max-width: none;
      padding: 0;
    }
  }

  .stream-columns {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    column-gap: 6mm;
    align-items: start;
  }

  .page-table {
    width: 100%;
    border-collapse: collapse;
  }

  .page-table td {
    padding: 0;
  }

  .page-columns {
    columns: 2;
    column-gap: 6mm;
  }
</style>
