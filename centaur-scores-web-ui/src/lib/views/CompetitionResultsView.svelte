<script lang="ts">
  import { onMount, tick } from 'svelte'
  import type { ApiClient } from '../api'
  import PageHeader from '../PageHeader.svelte'
  import { printAction } from '../pageActions'
  import { loadPrintOptions, printOptionsKey, savePrintOptions } from '../printOptions'
  import ResultsPrintDialog from '../ResultsPrintDialog.svelte'
  import ResultsReport from '../ResultsReport.svelte'
  import { flattenResults, parseLayoutOptions, type ResultsLayoutOptions } from '../resultsLayout'
  import type { CompetitionResultsDocument, Language } from '../types'

  export let api: ApiClient
  export let competitionId: string
  export let tenantLogoUrl: string | null | undefined = undefined
  export let language: Language
  export let labels: Record<string, string>
  export let onBack: () => void

  let document_: CompetitionResultsDocument | null = null
  let loadError = false

  // The print options are chosen in the print dialog and remembered per competition.
  const optionsKey = printOptionsKey('competition-results', competitionId)
  let options: ResultsLayoutOptions = loadPrintOptions(optionsKey, parseLayoutOptions)
  let dialogOpen = false

  $: units = document_ ? flattenResults(document_.groups) : []

  async function print(chosen: ResultsLayoutOptions) {
    options = chosen
    savePrintOptions(optionsKey, options)
    dialogOpen = false
    await tick()
    window.print()
  }

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

<PageHeader eyebrow={labels.competitions} title={labels.resultsPageTitle} subtitle={document_?.competitionName} backLabel={document_?.competitionName ?? labels.competitions} {onBack} actions={[printAction(labels, () => (dialogOpen = true), !document_)]} />

<ResultsPrintDialog bind:open={dialogOpen} {options} {labels} onPrint={print} />

<div class="print-output">
  {#if loadError}
    <p class="error">{labels.resultsLoadError}</p>
  {:else if !document_}
    <p class="muted">…</p>
  {:else}
    <ResultsReport title={document_.competitionName} startDate={document_.startDate} endDate={document_.endDate} logoUrl={tenantLogoUrl} {units} {options} {language} {labels} />
  {/if}
</div>
