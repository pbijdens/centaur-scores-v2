<script lang="ts">
  import ResultsReportHeader from './ResultsReportHeader.svelte'
  import ResultUnit from './ResultUnit.svelte'
  import type { PaperSize, PrintOrientation } from './paperSize'
  import { paginate, splitInTwo, unitsPerColumn, type ResultsLayoutOptions, type ResultUnit as Unit } from './resultsLayout'
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
  // The paper PDF mode is paginated for.
  export let paper: PaperSize
  export let language: Language
  export let labels: Record<string, string>

  $: columns = splitInTwo(units)
  // PDF mode is paginated for both orientations; the print media queries below show the one matching the paper.
  const orientations: PrintOrientation[] = ['portrait', 'landscape']
  $: pagesByOrientation = orientations.map((orientation) => ({ orientation, pages: paginate(units, unitsPerColumn(paper, orientation, options)) }))
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
    <!-- PDF mode: explicit pages of two columns, each with its own report header (see paginate in resultsLayout.ts;
         Safari ignores CSS multi-column when printing). -->
    {#each pagesByOrientation as { orientation, pages }}
      <div class="paged {orientation}">
        {#each pages as page}
          <div class="printed-page">
            {@render reportHeader()}
            <div class="stream-columns">
              {#each page as column}
                <div class="stream-column">
                  {#each column as unit}<ResultUnit {unit} averageLabel={labels.resultsAverageLabel} />{/each}
                </div>
              {/each}
            </div>
          </div>
        {/each}
      </div>
    {/each}
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

  .printed-page + .printed-page {
    break-before: page;
  }

  @media print and (orientation: portrait) {
    .paged.landscape {
      display: none;
    }
  }

  @media print and (orientation: landscape) {
    .paged.portrait {
      display: none;
    }
  }
</style>
