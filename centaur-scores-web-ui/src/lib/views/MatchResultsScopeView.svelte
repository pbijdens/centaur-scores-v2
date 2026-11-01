<script lang="ts">
  import { onMount, tick } from 'svelte'
  import type { ApiClient } from '../api'
  import MatchPageHeader from '../MatchPageHeader.svelte'
  import MatchProgress from '../MatchProgress.svelte'
  import { printAction, type MatchHeaderHandlers } from '../pageActions'
  import { loadPrintOptions, printOptionsKey, savePrintOptions } from '../printOptions'
  import ResultsPrintDialog from '../ResultsPrintDialog.svelte'
  import ResultsReport from '../ResultsReport.svelte'
  import { flattenMatchResults, parseLayoutOptions, type ResultsLayoutOptions } from '../resultsLayout'
  import type { Language, LiveScoringPage, Match } from '../types'

  // A match's results for one live scope, in the same report format as the competition results.
  export let api: ApiClient
  export let match: Match
  export let scope: string
  export let language: Language
  export let labels: Record<string, string>
  export let headerHandlers: MatchHeaderHandlers
  export let onBack: () => void

  let page: LiveScoringPage | null = null
  let loadError = false

  // The print options are chosen in the print dialog and remembered per match.
  const optionsKey = printOptionsKey('match-results', match.id)
  let options: ResultsLayoutOptions = loadPrintOptions(optionsKey, parseLayoutOptions)
  let dialogOpen = false

  $: units = page ? flattenMatchResults(page.blocks) : []

  async function print(chosen: ResultsLayoutOptions) {
    options = chosen
    savePrintOptions(optionsKey, options)
    dialogOpen = false
    await tick()
    window.print()
  }

  onMount(async () => {
    try {
      page = await api.fetchMatchLiveScoringPage(match.id, scope)
    } catch {
      loadError = true
    }
  })
</script>

<svelte:head>
  <title>{page ? `${page.matchName} - ${page.tenant}` : labels.resultsLabel}</title>
  <style>
    @page {
      margin: 10mm;
    }
  </style>
</svelte:head>

<MatchPageHeader {api} {match} {language} {labels} handlers={headerHandlers} current="results" title={`${labels.resultsLabel}: ${scope}`} subtitle={match.name} backLabel={match.name} {onBack} pageActions={[printAction(labels, () => (dialogOpen = true), !page)]}>
  {#if page}<div class="progress"><MatchProgress {page} label={labels.matchProgressLabel} /></div>{/if}
</MatchPageHeader>

<ResultsPrintDialog bind:open={dialogOpen} {options} {labels} onPrint={print} />

<div class="print-output">
  {#if loadError}
    <p class="error">{labels.matchResultsLoadError}</p>
  {:else if !page}
    <p class="muted">…</p>
  {:else}
    <ResultsReport title={page.matchName} startDate={page.matchDate} logoUrl={page.logo} {units} {options} {language} {labels} />
  {/if}
</div>

<style>
  .progress {
    max-width: 260px;
    margin-top: 10px;
  }
</style>
