<script lang="ts">
  import ResultsReportHeader from './ResultsReportHeader.svelte'
  import ResultUnit from './ResultUnit.svelte'
  import { splitInTwo, type ResultsLayoutOptions, type ResultUnit as Unit } from './resultsLayout'
  import type { Language } from './types'

  // A results report (see resultsLayout.ts), shared by the competition and match results pages. The screen always
  // shows the notice-board stream at normal size (one column on narrow screens); the printed report follows the
  // print options: layout mode, detail lines and font size.
  export let title: string
  export let startDate: string
  export let endDate: string | null | undefined = undefined
  export let logoUrl: string | null | undefined = undefined
  export let units: Unit[]
  export let options: ResultsLayoutOptions
  export let language: Language
  export let labels: Record<string, string>

  $: columns = splitInTwo(units)
</script>

{#snippet reportHeader()}
  <ResultsReportHeader {title} {startDate} {endDate} {logoUrl} {language} {labels} />
{/snippet}

{#snippet streamReport()}
  {@render reportHeader()}
  <div class="stream-columns">
    {#each columns as column}
      <div class="stream-column">
        {#each column as unit}<ResultUnit {unit} averageLabel={labels.resultsAverageLabel} />{/each}
      </div>
    {/each}
  </div>
{/snippet}

<div class="report screen-only" style="--detail-lines: {options.detailLines}">
  {@render streamReport()}
</div>
<div class="report print-only" style="--detail-lines: {options.detailLines}; --font-scale: {options.fontScale}">
  {#if options.mode === 'stream'}
    {@render streamReport()}
  {:else}
    <!-- PDF mode repeats the report header on every printed page: browsers repeat a table's <thead> when the table
         breaks across pages, so the header and the column flow share one single-cell table. -->
    <table class="page-table">
      <thead>
        <tr>
          <td>{@render reportHeader()}</td>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td>
            <div class="page-columns">
              {#each units as unit}<ResultUnit {unit} averageLabel={labels.resultsAverageLabel} />{/each}
            </div>
          </td>
        </tr>
      </tbody>
    </table>
  {/if}
</div>

<style>
  .report {
    background: #fff;
    border: 1px solid var(--line);
    border-radius: 8px;
    padding: 20px;
    font-family: 'DM Sans', sans-serif;
    color: #000;
  }

  @media print {
    .report {
      border: none;
      border-radius: 0;
      padding: 0;
    }
  }

  .stream-columns {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    column-gap: calc(6mm * var(--font-scale, 1));
    align-items: start;
  }

  /* On a narrow screen the two halves of the stream simply follow each other. */
  @media screen and (max-width: 700px) {
    .report {
      padding: 12px;
    }

    .stream-columns {
      grid-template-columns: minmax(0, 1fr);
    }
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
    column-gap: calc(6mm * var(--font-scale, 1));
  }
</style>
