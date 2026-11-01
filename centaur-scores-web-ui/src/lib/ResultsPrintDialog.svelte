<script lang="ts">
  import PaperSizeSelect from './PaperSizeSelect.svelte'
  import type { PaperSize } from './paperSize'
  import PrintDialog from './PrintDialog.svelte'
  import { resultsFontScales, type ResultsFontScale, type ResultsLayoutOptions } from './resultsLayout'

  // The print options of a results report (see ResultsReport.svelte): the paper it is paginated for, an extra
  // detail line and the font size. Edits a copy of `options` and `paper`; onPrint receives the choices.
  export let open = false
  export let options: ResultsLayoutOptions
  export let paper: PaperSize
  export let labels: Record<string, string>
  export let onPrint: (options: ResultsLayoutOptions, paper: PaperSize) => void

  let extraLine = false
  let fontScale: ResultsFontScale = 1
  let dialogPaper: PaperSize = paper
  let wasOpen = false

  $: if (open !== wasOpen) {
    wasOpen = open
    if (open) {
      extraLine = options.detailLines === 3
      fontScale = options.fontScale
      dialogPaper = paper
    }
  }

  function scaleLabel(scale: ResultsFontScale): string {
    const percent = `${Math.round(scale * 100)}%`
    if (scale === 1) return `${percent} (${labels.resultsFontSizeNormal})`
    if (scale === 1.41) return `${percent} (${labels.resultsFontSizeA3})`
    return percent
  }
</script>

<PrintDialog bind:open title={labels.printOptionsTitle} {labels} onPrint={() => onPrint({ detailLines: extraLine ? 3 : 2, fontScale }, dialogPaper)}>
  <PaperSizeSelect bind:value={dialogPaper} {labels} />
  <label>
    {labels.resultsFontSizeLabel}
    <select bind:value={fontScale}>
      {#each resultsFontScales as scale}<option value={scale}>{scaleLabel(scale)}</option>{/each}
    </select>
  </label>
  <label class="checkbox-label"><input type="checkbox" bind:checked={extraLine} /> {labels.resultsExtraDetailLine}</label>
</PrintDialog>
